using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Packaging;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using System.Runtime.CompilerServices;
using JSQViewer.Application.Charting;
using JSQViewer.Core;
using JSQViewer.Settings;

[assembly: InternalsVisibleTo("JSQViewer.Tests")]

namespace JSQViewer.Export
{
    public static class TemplateExporter
    {
        private static readonly Regex SplitNatRegex = new Regex("(\\d+)", RegexOptions.Compiled);
        private static readonly Regex A1RefRegex = new Regex("([$]?[A-Za-z]{1,3})([$]?)(\\d+)", RegexOptions.Compiled);
        private static readonly Dictionary<string, int> KeyToColumn = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            { "Pc", 4 }, { "Pe", 5 }, { "T-sie", 6 }, { "UR-sie", 7 },
            { "Tc", 8 }, { "Te", 9 }, { "T1", 10 }, { "T2", 11 },
            { "T3", 12 }, { "T4", 13 }, { "T5", 14 }, { "T6", 15 },
            { "T7", 16 }, { "I", 17 }, { "F", 18 }, { "V", 19 }, { "W", 20 }
        };

        public static byte[] Export(
            string templatePath,
            string loadedFolder,
            TestData data,
            IList<string> selectedChannels,
            bool includeExtra,
            string refrigerant,
            ViewerSettingsModel viewerSettings,
            long? rangeStartMs = null,
            long? rangeEndMs = null,
            Action<string> profilingSink = null)
        {
            if (data == null || data.TimestampsMs == null || data.TimestampsMs.Length == 0)
            {
                throw new InvalidOperationException("No loaded data to export.");
            }
            if (!File.Exists(templatePath))
            {
                throw new FileNotFoundException("template.xlsx not found.", templatePath);
            }

            bool profile = profilingSink != null
                || string.Equals(Environment.GetEnvironmentVariable("JSQ_EXPORT_PROFILE"), "1", StringComparison.Ordinal);
            Stopwatch totalTimer = profile ? Stopwatch.StartNew() : null;
            Stopwatch stageTimer = profile ? Stopwatch.StartNew() : null;
            long preparationMs = 0L;
            long templateLoadMs = 0L;
            long worksheetWriteMs = 0L;
            long postProcessMs = 0L;

            string rootFolder = loadedFolder ?? string.Empty;
            string refrigerantNorm = refrigerant == "R600a" ? "R600a" : "R290";
            string[] cols = data.ColumnNames ?? new string[0];
            long[] tList = data.TimestampsMs;
            TimeGridPreparationResult gridAndIndices = BuildTimeGridAndIndices(tList, rangeStartMs, rangeEndMs);
            List<int> idxs = gridAndIndices.Indices;

            Dictionary<string, string> fixedMap = ResolveFixedChannelMap(cols, selectedChannels);
            var selectedSet = new HashSet<string>(selectedChannels ?? new string[0], StringComparer.OrdinalIgnoreCase);

            var fixedCodes = new HashSet<string>(
                fixedMap.Values.Where(s => !string.IsNullOrWhiteSpace(s)),
                StringComparer.OrdinalIgnoreCase);

            List<string> extraCodes = includeExtra
                ? ResolveExtraChannelCodes(cols, selectedSet, fixedCodes)
                : new List<string>();

            extraCodes.Sort(new NaturalDisplayComparer(data.Channels));
            const int extraStartCol = 26;
            if (profile)
            {
                preparationMs = stageTimer.ElapsedMilliseconds;
                stageTimer.Restart();
            }

            byte[] templateBytes = File.ReadAllBytes(templatePath);
            using (var ms = new MemoryStream())
            {
                ms.Write(templateBytes, 0, templateBytes.Length);
                ms.Position = 0;

                using (Package package = Package.Open(ms, FileMode.Open, FileAccess.ReadWrite))
                {
                    Uri sheetUri = new Uri("/xl/worksheets/sheet1.xml", UriKind.Relative);
                    if (!package.PartExists(sheetUri))
                    {
                        throw new InvalidDataException("sheet1.xml part is missing in template.");
                    }

                    PackagePart sheetPart = package.GetPart(sheetUri);
                    XDocument doc;
                    using (Stream s = sheetPart.GetStream(FileMode.Open, FileAccess.Read))
                    {
                        doc = XDocument.Load(s, LoadOptions.PreserveWhitespace);
                    }

                    XNamespace ns = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
                    XElement worksheet = doc.Root;
                    XElement sheetData = worksheet.Element(ns + "sheetData");
                    if (sheetData == null)
                    {
                        throw new InvalidDataException("sheetData node is missing.");
                    }

                    BuildRowCache(sheetData, ns);

                    SetInlineString(sheetData, ns, 1, 2, refrigerantNorm);
                    SetInlineString(sheetData, ns, 1, 4, rootFolder);

                    int startRow = 4;
                    int headerRow = 3;
                    int[] baseRawColumns = KeyToColumn.Values.Distinct().OrderBy(v => v).ToArray();
                    Dictionary<int, string> patternFormulas = ReadPatternFormulas(sheetData, ns, startRow);
                    Dictionary<int, string> patternStyles = ReadPatternStyles(sheetData, ns, startRow);
                    string patternRowStyle = ReadPatternRowStyle(sheetData, ns, startRow);
                    XElement patternRowTemplate = FindRow(sheetData, ns, startRow);

                    for (int e = 0; e < extraCodes.Count; e++)
                    {
                        string code = extraCodes[e];
                        int col = extraStartCol + e;
                        SetInlineString(sheetData, ns, headerRow, col, BuildTemplateHeader(code, data.Channels));
                    }

                    if (profile)
                    {
                        templateLoadMs = stageTimer.ElapsedMilliseconds;
                        stageTimer.Restart();
                    }

                    if (idxs.Count > 0)
                    {
                        ApplyConditionalFormatting(package, worksheet, ns, startRow, startRow + idxs.Count - 1, viewerSettings);
                    }

                    var streamingContext = new TemplateSheetStreamingContext(
                        startRow,
                        idxs,
                        tList,
                        fixedMap,
                        selectedSet,
                        data,
                        extraCodes,
                        extraStartCol,
                        baseRawColumns,
                        patternFormulas,
                        patternStyles,
                        patternRowStyle,
                        patternRowTemplate == null ? null : new XElement(patternRowTemplate));

                    // Clear caches before writing
                    _rowCache = null;
                    _cellCache = null;

                    using (Stream outStream = sheetPart.GetStream(FileMode.Create, FileAccess.Write))
                    {
                        WriteWorksheetWithStreamedSheetData(outStream, doc, ns, sheetData, streamingContext);
                    }

                    if (profile)
                    {
                        worksheetWriteMs = stageTimer.ElapsedMilliseconds;
                        stageTimer.Restart();
                    }

                    RemoveCalcChain(package);
                    ForceFullCalcOnLoad(package);
                }

                if (profile)
                {
                    postProcessMs = stageTimer.ElapsedMilliseconds;
                    if (profilingSink != null)
                    {
                        profilingSink(
                        "TemplateExporter.Export timing (ms): preparation=" + preparationMs.ToString(CultureInfo.InvariantCulture)
                        + ", templateLoad=" + templateLoadMs.ToString(CultureInfo.InvariantCulture)
                        + ", worksheetWrite=" + worksheetWriteMs.ToString(CultureInfo.InvariantCulture)
                        + ", postProcess=" + postProcessMs.ToString(CultureInfo.InvariantCulture)
                        + ", total=" + totalTimer.ElapsedMilliseconds.ToString(CultureInfo.InvariantCulture));
                    }
                }

                return ms.ToArray();
            }
        }

