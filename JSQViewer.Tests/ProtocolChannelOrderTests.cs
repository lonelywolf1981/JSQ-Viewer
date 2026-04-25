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

        [TestMethod]
        public void Build_AllFixedKeys_OrderedCorrectly()
        {
            // Все 17 фиксированных ключей присутствуют, порядок должен совпадать с KeyToColumn
            string[] cols = new[] { "W", "V", "F", "I", "T7", "T6", "T5", "T4", "T3", "T2", "T1", "Te", "Tc", "UR-sie", "T-sie", "Pe", "Pc" };
            string[] expected = new[] { "Pc", "Pe", "T-sie", "UR-sie", "Tc", "Te", "T1", "T2", "T3", "T4", "T5", "T6", "T7", "I", "F", "V", "W" };
            var result = ProtocolChannelOrder.Build(cols, null);
            Assert.AreEqual(expected.Length, result.Count);
            for (int i = 0; i < expected.Length; i++)
            {
                Assert.AreEqual(expected[i], result[i], $"Position {i}: expected {expected[i]}, got {result[i]}");
            }
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

        // ── multi-source prefixed codes (source::code) ────────────────────

        [TestMethod]
        public void Build_PrefixedCols_FixedKeysStillMatchedFirst()
        {
            // При загрузке нескольких записей ВСЕ экземпляры каждого фиксированного ключа
            // должны быть в зоне фиксированных ключей (не в extras)
            string[] cols = new[] { "srcB::W", "srcA::T1", "srcB::T1", "srcA::Pc", "srcB::Pc" };
            var result = ProtocolChannelOrder.Build(cols, null);

            int idxPcA = result.IndexOf("srcA::Pc");
            int idxPcB = result.IndexOf("srcB::Pc");
            int idxT1A = result.IndexOf("srcA::T1");
            int idxT1B = result.IndexOf("srcB::T1");
            int idxW   = result.IndexOf("srcB::W");

            Assert.AreEqual(cols.Length, result.Count, "Количество каналов не должно измениться");
            Assert.IsTrue(idxPcA >= 0 && idxPcB >= 0 && idxT1A >= 0 && idxT1B >= 0 && idxW >= 0,
                "Все каналы должны присутствовать");

            // Оба Pc должны идти раньше обоих T1 (шаблонный порядок: Pc < T1 < W)
            Assert.IsTrue(idxPcA < idxT1A, "srcA::Pc должен быть перед srcA::T1");
            Assert.IsTrue(idxPcA < idxT1B, "srcA::Pc должен быть перед srcB::T1");
            Assert.IsTrue(idxPcB < idxT1A, "srcB::Pc должен быть перед srcA::T1");
            Assert.IsTrue(idxPcB < idxT1B, "srcB::Pc должен быть перед srcB::T1");

            // Оба T1 должны идти раньше W
            Assert.IsTrue(idxT1A < idxW, "srcA::T1 должен быть перед srcB::W");
            Assert.IsTrue(idxT1B < idxW, "srcB::T1 должен быть перед srcB::W");
        }

        [TestMethod]
        public void Build_MultiSource_SecondSourceChannelsInProtocolOrder()
        {
            // Воспроизводит баг: при загрузке двух записей каналы второго источника
            // должны быть упорядочены по шаблону, а не попадать в extras
            string[] cols = new[] { "src1::Pc", "src2::Pc", "src1::T1", "src2::T1", "src1::F", "src2::F" };
            var result = ProtocolChannelOrder.Build(cols, null);

            // T1 перед F в шаблоне — это должно быть верно для ОБОИХ источников
            Assert.IsTrue(result.IndexOf("src1::T1") < result.IndexOf("src1::F"),
                "src1::T1 должен быть перед src1::F");
            Assert.IsTrue(result.IndexOf("src2::T1") < result.IndexOf("src2::F"),
                "src2::T1 должен быть перед src2::F (баг: src2::T1 попадал в extras после src2::F)");

            Assert.AreEqual(cols.Length, result.Count, "Все каналы должны присутствовать");
        }

        [TestMethod]
        public void Build_PrefixedCols_APrefixPriorityStillWorks()
        {
            // Два источника, оба с A-Pc; A-приоритет должен работать через префикс
            string[] cols = new[] { "srcB::A-Pc", "srcA::A-Pc", "srcA::X1" };
            var result = ProtocolChannelOrder.Build(cols, null);
            // Первый из A-Pc кандидатов выбирается для слота Pc
            Assert.IsTrue(result[0] == "srcB::A-Pc" || result[0] == "srcA::A-Pc",
                "Первым должен быть один из A-Pc вариантов");
        }

        [TestMethod]
        public void Build_PrefixedCols_HashSuffixStripped()
        {
            // При дублировании в рамках одного источника добавляется #2
            string[] cols = new[] { "Pc#2", "T1", "X1" };
            var result = ProtocolChannelOrder.Build(cols, null);
            Assert.AreEqual("Pc#2", result[0]); // Pc#2 → базовый "Pc" → фиксированный ключ
            Assert.AreEqual("T1",   result[1]);
        }

        [TestMethod]
        public void Build_AllColsIncluded_WithPrefixes_NoLostChannels()
        {
            string[] cols = new[] { "srcA::Pc", "srcB::Pc", "srcA::T1", "srcB::T1" };
            var result = ProtocolChannelOrder.Build(cols, null);
            Assert.AreEqual(cols.Length, result.Count);
            CollectionAssert.AreEquivalent(cols, result);
        }
    }
}
