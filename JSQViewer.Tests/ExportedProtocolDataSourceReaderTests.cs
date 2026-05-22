using System;
using System.IO;
using System.Linq;
using JSQViewer.Infrastructure.DataImport;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace JSQViewer.Tests
{
    [TestClass]
    public class ExportedProtocolDataSourceReaderTests
    {
        [TestMethod]
        public void Read_LoadsExportedProtocolAsTestData()
        {
            string path = FindSampleProtocol();
            var reader = new ExportedProtocolDataSourceReader();

            var data = reader.Read(path);

            Assert.AreEqual(Path.GetFullPath(path), data.Root);
            Assert.IsTrue(data.RowCount > 0);
            CollectionAssert.Contains(data.ColumnNames, "Pc");
            Assert.IsTrue(data.Columns.ContainsKey("Pc"));
            Assert.IsTrue(data.SourceColumns.ContainsKey(data.Root));
            Assert.AreEqual(data.Root, data.CodeSources["Pc"]);
            Assert.IsTrue(data.SourceStartMs.ContainsKey(data.Root));
            Assert.IsTrue(data.SourceEndMs.ContainsKey(data.Root));
            Assert.IsTrue(IsMonotonic(data.TimestampsMs));

            string extra = data.ColumnNames.FirstOrDefault(code => code.StartsWith("unit C - Tc 8", StringComparison.OrdinalIgnoreCase));
            Assert.IsFalse(string.IsNullOrWhiteSpace(extra), "Expected extra channel from column Z.");
            Assert.AreEqual("°C", data.Channels[extra].Unit);
        }

        private static bool IsMonotonic(long[] values)
        {
            for (int i = 1; i < values.Length; i++)
            {
                if (values[i] < values[i - 1])
                {
                    return false;
                }
            }

            return true;
        }

        private static string FindSampleProtocol()
        {
            string current = AppDomain.CurrentDomain.BaseDirectory;
            while (!string.IsNullOrWhiteSpace(current))
            {
                string candidate = Path.Combine(current, "06.03.26 FORCE KA50 90G FULL 4040.xlsx");
                if (File.Exists(candidate))
                {
                    return candidate;
                }

                current = Directory.GetParent(current)?.FullName;
            }

            Assert.Inconclusive("Sample exported protocol was not found.");
            return string.Empty;
        }
    }
}