        internal static Dictionary<string, string> ResolveFixedChannelMap(string[] cols, IEnumerable<string> selectedChannels)
        {
            var selectedSet = new HashSet<string>(selectedChannels ?? new string[0], StringComparer.OrdinalIgnoreCase);
            var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (string key in KeyToColumn.Keys)
            {
                map[key] = ResolveForSelection(key, cols ?? new string[0], selectedSet);
            }

            return map;
        }

        internal static List<string> ResolveExtraChannelCodes(string[] cols, IEnumerable<string> selectedChannels, ISet<string> fixedCodes)
        {
            var selectedSet = new HashSet<string>(selectedChannels ?? new string[0], StringComparer.OrdinalIgnoreCase);
            List<string> candidates = selectedSet.Count > 0
                ? (cols ?? new string[0]).Where(c => selectedSet.Contains(c)).ToList()
                : (cols ?? new string[0]).ToList();

            if (fixedCodes == null || fixedCodes.Count == 0)
            {
                return candidates;
            }

            return candidates.Where(c => !fixedCodes.Contains(c)).ToList();
        }

        internal static TimeGridPreparationResult BuildTimeGridAndIndices(long[] timestamps, long? rangeStartMs = null, long? rangeEndMs = null)
        {
            var gridMs = new List<long>();
            var idxs = new List<int>();
            if (timestamps == null || timestamps.Length == 0)
            {
                return new TimeGridPreparationResult(gridMs, idxs);
            }

            var timestampRangeService = new TimestampRangeService();
            long startMs = rangeStartMs.HasValue ? rangeStartMs.Value : timestamps[0];
            long endMs = rangeEndMs.HasValue ? rangeEndMs.Value : timestamps[timestamps.Length - 1];
            if (startMs > endMs)
            {
                long tmp = startMs;
                startMs = endMs;
                endMs = tmp;
            }

            for (long g = startMs; g <= endMs; g += 20000)
            {
                gridMs.Add(g);
            }

            for (int i = 0; i < gridMs.Count; i++)
            {
                int idx = timestampRangeService.NearestIndex(timestamps, gridMs[i]);
                if (idx < 0 || Math.Abs(timestamps[idx] - gridMs[i]) > 30000)
                {
                    idxs.Add(-1);
                }
                else
                {
                    idxs.Add(idx);
                }
            }

            return new TimeGridPreparationResult(gridMs, idxs);
        }

        private static string ResolveForSelection(string key, string[] cols, HashSet<string> selected)
        {
            var matches = new List<string>();
            for (int i = 0; i < cols.Length; i++)
            {
                string c = cols[i];
                if (string.Equals(c, key, StringComparison.OrdinalIgnoreCase))
                {
                    matches.Add(c);
                }
            }

            string suf = "-" + key;
            for (int i = 0; i < cols.Length; i++)
            {
                string c = cols[i];
                if (c.EndsWith(suf, StringComparison.OrdinalIgnoreCase) && !matches.Contains(c))
                {
                    matches.Add(c);
                }
            }

            if (matches.Count == 0)
            {
                return string.Empty;
            }

            if (selected.Count > 0)
            {
                foreach (string pref in new[] { "A-", "C-" })
                {
                    foreach (string m in matches)
                    {
                        if (selected.Contains(m) && m.StartsWith(pref, StringComparison.OrdinalIgnoreCase))
                        {
                            return m;
                        }
                    }
                }
                foreach (string m in matches)
                {
                    if (selected.Contains(m))
                    {
                        return m;
                    }
                }
                return string.Empty;
            }

            foreach (string pref in new[] { "A-", "C-" })
            {
                foreach (string m in matches)
                {
                    if (m.StartsWith(pref, StringComparison.OrdinalIgnoreCase))
                    {
                        return m;
                    }
                }
            }

            return matches[0];
        }

        internal sealed class TimeGridPreparationResult
        {
            public TimeGridPreparationResult(List<long> gridMs, List<int> indices)
            {
                GridMs = gridMs ?? new List<long>();
                Indices = indices ?? new List<int>();
            }

            public List<long> GridMs { get; }

            public List<int> Indices { get; }
        }

        private static Dictionary<int, string> ReadPatternFormulas(XElement sheetData, XNamespace ns, int patternRow)
        {
            var map = new Dictionary<int, string>();
            XElement row = FindRow(sheetData, ns, patternRow);
            if (row == null)
            {
                return map;
            }

            foreach (XElement cell in row.Elements(ns + "c"))
            {
                XElement f = cell.Element(ns + "f");
                if (f == null || string.IsNullOrWhiteSpace(f.Value))
                {
                    continue;
                }
                XAttribute r = cell.Attribute("r");
                if (r == null)
                {
                    continue;
                }
                int col = ParseColumnIndexFromCellRef(r.Value);
                if (col > 0 && !map.ContainsKey(col))
                {
                    map[col] = f.Value;
                }
            }
            return map;
        }

        private static Dictionary<int, string> ReadPatternStyles(XElement sheetData, XNamespace ns, int patternRow)
        {
            var map = new Dictionary<int, string>();
            XElement row = FindRow(sheetData, ns, patternRow);
            if (row == null)
            {
                return map;
            }

            foreach (XElement cell in row.Elements(ns + "c"))
            {
                XAttribute sAttr = cell.Attribute("s");
                if (sAttr == null || string.IsNullOrWhiteSpace(sAttr.Value))
                {
                    continue;
                }
                XAttribute r = cell.Attribute("r");
                if (r == null)
                {
                    continue;
                }
                int col = ParseColumnIndexFromCellRef(r.Value);
                if (col > 0 && !map.ContainsKey(col))
                {
                    map[col] = sAttr.Value;
                }
            }
            return map;
        }

        private static string ReadPatternRowStyle(XElement sheetData, XNamespace ns, int patternRow)
        {
            XElement row = FindRow(sheetData, ns, patternRow);
            if (row == null)
            {
                return null;
            }
            XAttribute sAttr = row.Attribute("s");
            return sAttr != null ? sAttr.Value : null;
        }

        private static void ApplyPatternStyles(XElement sheetData, XNamespace ns, Dictionary<int, string> patternStyles, string patternRowStyle, int targetRow)
        {
            if (patternStyles == null || patternStyles.Count == 0)
            {
                return;
            }

            XElement row = FindRow(sheetData, ns, targetRow);
            if (row == null)
            {
                return;
            }

            // Apply row-level style
            if (!string.IsNullOrWhiteSpace(patternRowStyle))
            {
                if (row.Attribute("s") == null)
                {
                    row.SetAttributeValue("s", patternRowStyle);
                    row.SetAttributeValue("customFormat", "1");
                }
            }

            // Apply cell-level styles
            foreach (XElement cell in row.Elements(ns + "c"))
            {
                XAttribute r = cell.Attribute("r");
                if (r == null)
                {
                    continue;
                }
                int col = ParseColumnIndexFromCellRef(r.Value);
                string style;
                if (col > 0 && patternStyles.TryGetValue(col, out style))
                {
                    if (cell.Attribute("s") == null)
                    {
                        cell.SetAttributeValue("s", style);
                    }
                }
            }
        }

