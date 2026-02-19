using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace LeMuReViewer.Core
{
    public static class TestLoader
    {
        private static readonly Regex ProvaDbfRegex = new Regex(@"Prova(\d+)\.dbf$", RegexOptions.IgnoreCase);
        private static readonly Regex ProvaDatRegex = new Regex(@"Prova\d+\.dat$", RegexOptions.IgnoreCase);

        private static readonly string[] TimeColumns = { "Data", "Ore", "Minuti", "Secondi", "mSecondi" };

        public static TestData LoadTest(string folder)
        {
            string root = FindTestRoot(folder);
            string setDir = Path.Combine(root, "Set");

            Dictionary<string, ChannelInfo> channels = CanaliParser.ParseCanaliDef(Path.Combine(setDir, "Canali.def"));

            Dictionary<string, string> meta = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (string file in Directory.GetFiles(root))
            {
                if (ProvaDatRegex.IsMatch(Path.GetFileName(file)))
                {
                    meta = CanaliParser.ParseProvaDat(file);
                    break;
                }
            }

            string[] dbfFiles = Directory.GetFiles(root, "Prova*.dbf", SearchOption.TopDirectoryOnly)
                .Where(path => ProvaDbfRegex.IsMatch(Path.GetFileName(path)))
                .OrderBy(path => DbfSortKey(path))
                .ToArray();

            if (dbfFiles.Length == 0)
            {
                throw new FileNotFoundException("No Prova*.dbf files found in test folder.");
            }

            // === Pass 1: Read headers, count total records, collect column names ===
            DbfHeader[] headers = new DbfHeader[dbfFiles.Length];
            int totalRecords = 0;
            var colSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var timeColumnSet = new HashSet<string>(TimeColumns, StringComparer.OrdinalIgnoreCase);

            for (int i = 0; i < dbfFiles.Length; i++)
            {
                headers[i] = DbfReader.ReadHeaderFromFile(dbfFiles[i]);
                totalRecords += headers[i].Records;
                for (int f = 0; f < headers[i].Fields.Count; f++)
                {
                    string name = headers[i].Fields[f].Name;
                    if (!timeColumnSet.Contains(name))
                    {
                        colSet.Add(name);
                    }
                }
            }

            // === Pre-allocate arrays ===
            string[] columnNames = colSet.OrderBy(s => s, StringComparer.OrdinalIgnoreCase).ToArray();
            long[] tMs = new long[totalRecords];
            var columns = new Dictionary<string, double?[]>(StringComparer.OrdinalIgnoreCase);
            foreach (string col in columnNames)
            {
                columns[col] = new double?[totalRecords];
            }

            // === Pass 2: Read data directly into pre-allocated arrays ===
            int[] fileCounts = new int[dbfFiles.Length];
            int[] fileOffsets = new int[dbfFiles.Length];
            int offset = 0;
            for (int i = 0; i < dbfFiles.Length; i++)
            {
                fileOffsets[i] = offset;
                offset += headers[i].Records;
            }

            // Read files in parallel
            Parallel.For(0, dbfFiles.Length, i =>
            {
                int[] timeFieldIndices = BuildTimeFieldIndices(headers[i]);
                fileCounts[i] = DbfReader.ReadAllRecordsDirect(
                    dbfFiles[i], headers[i], tMs, columns, columnNames,
                    fileOffsets[i], timeFieldIndices);
            });

            // === Compact: remove gaps from deleted/skipped records ===
            int totalValid = 0;
            for (int i = 0; i < dbfFiles.Length; i++) totalValid += fileCounts[i];

            long[] compactTs = new long[totalValid];
            var compactCols = new Dictionary<string, double?[]>(StringComparer.OrdinalIgnoreCase);
            foreach (string col in columnNames)
            {
                compactCols[col] = new double?[totalValid];
            }

            int writePos = 0;
            for (int i = 0; i < dbfFiles.Length; i++)
            {
                int srcStart = fileOffsets[i];
                int count = fileCounts[i];
                if (count == 0) continue;
                Array.Copy(tMs, srcStart, compactTs, writePos, count);
                foreach (string col in columnNames)
                {
                    Array.Copy(columns[col], srcStart, compactCols[col], writePos, count);
                }
                writePos += count;
            }

            // === Sort by timestamp using index array ===
            int[] sortIndices = new int[totalValid];
            for (int i = 0; i < totalValid; i++) sortIndices[i] = i;
            Array.Sort(sortIndices, delegate(int a, int b) { return compactTs[a].CompareTo(compactTs[b]); });

            long[] sortedTs = new long[totalValid];
            var sortedCols = new Dictionary<string, double?[]>(StringComparer.OrdinalIgnoreCase);
            foreach (string col in columnNames)
            {
                sortedCols[col] = new double?[totalValid];
            }

            for (int i = 0; i < totalValid; i++)
            {
                int src = sortIndices[i];
                sortedTs[i] = compactTs[src];
            }

            foreach (string col in columnNames)
            {
                double?[] srcArr = compactCols[col];
                double?[] dstArr = sortedCols[col];
                for (int i = 0; i < totalValid; i++)
                {
                    dstArr[i] = srcArr[sortIndices[i]];
                }
            }

            return new TestData
            {
                Root = root,
                Meta = meta,
                Channels = channels,
                CodeSources = columnNames.ToDictionary(c => c, c => root, StringComparer.OrdinalIgnoreCase),
                SourceStartMs = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase)
                {
                    { root, totalValid > 0 ? sortedTs[0] : 0L }
                },
                SourceEndMs = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase)
                {
                    { root, totalValid > 0 ? sortedTs[totalValid - 1] : 0L }
                },
                TimestampsMs = sortedTs,
                Columns = sortedCols,
                ColumnNames = columnNames,
                SourceColumns = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
                {
                    { root, columnNames.ToArray() }
                },
                RowCount = totalValid
            };
        }

        public static List<TestData> LoadTests(IList<string> folders)
        {
            if (folders == null || folders.Count == 0)
            {
                throw new ArgumentException("No folders provided for loading.", "folders");
            }

            var list = new List<TestData>(folders.Count);
            for (int i = 0; i < folders.Count; i++)
            {
                list.Add(LoadTest(folders[i]));
            }
            return list;
        }

        public static List<string> FindOverlappingCodes(IList<TestData> list)
        {
            var codeToSources = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            if (list == null) return new List<string>();
            for (int i = 0; i < list.Count; i++)
            {
                TestData td = list[i];
                var seenInSource = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                for (int c = 0; c < td.ColumnNames.Length; c++)
                {
                    string code = td.ColumnNames[c];
                    if (!seenInSource.Add(code)) continue;
                    int count;
                    codeToSources.TryGetValue(code, out count);
                    codeToSources[code] = count + 1;
                }
            }
            return codeToSources.Where(kv => kv.Value > 1).Select(kv => kv.Key).OrderBy(s => s, StringComparer.OrdinalIgnoreCase).ToList();
        }

        public static TestData MergeLoadedTests(IList<TestData> list, bool splitOverlappingCodes)
        {
            if (list.Count == 1)
            {
                return list[0];
            }

            Dictionary<string, string> sourceTags = BuildSourceTags(list);
            HashSet<string> overlaps = new HashSet<string>(FindOverlappingCodes(list), StringComparer.OrdinalIgnoreCase);
            var codeMapsBySource = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
            var usedCodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            for (int i = 0; i < list.Count; i++)
            {
                TestData td = list[i];
                string source = td.Root ?? ("source_" + (i + 1).ToString(CultureInfo.InvariantCulture));
                string tag = sourceTags[source];
                var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                for (int c = 0; c < td.ColumnNames.Length; c++)
                {
                    string code = td.ColumnNames[c];
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
            var meta = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var colSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            int totalRows = 0;
            var sourceStartMs = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
            var sourceEndMs = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);

            for (int i = 0; i < list.Count; i++)
            {
                TestData td = list[i];
                string source = td.Root ?? ("source_" + (i + 1).ToString(CultureInfo.InvariantCulture));
                string tag = sourceTags[source];
                Dictionary<string, string> codeMap = codeMapsBySource[source];
                totalRows += td.RowCount;
                long sourceStart = td.SourceStartMs != null && td.SourceStartMs.ContainsKey(source)
                    ? td.SourceStartMs[source]
                    : (td.TimestampsMs != null && td.TimestampsMs.Length > 0 ? td.TimestampsMs[0] : 0L);
                long sourceEnd = td.SourceEndMs != null && td.SourceEndMs.ContainsKey(source)
                    ? td.SourceEndMs[source]
                    : (td.TimestampsMs != null && td.TimestampsMs.Length > 0 ? td.TimestampsMs[td.TimestampsMs.Length - 1] : 0L);
                sourceStartMs[source] = sourceStart;
                sourceEndMs[source] = sourceEnd;

                foreach (var kv in td.Meta)
                {
                    if (!meta.ContainsKey(kv.Key))
                    {
                        meta[kv.Key] = kv.Value;
                    }
                }

                foreach (var kv in td.Channels)
                {
                    string mergedCode;
                    if (!codeMap.TryGetValue(kv.Key, out mergedCode))
                    {
                        mergedCode = kv.Key;
                    }
                    ChannelInfo existing;
                    if (!channels.TryGetValue(mergedCode, out existing))
                    {
                        string mergedName = kv.Value.Name;
                        if (splitOverlappingCodes && overlaps.Contains(kv.Key))
                        {
                            mergedName = string.IsNullOrWhiteSpace(mergedName) ? ("[" + tag + "]") : (mergedName + " [" + tag + "]");
                        }
                        channels[mergedCode] = new ChannelInfo { Code = mergedCode, Name = mergedName, Unit = kv.Value.Unit };
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

                for (int c = 0; c < td.ColumnNames.Length; c++)
                {
                    string origCode = td.ColumnNames[c];
                    string mergedCode;
                    if (!codeMap.TryGetValue(origCode, out mergedCode))
                    {
                        mergedCode = origCode;
                    }
                    colSet.Add(mergedCode);
                    if (!codeSources.ContainsKey(mergedCode))
                    {
                        codeSources[mergedCode] = source;
                    }
                }
            }

            string[] columnNames = colSet.OrderBy(s => s, StringComparer.OrdinalIgnoreCase).ToArray();
            long[] compactTs = new long[totalRows];
            var compactCols = new Dictionary<string, double?[]>(StringComparer.OrdinalIgnoreCase);
            foreach (string col in columnNames)
            {
                compactCols[col] = new double?[totalRows];
            }

            int writePos = 0;
            for (int i = 0; i < list.Count; i++)
            {
                TestData td = list[i];
                string source = td.Root ?? ("source_" + (i + 1).ToString(CultureInfo.InvariantCulture));
                Dictionary<string, string> codeMap = codeMapsBySource[source];
                int n = td.RowCount;
                if (n <= 0) continue;

                Array.Copy(td.TimestampsMs, 0, compactTs, writePos, n);
                for (int c = 0; c < td.ColumnNames.Length; c++)
                {
                    string col = td.ColumnNames[c];
                    string mergedCol;
                    if (!codeMap.TryGetValue(col, out mergedCol))
                    {
                        mergedCol = col;
                    }
                    double?[] src;
                    if (!td.Columns.TryGetValue(col, out src)) continue;
                    Array.Copy(src, 0, compactCols[mergedCol], writePos, n);
                }
                writePos += n;
            }

            int[] sortIndices = new int[totalRows];
            for (int i = 0; i < totalRows; i++) sortIndices[i] = i;
            Array.Sort(sortIndices, delegate(int a, int b) { return compactTs[a].CompareTo(compactTs[b]); });

            long[] sortedTs = new long[totalRows];
            var sortedCols = new Dictionary<string, double?[]>(StringComparer.OrdinalIgnoreCase);
            foreach (string col in columnNames)
            {
                sortedCols[col] = new double?[totalRows];
            }

            for (int i = 0; i < totalRows; i++)
            {
                int srcIdx = sortIndices[i];
                sortedTs[i] = compactTs[srcIdx];
            }

            foreach (string col in columnNames)
            {
                double?[] srcArr = compactCols[col];
                double?[] dstArr = sortedCols[col];
                for (int i = 0; i < totalRows; i++)
                {
                    dstArr[i] = srcArr[sortIndices[i]];
                }
            }

            return new TestData
            {
                Root = string.Join(" ; ", list.Select(d => d.Root).Where(s => !string.IsNullOrWhiteSpace(s))),
                Meta = meta,
                Channels = channels,
                CodeSources = codeSources,
                SourceStartMs = sourceStartMs,
                SourceEndMs = sourceEndMs,
                TimestampsMs = sortedTs,
                Columns = sortedCols,
                ColumnNames = columnNames,
                SourceColumns = list.ToDictionary(
                    d => d.Root,
                    d =>
                    {
                        Dictionary<string, string> map = codeMapsBySource[d.Root];
                        var arr = new string[d.ColumnNames.Length];
                        for (int i = 0; i < d.ColumnNames.Length; i++)
                        {
                            string merged;
                            arr[i] = map.TryGetValue(d.ColumnNames[i], out merged) ? merged : d.ColumnNames[i];
                        }
                        return arr;
                    },
                    StringComparer.OrdinalIgnoreCase),
                RowCount = totalRows
            };
        }

        public static TestData LoadAndMergeTests(IList<string> folders)
        {
            List<TestData> list = LoadTests(folders);
            return MergeLoadedTests(list, false);
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

        private static int[] BuildTimeFieldIndices(DbfHeader header)
        {
            int[] indices = { -1, -1, -1, -1, -1 }; // Data, Ore, Minuti, Secondi, mSecondi
            for (int f = 0; f < header.Fields.Count; f++)
            {
                string name = header.Fields[f].Name;
                if (string.Equals(name, "Data", StringComparison.OrdinalIgnoreCase)) indices[0] = f;
                else if (string.Equals(name, "Ore", StringComparison.OrdinalIgnoreCase)) indices[1] = f;
                else if (string.Equals(name, "Minuti", StringComparison.OrdinalIgnoreCase)) indices[2] = f;
                else if (string.Equals(name, "Secondi", StringComparison.OrdinalIgnoreCase)) indices[3] = f;
                else if (string.Equals(name, "mSecondi", StringComparison.OrdinalIgnoreCase)) indices[4] = f;
            }
            return indices;
        }

        public static string FindTestRoot(string folder)
        {
            string abs = Path.GetFullPath(folder);
            if (!Directory.Exists(abs))
            {
                throw new DirectoryNotFoundException("Folder not found: " + abs);
            }

            string[] localDbf = Directory.GetFiles(abs, "Prova*.dbf", SearchOption.TopDirectoryOnly)
                .Where(path => ProvaDbfRegex.IsMatch(Path.GetFileName(path)))
                .ToArray();
            if (localDbf.Length > 0)
            {
                return abs;
            }

            var found = new List<string>();
            SearchDbfRecursive(abs, 0, 3, found);
            if (found.Count == 0)
            {
                throw new FileNotFoundException("No Prova*.dbf files found in selected folder.");
            }

            return found.Select(Path.GetDirectoryName)
                .Where(p => !string.IsNullOrEmpty(p))
                .OrderBy(p => p.Length)
                .First();
        }

        private static void SearchDbfRecursive(string dir, int depth, int maxDepth, List<string> results)
        {
            if (depth > maxDepth)
            {
                return;
            }
            try
            {
                string[] files = Directory.GetFiles(dir, "Prova*.dbf", SearchOption.TopDirectoryOnly);
                for (int i = 0; i < files.Length; i++)
                {
                    if (ProvaDbfRegex.IsMatch(Path.GetFileName(files[i])))
                    {
                        results.Add(files[i]);
                    }
                }
                if (results.Count > 0)
                {
                    return;
                }
                string[] subDirs = Directory.GetDirectories(dir);
                for (int i = 0; i < subDirs.Length; i++)
                {
                    SearchDbfRecursive(subDirs[i], depth + 1, maxDepth, results);
                    if (results.Count > 0)
                    {
                        return;
                    }
                }
            }
            catch (UnauthorizedAccessException)
            {
            }
        }

        private static bool TryBuildTimestampMs(Dictionary<string, object> dbfRow, out long timestampMs)
        {
            timestampMs = 0;
            object dObj;
            if (!dbfRow.TryGetValue("Data", out dObj) || !(dObj is DateTime))
            {
                return false;
            }

            DateTime d = ((DateTime)dObj).Date;
            int hh = ToInt(dbfRow, "Ore");
            int mm = ToInt(dbfRow, "Minuti");
            int ss = ToInt(dbfRow, "Secondi");
            int mss = ToInt(dbfRow, "mSecondi");

            DateTime dt;
            try
            {
                dt = new DateTime(d.Year, d.Month, d.Day, hh, mm, ss, DateTimeKind.Local).AddMilliseconds(mss);
            }
            catch
            {
                return false;
            }

            DateTime epochUtc = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            timestampMs = (long)(dt.ToUniversalTime() - epochUtc).TotalMilliseconds;
            return true;
        }

        private static int DbfSortKey(string path)
        {
            Match m = ProvaDbfRegex.Match(Path.GetFileName(path));
            int key;
            if (m.Success && int.TryParse(m.Groups[1].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out key))
            {
                return key;
            }

            return 0;
        }

        private static int ToInt(Dictionary<string, object> row, string key)
        {
            object value;
            if (!row.TryGetValue(key, out value) || value == null)
            {
                return 0;
            }

            if (value is double)
            {
                return (int)(double)value;
            }

            double parsed;
            if (double.TryParse(Convert.ToString(value, CultureInfo.InvariantCulture), NumberStyles.Float, CultureInfo.InvariantCulture, out parsed))
            {
                return (int)parsed;
            }

            return 0;
        }

        private static bool IsTimeColumn(string col)
        {
            return string.Equals(col, "Data", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(col, "Ore", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(col, "Minuti", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(col, "Secondi", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(col, "mSecondi", StringComparison.OrdinalIgnoreCase);
        }

        private static double? ToNullableDouble(object value)
        {
            if (value == null)
            {
                return null;
            }

            if (value is double)
            {
                return (double)value;
            }

            if (value is int)
            {
                return (int)value;
            }

            string s = Convert.ToString(value, CultureInfo.InvariantCulture);
            if (string.IsNullOrWhiteSpace(s))
            {
                return null;
            }

            double parsed;
            if (double.TryParse(s.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out parsed))
            {
                return parsed;
            }

            return null;
        }

        private sealed class RowData
        {
            public long TimestampMs { get; set; }
            public Dictionary<string, double?> Values { get; set; }
        }
    }
}
