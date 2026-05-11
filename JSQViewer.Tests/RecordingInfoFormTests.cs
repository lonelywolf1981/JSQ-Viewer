using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using JSQViewer.Application.Recording;
using JSQViewer.UI;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace JSQViewer.Tests
{
    [TestClass]
    public class RecordingInfoFormTests
    {
        [TestMethod]
        public void Constructor_RendersValuesAsSelectableReadOnlyTextBoxes()
        {
            var result = new RecordingInfoResult
            {
                SourceRoot = @"C:\Data\Test",
                SourceStartTime = new DateTime(2026, 4, 2, 15, 55, 18, DateTimeKind.Local),
                T1Min = 4.9,
                T1MinElapsedMs = 13 * 3600_000L + 51 * 60_000L + 40_000L,
                T1MinTime = new DateTime(2026, 4, 3, 5, 46, 58, DateTimeKind.Local),
                T1DropRatePerMinute = -0.02,
                Meta = new List<KeyValuePair<string, string>>
                {
                    new KeyValuePair<string, string>("MODEL/TYPE", "modelC")
                }
            };

            using (var form = new RecordingInfoForm(result))
            {
                TextBox[] valueBoxes = FindControls<TextBox>(form).ToArray();

                Assert.IsTrue(valueBoxes.Any(box => box.Text == result.T1Min.Value.ToString("F1") + " °C"));
                Assert.IsTrue(valueBoxes.Any(box => box.Text == "modelC"));
                Assert.IsTrue(valueBoxes.All(box => box.ReadOnly));
                Assert.IsTrue(valueBoxes.All(box => box.BorderStyle == BorderStyle.None));
                Assert.IsTrue(valueBoxes.All(box => box.TabStop == false));
            }
        }

        private static IEnumerable<T> FindControls<T>(Control root)
            where T : Control
        {
            foreach (Control child in root.Controls)
            {
                T typed = child as T;
                if (typed != null)
                    yield return typed;

                foreach (T nested in FindControls<T>(child))
                    yield return nested;
            }
        }
    }
}
