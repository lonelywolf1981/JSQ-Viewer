using System.Drawing;
using JSQViewer.Presentation.WinForms.Presenters;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace JSQViewer.Tests
{
    [TestClass]
    public class SourceChannelWindowLayoutTests
    {
        [TestMethod]
        public void GetBounds_WrapsAdditionalWindowsInsideWorkingArea()
        {
            Rectangle workingArea = new Rectangle(0, 0, 1920, 1080);
            Rectangle ownerBounds = new Rectangle(0, 0, 1920, 430);

            Rectangle fifth = SourceChannelWindowLayout.GetBounds(workingArea, ownerBounds, 4);

            Assert.IsTrue(workingArea.Contains(fifth), fifth.ToString());
            Assert.IsTrue(fifth.Left < workingArea.Right - fifth.Width, fifth.ToString());
        }

        [TestMethod]
        public void GetBounds_FitsWindowSizeToSmallWorkingArea()
        {
            Rectangle workingArea = new Rectangle(100, 50, 500, 360);
            Rectangle ownerBounds = new Rectangle(100, 50, 500, 300);

            Rectangle bounds = SourceChannelWindowLayout.GetBounds(workingArea, ownerBounds, 0);

            Assert.IsTrue(workingArea.Contains(bounds), bounds.ToString());
            Assert.IsTrue(bounds.Width <= workingArea.Width - 24, bounds.ToString());
            Assert.IsTrue(bounds.Height <= workingArea.Height - 24, bounds.ToString());
        }

        [TestMethod]
        public void GetBounds_UsesCompactSourceWindowWidth()
        {
            Rectangle workingArea = new Rectangle(0, 0, 1920, 1080);
            Rectangle ownerBounds = new Rectangle(0, 0, 1920, 430);

            Rectangle bounds = SourceChannelWindowLayout.GetBounds(workingArea, ownerBounds, 0);

            Assert.IsTrue(bounds.Width <= 460, bounds.ToString());
        }

        [TestMethod]
        public void GetBounds_UsesSavedSourceWindowWidthWhenAvailable()
        {
            Rectangle workingArea = new Rectangle(0, 0, 1920, 1080);
            Rectangle ownerBounds = new Rectangle(0, 0, 1920, 430);

            Rectangle bounds = SourceChannelWindowLayout.GetBounds(workingArea, ownerBounds, 0, 520);

            Assert.AreEqual(520, bounds.Width);
        }

        [TestMethod]
        public void GetBounds_GrowsHeightToFitChannelCountWhenScreenAllows()
        {
            Rectangle workingArea = new Rectangle(0, 0, 1920, 1080);
            Rectangle ownerBounds = new Rectangle(0, 0, 1920, 160);

            Rectangle small = SourceChannelWindowLayout.GetBounds(workingArea, ownerBounds, 0, 440, 10);
            Rectangle large = SourceChannelWindowLayout.GetBounds(workingArea, ownerBounds, 0, 440, 34);

            Assert.IsTrue(large.Height > small.Height, "large=" + large + " small=" + small);
            Assert.IsTrue(workingArea.Contains(large), large.ToString());
        }

        [TestMethod]
        public void GetBounds_DoesNotCapLargeChannelListsAtDefaultHeightWhenScreenAllows()
        {
            Rectangle workingArea = new Rectangle(0, 0, 1920, 1200);
            Rectangle ownerBounds = new Rectangle(0, 0, 1920, 160);

            Rectangle bounds = SourceChannelWindowLayout.GetBounds(workingArea, ownerBounds, 0, 440, 58);

            Assert.IsTrue(bounds.Height > 640, bounds.ToString());
            Assert.IsTrue(workingArea.Contains(bounds), bounds.ToString());
        }

        [TestMethod]
        public void GetBounds_WhenOwnerLeavesNoRoomBelow_KeepsWindowNearBottom()
        {
            Rectangle workingArea = new Rectangle(0, 0, 1920, 1080);
            Rectangle ownerBounds = new Rectangle(0, 0, 1920, 1040);

            Rectangle bounds = SourceChannelWindowLayout.GetBounds(workingArea, ownerBounds, 0);

            Assert.IsTrue(workingArea.Contains(bounds), bounds.ToString());
            Assert.IsTrue(bounds.Top > workingArea.Top + 300, bounds.ToString());
        }
    }
}
