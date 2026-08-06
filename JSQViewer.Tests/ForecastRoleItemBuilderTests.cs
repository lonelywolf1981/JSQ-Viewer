using System;
using System.Collections.Generic;
using System.Linq;
using JSQViewer.Core;
using JSQViewer.Presentation.WinForms.Presenters;
using JSQViewer.Presentation.WinForms.ViewModels;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace JSQViewer.Tests
{
    [TestClass]
    public sealed class ForecastRoleItemBuilderTests
    {
        [TestMethod]
        public void Build_UsesResolvedTitlesAndDistinguishesDuplicateDatabaseNames()
        {
            const string sourceA = "jsqdb://recording/a";
            const string sourceB = "jsqdb://recording/b";
            TestData data = CreateData(sourceA, sourceB);
            data.SourceDisplayNames[sourceA] = "KA50";
            data.SourceDisplayNames[sourceB] = "KA50";

            IReadOnlyList<DynamicsForecastRoleItemViewModel> result =
                new DynamicsForecastRoleItemBuilder().Build(data, new[] { "a::T1", "b::T1" });

            CollectionAssert.AreEqual(
                new[] { "[KA50 [a]] T1", "[KA50 [b]] T1" },
                result.Select(item => item.Label).ToArray());
        }

        [TestMethod]
        public void Build_PreservesSelectedOrderAndCodeIdentityWhileNormalizingDisplayCode()
        {
            const string source = "jsqdb://recording/a";
            TestData data = CreateData(source);
            data.SourceDisplayNames[source] = "Прогон A";
            data.CodeSources["merged::T1#2"] = source;
            data.CodeSources["merged::T2"] = source;

            IReadOnlyList<DynamicsForecastRoleItemViewModel> result =
                new DynamicsForecastRoleItemBuilder().Build(data, new[] { "merged::T2", " ", "merged::T1#2" });

            CollectionAssert.AreEqual(
                new[] { "merged::T2", "merged::T1#2" },
                result.Select(item => item.Code).ToArray());
            CollectionAssert.AreEqual(
                new[] { "[Прогон A] T2", "[Прогон A] T1" },
                result.Select(item => item.Label).ToArray());
        }

        [TestMethod]
        public void Build_UsesTechnicalRootDurationAndInfinityWhenBoundsAreMissing()
        {
            const string sourceA = "jsqdb://recording/a";
            const string sourceB = "jsqdb://recording/b";
            TestData data = CreateData(sourceA, sourceB);
            data.SourceStartMs[sourceA] = 3600000L;
            data.SourceEndMs[sourceA] = 12600000L;

            IReadOnlyList<DynamicsForecastRoleItemViewModel> result =
                new DynamicsForecastRoleItemBuilder().Build(data, new[] { "a::T1", "b::T1" });

            Assert.AreEqual(2.5d, result[0].DurationHours, 1e-9);
            Assert.IsTrue(double.IsPositiveInfinity(result[1].DurationHours));
        }

        [TestMethod]
        public void Build_NullDataOrSelectionReturnsEmptyAndViewModelIsNullSafe()
        {
            var builder = new DynamicsForecastRoleItemBuilder();

            Assert.AreEqual(0, builder.Build(null, new[] { "T1" }).Count);
            Assert.AreEqual(0, builder.Build(new TestData(), null).Count);

            var item = new DynamicsForecastRoleItemViewModel(null, null, double.PositiveInfinity);
            Assert.AreEqual(string.Empty, item.Code);
            Assert.AreEqual(string.Empty, item.Label);
            Assert.AreEqual(string.Empty, item.ToString());
        }

        private static TestData CreateData(params string[] sources)
        {
            var data = new TestData
            {
                SourceOrder = sources,
                SourceColumns = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
            };

            for (int i = 0; i < sources.Length; i++)
            {
                string code = (i == 0 ? "a" : "b") + "::T1";
                data.SourceColumns[sources[i]] = new[] { code };
                data.CodeSources[code] = sources[i];
            }

            return data;
        }
    }
}
