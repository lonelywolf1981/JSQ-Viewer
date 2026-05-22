using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using JSQViewer.Application.Workspace.Ports;
using JSQViewer.Core;

namespace JSQViewer.Infrastructure.DataImport
{
    public sealed class ExportedProtocolDataSourceReader : ITestDataSourceReader
    {
        private static readonly Regex UnitRegex = new Regex(@"\[(?<unit>[^\]]+)\]\s*$", RegexOptions.Compiled);
        private static readonly Dictionary<int, string> FixedColumns = new Dictionary<int, string>
        {
            { 4, "Pc" },
            { 5, "Pe" },
            { 6, "T-sie" },
            { 7, "UR-sie" },
            { 8, "Tc" },
            { 9, "Te" },
            { 10, "T1" },
            { 11, "T2" },
            { 12, "T3" },
            { 13, "T4" },
            { 14, "T5" },
            { 15, "T6" },
            { 16, "T7" },
            { 17, "I" },
            { 18, "F" },
            { 19, "V" },
            { 20, "W" }
        };

        public TestData Read(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                throw new ArgumentException("Protocol path is required.", nameof(path));
            }

            string root = Path.GetFullPath(path);
            if (!File.Exists(root))
            {
                throw new FileNotFoundException("Exported protocol file was not found.", root);
            }

            using (ZipArchive archive = ZipFile.OpenRead(root))
            {
                List<string> sharedStrings = ReadSharedStrings(archive);
                XDocument sheet = ReadWorksheet(archive);
                return BuildData(root, sheet, sharedStrings);
            }
        }

        public TestData Read(string root, Dictionary<string, ChannelInfo> channels, Dictionary<string, string> metadata)
        {
            return Read(root);
        }