        private static void ApplyTranslatedFormulas(XElement sheetData, XNamespace ns, Dictionary<int, string> patternFormulas, int patternRow, int targetRow)
        {
            if (patternFormulas == null || patternFormulas.Count == 0)
            {
                return;
            }

            foreach (var kv in patternFormulas)
            {
                int col = kv.Key;
                string formula = kv.Value;
                XElement cell = GetOrCreateCell(sheetData, ns, targetRow, col);
                XElement f = cell.Element(ns + "f");
                if (f != null && !string.IsNullOrWhiteSpace(f.Value))
                {
                    // Formula already present — just clear cached value so Excel recalculates
                    XElement existingV = cell.Element(ns + "v");
                    if (existingV != null) existingV.Remove();
                    continue;
                }

                string translated = TranslateFormula(formula, patternRow, targetRow);
                if (f == null)
                {
                    f = new XElement(ns + "f");
                    cell.AddFirst(f);
                }
                f.Value = translated;

                // Remove cached value so Excel recalculates on open
                XElement cachedV = cell.Element(ns + "v");
                if (cachedV != null) cachedV.Remove();
            }
        }

        private static string TranslateFormula(string formula, int patternRow, int targetRow)
        {
            int delta = targetRow - patternRow;
            if (delta == 0 || string.IsNullOrEmpty(formula))
            {
                return formula ?? string.Empty;
            }

            return A1RefRegex.Replace(formula, delegate(Match m)
            {
                string colPart = m.Groups[1].Value;
                string absRow = m.Groups[2].Value;
                string rowPart = m.Groups[3].Value;

                int prevIndex = m.Index - 1;
                if (prevIndex >= 0)
                {
                    char prev = formula[prevIndex];
                    if (char.IsLetterOrDigit(prev) || prev == '_' || prev == '.')
                    {
                        return m.Value;
                    }
                }

                int nextIndex = m.Index + m.Length;
                while (nextIndex < formula.Length && char.IsWhiteSpace(formula[nextIndex]))
                {
                    nextIndex++;
                }
                if (nextIndex < formula.Length && formula[nextIndex] == '(')
                {
                    return m.Value;
                }

                // Do not translate references that are inside quoted text literals,
                // e.g. "R290" / "R600a".
                if (IsInsideQuotedString(formula, m.Index))
                {
                    return m.Value;
                }

                if (absRow == "$")
                {
                    return colPart + absRow + rowPart;
                }

                int row;
                if (!int.TryParse(rowPart, NumberStyles.Integer, CultureInfo.InvariantCulture, out row))
                {
                    return m.Value;
                }

                int shifted = Math.Max(1, row + delta);
                return colPart + shifted.ToString(CultureInfo.InvariantCulture);
            });
        }

        private static bool IsInsideQuotedString(string text, int index)
        {
            bool inside = false;
            for (int i = 0; i < index && i < text.Length; i++)
            {
                if (text[i] != '"') continue;

                // Escaped quote inside a string literal: ""
                if (inside && i + 1 < index && text[i + 1] == '"')
                {
                    i++;
                    continue;
                }

                inside = !inside;
            }
            return inside;
        }

        private static void ApplyConditionalFormatting(
            Package package,
            XElement worksheet,
            XNamespace ns,
            int firstRow,
            int lastRow,
            ViewerSettingsModel settings)
        {
            if (settings == null)
            {
                settings = ViewerSettingsModel.CreateDefault();
            }
            if (settings.row_mark == null) settings.row_mark = ViewerSettingsModel.CreateDefault().row_mark;
            if (settings.discharge_mark == null) settings.discharge_mark = ViewerSettingsModel.CreateDefault().discharge_mark;
            if (settings.suction_mark == null) settings.suction_mark = ViewerSettingsModel.CreateDefault().suction_mark;
            if (settings.scales == null) settings.scales = ViewerSettingsModel.CreateDefault().scales;
            if (!settings.scales.ContainsKey("W")) settings.scales["W"] = ScaleSettings.CreateDefault();
            if (!settings.scales.ContainsKey("X")) settings.scales["X"] = ScaleSettings.CreateDefault();
            if (!settings.scales.ContainsKey("Y")) settings.scales["Y"] = ScaleSettings.CreateDefault();
            if (settings.scales["W"].colors == null) settings.scales["W"].colors = ScaleSettings.CreateDefault().colors;
            if (settings.scales["X"].colors == null) settings.scales["X"].colors = ScaleSettings.CreateDefault().colors;
            if (settings.scales["Y"].colors == null) settings.scales["Y"].colors = ScaleSettings.CreateDefault().colors;

            Uri stylesUri = new Uri("/xl/styles.xml", UriKind.Relative);
            if (!package.PartExists(stylesUri))
            {
                return;
            }

            PackagePart stylesPart = package.GetPart(stylesUri);
            XDocument stylesDoc;
            using (Stream s = stylesPart.GetStream(FileMode.Open, FileAccess.Read))
            {
                stylesDoc = XDocument.Load(s, LoadOptions.PreserveWhitespace);
            }

            XNamespace sns = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
            XElement styleSheet = stylesDoc.Root;
            if (styleSheet == null)
            {
                return;
            }

            XElement dxfs = styleSheet.Element(sns + "dxfs");
            if (dxfs == null)
            {
                dxfs = new XElement(sns + "dxfs", new XAttribute("count", "0"));
                styleSheet.Add(dxfs);
            }

            int prio = 1;
            int dxfRow = AddDxfFill(dxfs, sns, ToArgb(BlendWithWhite(ParseHex(settings.row_mark.color), settings.row_mark.intensity)));
            int dxfDischarge = AddDxfFill(dxfs, sns, ToArgb(ParseHex(settings.discharge_mark.color)));
            int dxfSuction = AddDxfFill(dxfs, sns, ToArgb(ParseHex(settings.suction_mark.color)));
            int dxfWLow = AddDxfFill(dxfs, sns, ToArgb(ParseHex(settings.scales["W"].colors.min)));
            int dxfWMid = AddDxfFill(dxfs, sns, ToArgb(ParseHex(settings.scales["W"].colors.opt)));
            int dxfWHigh = AddDxfFill(dxfs, sns, ToArgb(ParseHex(settings.scales["W"].colors.max)));
            int dxfXLow = AddDxfFill(dxfs, sns, ToArgb(ParseHex(settings.scales["X"].colors.min)));
            int dxfXMid = AddDxfFill(dxfs, sns, ToArgb(ParseHex(settings.scales["X"].colors.opt)));
            int dxfXHigh = AddDxfFill(dxfs, sns, ToArgb(ParseHex(settings.scales["X"].colors.max)));
            int dxfYLow = AddDxfFill(dxfs, sns, ToArgb(ParseHex(settings.scales["Y"].colors.min)));
            int dxfYMid = AddDxfFill(dxfs, sns, ToArgb(ParseHex(settings.scales["Y"].colors.opt)));
            int dxfYHigh = AddDxfFill(dxfs, sns, ToArgb(ParseHex(settings.scales["Y"].colors.max)));

            dxfs.SetAttributeValue("count", dxfs.Elements(sns + "dxf").Count().ToString(CultureInfo.InvariantCulture));

            worksheet.Elements(ns + "conditionalFormatting").Remove();

            AddCf(worksheet, ns, "B" + firstRow + ":P" + lastRow, prio++, dxfRow, "$T" + firstRow + "<" + Fmt(settings.row_mark.threshold_T));

            if (settings.discharge_mark.threshold.HasValue)
            {
                AddCf(worksheet, ns, "H" + firstRow + ":H" + lastRow, prio++, dxfDischarge,
                    "AND($H" + firstRow + "<>\"\",$H" + firstRow + ">" + Fmt(settings.discharge_mark.threshold.Value) + ")");
            }
            if (settings.suction_mark.threshold.HasValue)
            {
                AddCf(worksheet, ns, "I" + firstRow + ":I" + lastRow, prio++, dxfSuction,
                    "AND($I" + firstRow + "<>\"\",$I" + firstRow + "<" + Fmt(settings.suction_mark.threshold.Value) + ")");
            }

            AddScaleCf(worksheet, ns, "W", firstRow, lastRow, settings.scales["W"], ref prio, dxfWLow, dxfWMid, dxfWHigh);
            AddScaleCf(worksheet, ns, "X", firstRow, lastRow, settings.scales["X"], ref prio, dxfXLow, dxfXMid, dxfXHigh);
            AddScaleCf(worksheet, ns, "Y", firstRow, lastRow, settings.scales["Y"], ref prio, dxfYLow, dxfYMid, dxfYHigh);

            using (Stream outStream = stylesPart.GetStream(FileMode.Create, FileAccess.Write))
            {
                stylesDoc.Save(outStream);
            }
        }

