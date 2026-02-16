using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace LeMuReViewer.Core
{
    public static class TestLoader
    {
        private static readonly Regex ProvaDbfRegex = new Regex(@"Prova(\d+)\.dbf$", RegexOptions.IgnoreCase);
        private static readonly Regex ProvaDatRegex = new Regex(@"Prova\d+\.dat$", RegexOptions.IgnoreCase);

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

            var rows = new List<RowData>(8192);
            var colSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            for (int i = 0; i < dbfFiles.Length; i++)
            {
                foreach (Dictionary<string, object> dbfRow in DbfReader.IterateRows(dbfFiles[i]))
                {
                    long tsMs;
                    if (!TryBuildTimestampMs(dbfRow, out tsMs))
                    {
                        continue;
                    }

                    var values = new Dictionary<string, double?>(StringComparer.OrdinalIgnoreCase);
                    foreach (var kv in dbfRow)
                    {
                        string col = kv.Key;
                        if (IsTimeColumn(col))
                        {
                            continue;
                        }

                        double? parsed = ToNullableDouble(kv.Value);
                        values[col] = parsed;
                        colSet.Add(col);
                    }

                    rows.Add(new RowData { TimestampMs = tsMs, Values = values });
                }
            }

            rows.Sort(delegate(RowData a, RowData b) { return a.TimestampMs.CompareTo(b.TimestampMs); });

            string[] columnNames = colSet.OrderBy(s => s, StringComparer.OrdinalIgnoreCase).ToArray();
            long[] tMs = new long[rows.Count];
            var columns = new Dictionary<string, double?[]>(StringComparer.OrdinalIgnoreCase);
            foreach (string col in columnNames)
            {
                columns[col] = new double?[rows.Count];
            }

            for (int i = 0; i < rows.Count; i++)
            {
                tMs[i] = rows[i].TimestampMs;
                var values = rows[i].Values;
                foreach (string col in columnNames)
                {
                    double? value;
                    if (values.TryGetValue(col, out value))
                    {
                        columns[col][i] = value;
                    }
                }
            }

            return new TestData
            {
                Root = root,
                Meta = meta,
                Channels = channels,
                TimestampsMs = tMs,
                Columns = columns,
                ColumnNames = columnNames,
                RowCount = rows.Count
            };
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
