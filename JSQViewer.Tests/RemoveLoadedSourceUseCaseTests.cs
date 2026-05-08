using System;
using System.Collections.Generic;
using System.Linq;
using JSQViewer.Application.Workspace.UseCases;
using JSQViewer.Core;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace JSQViewer.Tests
{
    [TestClass]
    public class RemoveLoadedSourceUseCaseTests
    {
        [TestMethod]
        public void Execute_RemovesClosedSourceWithoutKeepingBlankRows()
        {
            TestData data = CreateMergedData();
            var useCase = new RemoveLoadedSourceUseCase();

            TestData result = useCase.Execute(data, "C:\\srcA");

            Assert.AreEqual("C:\\srcB", result.Root);
            CollectionAssert.AreEqual(new[] { "B-01" }, result.ColumnNames);
            CollectionAssert.AreEqual(new[] { 20L, 30L }, result.TimestampsMs);
            CollectionAssert.AreEqual(new double?[] { 2d, 3d }, result.Columns["B-01"]);
            CollectionAssert.AreEqual(new[] { "C:\\srcB" }, result.SourceColumns.Keys.ToArray());
            CollectionAssert.AreEqual(new[] { "B-01" }, result.SourceColumns["C:\\srcB"]);
            Assert.IsFalse(result.CodeSources.ContainsKey("A-01"));
            Assert.AreEqual("C:\\srcB", result.CodeSources["B-01"]);
            Assert.AreEqual(2, result.RowCount);
        }

        private static TestData CreateMergedData()
        {
            var data = new TestData
            {
                Root = "C:\\srcA ; C:\\srcB",
                RowCount = 4,
                TimestampsMs = new[] { 10L, 20L, 30L, 40L },
                ColumnNames = new[] { "A-01", "B-01" },
                SourceColumns = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
                {
                    ["C:\\srcA"] = new[] { "A-01" },
                    ["C:\\srcB"] = new[] { "B-01" }
                },
                SourceStartMs = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase)
                {
                    ["C:\\srcA"] = 10L,
                    ["C:\\srcB"] = 20L
                },
                SourceEndMs = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase)
                {
                    ["C:\\srcA"] = 40L,
                    ["C:\\srcB"] = 30L
                }
            };

            data.Columns["A-01"] = new double?[] { 1d, null, null, 4d };
            data.Columns["B-01"] = new double?[] { null, 2d, 3d, null };
            data.Channels["A-01"] = new ChannelInfo { Code = "A-01", Name = "A", Unit = "u" };
            data.Channels["B-01"] = new ChannelInfo { Code = "B-01", Name = "B", Unit = "u" };
            data.CodeSources["A-01"] = "C:\\srcA";
            data.CodeSources["B-01"] = "C:\\srcB";
            return data;
        }
    }
}
