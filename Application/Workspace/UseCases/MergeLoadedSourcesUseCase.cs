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

            if (list.Count == 1)
            {
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

                long start = data.SourceStartMs != null && data.SourceStartMs.ContainsKey(source)
                    ? data.SourceStartMs[source]
                    : (data.TimestampsMs != null && data.TimestampsMs.Length > 0 ? data.TimestampsMs[0] : 0L);
                long end = data.SourceEndMs != null && data.SourceEndMs.ContainsKey(source)
                    ? data.SourceEndMs[source]
                    : (data.TimestampsMs != null && data.TimestampsMs.Length > 0 ? data.TimestampsMs[data.TimestampsMs.Length - 1] : 0L);
                sourceStartMs[source] = start;
                sourceEndMs[source] = end;

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

                    columnSet.Add(mergedCode);
                    if (!codeSources.ContainsKey(mergedCode))
                    {
                        codeSources[mergedCode] = source;
                    }
                }
            }

            string[] columnNames = columnSet.OrderBy(name => name, StringComparer.OrdinalIgnoreCase).ToArray();
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
                if (data.RowCount <= 0)
                {
                    continue;
                }

                Array.Copy(data.TimestampsMs, 0, compactTimestamps, writeOffset, data.RowCount);
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

                    Array.Copy(sourceArray, 0, compactColumns[mergedColumn], writeOffset, data.RowCount);
                }

                writeOffset += data.RowCount;
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

            return new TestData
            {
                Root = string.Join(" ; ", list.Select(data => data.Root).Where(root => !string.IsNullOrWhiteSpace(root))),
                Meta = metadata,
                Channels = channels,
                CodeSources = codeSources,
                SourceStartMs = sourceStartMs,
                SourceEndMs = sourceEndMs,
                TimestampsMs = sortedTimestamps,
                Columns = sortedColumns,
                ColumnNames = columnNames,
                SourceColumns = list.ToDictionary(
                    data => data.Root,
                    data =>
                    {
                        Dictionary<string, string> map = codeMapsBySource[data.Root];
                        var columns = new string[data.ColumnNames.Length];
                        for (int i = 0; i < data.ColumnNames.Length; i++)
                        {
                            string merged;
                            columns[i] = map.TryGetValue(data.ColumnNames[i], out merged) ? merged : data.ColumnNames[i];
                        }

                        return columns;
                    },
                    StringComparer.OrdinalIgnoreCase),
                RowCount = totalRows
            };
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
    }
}