        private static TestData BuildData(string root, XDocument sheet, List<string> sharedStrings)
        {
            XNamespace ns = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
            XElement sheetData = sheet.Root == null ? null : sheet.Root.Element(ns + "sheetData");
            if (sheetData == null)
            {
                throw new InvalidDataException("Exported protocol worksheet does not contain sheetData.");
            }

            Dictionary<int, Dictionary<int, string>> rows = ReadRows(sheetData, ns, sharedStrings);
            if (!rows.ContainsKey(4))
            {
                throw new InvalidDataException("Exported protocol does not contain data rows.");
            }

            Dictionary<int, string> columnCodes = BuildColumnCodes(rows);
            if (columnCodes.Count == 0)
            {
                throw new InvalidDataException("Exported protocol does not contain readable channels.");
            }

            var timestamps = new List<long>();
            var values = columnCodes.Values.ToDictionary(code => code, code => new List<double?>(), StringComparer.OrdinalIgnoreCase);

            foreach (int rowNumber in rows.Keys.Where(row => row >= 4).OrderBy(row => row))
            {
                Dictionary<int, string> row = rows[rowNumber];
                double timeValue;
                if (!TryGetNumber(row, 2, out timeValue) && !TryGetNumber(row, 3, out timeValue))
                {
                    continue;
                }

                timestamps.Add(ExcelDaysToMilliseconds(timeValue));
                foreach (KeyValuePair<int, string> pair in columnCodes)
                {
                    double number;
                    values[pair.Value].Add(TryGetNumber(row, pair.Key, out number) ? (double?)number : null);
                }
            }

            if (timestamps.Count == 0)
            {
                throw new InvalidDataException("Exported protocol does not contain readable data rows.");
            }

            int[] order = Enumerable.Range(0, timestamps.Count)
                .OrderBy(i => timestamps[i])
                .ToArray();

            long[] sortedTimestamps = order.Select(i => timestamps[i]).ToArray();
            var columns = new Dictionary<string, double?[]>(StringComparer.OrdinalIgnoreCase);
            foreach (KeyValuePair<string, List<double?>> pair in values)
            {
                columns[pair.Key] = order.Select(i => pair.Value[i]).ToArray();
            }

            string[] columnNames = columnCodes.Values.ToArray();
            Dictionary<string, ChannelInfo> channels = BuildChannels(columnNames);
            Dictionary<string, string> metadata = BuildMetadata(root, rows);

            return new TestData
            {
                Root = root,
                Meta = metadata,
                Channels = channels,
                CodeSources = columnNames.ToDictionary(code => code, code => root, StringComparer.OrdinalIgnoreCase),
                SourceStartMs = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase)
                {
                    [root] = sortedTimestamps[0]
                },
                SourceEndMs = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase)
                {
                    [root] = sortedTimestamps[sortedTimestamps.Length - 1]
                },
                TimestampsMs = sortedTimestamps,
                Columns = columns,
                ColumnNames = columnNames,
                SourceColumns = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
                {
                    [root] = columnNames.ToArray()
                },
                RowCount = sortedTimestamps.Length
            };
        }

        private static Dictionary<int, string> BuildColumnCodes(Dictionary<int, Dictionary<int, string>> rows)
        {
            var result = new Dictionary<int, string>();
            foreach (KeyValuePair<int, string> pair in FixedColumns)
            {
                result[pair.Key] = pair.Value;
            }

            Dictionary<int, string> headerRow;
            if (!rows.TryGetValue(3, out headerRow))
            {
                return result;
            }

            var used = new HashSet<string>(result.Values, StringComparer.OrdinalIgnoreCase);
            foreach (KeyValuePair<int, string> cell in headerRow.OrderBy(pair => pair.Key))
            {
                if (cell.Key < 26)
                {
                    continue;
                }

                string header = (cell.Value ?? string.Empty).Trim();
                if (string.IsNullOrWhiteSpace(header) || string.Equals(header, "1", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                result[cell.Key] = MakeUniqueCode(header, used);
            }

            return result;
        }

        private static Dictionary<string, ChannelInfo> BuildChannels(string[] columnNames)
        {
            var channels = new Dictionary<string, ChannelInfo>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < columnNames.Length; i++)
            {
                string code = columnNames[i];
                string unit = string.Empty;
                Match match = UnitRegex.Match(code);
                if (match.Success)
                {
                    unit = match.Groups["unit"].Value.Trim();
                }

                channels[code] = new ChannelInfo
                {
                    Code = code,
                    Name = code,
                    Unit = unit
                };
            }

            return channels;
        }

        private static Dictionary<string, string> BuildMetadata(string root, Dictionary<int, Dictionary<int, string>> rows)
        {
            var metadata = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["ProtocolPath"] = root
            };

            Dictionary<int, string> row1;
            if (rows.TryGetValue(1, out row1))
            {
                string refrigerant;
                if (row1.TryGetValue(2, out refrigerant) && !string.IsNullOrWhiteSpace(refrigerant))
                {
                    metadata["Refrigerant"] = refrigerant;
                }

                string exportedRoot;
                if (row1.TryGetValue(4, out exportedRoot) && !string.IsNullOrWhiteSpace(exportedRoot))
                {
                    metadata["ExportedSourceRoot"] = exportedRoot;
                }
            }

            return metadata;
        }

        private static Dictionary<int, Dictionary<int, string>> ReadRows(XElement sheetData, XNamespace ns, List<string> sharedStrings)
        {
            var rows = new Dictionary<int, Dictionary<int, string>>();
            foreach (XElement row in sheetData.Elements(ns + "row"))
            {
                int rowNumber;
                if (!int.TryParse((string)row.Attribute("r"), NumberStyles.Integer, CultureInfo.InvariantCulture, out rowNumber))
                {
                    continue;
                }

                var cells = new Dictionary<int, string>();
                foreach (XElement cell in row.Elements(ns + "c"))
                {
                    int column = GetColumnIndex((string)cell.Attribute("r"));
                    if (column <= 0)
                    {
                        continue;
                    }

                    cells[column] = ReadCellValue(cell, ns, sharedStrings);
                }

                rows[rowNumber] = cells;
            }

            return rows;
        }

        private static string ReadCellValue(XElement cell, XNamespace ns, List<string> sharedStrings)
        {
            string type = (string)cell.Attribute("t");
            if (string.Equals(type, "inlineStr", StringComparison.OrdinalIgnoreCase))
            {
                XElement inline = cell.Element(ns + "is");
                return inline == null ? string.Empty : string.Concat(inline.Descendants(ns + "t").Select(t => t.Value));
            }

            string raw = (string)cell.Element(ns + "v") ?? string.Empty;
            if (string.Equals(type, "s", StringComparison.OrdinalIgnoreCase))
            {
                int index;
                if (int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out index)
                    && index >= 0
                    && index < sharedStrings.Count)
                {
                    return sharedStrings[index];
                }
            }

            return raw;
        }

        private static List<string> ReadSharedStrings(ZipArchive archive)
        {
            ZipArchiveEntry entry = archive.GetEntry("xl/sharedStrings.xml");
            if (entry == null)
            {
                return new List<string>();
            }

            XNamespace ns = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
            using (Stream stream = entry.Open())
            {
                XDocument doc = XDocument.Load(stream);
                return doc.Root == null
                    ? new List<string>()
                    : doc.Root.Elements(ns + "si")
                        .Select(si => string.Concat(si.Descendants(ns + "t").Select(t => t.Value)))
                        .ToList();
            }
        }

        private static XDocument ReadWorksheet(ZipArchive archive)
        {
            ZipArchiveEntry entry = archive.GetEntry("xl/worksheets/sheet1.xml");
            if (entry == null)
            {
                throw new InvalidDataException("Exported protocol does not contain xl/worksheets/sheet1.xml.");
            }

            using (Stream stream = entry.Open())
            {
                return XDocument.Load(stream);
            }
        }

        private static bool TryGetNumber(Dictionary<int, string> row, int column, out double value)
        {
            string text;
            if (!row.TryGetValue(column, out text))
            {
                value = 0d;
                return false;
            }

            return double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
        }

        private static long ExcelDaysToMilliseconds(double days)
        {
            return (long)Math.Round(days * 86400000d);
        }

        private static int GetColumnIndex(string cellReference)
        {
            if (string.IsNullOrWhiteSpace(cellReference))
            {
                return 0;
            }

            int index = 0;
            for (int i = 0; i < cellReference.Length; i++)
            {
                char ch = cellReference[i];
                if (ch < 'A' || ch > 'Z')
                {
                    break;
                }

                index = index * 26 + (ch - 'A' + 1);
            }

            return index;
        }

        private static string RemoveUnitSuffix(string header)
        {
            return UnitRegex.Replace(header ?? string.Empty, string.Empty).Trim();
        }

        private static string MakeUniqueCode(string preferred, HashSet<string> used)
        {
            string code = string.IsNullOrWhiteSpace(preferred) ? "XLSX-Channel" : preferred.Trim();
            if (used.Add(code))
            {
                return code;
            }

            int suffix = 2;
            while (!used.Add(code + "#" + suffix.ToString(CultureInfo.InvariantCulture)))
            {
                suffix++;
            }

            return code + "#" + suffix.ToString(CultureInfo.InvariantCulture);
        }
    }
}
