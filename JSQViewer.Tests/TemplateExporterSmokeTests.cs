using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Xml.Linq;
using System.Text.RegularExpressions;
using JSQViewer.Core;
using JSQViewer.Export;
using JSQViewer.Settings;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace JSQViewer.Tests
{
    [TestClass]
    public class TemplateExporterSmokeTests
    {
        [TestMethod]
        public void Export_SingleCabinet_PreservesWorkbookStructureAndDataRows()
        {
            byte[] workbook = TemplateExporter.Export(
                GetTemplatePath(),
                "C:\\tests\\single-cabinet",
                CreateData(
                    new long[] { 0L, 20000L, 40000L },
                    new Dictionary<string, double?[]>
                    {
                        ["Pc"] = new double?[] { 11d, 12d, 13d },
                        ["Pe"] = new double?[] { 21d, 22d, 23d },
                        ["T-sie"] = new double?[] { 31d, 32d, 33d },
                        ["UR-sie"] = new double?[] { 41d, 42d, 43d }
                    }),
                new[] { "Pc", "Pe", "T-sie", "UR-sie" },
                includeExtra: false,
                refrigerant: "R290",
                viewerSettings: ViewerSettingsModel.CreateDefault());

            WorkbookSnapshot snapshot = ReadWorkbook(workbook);

            Assert.IsTrue(snapshot.HasSheet1);
            Assert.IsTrue(snapshot.HasWorkbookPart);
            Assert.AreEqual("R290", snapshot.GetInlineString("B1"));
            Assert.AreEqual("C:\\tests\\single-cabinet", snapshot.GetInlineString("D1"));
            Assert.AreEqual("Время испытания, мин", snapshot.GetCellText("B2"));
            Assert.AreEqual("Текущее время, Мин", snapshot.GetCellText("C2"));
            Assert.AreEqual("Рc, Давление нагнетания", snapshot.GetCellText("D2"));
            Assert.AreEqual("Tcd температура конденсації", snapshot.GetCellText("U2"));
            Assert.AreEqual("11", snapshot.GetNumericValue("D4"));
            Assert.AreEqual("21", snapshot.GetNumericValue("E4"));
            Assert.IsTrue(snapshot.RowNumbers.Contains(4));
            Assert.IsTrue(snapshot.RowNumbers.Contains(5));
            Assert.IsTrue(snapshot.RowNumbers.Contains(6));
        }

        [TestMethod]
        public void Export_DoubleCabinet_IncludesExtraHeadersAndWorkbookStructure()
        {
            byte[] workbook = TemplateExporter.Export(
                GetTemplatePath(),
                "C:\\tests\\double-cabinet",
                CreateData(
                    new long[] { 0L, 20000L, 40000L },
                    new Dictionary<string, double?[]>
                    {
                        ["Pc"] = new double?[] { 51d, 52d, 53d },
                        ["Pe"] = new double?[] { 61d, 62d, 63d },
                        ["T-sie"] = new double?[] { 71d, 72d, 73d },
                        ["UR-sie"] = new double?[] { 81d, 82d, 83d },
                        ["X-01"] = new double?[] { 91d, 92d, 93d }
                    }),
                new[] { "Pc", "Pe", "T-sie", "UR-sie", "X-01" },
                includeExtra: true,
                refrigerant: "R600a",
                viewerSettings: ViewerSettingsModel.CreateDefault());

            WorkbookSnapshot snapshot = ReadWorkbook(workbook);

            Assert.IsTrue(snapshot.HasSheet1);
            Assert.IsTrue(snapshot.HasWorkbookPart);
            Assert.AreEqual("R600a", snapshot.GetInlineString("B1"));
            Assert.AreEqual("C:\\tests\\double-cabinet", snapshot.GetInlineString("D1"));
            Assert.AreEqual("Время испытания, мин", snapshot.GetCellText("B2"));
            Assert.AreEqual("Текущее время, Мин", snapshot.GetCellText("C2"));
            Assert.AreEqual("Рc, Давление нагнетания", snapshot.GetCellText("D2"));
            Assert.AreEqual("Tcd температура конденсації", snapshot.GetCellText("U2"));
            Assert.AreEqual("51", snapshot.GetNumericValue("D4"));
            Assert.AreEqual("91", snapshot.GetNumericValue("Z4"));
            Assert.AreEqual("X-01 [u]", snapshot.GetInlineString("Z3"));
            Assert.IsTrue(snapshot.RowNumbers.Contains(4));
            Assert.IsTrue(snapshot.RowNumbers.Contains(5));
            Assert.IsTrue(snapshot.RowNumbers.Contains(6));
        }

        [TestMethod]
        public void Export_DoubleCabinet_SortsMultipleExtraHeadersByNaturalOrder()
        {
            TestData data = CreateData(
                new long[] { 0L, 20000L, 40000L },
                new Dictionary<string, double?[]>
                {
                    ["Pc"] = new double?[] { 51d, 52d, 53d },
                    ["Pe"] = new double?[] { 61d, 62d, 63d },
                    ["T-sie"] = new double?[] { 71d, 72d, 73d },
                    ["UR-sie"] = new double?[] { 81d, 82d, 83d },
                    ["X-10"] = new double?[] { 101d, 102d, 103d },
                    ["X-2"] = new double?[] { 201d, 202d, 203d }
                });
            data.ColumnNames = new[] { "Pc", "Pe", "T-sie", "UR-sie", "X-10", "X-2" };

            byte[] workbook = TemplateExporter.Export(
                GetTemplatePath(),
                "C:\\tests\\double-cabinet-order",
                data,
                new[] { "Pc", "Pe", "T-sie", "UR-sie", "X-10", "X-2" },
                includeExtra: true,
                refrigerant: "R600a",
                viewerSettings: ViewerSettingsModel.CreateDefault());

            WorkbookSnapshot snapshot = ReadWorkbook(workbook);

            Assert.AreEqual("X-2 [u]", snapshot.GetInlineString("Z3"));
            Assert.AreEqual("X-10 [u]", snapshot.GetInlineString("AA3"));
            Assert.AreEqual("201", snapshot.GetNumericValue("Z4"));
            Assert.AreEqual("101", snapshot.GetNumericValue("AA4"));
            CollectionAssert.AreEqual(new[] { 1, 2, 3, 4, 5, 6 }, snapshot.RowNumbersInDocumentOrder.ToArray());

            string u4 = snapshot.GetFormula("U4");
            string u5 = snapshot.GetFormula("U5");
            string u6 = snapshot.GetFormula("U6");
            string w6 = snapshot.GetFormula("W6");

            Assert.IsFalse(string.IsNullOrWhiteSpace(u4));
            Assert.IsFalse(string.IsNullOrWhiteSpace(u5));
            Assert.IsFalse(string.IsNullOrWhiteSpace(u6));
            Assert.IsFalse(string.IsNullOrWhiteSpace(w6));
            StringAssert.Contains(u5, "D5");
            StringAssert.Contains(w6, "Z6:BM6");
            Assert.AreEqual("2:55", snapshot.GetRowAttribute(4, "spans"));
            Assert.AreEqual("0.25", snapshot.GetRowAttribute(4, "dyDescent", "http://schemas.microsoft.com/office/spreadsheetml/2009/9/ac"));
            Assert.AreEqual("e", snapshot.GetCellAttribute("Y4", "t"));
            Assert.AreEqual("37", snapshot.GetCellAttribute("Y4", "s"));
            Assert.AreEqual("shared", snapshot.GetFormulaAttribute("W4", "t"));
            Assert.IsNull(snapshot.GetFormulaAttribute("W5", "t"));
            Assert.IsNull(snapshot.GetFormulaAttribute("W5", "si"));
            Assert.IsNull(snapshot.GetFormulaAttribute("W5", "ref"));
            Assert.IsNull(snapshot.GetFormulaAttribute("Y5", "t"));
            Assert.IsNull(snapshot.GetFormulaAttribute("Y5", "si"));
        }

        [TestMethod]
        public void Export_DoubleCabinet_WithLargeDataset_StillProducesWorkbookStructure()
        {
            const int rowCount = 2000;
            byte[] workbook = TemplateExporter.Export(
                GetTemplatePath(),
                "C:\\tests\\double-cabinet-large",
                CreateData(
                    CreateTimestamps(rowCount),
                    new Dictionary<string, double?[]>
                    {
                        ["Pc"] = CreateSeries(rowCount, 100d),
                        ["Pe"] = CreateSeries(rowCount, 200d),
                        ["T-sie"] = CreateSeries(rowCount, 300d),
                        ["UR-sie"] = CreateSeries(rowCount, 400d),
                        ["X-01"] = CreateSeries(rowCount, 500d)
                    }),
                new[] { "Pc", "Pe", "T-sie", "UR-sie", "X-01" },
                includeExtra: true,
                refrigerant: "R600a",
                viewerSettings: ViewerSettingsModel.CreateDefault());

            WorkbookSnapshot snapshot = ReadWorkbook(workbook);

            Assert.IsTrue(snapshot.HasSheet1);
            Assert.IsTrue(snapshot.HasWorkbookPart);
            Assert.AreEqual("R600a", snapshot.GetInlineString("B1"));
            Assert.AreEqual("C:\\tests\\double-cabinet-large", snapshot.GetInlineString("D1"));
            Assert.AreEqual("Время испытания, мин", snapshot.GetCellText("B2"));
            Assert.AreEqual("Текущее время, Мин", snapshot.GetCellText("C2"));
            Assert.AreEqual("Рc, Давление нагнетания", snapshot.GetCellText("D2"));
            Assert.AreEqual("Tcd температура конденсації", snapshot.GetCellText("U2"));
            Assert.AreEqual("100", snapshot.GetNumericValue("D4"));
            Assert.AreEqual("500", snapshot.GetNumericValue("Z4"));
            Assert.AreEqual("X-01 [u]", snapshot.GetInlineString("Z3"));
            Assert.IsTrue(snapshot.RowNumbers.Contains(4));
            Assert.IsTrue(snapshot.RowNumbers.Contains(2003));
        }

        [TestMethod]
        public void Export_WithGeneratedRows_PreservesWorkbookPostProcessingAndValidation()
        {
            byte[] workbook = TemplateExporter.Export(
                GetTemplatePath(),
                "C:\\tests\\streaming-postprocess",
                CreateData(
                    new long[] { 0L, 20000L, 40000L },
                    new Dictionary<string, double?[]>
                    {
                        ["Pc"] = new double?[] { 11d, 12d, 13d },
                        ["Pe"] = new double?[] { 21d, 22d, 23d },
                        ["T-sie"] = new double?[] { 31d, 32d, 33d },
                        ["UR-sie"] = new double?[] { 41d, 42d, 43d }
                    }),
                new[] { "Pc", "Pe", "T-sie", "UR-sie" },
                includeExtra: false,
                refrigerant: "R290",
                viewerSettings: ViewerSettingsModel.CreateDefault());

            WorkbookSnapshot snapshot = ReadWorkbook(workbook);

            Assert.IsTrue(snapshot.ConditionalFormattingRanges.Count > 0, "Expected conditional formatting rules on exported worksheet.");
            CollectionAssert.Contains(snapshot.ConditionalFormattingRanges.ToList(), "B4:P6");
            CollectionAssert.Contains(snapshot.ConditionalFormattingRanges.ToList(), "W4:W6");
            CollectionAssert.Contains(snapshot.ConditionalFormattingRanges.ToList(), "X4:X6");
            CollectionAssert.Contains(snapshot.ConditionalFormattingRanges.ToList(), "Y4:Y6");
            Assert.IsTrue(
                snapshot.ConditionalFormattingRanges.All(range => RangeTargetsRows(range, 4, 6)),
                "All conditional formatting ranges must target generated rows 4 through 6.");

            Assert.IsFalse(snapshot.HasEntry("xl/calcChain.xml"), "calcChain.xml must be removed after export post-processing.");
            Assert.AreEqual("1", snapshot.GetWorkbookCalcPrAttribute("fullCalcOnLoad"));
            Assert.AreEqual("auto", snapshot.GetWorkbookCalcPrAttribute("calcMode"));

            TemplateValidationResult validation = TemplateExportValidator.Validate(workbook);
            Assert.IsTrue(validation.Ok, validation.Message);
            Assert.IsTrue(snapshot.AllXmlPartsAreWellFormed, snapshot.XmlPartValidationError);
        }

        [TestMethod]
        public void Export_WithProfilingSink_WritesTimingBreakdown()
        {
            var profileLines = new List<string>();
            const int rowCount = 2000;

            byte[] workbook = TemplateExporter.Export(
                GetTemplatePath(),
                "C:\\tests\\profiled-export",
                CreateData(
                    CreateTimestamps(rowCount),
                    new Dictionary<string, double?[]>
                    {
                        ["Pc"] = CreateSeries(rowCount, 51d),
                        ["Pe"] = CreateSeries(rowCount, 61d),
                        ["T-sie"] = CreateSeries(rowCount, 71d),
                        ["UR-sie"] = CreateSeries(rowCount, 81d),
                        ["X-01"] = CreateSeries(rowCount, 91d)
                    }),
                new[] { "Pc", "Pe", "T-sie", "UR-sie", "X-01" },
                includeExtra: true,
                refrigerant: "R600a",
                viewerSettings: ViewerSettingsModel.CreateDefault(),
                profilingSink: profileLines.Add);

            Assert.IsTrue(workbook.Length > 0);

            Assert.AreEqual(1, profileLines.Count);
            string output = profileLines[0];
            StringAssert.Contains(output, "preparation=");
            StringAssert.Contains(output, "templateLoad=");
            StringAssert.Contains(output, "worksheetWrite=");
            StringAssert.Contains(output, "postProcess=");
            StringAssert.Contains(output, "total=");

            Match match = Regex.Match(
                output,
                @"preparation=(?<pre>\d+), templateLoad=(?<load>\d+), worksheetWrite=(?<write>\d+), postProcess=(?<post>\d+), total=(?<total>\d+)");
            Assert.IsTrue(match.Success, "Expected a parseable timing summary from the profiling sink.");

            long preparation = long.Parse(match.Groups["pre"].Value);
            long templateLoad = long.Parse(match.Groups["load"].Value);
            long worksheetWrite = long.Parse(match.Groups["write"].Value);
            long postProcess = long.Parse(match.Groups["post"].Value);
            long total = long.Parse(match.Groups["total"].Value);

            Assert.IsTrue(preparation >= 0);
            Assert.IsTrue(templateLoad >= 0);
            Assert.IsTrue(worksheetWrite >= 0);
            Assert.IsTrue(postProcess >= 0);
            Assert.IsTrue(total >= 0);
            Assert.IsTrue(preparation > 0 || templateLoad > 0 || worksheetWrite > 0 || postProcess > 0 || total > 0);
        }

        private static TestData CreateData(long[] timestamps, Dictionary<string, double?[]> columns)
        {
            var data = new TestData
            {
                Root = "C:\\tests\\root",
                RowCount = timestamps.Length,
                TimestampsMs = timestamps,
                ColumnNames = columns.Keys.ToArray()
            };

            foreach (KeyValuePair<string, double?[]> pair in columns)
            {
                data.Columns[pair.Key] = pair.Value;
                data.Channels[pair.Key] = new ChannelInfo
                {
                    Code = pair.Key,
                    Name = pair.Key,
                    Unit = "u"
                };
            }

            return data;
        }

        private static long[] CreateTimestamps(int rowCount)
        {
            var timestamps = new long[rowCount];
            for (int i = 0; i < rowCount; i++)
            {
                timestamps[i] = (long)i * 20000L;
            }

            return timestamps;
        }

        private static double?[] CreateSeries(int rowCount, double startValue)
        {
            var values = new double?[rowCount];
            for (int i = 0; i < rowCount; i++)
            {
                values[i] = startValue + i;
            }

            return values;
        }

        private static bool RangeTargetsRows(string sqref, int expectedFirstRow, int expectedLastRow)
        {
            if (string.IsNullOrWhiteSpace(sqref))
            {
                return false;
            }

            string[] ranges = sqref.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (string range in ranges)
            {
                Match match = Regex.Match(range, @"^[A-Za-z]+\$?(?<first>\d+):[A-Za-z]+\$?(?<last>\d+)$");
                if (!match.Success)
                {
                    return false;
                }

                int firstRow;
                int lastRow;
                if (!int.TryParse(match.Groups["first"].Value, out firstRow)
                    || !int.TryParse(match.Groups["last"].Value, out lastRow))
                {
                    return false;
                }

                if (firstRow != expectedFirstRow || lastRow != expectedLastRow)
                {
                    return false;
                }
            }

            return true;
        }

        private static string GetTemplatePath()
        {
            string current = AppDomain.CurrentDomain.BaseDirectory;
            for (int i = 0; i < 8; i++)
            {
                string candidate = Path.Combine(current, "template.xlsx");
                if (File.Exists(candidate))
                {
                    return candidate;
                }

                DirectoryInfo parent = Directory.GetParent(current);
                if (parent == null)
                {
                    break;
                }

                current = parent.FullName;
            }

            throw new FileNotFoundException("Unable to locate template.xlsx from the test output directory.");
        }

        private static WorkbookSnapshot ReadWorkbook(byte[] workbook)
        {
            using (var ms = new MemoryStream(workbook))
            using (var zip = new ZipArchive(ms, ZipArchiveMode.Read, true))
            {
                ZipArchiveEntry workbookEntry = zip.GetEntry("xl/workbook.xml");
                ZipArchiveEntry sheetEntry = zip.GetEntry("xl/worksheets/sheet1.xml");
                List<string> sharedStrings = ReadSharedStrings(zip.GetEntry("xl/sharedStrings.xml"));
                var entryNames = new HashSet<string>(
                    zip.Entries.Select(e => e.FullName),
                    StringComparer.OrdinalIgnoreCase);

                bool allXmlPartsAreWellFormed = true;
                string xmlPartValidationError = null;
                foreach (ZipArchiveEntry entry in zip.Entries.Where(e => e.FullName.EndsWith(".xml", StringComparison.OrdinalIgnoreCase)))
                {
                    try
                    {
                        using (Stream stream = entry.Open())
                        {
                            XDocument.Load(stream, LoadOptions.PreserveWhitespace);
                        }
                    }
                    catch (Exception ex)
                    {
                        allXmlPartsAreWellFormed = false;
                        xmlPartValidationError = "Invalid XML part: " + entry.FullName + ". " + ex.Message;
                        break;
                    }
                }

                XDocument workbookDocument = null;
                if (workbookEntry != null)
                {
                    using (Stream stream = workbookEntry.Open())
                    {
                        workbookDocument = XDocument.Load(stream, LoadOptions.PreserveWhitespace);
                    }
                }

                XDocument sheetDocument = null;
                if (sheetEntry != null)
                {
                    using (Stream stream = sheetEntry.Open())
                    {
                        sheetDocument = XDocument.Load(stream, LoadOptions.PreserveWhitespace);
                    }
                }

                return new WorkbookSnapshot(
                    workbookEntry != null,
                    sheetEntry != null,
                    workbookDocument,
                    sheetDocument,
                    sharedStrings,
                    entryNames,
                    allXmlPartsAreWellFormed,
                    xmlPartValidationError);
            }
        }

        private static WorkbookSnapshot ReadWorkbook(string workbookPath)
        {
            return ReadWorkbook(File.ReadAllBytes(workbookPath));
        }

        private static List<string> ReadSharedStrings(ZipArchiveEntry sharedStringsEntry)
        {
            if (sharedStringsEntry == null)
            {
                return new List<string>();
            }

            using (Stream stream = sharedStringsEntry.Open())
            {
                XDocument sharedStringsDocument = XDocument.Load(stream, LoadOptions.PreserveWhitespace);
                XNamespace ns = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
                return sharedStringsDocument
                    .Root?
                    .Elements(ns + "si")
                    .Select(si => string.Concat(si.Descendants(ns + "t").Select(t => t.Value)))
                    .ToList() ?? new List<string>();
            }
        }

        private sealed class WorkbookSnapshot
        {
            private static readonly XNamespace Ns = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";

            private readonly XDocument _workbookDocument;
            private readonly XDocument _sheetDocument;
            private readonly List<string> _sharedStrings;
            private readonly HashSet<string> _entryNames;

            public WorkbookSnapshot(
                bool hasWorkbookPart,
                bool hasSheet1,
                XDocument workbookDocument,
                XDocument sheetDocument,
                List<string> sharedStrings,
                HashSet<string> entryNames,
                bool allXmlPartsAreWellFormed,
                string xmlPartValidationError)
            {
                HasWorkbookPart = hasWorkbookPart;
                HasSheet1 = hasSheet1;
                _workbookDocument = workbookDocument;
                _sheetDocument = sheetDocument;
                _sharedStrings = sharedStrings ?? new List<string>();
                _entryNames = entryNames ?? new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                AllXmlPartsAreWellFormed = allXmlPartsAreWellFormed;
                XmlPartValidationError = xmlPartValidationError;
            }

            public bool HasWorkbookPart { get; }

            public bool HasSheet1 { get; }

            public bool AllXmlPartsAreWellFormed { get; }

            public string XmlPartValidationError { get; }

            public List<string> ConditionalFormattingRanges
            {
                get
                {
                    var ranges = new List<string>();
                    if (_sheetDocument == null || _sheetDocument.Root == null)
                    {
                        return ranges;
                    }

                    foreach (XElement conditionalFormatting in _sheetDocument.Root.Elements(Ns + "conditionalFormatting"))
                    {
                        XAttribute sqref = conditionalFormatting.Attribute("sqref");
                        if (sqref != null && !string.IsNullOrWhiteSpace(sqref.Value))
                        {
                            ranges.Add(sqref.Value);
                        }
                    }

                    return ranges;
                }
            }

            public HashSet<int> RowNumbers
            {
                get
                {
                    var rows = new HashSet<int>();
                    XElement sheetData = GetSheetData();
                    if (sheetData == null)
                    {
                        return rows;
                    }

                    foreach (XElement row in sheetData.Elements(Ns + "row"))
                    {
                        int rowIndex;
                        XAttribute r = row.Attribute("r");
                        if (r != null && int.TryParse(r.Value, out rowIndex))
                        {
                            rows.Add(rowIndex);
                        }
                    }

                    return rows;
                }
            }

            public List<int> RowNumbersInDocumentOrder
            {
                get
                {
                    var rows = new List<int>();
                    XElement sheetData = GetSheetData();
                    if (sheetData == null)
                    {
                        return rows;
                    }

                    foreach (XElement row in sheetData.Elements(Ns + "row"))
                    {
                        int rowIndex;
                        XAttribute r = row.Attribute("r");
                        if (r != null && int.TryParse(r.Value, out rowIndex))
                        {
                            rows.Add(rowIndex);
                        }
                    }

                    return rows;
                }
            }

            public string GetInlineString(string cellRef)
            {
                XElement cell = FindCell(cellRef);
                if (cell == null)
                {
                    return null;
                }

                XElement isNode = cell.Element(Ns + "is");
                if (isNode == null)
                {
                    return null;
                }

                XElement text = isNode.Element(Ns + "t");
                return text == null ? null : text.Value;
            }

            public string GetCellText(string cellRef)
            {
                XElement cell = FindCell(cellRef);
                if (cell == null)
                {
                    return null;
                }

                string type = (string)cell.Attribute("t");
                if (string.Equals(type, "inlineStr", StringComparison.OrdinalIgnoreCase))
                {
                    return GetInlineString(cellRef);
                }

                XElement value = cell.Element(Ns + "v");
                if (value == null)
                {
                    return null;
                }

                if (string.Equals(type, "s", StringComparison.OrdinalIgnoreCase))
                {
                    int index;
                    if (int.TryParse(value.Value, out index) && index >= 0 && index < _sharedStrings.Count)
                    {
                        return _sharedStrings[index];
                    }
                }

                return value.Value;
            }

            public string GetNumericValue(string cellRef)
            {
                XElement cell = FindCell(cellRef);
                if (cell == null)
                {
                    return null;
                }

                XElement value = cell.Element(Ns + "v");
                return value == null ? null : value.Value;
            }

            public string GetFormula(string cellRef)
            {
                XElement cell = FindCell(cellRef);
                if (cell == null)
                {
                    return null;
                }

                XElement formula = cell.Element(Ns + "f");
                return formula == null ? null : formula.Value;
            }

            public string GetFormulaAttribute(string cellRef, string attributeLocalName, string namespaceUri = null)
            {
                XElement cell = FindCell(cellRef);
                if (cell == null)
                {
                    return null;
                }

                XElement formula = cell.Element(Ns + "f");
                if (formula == null)
                {
                    return null;
                }

                XAttribute attribute = string.IsNullOrWhiteSpace(namespaceUri)
                    ? formula.Attribute(attributeLocalName)
                    : formula.Attribute(XName.Get(attributeLocalName, namespaceUri));
                return attribute == null ? null : attribute.Value;
            }

            public string GetRowAttribute(int rowIndex, string attributeLocalName, string namespaceUri = null)
            {
                XElement row = FindRow(rowIndex);
                if (row == null)
                {
                    return null;
                }

                XAttribute attribute = string.IsNullOrWhiteSpace(namespaceUri)
                    ? row.Attribute(attributeLocalName)
                    : row.Attribute(XName.Get(attributeLocalName, namespaceUri));
                return attribute == null ? null : attribute.Value;
            }

            public string GetCellAttribute(string cellRef, string attributeLocalName, string namespaceUri = null)
            {
                XElement cell = FindCell(cellRef);
                if (cell == null)
                {
                    return null;
                }

                XAttribute attribute = string.IsNullOrWhiteSpace(namespaceUri)
                    ? cell.Attribute(attributeLocalName)
                    : cell.Attribute(XName.Get(attributeLocalName, namespaceUri));
                return attribute == null ? null : attribute.Value;
            }

            public bool HasEntry(string entryPath)
            {
                if (string.IsNullOrWhiteSpace(entryPath))
                {
                    return false;
                }

                return _entryNames.Contains(entryPath);
            }

            public string GetWorkbookCalcPrAttribute(string attributeLocalName)
            {
                if (_workbookDocument == null || _workbookDocument.Root == null || string.IsNullOrWhiteSpace(attributeLocalName))
                {
                    return null;
                }

                XElement calcPr = _workbookDocument.Root.Element(Ns + "calcPr");
                if (calcPr == null)
                {
                    return null;
                }

                XAttribute attribute = calcPr.Attribute(attributeLocalName);
                return attribute == null ? null : attribute.Value;
            }

            private XElement FindCell(string cellRef)
            {
                XElement sheetData = GetSheetData();
                if (sheetData == null)
                {
                    return null;
                }

                foreach (XElement row in sheetData.Elements(Ns + "row"))
                {
                    foreach (XElement cell in row.Elements(Ns + "c"))
                    {
                        XAttribute r = cell.Attribute("r");
                        if (r != null && string.Equals(r.Value, cellRef, StringComparison.OrdinalIgnoreCase))
                        {
                            return cell;
                        }
                    }
                }

                return null;
            }

            private XElement FindRow(int rowIndex)
            {
                XElement sheetData = GetSheetData();
                if (sheetData == null)
                {
                    return null;
                }

                foreach (XElement row in sheetData.Elements(Ns + "row"))
                {
                    XAttribute r = row.Attribute("r");
                    int currentRow;
                    if (r != null
                        && int.TryParse(r.Value, out currentRow)
                        && currentRow == rowIndex)
                    {
                        return row;
                    }
                }

                return null;
            }

            private XElement GetSheetData()
            {
                if (_sheetDocument == null || _sheetDocument.Root == null)
                {
                    return null;
                }

                return _sheetDocument.Root.Element(Ns + "sheetData");
            }
        }
    }
}
