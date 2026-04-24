using System.Collections.Generic;
using JSQViewer.Application.Channels;
using JSQViewer.Core;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace JSQViewer.Tests
{
    [TestClass]
    public class ProtocolChannelOrderTests
    {
        // ── null / empty ──────────────────────────────────────────────────

        [TestMethod]
        public void Build_NullCols_ReturnsEmpty()
        {
            var result = ProtocolChannelOrder.Build(null, null);
            Assert.AreEqual(0, result.Count);
        }

        [TestMethod]
        public void Build_EmptyCols_ReturnsEmpty()
        {
            var result = ProtocolChannelOrder.Build(new string[0], null);
            Assert.AreEqual(0, result.Count);
        }

        // ── fixed keys ────────────────────────────────────────────────────

        [TestMethod]
        public void Build_PlacesFixedKeysFirst_InDefinedOrder()
        {
            // W, Pc, T1, F — все фиксированные, порядок должен быть Pc, T1, F, W
            string[] cols = new[] { "W", "Pc", "T1", "F" };
            var result = ProtocolChannelOrder.Build(cols, null);
            Assert.AreEqual(4, result.Count);
            Assert.AreEqual("Pc", result[0]);
            Assert.AreEqual("T1", result[1]);
            Assert.AreEqual("F",  result[2]);
            Assert.AreEqual("W",  result[3]);
        }

        [TestMethod]
        public void Build_MissingFixedKey_IsSkipped()
        {
            // Только T1 из фиксированных
            string[] cols = new[] { "T1", "X1" };
            var result = ProtocolChannelOrder.Build(cols, null);
            Assert.AreEqual(2, result.Count);
            Assert.AreEqual("T1", result[0]);
            Assert.AreEqual("X1", result[1]);
        }

        // ── suffix resolution ─────────────────────────────────────────────

        [TestMethod]
        public void Build_SuffixMatch_APrefixWinsOverCPrefix()
        {
            // A-Pc и C-Pc — оба совпадают с ключом "Pc", A- должен быть выбран
            string[] cols = new[] { "C-Pc", "A-Pc", "X1" };
            var result = ProtocolChannelOrder.Build(cols, null);
            Assert.AreEqual("A-Pc", result[0]); // выбран для Pc
            // C-Pc и X1 попадают в extras
            CollectionAssert.Contains(result, "C-Pc");
            CollectionAssert.Contains(result, "X1");
        }

        [TestMethod]
        public void Build_SuffixMatch_CPrefixWinsOverOthers()
        {
            // C-Pc и Z-Pc — C- должен быть выбран (A- отсутствует)
            string[] cols = new[] { "Z-Pc", "C-Pc", "X1" };
            var result = ProtocolChannelOrder.Build(cols, null);
            Assert.AreEqual("C-Pc", result[0]);
        }

        [TestMethod]
        public void Build_APrefixWinsEvenOverExactMatch()
        {
            // "A-Pc" — суффиксное совпадение с A-префиксом;
            // "Pc"   — точное совпадение без префикса.
            // A- имеет наивысший приоритет → выбирается A-Pc, Pc уходит в extras.
            string[] cols = new[] { "A-Pc", "Pc" };
            var result = ProtocolChannelOrder.Build(cols, null);
            Assert.AreEqual("A-Pc", result[0]); // A- выигрывает
            Assert.AreEqual("Pc",   result[1]); // Pc → extras
        }

        // ── extras sorting ────────────────────────────────────────────────

        [TestMethod]
        public void Build_SortsExtrasByChannelName()
        {
            string[] cols = new[] { "Z-sensor", "A-sensor" };
            var channels = new Dictionary<string, ChannelInfo>(System.StringComparer.OrdinalIgnoreCase)
            {
                ["Z-sensor"] = new ChannelInfo { Code = "Z-sensor", Name = "Zebra" },
                ["A-sensor"] = new ChannelInfo { Code = "A-sensor", Name = "Alpha" }
            };
            var result = ProtocolChannelOrder.Build(cols, channels);
            Assert.AreEqual("A-sensor", result[0]); // "Alpha" < "Zebra"
            Assert.AreEqual("Z-sensor", result[1]);
        }

        [TestMethod]
        public void Build_SortsExtrasByCodeWhenNoChannelName()
        {
            // Нет ChannelInfo — сортировка по коду
            string[] cols = new[] { "X3", "X1", "X2" };
            var result = ProtocolChannelOrder.Build(cols, null);
            Assert.AreEqual("X1", result[0]);
            Assert.AreEqual("X2", result[1]);
            Assert.AreEqual("X3", result[2]);
        }

        [TestMethod]
        public void Build_NaturalSortForExtras()
        {
            // Натуральная сортировка: X2 < X10
            string[] cols = new[] { "X10", "X2", "X1" };
            var result = ProtocolChannelOrder.Build(cols, null);
            Assert.AreEqual("X1",  result[0]);
            Assert.AreEqual("X2",  result[1]);
            Assert.AreEqual("X10", result[2]);
        }

        // ── combined ──────────────────────────────────────────────────────

        [TestMethod]
        public void Build_FullScenario_FixedFirstThenSortedExtras()
        {
            string[] cols = new[] { "X2", "A-Pc", "T1", "X1" };
            var result = ProtocolChannelOrder.Build(cols, null);
            // Pc (mapped from A-Pc) first, T1 second, then extras X1, X2
            Assert.AreEqual("A-Pc", result[0]);
            Assert.AreEqual("T1",   result[1]);
            Assert.AreEqual("X1",   result[2]);
            Assert.AreEqual("X2",   result[3]);
        }

        [TestMethod]
        public void Build_AllColsIncluded_NoLostChannels()
        {
            string[] cols = new[] { "Pc", "T1", "Extra1", "Extra2" };
            var result = ProtocolChannelOrder.Build(cols, null);
            Assert.AreEqual(cols.Length, result.Count);
        }
    }
}
