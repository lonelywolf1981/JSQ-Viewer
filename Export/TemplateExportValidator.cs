using System;
using System.IO;
using System.IO.Compression;
using System.Xml.Linq;

namespace LeMuReViewer.Export
{
    public sealed class TemplateValidationResult
    {
        public bool Ok { get; set; }
        public string Message { get; set; }
    }

    public static class TemplateExportValidator
    {
        public static TemplateValidationResult Validate(byte[] xlsxBytes)
        {
            try
            {
                using (var ms = new MemoryStream(xlsxBytes))
                using (var zip = new ZipArchive(ms, ZipArchiveMode.Read, true))
                {
                    ZipArchiveEntry sheetEntry = zip.GetEntry("xl/worksheets/sheet1.xml");
                    if (sheetEntry == null)
                    {
                        return new TemplateValidationResult { Ok = false, Message = "sheet1.xml is missing." };
                    }

                    XDocument sheet;
                    using (Stream s = sheetEntry.Open())
                    {
                        sheet = XDocument.Load(s, LoadOptions.PreserveWhitespace);
                    }

                    XNamespace ns = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
                    XElement ws = sheet.Root;
                    if (ws == null)
                    {
                        return new TemplateValidationResult { Ok = false, Message = "Worksheet XML is invalid." };
                    }

                    int cfCount = 0;
                    foreach (XElement cf in ws.Elements(ns + "conditionalFormatting"))
                    {
                        cfCount++;
                    }

                    XElement sheetData = ws.Element(ns + "sheetData");
                    if (sheetData == null)
                    {
                        return new TemplateValidationResult { Ok = false, Message = "sheetData is missing." };
                    }

                    bool hasFormulaAfterRow4 = false;
                    foreach (XElement row in sheetData.Elements(ns + "row"))
                    {
                        XAttribute r = row.Attribute("r");
                        int rowIndex;
                        if (r == null || !int.TryParse(r.Value, out rowIndex) || rowIndex < 5)
                        {
                            continue;
                        }

                        foreach (XElement cell in row.Elements(ns + "c"))
                        {
                            XElement f = cell.Element(ns + "f");
                            if (f != null && !string.IsNullOrWhiteSpace(f.Value))
                            {
                                hasFormulaAfterRow4 = true;
                                break;
                            }
                        }
                        if (hasFormulaAfterRow4)
                        {
                            break;
                        }
                    }

                    if (cfCount <= 0)
                    {
                        return new TemplateValidationResult { Ok = false, Message = "Conditional formatting rules are missing." };
                    }
                    if (!hasFormulaAfterRow4)
                    {
                        return new TemplateValidationResult { Ok = false, Message = "No formulas detected in exported data rows." };
                    }

                    return new TemplateValidationResult { Ok = true, Message = "Template export validation passed." };
                }
            }
            catch (Exception ex)
            {
                return new TemplateValidationResult { Ok = false, Message = "Template validation error: " + ex.Message };
            }
        }
    }
}
