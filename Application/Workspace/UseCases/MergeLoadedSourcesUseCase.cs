using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using JSQViewer.Core;

namespace JSQViewer.Application.Workspace.UseCases
{
    public sealed class MergeLoadedSourcesUseCase
    {
        private readonly AnalyzeOverlapConflictsUseCase _analyzeOverlapConflictsUseCase;

        public MergeLoadedSourcesUseCase()
            : this(new AnalyzeOverlapConflictsUseCase())
        {
        }

        public MergeLoadedSourcesUseCase(AnalyzeOverlapConflictsUseCase analyzeOverlapConflictsUseCase)
        {
            _analyzeOverlapConflictsUseCase = analyzeOverlapConflictsUseCase ?? throw new ArgumentNullException(nameof(analyzeOverlapConflictsUseCase));
        }

        public TestData Execute(IList<TestData> list, bool splitOverlappingCodes)
        {
            if (list == null || list.Count == 0)
            {
                throw new ArgumentException("No loaded sources were provided.", nameof(list));
            }

            Dictionary<string, string> sourceDisplayNames = BuildSourceDisplayNames(list);
            string[] sourceOrder = BuildSourceOrder(list);

            if (list.Count == 1)
            {
                ApplyIdentityToRetainedData(list[0], sourceDisplayNames, sourceOrder);
                return list[0];
            }

            list = DeduplicateByRoot(list);
            if (list.Count == 1)
            {
                ApplyIdentityToRetainedData(list[0], sourceDisplayNames, sourceOrder);
                return list[0];
            }

            Dictionary<string, string> sourceTags = BuildSourceTags(list);
            HashSet<string> overlaps = new HashSet<string>(_analyzeOverlapConflictsUseCase.Execute(list), StringComparer.OrdinalIgnoreCase);
            var codeMapsBySource = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
            var usedCodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            for (int i = 0; i < list.Count; i++)
            {
                TestData data = list[i];
                string source = data.Root ?? ("source_" + (i + 1).ToString(CultureInfo.InvariantCulture));
                string tag = sourceTags[source];
                var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                for (int c = 0; c < data.ColumnNames.Length; c++)
                {
                    string code = data.ColumnNames[c];
                    string mergedCode = code;
                    if (splitOverlappingCodes && overlaps.Contains(code))
                    {
                        mergedCode = tag + "::" + code;
                    }

                    if (usedCodes.Contains(mergedCode))
                    {
                        int suffix = 2;
                        string candidate = mergedCode + "#" + suffix.ToString(CultureInfo.InvariantCulture);
                        while (usedCodes.Contains(candidate))
                        {
                            suffix++;
                            candidate = mergedCode + "#" + suffix.ToString(CultureInfo.InvariantCulture);
                        }

                        mergedCode = candidate;
                    }

                    usedCodes.Add(mergedCode);
                    map[code] = mergedCode;
                }

                codeMapsBySource[source] = map;
            }

            var channels = new Dictionary<string, ChannelInfo>(StringComparer.OrdinalIgnoreCase);
            var codeSources = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var metadata = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var columnSetOrder = new List<string>();
            var columnSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            int totalRows = 0;
            var sourceStartMs = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
            var sourceEndMs = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);