        private static void AddScaleCf(XElement worksheet, XNamespace ns, string col, int firstRow, int lastRow, ScaleSettings scale, ref int prio, int dxfLow, int dxfMid, int dxfHigh)
        {
            double min = scale.min;
            double opt = scale.opt;
            if (min > opt)
            {
                double t = min; min = opt; opt = t;
            }

            string rng = col + firstRow + ":" + col + lastRow;
            string fLow = "AND($" + col + firstRow + "<>\"\",$" + col + firstRow + "<" + Fmt(min) + ")";
            string fMid = "AND($" + col + firstRow + "<>\"\",$" + col + firstRow + ">=" + Fmt(min) + ",$" + col + firstRow + "<=" + Fmt(opt) + ")";
            string fHigh = "AND($" + col + firstRow + "<>\"\",$" + col + firstRow + ">" + Fmt(opt) + ")";

            AddCf(worksheet, ns, rng, prio++, dxfLow, fLow);
            AddCf(worksheet, ns, rng, prio++, dxfMid, fMid);
            AddCf(worksheet, ns, rng, prio++, dxfHigh, fHigh);
        }

        private static void AddCf(XElement worksheet, XNamespace ns, string sqref, int priority, int dxfId, string formula)
        {
            var cf = new XElement(ns + "conditionalFormatting", new XAttribute("sqref", sqref));
            var rule = new XElement(ns + "cfRule",
                new XAttribute("type", "expression"),
                new XAttribute("priority", priority.ToString(CultureInfo.InvariantCulture)),
                new XAttribute("dxfId", dxfId.ToString(CultureInfo.InvariantCulture)),
                new XAttribute("stopIfTrue", "1"),
                new XElement(ns + "formula", formula));
            cf.Add(rule);
            InsertConditionalFormatting(worksheet, ns, cf);
        }

        private static void InsertConditionalFormatting(XElement worksheet, XNamespace ns, XElement cf)
        {
            string[] afterCfNodes =
            {
                "dataValidations",
                "hyperlinks",
                "printOptions",
                "pageMargins",
                "pageSetup",
                "headerFooter",
                "rowBreaks",
                "colBreaks",
                "customProperties",
                "cellWatches",
                "ignoredErrors",
                "smartTags",
                "drawing",
                "legacyDrawing",
                "legacyDrawingHF",
                "picture",
                "oleObjects",
                "controls",
                "webPublishItems",
                "tableParts",
                "extLst"
            };

            for (int i = 0; i < afterCfNodes.Length; i++)
            {
                XElement node = worksheet.Element(ns + afterCfNodes[i]);
                if (node != null)
                {
                    node.AddBeforeSelf(cf);
                    return;
                }
            }

            XElement sheetData = worksheet.Element(ns + "sheetData");
            if (sheetData != null)
            {
                sheetData.AddAfterSelf(cf);
                return;
            }

            worksheet.Add(cf);
        }

        private static void RemoveCalcChain(Package package)
        {
            Uri workbookUri = new Uri("/xl/workbook.xml", UriKind.Relative);
            if (package.PartExists(workbookUri))
            {
                PackagePart wbPart = package.GetPart(workbookUri);
                string relType = "http://schemas.openxmlformats.org/officeDocument/2006/relationships/calcChain";
                var rels = wbPart.GetRelationshipsByType(relType).ToList();
                for (int i = 0; i < rels.Count; i++)
                {
                    PackageRelationship rel = rels[i];
                    Uri target = PackUriHelper.ResolvePartUri(workbookUri, rel.TargetUri);
                    wbPart.DeleteRelationship(rel.Id);
                    if (package.PartExists(target))
                    {
                        package.DeletePart(target);
                    }
                }
            }

            Uri direct = new Uri("/xl/calcChain.xml", UriKind.Relative);
            if (package.PartExists(direct))
            {
                package.DeletePart(direct);
            }
        }

        private static void ForceFullCalcOnLoad(Package package)
        {
            Uri workbookUri = new Uri("/xl/workbook.xml", UriKind.Relative);
            if (!package.PartExists(workbookUri)) return;

            PackagePart wbPart = package.GetPart(workbookUri);
            XDocument wbDoc;
            using (Stream s = wbPart.GetStream(FileMode.Open, FileAccess.Read))
            {
                wbDoc = XDocument.Load(s, LoadOptions.PreserveWhitespace);
            }

            XNamespace wns = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
            XElement root = wbDoc.Root;
            if (root == null) return;

            XElement calcPr = root.Element(wns + "calcPr");
            if (calcPr == null)
            {
                calcPr = new XElement(wns + "calcPr");
                root.Add(calcPr);
            }
            calcPr.SetAttributeValue("fullCalcOnLoad", "1");
            calcPr.SetAttributeValue("calcMode", "auto");

            using (Stream outStream = wbPart.GetStream(FileMode.Create, FileAccess.Write))
            {
                wbDoc.Save(outStream);
            }
        }

        private static int AddDxfFill(XElement dxfs, XNamespace ns, string argb)
        {
            int index = dxfs.Elements(ns + "dxf").Count();
            var dxf = new XElement(ns + "dxf",
                new XElement(ns + "fill",
                    new XElement(ns + "patternFill",
                        new XAttribute("patternType", "solid"),
                        new XElement(ns + "fgColor", new XAttribute("rgb", argb)),
                        new XElement(ns + "bgColor", new XAttribute("rgb", argb)))));
            dxfs.Add(dxf);
            return index;
        }

