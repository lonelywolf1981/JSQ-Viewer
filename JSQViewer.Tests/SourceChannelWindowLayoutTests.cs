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