            for (int i = 0; i < list.Count; i++)
            {
                TestData data = list[i];
                string source = data.Root ?? ("source_" + (i + 1).ToString(CultureInfo.InvariantCulture));
                Dictionary<string, string> codeMap = codeMapsBySource[source];
                totalRows += data.RowCount;

                foreach (var kv in data.Meta)
                {
                    if (!metadata.ContainsKey(kv.Key))
                    {
                        metadata[kv.Key] = kv.Value;
                    }
                }

                foreach (var kv in data.Channels)
                {
                    string mergedCode;
                    if (!codeMap.TryGetValue(kv.Key, out mergedCode))
                    {
                        mergedCode = kv.Key;
                    }

                    ChannelInfo existing;
                    if (!channels.TryGetValue(mergedCode, out existing))
                    {
                        channels[mergedCode] = new ChannelInfo
                        {
                            Code = mergedCode,
                            Name = kv.Value.Name,
                            Unit = kv.Value.Unit
                        };
                    }
                    else
                    {
                        if (string.IsNullOrWhiteSpace(existing.Name) && !string.IsNullOrWhiteSpace(kv.Value.Name))
                        {
                            existing.Name = kv.Value.Name;
                        }

                        if (string.IsNullOrWhiteSpace(existing.Unit) && !string.IsNullOrWhiteSpace(kv.Value.Unit))
                        {
                            existing.Unit = kv.Value.Unit;
                        }
                    }
                }

                for (int c = 0; c < data.ColumnNames.Length; c++)
                {
                    string originalCode = data.ColumnNames[c];
                    string mergedCode;
                    if (!codeMap.TryGetValue(originalCode, out mergedCode))
                    {
                        mergedCode = originalCode;
                    }

                    if (columnSet.Add(mergedCode))
                        columnSetOrder.Add(mergedCode);
                    if (!codeSources.ContainsKey(mergedCode))
                    {
                        codeSources[mergedCode] = ResolveColumnSource(data, originalCode, source);
                    }
                }
            }

            string[] columnNames = columnSetOrder.ToArray();
            long[] compactTimestamps = new long[totalRows];
            var compactColumns = new Dictionary<string, double?[]>(StringComparer.OrdinalIgnoreCase);
            foreach (string column in columnNames)
            {
                compactColumns[column] = new double?[totalRows];
            }

            int writeOffset = 0;
            for (int i = 0; i < list.Count; i++)
            {
                TestData data = list[i];
                string source = data.Root ?? ("source_" + (i + 1).ToString(CultureInfo.InvariantCulture));
                Dictionary<string, string> codeMap = codeMapsBySource[source];
                int rowsToCopy = Math.Min(
                    data.RowCount,
                    data.TimestampsMs != null ? data.TimestampsMs.Length : 0);
                if (rowsToCopy <= 0)
                {
                    continue;
                }

                Array.Copy(data.TimestampsMs, 0, compactTimestamps, writeOffset, rowsToCopy);
                for (int c = 0; c < data.ColumnNames.Length; c++)
                {
                    string column = data.ColumnNames[c];
                    string mergedColumn;
                    if (!codeMap.TryGetValue(column, out mergedColumn))
                    {
                        mergedColumn = column;
                    }

                    double?[] sourceArray;
                    if (!data.Columns.TryGetValue(column, out sourceArray))
                    {
                        continue;
                    }

                    Array.Copy(sourceArray, 0, compactColumns[mergedColumn], writeOffset, rowsToCopy);
                }

                writeOffset += rowsToCopy;
            }

            int[] sortIndices = new int[totalRows];
            for (int i = 0; i < totalRows; i++)
            {
                sortIndices[i] = i;
            }

            Array.Sort(sortIndices, (left, right) => compactTimestamps[left].CompareTo(compactTimestamps[right]));

            long[] sortedTimestamps = new long[totalRows];
            var sortedColumns = new Dictionary<string, double?[]>(StringComparer.OrdinalIgnoreCase);
            foreach (string column in columnNames)
            {
                sortedColumns[column] = new double?[totalRows];
            }

            for (int i = 0; i < totalRows; i++)
            {
                int sourceIndex = sortIndices[i];
                sortedTimestamps[i] = compactTimestamps[sourceIndex];
            }

            foreach (string column in columnNames)
            {
                double?[] sourceArray = compactColumns[column];
                double?[] destinationArray = sortedColumns[column];
                for (int i = 0; i < totalRows; i++)
                {
                    destinationArray[i] = sourceArray[sortIndices[i]];
                }
            }

            var sourceColumnLists = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < list.Count; i++)
            {
                TestData d = list[i];
                string src = d.Root ?? ("source_" + (i + 1).ToString(CultureInfo.InvariantCulture));
                Dictionary<string, string> map = codeMapsBySource[src];
                if (d.SourceColumns != null && d.SourceColumns.Count > 0)
                {
                    foreach (KeyValuePair<string, string[]> pair in d.SourceColumns)
                    {
                        AppendMappedSourceColumns(sourceColumnLists, pair.Key, pair.Value, map);
                        AddSourceBounds(d, pair.Key, sourceStartMs, sourceEndMs);
                    }
                }
                else
                {
                    AppendMappedSourceColumns(sourceColumnLists, src, d.ColumnNames, map);
                    AddSourceBounds(d, src, sourceStartMs, sourceEndMs);
                }
            }