        private static string ParseHex(string hex)
        {
            string s = (hex ?? string.Empty).Trim();
            if (s.StartsWith("#")) s = s.Substring(1);
            if (s.Length != 6) return "FFFFFF";
            for (int i = 0; i < s.Length; i++)
            {
                char ch = s[i];
                bool ok = (ch >= '0' && ch <= '9') || (ch >= 'A' && ch <= 'F') || (ch >= 'a' && ch <= 'f');
                if (!ok) return "FFFFFF";
            }
            return s.ToUpperInvariant();
        }

        private static string ToArgb(string rgb)
        {
            return "FF" + rgb;
        }

        private static string BlendWithWhite(string rgb, int intensity)
        {
            int i = Math.Max(0, Math.Min(100, intensity));
            double w = 1.0 - (i / 100.0);
            int r = int.Parse(rgb.Substring(0, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
            int g = int.Parse(rgb.Substring(2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
            int b = int.Parse(rgb.Substring(4, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
            int r2 = (int)Math.Round(r * (1.0 - w) + 255 * w);
            int g2 = (int)Math.Round(g * (1.0 - w) + 255 * w);
            int b2 = (int)Math.Round(b * (1.0 - w) + 255 * w);
            return r2.ToString("X2", CultureInfo.InvariantCulture)
                 + g2.ToString("X2", CultureInfo.InvariantCulture)
                 + b2.ToString("X2", CultureInfo.InvariantCulture);
        }

        private static string Fmt(double v)
        {
            if (Math.Abs(v - Math.Round(v)) < 0.0000001)
            {
                return ((int)Math.Round(v)).ToString(CultureInfo.InvariantCulture);
            }
            return v.ToString("G", CultureInfo.InvariantCulture);
        }

        private static string BuildTemplateHeader(string code, Dictionary<string, ChannelInfo> channels)
        {
            ChannelInfo ch;
            if (channels != null && channels.TryGetValue(code, out ch))
            {
                string name = !string.IsNullOrWhiteSpace(ch.Name) ? ch.Name.Trim() : code;
                string unit = ch.Unit == null ? string.Empty : ch.Unit.Trim();
                if (!string.IsNullOrWhiteSpace(unit))
                {
                    return name + " [" + unit + "]";
                }
                return name;
            }
            return code;
        }

        private static void WriteWorksheetWithStreamedSheetData(
            Stream outputStream,
            XDocument worksheetDocument,
            XNamespace ns,
            XElement templateSheetData,
            TemplateSheetStreamingContext context)
        {
            if (outputStream == null)
            {
                throw new ArgumentNullException("outputStream");
            }
            if (worksheetDocument == null || worksheetDocument.Root == null)
            {
                throw new InvalidDataException("worksheet xml is missing.");
            }
            if (templateSheetData == null)
            {
                throw new InvalidDataException("sheetData node is missing.");
            }

            XElement worksheet = worksheetDocument.Root;
            var streamedWorksheet = new XStreamingElement(
                worksheet.Name,
                worksheet.Attributes(),
                StreamWorksheetNodes(worksheet, templateSheetData, ns, context));

            var settings = new XmlWriterSettings
            {
                Encoding = new UTF8Encoding(false),
                OmitXmlDeclaration = worksheetDocument.Declaration == null,
                CloseOutput = false
            };

            using (XmlWriter writer = XmlWriter.Create(outputStream, settings))
            {
                if (worksheetDocument.Declaration != null)
                {
                    writer.WriteStartDocument();
                }

                streamedWorksheet.WriteTo(writer);
                writer.Flush();
            }
        }

        private static IEnumerable<object> StreamWorksheetNodes(
            XElement worksheet,
            XElement templateSheetData,
            XNamespace ns,
            TemplateSheetStreamingContext context)
        {
            foreach (XNode node in worksheet.Nodes())
            {
                XElement element = node as XElement;
                if (element != null)
                {
                    if (object.ReferenceEquals(element, templateSheetData))
                    {
                        yield return new XStreamingElement(
                            templateSheetData.Name,
                            templateSheetData.Attributes(),
                            StreamSheetDataRows(templateSheetData, ns, context));
                    }
                    else
                    {
                        yield return new XElement(element);
                    }

                    continue;
                }

                XNode cloned = CloneNodeForStreaming(node);
                if (cloned != null)
                {
                    yield return cloned;
                }
            }
        }

        private static XNode CloneNodeForStreaming(XNode node)
        {
            if (node == null)
            {
                return null;
            }

            var text = node as XText;
            if (text != null)
            {
                return new XText(text.Value);
            }

            var cdata = node as XCData;
            if (cdata != null)
            {
                return new XCData(cdata.Value);
            }

            var comment = node as XComment;
            if (comment != null)
            {
                return new XComment(comment.Value);
            }

            var pi = node as XProcessingInstruction;
            if (pi != null)
            {
                return new XProcessingInstruction(pi.Target, pi.Data);
            }

            var doctype = node as XDocumentType;
            if (doctype != null)
            {
                return new XDocumentType(doctype.Name, doctype.PublicId, doctype.SystemId, doctype.InternalSubset);
            }

            return null;
        }

        private static IEnumerable<object> StreamSheetDataRows(
            XElement templateSheetData,
            XNamespace ns,
            TemplateSheetStreamingContext context)
        {
            int generatedStartRow = context.StartRow;
            int generatedEndRow = context.GeneratedRowCount > 0
                ? context.StartRow + context.GeneratedRowCount - 1
                : context.StartRow - 1;
            bool generatedRowsInserted = false;

            foreach (XNode node in templateSheetData.Nodes())
            {
                XElement row = node as XElement;
                if (row != null && row.Name == ns + "row")
                {
                    int rowIndex;
                    if (TryParseRowIndex(row, out rowIndex))
                    {
                        if (rowIndex < generatedStartRow)
                        {
                            yield return new XElement(row);
                        }
                        else if (rowIndex > generatedEndRow)
                        {
                            if (!generatedRowsInserted)
                            {
                                for (int rowOffset = 0; rowOffset < context.GeneratedRowCount; rowOffset++)
                                {
                                    yield return BuildGeneratedRow(ns, context, rowOffset);
                                }

                                generatedRowsInserted = true;
                            }

                            yield return new XElement(row);
                        }

                        continue;
                    }

                    yield return new XElement(row);
                    continue;
                }

                XNode clone = CloneNodeForStreaming(node);
                if (clone != null)
                {
                    yield return clone;
                }
            }

            if (!generatedRowsInserted)
            {
                for (int rowOffset = 0; rowOffset < context.GeneratedRowCount; rowOffset++)
                {
                    yield return BuildGeneratedRow(ns, context, rowOffset);
                }
            }
        }

        private static XElement BuildGeneratedRow(XNamespace ns, TemplateSheetStreamingContext context, int rowOffset)
        {
            int rowIndex = context.StartRow + rowOffset;
            int sourceIndex = context.SampleIndices[rowOffset];
            XElement row = ClonePatternRowForTarget(ns, context.PatternRowTemplate, context.StartRow, rowIndex, context.PatternFormulas);
            var cells = BuildCellMap(row, ns);

            double elapsedDays = (rowOffset * 20.0) / 86400.0;
            SetNumericCellValue(row, cells, ns, rowIndex, 2, elapsedDays, context.PatternStyles);

            if (sourceIndex >= 0)
            {
                DateTime dt = context.TimestampRangeService.UnixMsToLocalDateTime(context.Timestamps[sourceIndex]);
                double timeOfDayDays = dt.TimeOfDay.TotalSeconds / 86400.0;
                SetNumericCellValue(row, cells, ns, rowIndex, 3, timeOfDayDays, context.PatternStyles);
            }
            else
            {
                ClearCellValue(row, cells, ns, rowIndex, 3, context.PatternStyles);
            }

            for (int i = 0; i < context.BaseRawColumns.Length; i++)
            {
                ClearCellValue(row, cells, ns, rowIndex, context.BaseRawColumns[i], context.PatternStyles);
            }

            if (sourceIndex >= 0)
            {
                foreach (KeyValuePair<string, int> keyPair in KeyToColumn)
                {
                    string code;
                    if (!context.FixedMap.TryGetValue(keyPair.Key, out code) || string.IsNullOrWhiteSpace(code))
                    {
                        continue;
                    }
                    if (context.SelectedSet.Count > 0 && !context.SelectedSet.Contains(code))
                    {
                        continue;
                    }

                    double?[] sourceValues;
                    if (!context.Data.Columns.TryGetValue(code, out sourceValues)
                        || sourceIndex >= sourceValues.Length
                        || !sourceValues[sourceIndex].HasValue)
                    {
                        continue;
                    }

                    SetNumericCellValue(row, cells, ns, rowIndex, keyPair.Value, sourceValues[sourceIndex].Value, context.PatternStyles);
                }

                for (int i = 0; i < context.ExtraCodes.Count; i++)
                {
                    int col = context.ExtraStartColumn + i;
                    string code = context.ExtraCodes[i];
                    double?[] sourceValues;
                    if (!context.Data.Columns.TryGetValue(code, out sourceValues)
                        || sourceIndex >= sourceValues.Length
                        || !sourceValues[sourceIndex].HasValue)
                    {
                        ClearCellValue(row, cells, ns, rowIndex, col, context.PatternStyles);
                        continue;
                    }

                    SetNumericCellValue(row, cells, ns, rowIndex, col, sourceValues[sourceIndex].Value, context.PatternStyles);
                }
            }
            else
            {
                for (int i = 0; i < context.ExtraCodes.Count; i++)
                {
                    ClearCellValue(row, cells, ns, rowIndex, context.ExtraStartColumn + i, context.PatternStyles);
                }
            }

            foreach (KeyValuePair<int, string> formulaPair in context.PatternFormulas)
            {
                int col = formulaPair.Key;
                XElement cell = EnsureCell(row, cells, ns, rowIndex, col, context.PatternStyles);
                XElement formulaNode = cell.Element(ns + "f");
                if (formulaNode == null)
                {
                    formulaNode = new XElement(ns + "f");
                    cell.AddFirst(formulaNode);
                }
                formulaNode.Value = TranslateFormula(formulaPair.Value, context.StartRow, rowIndex);

                XElement cachedValue = cell.Element(ns + "v");
                if (cachedValue != null)
                {
                    cachedValue.Remove();
                }
            }

            UpdateRowSpansAttribute(row, ns);
            return row;
        }

        private static XElement ClonePatternRowForTarget(
            XNamespace ns,
            XElement patternRowTemplate,
            int patternRowIndex,
            int rowIndex,
            Dictionary<int, string> patternFormulas = null)
        {
            if (patternRowTemplate == null)
            {
                return new XElement(
                    ns + "row",
                    new XAttribute("r", rowIndex.ToString(CultureInfo.InvariantCulture)));
            }

            XElement row = new XElement(patternRowTemplate);
            row.SetAttributeValue("r", rowIndex.ToString(CultureInfo.InvariantCulture));

            foreach (XElement cell in row.Elements(ns + "c"))
            {
                XAttribute cellRefAttribute = cell.Attribute("r");
                int columnIndex = ParseColumnIndexFromCellRef(cellRefAttribute == null ? string.Empty : cellRefAttribute.Value);
                if (columnIndex > 0)
                {
                    cell.SetAttributeValue("r", ColumnName(columnIndex) + rowIndex.ToString(CultureInfo.InvariantCulture));
                }

                XElement formulaNode = cell.Element(ns + "f");
                if (formulaNode == null)
                {
                    continue;
                }

                if (!string.IsNullOrWhiteSpace(formulaNode.Value))
                {
                    formulaNode.Value = TranslateFormula(formulaNode.Value, patternRowIndex, rowIndex);
                }
                else if (patternFormulas != null)
                {
                    int formulaColumn = ParseColumnIndexFromCellRef(cellRefAttribute == null ? string.Empty : cellRefAttribute.Value);
                    string patternFormula;
                    if (formulaColumn > 0 && patternFormulas.TryGetValue(formulaColumn, out patternFormula))
                    {
                        formulaNode.Value = TranslateFormula(patternFormula, patternRowIndex, rowIndex);
                    }
                }

                XAttribute formulaRef = formulaNode.Attribute("ref");
                if (formulaRef != null && !string.IsNullOrWhiteSpace(formulaRef.Value))
                {
                    formulaNode.SetAttributeValue(formulaRef.Name, TranslateFormula(formulaRef.Value, patternRowIndex, rowIndex));
                }

                if (rowIndex != patternRowIndex)
                {
                    XAttribute formulaType = formulaNode.Attribute("t");
                    if (formulaType != null && string.Equals(formulaType.Value, "shared", StringComparison.OrdinalIgnoreCase))
                    {
                        formulaType.Remove();
                        XAttribute sharedIndex = formulaNode.Attribute("si");
                        if (sharedIndex != null)
                        {
                            sharedIndex.Remove();
                        }
                        XAttribute translatedRef = formulaNode.Attribute("ref");
                        if (translatedRef != null)
                        {
                            translatedRef.Remove();
                        }
                    }
                }

                XElement cachedValue = cell.Element(ns + "v");
                if (cachedValue != null)
                {
                    cachedValue.Remove();
                }
            }

            return row;
        }

        private static Dictionary<int, XElement> BuildCellMap(XElement row, XNamespace ns)
        {
            var cells = new Dictionary<int, XElement>();
            foreach (XElement cell in row.Elements(ns + "c"))
            {
                XAttribute cellRef = cell.Attribute("r");
                int columnIndex = ParseColumnIndexFromCellRef(cellRef == null ? string.Empty : cellRef.Value);
                if (columnIndex > 0)
                {
                    cells[columnIndex] = cell;
                }
            }

            return cells;
        }

        private static XElement EnsureCell(
            XElement row,
            Dictionary<int, XElement> cells,
            XNamespace ns,
            int rowIndex,
            int colIndex,
            Dictionary<int, string> patternStyles)
        {
            XElement cell;
            if (cells.TryGetValue(colIndex, out cell))
            {
                return cell;
            }

            string cellRef = ColumnName(colIndex) + rowIndex.ToString(CultureInfo.InvariantCulture);
            cell = new XElement(ns + "c", new XAttribute("r", cellRef));

            string style;
            if (patternStyles != null
                && patternStyles.TryGetValue(colIndex, out style)
                && !string.IsNullOrWhiteSpace(style))
            {
                cell.SetAttributeValue("s", style);
            }

            InsertCellInOrder(row, ns, cell, colIndex);
            cells[colIndex] = cell;
            return cell;
        }

        private static void ClearCellValue(
            XElement row,
            Dictionary<int, XElement> cells,
            XNamespace ns,
            int rowIndex,
            int colIndex,
            Dictionary<int, string> patternStyles)
        {
            XElement cell = EnsureCell(row, cells, ns, rowIndex, colIndex, patternStyles);
            cell.SetAttributeValue("t", null);

            XElement inline = cell.Element(ns + "is");
            if (inline != null)
            {
                inline.Remove();
            }

            XElement valueNode = cell.Element(ns + "v");
            if (valueNode != null)
            {
                valueNode.Remove();
            }
        }

        private static void SetNumericCellValue(
            XElement row,
            Dictionary<int, XElement> cells,
            XNamespace ns,
            int rowIndex,
            int colIndex,
            double value,
            Dictionary<int, string> patternStyles)
        {
            XElement cell = EnsureCell(row, cells, ns, rowIndex, colIndex, patternStyles);
            cell.SetAttributeValue("t", null);

            XElement inline = cell.Element(ns + "is");
            if (inline != null)
            {
                inline.Remove();
            }

            XElement formula = cell.Element(ns + "f");
            if (formula != null)
            {
                formula.Remove();
            }

            XElement valueNode = cell.Element(ns + "v");
            if (valueNode == null)
            {
                valueNode = new XElement(ns + "v");
                cell.Add(valueNode);
            }
            valueNode.Value = value.ToString("G17", CultureInfo.InvariantCulture);
        }

        private static bool TryParseRowIndex(XElement row, out int rowIndex)
        {
            rowIndex = -1;
            if (row == null)
            {
                return false;
            }

            XAttribute rowAttr = row.Attribute("r");
            return rowAttr != null
                && int.TryParse(rowAttr.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out rowIndex);
        }

        private static void UpdateRowSpansAttribute(XElement row, XNamespace ns)
        {
            if (row == null)
            {
                return;
            }

            int minCol = int.MaxValue;
            int maxCol = int.MinValue;
            foreach (XElement cell in row.Elements(ns + "c"))
            {
                XAttribute cellRef = cell.Attribute("r");
                int col = ParseColumnIndexFromCellRef(cellRef == null ? string.Empty : cellRef.Value);
                if (col <= 0)
                {
                    continue;
                }

                if (col < minCol)
                {
                    minCol = col;
                }
                if (col > maxCol)
                {
                    maxCol = col;
                }
            }

            if (minCol == int.MaxValue || maxCol == int.MinValue)
            {
                row.SetAttributeValue("spans", null);
                return;
            }

            row.SetAttributeValue(
                "spans",
                minCol.ToString(CultureInfo.InvariantCulture) + ":" + maxCol.ToString(CultureInfo.InvariantCulture));
        }

        private sealed class TemplateSheetStreamingContext
        {
            public TemplateSheetStreamingContext(
                int startRow,
                IList<int> sampleIndices,
                long[] timestamps,
                Dictionary<string, string> fixedMap,
                ISet<string> selectedSet,
                TestData data,
                IList<string> extraCodes,
                int extraStartColumn,
                int[] baseRawColumns,
                Dictionary<int, string> patternFormulas,
                Dictionary<int, string> patternStyles,
                string patternRowStyle,
                XElement patternRowTemplate)
            {
                StartRow = startRow;
                SampleIndices = sampleIndices ?? new List<int>();
                Timestamps = timestamps ?? new long[0];
                FixedMap = fixedMap ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                SelectedSet = selectedSet ?? new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                Data = data ?? new TestData();
                ExtraCodes = extraCodes ?? new List<string>();
                ExtraStartColumn = extraStartColumn;
                BaseRawColumns = baseRawColumns ?? new int[0];
                PatternFormulas = patternFormulas ?? new Dictionary<int, string>();
                PatternStyles = patternStyles ?? new Dictionary<int, string>();
                PatternRowStyle = patternRowStyle;
                PatternRowTemplate = patternRowTemplate;
                TimestampRangeService = new TimestampRangeService();
            }

            public int StartRow { get; }

            public IList<int> SampleIndices { get; }

            public long[] Timestamps { get; }

            public Dictionary<string, string> FixedMap { get; }

            public ISet<string> SelectedSet { get; }

            public TestData Data { get; }

            public IList<string> ExtraCodes { get; }

            public int ExtraStartColumn { get; }

            public int[] BaseRawColumns { get; }

            public Dictionary<int, string> PatternFormulas { get; }

            public Dictionary<int, string> PatternStyles { get; }

            public string PatternRowStyle { get; }

            public XElement PatternRowTemplate { get; }

            public TimestampRangeService TimestampRangeService { get; }

            public int GeneratedRowCount
            {
                get { return SampleIndices.Count; }
            }
        }

        // Cached row/cell lookup for performance — avoids O(n) linear scan per access
        [ThreadStatic] private static Dictionary<int, XElement> _rowCache;
        [ThreadStatic] private static Dictionary<long, XElement> _cellCache;

        private static void BuildRowCache(XElement sheetData, XNamespace ns)
        {
            _rowCache = new Dictionary<int, XElement>();
            _cellCache = new Dictionary<long, XElement>();
            foreach (XElement row in sheetData.Elements(ns + "row"))
            {
                XAttribute r = row.Attribute("r");
                int rowIdx;
                if (r != null && int.TryParse(r.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out rowIdx))
                {
                    _rowCache[rowIdx] = row;
                    foreach (XElement c in row.Elements(ns + "c"))
                    {
                        XAttribute cr = c.Attribute("r");
                        if (cr != null)
                        {
                            int col = ParseColumnIndexFromCellRef(cr.Value);
                            if (col > 0)
                            {
                                long key = ((long)rowIdx << 20) | (uint)col;
                                _cellCache[key] = c;
                            }
                        }
                    }
                }
            }
        }

        private static XElement GetOrCreateRow(XElement sheetData, XNamespace ns, int rowIndex)
        {
            XElement found;
            if (_rowCache != null && _rowCache.TryGetValue(rowIndex, out found))
            {
                return found;
            }

            string wanted = rowIndex.ToString(CultureInfo.InvariantCulture);
            var newRow = new XElement(ns + "row", new XAttribute("r", wanted));
            sheetData.Add(newRow);
            if (_rowCache != null) _rowCache[rowIndex] = newRow;
            return newRow;
        }

        private static XElement FindRow(XElement sheetData, XNamespace ns, int rowIndex)
        {
            XElement found;
            if (_rowCache != null && _rowCache.TryGetValue(rowIndex, out found))
            {
                return found;
            }
            return null;
        }

        private static XElement GetOrCreateCell(XElement sheetData, XNamespace ns, int rowIndex, int colIndex)
        {
            long cacheKey = ((long)rowIndex << 20) | (uint)colIndex;
            XElement found;
            if (_cellCache != null && _cellCache.TryGetValue(cacheKey, out found))
            {
                return found;
            }

            XElement row = GetOrCreateRow(sheetData, ns, rowIndex);
            string cellRef = ColumnName(colIndex) + rowIndex.ToString(CultureInfo.InvariantCulture);

            var cell = new XElement(ns + "c", new XAttribute("r", cellRef));
            InsertCellInOrder(row, ns, cell, colIndex);
            if (_cellCache != null) _cellCache[cacheKey] = cell;
            return cell;
        }

        private static void InsertCellInOrder(XElement row, XNamespace ns, XElement newCell, int newCol)
        {
            foreach (XElement existing in row.Elements(ns + "c"))
            {
                XAttribute r = existing.Attribute("r");
                int col = ParseColumnIndexFromCellRef(r == null ? string.Empty : r.Value);
                if (col > newCol)
                {
                    existing.AddBeforeSelf(newCell);
                    return;
                }
            }
            row.Add(newCell);
        }

        private static void SetInlineString(XElement sheetData, XNamespace ns, int rowIndex, int colIndex, string value)
        {
            XElement cell = GetOrCreateCell(sheetData, ns, rowIndex, colIndex);
            cell.SetAttributeValue("t", "inlineStr");
            XElement v = cell.Element(ns + "v");
            if (v != null) v.Remove();
            XElement f = cell.Element(ns + "f");
            if (f != null) f.Remove();
            XElement isNode = cell.Element(ns + "is");
            if (isNode != null) isNode.Remove();
            cell.Add(new XElement(ns + "is", new XElement(ns + "t", value ?? string.Empty)));
        }

        private static void SetNumber(XElement sheetData, XNamespace ns, int rowIndex, int colIndex, double value)
        {
            XElement cell = GetOrCreateCell(sheetData, ns, rowIndex, colIndex);
            cell.SetAttributeValue("t", null);
            XElement isNode = cell.Element(ns + "is");
            if (isNode != null) isNode.Remove();
            XElement v = cell.Element(ns + "v");
            if (v == null)
            {
                v = new XElement(ns + "v");
                cell.Add(v);
            }
            v.Value = value.ToString("G17", CultureInfo.InvariantCulture);
        }

        private static void ClearCellValue(XElement sheetData, XNamespace ns, int rowIndex, int colIndex)
        {
            XElement cell = GetOrCreateCell(sheetData, ns, rowIndex, colIndex);
            cell.SetAttributeValue("t", null);
            XElement isNode = cell.Element(ns + "is");
            if (isNode != null) isNode.Remove();
            XElement v = cell.Element(ns + "v");
            if (v != null) v.Remove();
            // Keep existing formulas/styles untouched.
        }

        private static string ColumnName(int oneBased)
        {
            int index = oneBased;
            var chars = new List<char>(4);
            while (index > 0)
            {
                int rem = (index - 1) % 26;
                chars.Add((char)('A' + rem));
                index = (index - 1) / 26;
            }
            chars.Reverse();
            return new string(chars.ToArray());
        }

        private static int ParseColumnIndexFromCellRef(string cellRef)
        {
            if (string.IsNullOrWhiteSpace(cellRef))
            {
                return -1;
            }

            int col = 0;
            for (int i = 0; i < cellRef.Length; i++)
            {
                char ch = cellRef[i];
                if (ch >= 'A' && ch <= 'Z')
                {
                    col = col * 26 + (ch - 'A' + 1);
                }
                else if (ch >= 'a' && ch <= 'z')
                {
                    col = col * 26 + (ch - 'a' + 1);
                }
                else if (ch == '$')
                {
                    continue;
                }
                else
                {
                    break;
                }
            }
            return col;
        }

        private static int ParseRowIndexFromCellRef(string cellRef)
        {
            if (string.IsNullOrWhiteSpace(cellRef))
            {
                return -1;
            }
            int start = 0;
            while (start < cellRef.Length && (char.IsLetter(cellRef[start]) || cellRef[start] == '$'))
            {
                start++;
            }
            if (start >= cellRef.Length)
            {
                return -1;
            }
            int row;
            return int.TryParse(cellRef.Substring(start), NumberStyles.Integer, CultureInfo.InvariantCulture, out row) ? row : -1;
        }

        private static void NormalizeSheetData(XElement sheetData, XNamespace ns)
        {
            var rows = sheetData.Elements(ns + "row")
                .OrderBy(r =>
                {
                    XAttribute a = r.Attribute("r");
                    int v;
                    return a != null && int.TryParse(a.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out v) ? v : int.MaxValue;
                })
                .ToList();

            foreach (XElement row in rows)
            {
                XAttribute spans = row.Attribute("spans");
                if (spans != null) spans.Remove();

                var orderedCells = row.Elements(ns + "c")
                    .Select(c =>
                    {
                        XAttribute rAttr = c.Attribute("r");
                        string rValue = rAttr == null ? string.Empty : rAttr.Value;
                        int col = ParseColumnIndexFromCellRef(rValue);
                        int rr = ParseRowIndexFromCellRef(rValue);
                        return new { Cell = c, Col = col, Row = rr, Ref = rValue };
                    })
                    .GroupBy(x => x.Ref, StringComparer.OrdinalIgnoreCase)
                    .Select(g => g.Last())
                    .OrderBy(x => x.Col)
                    .ThenBy(x => x.Row)
                    .ToList();

                row.Elements(ns + "c").Remove();
                for (int i = 0; i < orderedCells.Count; i++)
                {
                    row.Add(orderedCells[i].Cell);
                }
            }

            sheetData.Elements(ns + "row").Remove();
            for (int i = 0; i < rows.Count; i++)
            {
                sheetData.Add(rows[i]);
            }
        }

        private sealed class NaturalDisplayComparer : IComparer<string>
        {
            private readonly Dictionary<string, ChannelInfo> _channels;

            public NaturalDisplayComparer(Dictionary<string, ChannelInfo> channels)
            {
                _channels = channels ?? new Dictionary<string, ChannelInfo>(StringComparer.OrdinalIgnoreCase);
            }

            public int Compare(string x, string y)
            {
                string dx = DisplayName(x);
                string dy = DisplayName(y);
                int byName = CompareNat(dx, dy);
                if (byName != 0)
                {
                    return byName;
                }
                return CompareNat(x, y);
            }

            private string DisplayName(string code)
            {
                ChannelInfo ch;
                if (_channels.TryGetValue(code, out ch))
                {
                    if (!string.IsNullOrWhiteSpace(ch.Name))
                    {
                        return ch.Name.Trim();
                    }
                    if (!string.IsNullOrWhiteSpace(ch.Label))
                    {
                        return ch.Label.Trim();
                    }
                }
                return code ?? string.Empty;
            }

            private static int CompareNat(string a, string b)
            {
                string[] pa = SplitNatRegex.Split(a ?? string.Empty);
                string[] pb = SplitNatRegex.Split(b ?? string.Empty);
                int n = Math.Max(pa.Length, pb.Length);
                for (int i = 0; i < n; i++)
                {
                    if (i >= pa.Length) return -1;
                    if (i >= pb.Length) return 1;

                    int ia;
                    int ib;
                    bool na = int.TryParse(pa[i], out ia);
                    bool nb = int.TryParse(pb[i], out ib);
                    if (na && nb)
                    {
                        int cmp = ia.CompareTo(ib);
                        if (cmp != 0) return cmp;
                        continue;
                    }

                    int t = string.Compare(pa[i], pb[i], StringComparison.OrdinalIgnoreCase);
                    if (t != 0) return t;
                }
                return 0;
            }
        }
    }
}
