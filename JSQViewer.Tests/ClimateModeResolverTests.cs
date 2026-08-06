using JSQViewer.Application.Database;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace JSQViewer.Tests
{
    [TestClass]
    public sealed class ClimateModeResolverTests
    {
        [TestMethod]
        public void Resolve_WithModeIdFromRecord_UsesRecordValue()
        {
            ClimateModeInfo info = new ClimateModeResolver().Resolve("32_65", 25.0, 60.0);

            Assert.AreEqual("32/65", info.Label);
            Assert.AreEqual(ClimateModeSource.FromRecord, info.Source);
            Assert.IsTrue(info.IsKnown);
        }

        [TestMethod]
        public void Resolve_WithUnknownModeId_FallsBackToTemperature()
        {
            ClimateModeInfo info = new ClimateModeResolver().Resolve("18_50", 25.2, 59.7);

            Assert.AreEqual("25/60", info.Label);
            Assert.AreEqual(ClimateModeSource.FromChannels, info.Source);
        }

        [TestMethod]
        public void Resolve_WithoutModeId_ClassifiesEachKnownMode()
        {
            var resolver = new ClimateModeResolver();

            Assert.AreEqual("25/60", resolver.Resolve(null, 25.2, 59.7).Label);
            Assert.AreEqual("32/65", resolver.Resolve(null, 32.4, 64.6).Label);
            Assert.AreEqual("40/40", resolver.Resolve(null, 39.5, 41.2).Label);
        }

        [TestMethod]
        public void Resolve_IgnoresHumidityWhenClassifying()
        {
            ClimateModeInfo info = new ClimateModeResolver().Resolve(null, 32.3, 55.1);

            Assert.AreEqual("32/65", info.Label);
        }

        [TestMethod]
        public void Resolve_AtToleranceBoundary_StillClassifies()
        {
            ClimateModeInfo info = new ClimateModeResolver().Resolve(null, 35.0, null);

            Assert.AreEqual("32/65", info.Label);
        }

        [TestMethod]
        public void Resolve_BeyondTolerance_ReturnsUnknown()
        {
            ClimateModeInfo info = new ClimateModeResolver().Resolve(null, 35.1, null);

            Assert.AreEqual(ClimateModeSource.Unknown, info.Source);
            Assert.AreEqual(string.Empty, info.Label);
            Assert.IsFalse(info.IsKnown);
        }

        [TestMethod]
        public void Resolve_WithoutTemperature_ReturnsUnknown()
        {
            ClimateModeInfo info = new ClimateModeResolver().Resolve("   ", null, 64.0);

            Assert.AreEqual(ClimateModeSource.Unknown, info.Source);
            Assert.IsFalse(info.IsKnown);
        }

        [TestMethod]
        public void Resolve_KeepsMeasuredValuesForTooltip()
        {
            ClimateModeInfo info = new ClimateModeResolver().Resolve(null, 32.34, 64.12);

            Assert.AreEqual(32.34, info.TemperatureCelsius.Value, 0.001);
            Assert.AreEqual(64.12, info.HumidityPercent.Value, 0.001);
        }
    }
}