            var sourceColumnMap = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);
            foreach (KeyValuePair<string, List<string>> pair in sourceColumnLists)
            {
                sourceColumnMap[pair.Key] = pair.Value
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToArray();
            }

            string[] normalizedSourceOrder = NormalizeSourceOrder(sourceOrder, sourceColumnMap.Keys);
            Dictionary<string, string> normalizedDisplayNames = FilterDisplayNamesToSources(
                sourceDisplayNames,
                sourceColumnMap.Keys);

            // A merged Meta is workspace-global and cannot be attributed to any single source
            // once several recordings are combined, so the per-recording keys are dropped here.
            metadata.Remove(WorkspaceMetadataKeys.EquipmentModel);
            metadata.Remove(WorkspaceMetadataKeys.Compressor);
            metadata.Remove(WorkspaceMetadataKeys.ExperimentType);
            metadata.Remove(WorkspaceMetadataKeys.ClimateMode);

            return new TestData
            {
                Root = string.Join(" ; ", normalizedSourceOrder),
                Meta = metadata,
                Channels = channels,
                CodeSources = codeSources,
                SourceStartMs = sourceStartMs,
                SourceEndMs = sourceEndMs,
                SourceDisplayNames = normalizedDisplayNames,
                SourceOrder = normalizedSourceOrder,
                TimestampsMs = sortedTimestamps,
                Columns = sortedColumns,
                ColumnNames = columnNames,
                SourceColumns = sourceColumnMap,
                RowCount = totalRows
            };
        }

        private static void ApplyIdentityToRetainedData(
            TestData data,
            Dictionary<string, string> sourceDisplayNames,
            string[] sourceOrder)
        {
            IEnumerable<string> actualSources;
            if (data.SourceColumns != null && data.SourceColumns.Count > 0)
            {
                actualSources = data.SourceColumns.Keys;
            }
            else if (!string.IsNullOrWhiteSpace(data.Root))
            {
                actualSources = new[] { data.Root };
            }
            else
            {
                actualSources = new string[0];
            }

            data.SourceDisplayNames = FilterDisplayNamesToSources(sourceDisplayNames, actualSources);
            data.SourceOrder = NormalizeSourceOrder(sourceOrder, actualSources);
        }

        private static string ResolveColumnSource(TestData data, string column, string fallbackSource)
        {
            string source;
            if (data.CodeSources != null
                && data.CodeSources.TryGetValue(column, out source)
                && !string.IsNullOrWhiteSpace(source))
            {
                return source;
            }

            if (data.SourceColumns != null)
            {
                foreach (KeyValuePair<string, string[]> pair in data.SourceColumns)
                {
                    if (pair.Value != null
                        && pair.Value.Contains(column, StringComparer.OrdinalIgnoreCase))
                    {
                        return pair.Key;
                    }
                }
            }

            return fallbackSource;
        }

        private static void AppendMappedSourceColumns(
            Dictionary<string, List<string>> sourceColumns,
            string source,
            IEnumerable<string> columns,
            Dictionary<string, string> codeMap)
        {
            List<string> result;
            if (!sourceColumns.TryGetValue(source, out result))
            {
                result = new List<string>();
                sourceColumns[source] = result;
            }

            if (columns == null)
            {
                return;
            }

            foreach (string column in columns)
            {
                string merged;
                result.Add(codeMap.TryGetValue(column, out merged) ? merged : column);
            }
        }

        private static void AddSourceBounds(
            TestData data,
            string source,
            Dictionary<string, long> sourceStartMs,
            Dictionary<string, long> sourceEndMs)
        {
            if (!sourceStartMs.ContainsKey(source))
            {
                long start;
                sourceStartMs[source] = data.SourceStartMs != null
                    && data.SourceStartMs.TryGetValue(source, out start)
                    ? start
                    : (data.TimestampsMs != null && data.TimestampsMs.Length > 0 ? data.TimestampsMs[0] : 0L);
            }

            if (!sourceEndMs.ContainsKey(source))
            {
                long end;
                sourceEndMs[source] = data.SourceEndMs != null
                    && data.SourceEndMs.TryGetValue(source, out end)
                    ? end
                    : (data.TimestampsMs != null && data.TimestampsMs.Length > 0
                        ? data.TimestampsMs[data.TimestampsMs.Length - 1]
                        : 0L);
            }
        }

        private static string[] NormalizeSourceOrder(
            IEnumerable<string> sourceOrder,
            IEnumerable<string> actualSources)
        {
            var actual = new HashSet<string>(actualSources, StringComparer.OrdinalIgnoreCase);
            var result = new List<string>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (sourceOrder != null)
            {
                foreach (string source in sourceOrder)
                {
                    if (!string.IsNullOrWhiteSpace(source)
                        && actual.Contains(source)
                        && seen.Add(source))
                    {
                        result.Add(source);
                    }
                }
            }

            foreach (string source in actualSources)
            {
                if (!string.IsNullOrWhiteSpace(source) && seen.Add(source))
                {
                    result.Add(source);
                }
            }

            return result.ToArray();
        }

        private static Dictionary<string, string> FilterDisplayNamesToSources(
            Dictionary<string, string> sourceDisplayNames,
            IEnumerable<string> actualSources)
        {
            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (string source in actualSources)
            {
                string displayName;
                if (sourceDisplayNames.TryGetValue(source, out displayName))
                {
                    result[source] = displayName;
                }
            }

            return result;
        }

        private static Dictionary<string, string> BuildSourceDisplayNames(IList<TestData> list)
        {
            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < list.Count; i++)
            {
                Dictionary<string, string> displayNames = list[i].SourceDisplayNames;
                if (displayNames == null)
                {
                    continue;
                }

                foreach (KeyValuePair<string, string> pair in displayNames)
                {
                    if (string.IsNullOrWhiteSpace(pair.Key)
                        || string.IsNullOrWhiteSpace(pair.Value)
                        || result.ContainsKey(pair.Key))
                    {
                        continue;
                    }

                    result[pair.Key] = pair.Value.Trim();
                }
            }

            return result;
        }

        private static string[] BuildSourceOrder(IList<TestData> list)
        {
            var result = new List<string>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < list.Count; i++)
            {
                TestData data = list[i];
                AppendDistinctRoots(result, seen, data.SourceOrder);
                AppendDistinctRoots(
                    result,
                    seen,
                    data.SourceColumns == null ? null : data.SourceColumns.Keys);
            }

            return result.ToArray();
        }

        private static void AppendDistinctRoots(
            List<string> destination,
            HashSet<string> seen,
            IEnumerable<string> roots)
        {
            if (roots == null)
            {
                return;
            }

            foreach (string root in roots)
            {
                if (!string.IsNullOrWhiteSpace(root) && seen.Add(root))
                {
                    destination.Add(root);
                }
            }
        }

        private static Dictionary<string, string> BuildSourceTags(IList<TestData> list)
        {
            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < list.Count; i++)
            {
                string root = list[i].Root ?? ("source_" + (i + 1).ToString(CultureInfo.InvariantCulture));
                string trimmed = root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                string baseName = Path.GetFileName(trimmed);
                if (string.IsNullOrWhiteSpace(baseName))
                {
                    baseName = "source_" + (i + 1).ToString(CultureInfo.InvariantCulture);
                }

                string tag = baseName;
                int suffix = 2;
                while (used.Contains(tag))
                {
                    tag = baseName + "_" + suffix.ToString(CultureInfo.InvariantCulture);
                    suffix++;
                }

                used.Add(tag);
                result[root] = tag;
            }

            return result;
        }

        private static IList<TestData> DeduplicateByRoot(IList<TestData> list)
        {
            var result = new List<TestData>(list.Count);
            var roots = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < list.Count; i++)
            {
                TestData data = list[i];
                if (string.IsNullOrWhiteSpace(data.Root) || roots.Add(data.Root))
                {
                    result.Add(data);
                }
            }

            return result;
        }
    }
}
