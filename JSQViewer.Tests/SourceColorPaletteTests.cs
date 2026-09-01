using System.Drawing;
using JSQViewer.Presentation.WinForms.Charting;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace JSQViewer.Tests
{
    [TestClass]
    public sealed class SourceColorPaletteTests
    {
        [TestMethod]
        public void ForSourceIndex_GivesDistinctColorsToNeighbouringSources()
        {
            Assert.AreNotEqual(SourceColorPalette.ForSourceIndex(0), SourceColorPalette.ForSourceIndex(1));
            Assert.AreNotEqual(SourceColorPalette.ForSourceIndex(1), SourceColorPalette.ForSourceIndex(2));
        }

        [TestMethod]
        public void ForSourceIndex_WrapsAroundAndHandlesNegativeIndex()
        {
            Assert.AreEqual(SourceColorPalette.ForSourceIndex(0), SourceColorPalette.ForSourceIndex(SourceColorPalette.Count));
            Assert.AreEqual(SourceColorPalette.ForSourceIndex(0), SourceColorPalette.ForSourceIndex(-1));
        }

        [TestMethod]
        public void ForSourceIndex_ReturnsOpaqueColors()
        {
            for (int i = 0; i < SourceColorPalette.Count; i++)
            {
                Color color = SourceColorPalette.ForSourceIndex(i);
                Assert.AreEqual(255, color.A);
            }
        }
    }
}
