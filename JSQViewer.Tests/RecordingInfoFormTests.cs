using System;
using System.Collections.Generic;
using System.Drawing;
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

        [TestMethod]
        public void Constructor_RendersT1FirstCoolingMinimumRows()
        {
            var result = new RecordingInfoResult
            {
                SourceRoot = @"C:\Data\Test",
                SourceStartTime = new DateTime(2026, 5, 8, 12, 22, 39, DateTimeKind.Local),
                T1InitialTemperature = 32.0,
                T1Min = 5.0,
                T1MinElapsedMs = 300 * 60_000L,
                T1MinTime = new DateTime(2026, 5, 10, 0, 56, 19, DateTimeKind.Local),
                T1FirstCoolingMin = 6.0,
                T1FirstCoolingMinElapsedMs = 80 * 60_000L,
                T1FirstCoolingMinTime = new DateTime(2026, 5, 9, 12, 0, 0, DateTimeKind.Local),
                Meta = new List<KeyValuePair<string, string>>()
            };

            using (var form = new RecordingInfoForm(result))
            {
                Label firstMinLabel = FindControls<Label>(form)
                    .FirstOrDefault(label => label.Text == "Первый минимум");
                TextBox[] valueBoxes = FindControls<TextBox>(form).ToArray();

                Assert.IsNotNull(firstMinLabel);
                Assert.IsTrue(valueBoxes.Any(box => box.Text == "6,0 °C" || box.Text == "6.0 °C"));
                Assert.IsTrue(valueBoxes.Any(box => box.Text == "01:20:00"));
                Assert.IsTrue(valueBoxes.Any(box => box.Text == "09.05.26 12:00:00"));
            }
        }

        [TestMethod]
        public void Constructor_RendersT1StartThenInitialTemperatureBeforeMinimum()
        {
            var result = new RecordingInfoResult
            {
                SourceRoot = @"C:\Data\Test",
                SourceStartTime = new DateTime(2026, 5, 8, 12, 22, 39, DateTimeKind.Local),
                T1InitialTemperature = 31.5,
                T1Min = 5.0,
                T1MinElapsedMs = 300 * 60_000L,
                T1MinTime = new DateTime(2026, 5, 10, 0, 56, 19, DateTimeKind.Local),
                T1FirstCoolingMin = 6.0,
                T1FirstCoolingMinElapsedMs = 80 * 60_000L,
                T1FirstCoolingMinTime = new DateTime(2026, 5, 9, 12, 0, 0, DateTimeKind.Local),
                Meta = new List<KeyValuePair<string, string>>()
            };

            using (var form = new RecordingInfoForm(result))
            {
                Label[] labels = FindControls<Label>(form).ToArray();
                int startIndex = Array.FindIndex(labels, label => label.Text == "Старт записи");
                int initialIndex = Array.FindIndex(labels, label => label.Text == "Начальная температура");
                int firstMinIndex = Array.FindIndex(labels, label => label.Text == "Первый минимум");
                int minIndex = Array.FindIndex(labels, label => label.Text == "Минимум");
                TextBox initialBox = FindControls<TextBox>(form)
                    .FirstOrDefault(box => box.Text == result.T1InitialTemperature.Value.ToString("F1") + " °C");

                Assert.IsTrue(startIndex >= 0);
                Assert.IsTrue(initialIndex > startIndex);
                Assert.IsTrue(firstMinIndex > initialIndex);
                Assert.IsTrue(minIndex > firstMinIndex);
                Assert.IsNotNull(initialBox);
            }
        }

        [TestMethod]
        public void Constructor_RendersT1EnergyToTarget()
        {
            var result = new RecordingInfoResult
            {
                SourceRoot = @"C:\Data\Test",
                T1Min = 5.0,
                T1EnergyToTargetKWh = 0.16,
                T1EnergyTargetElapsedMs = 80 * 60_000L,
                Meta = new List<KeyValuePair<string, string>>()
            };

            using (var form = new RecordingInfoForm(result))
            {
                Label energyLabel = FindControls<Label>(form)
                    .FirstOrDefault(label => label.Text == "Энергопотребление");
                TextBox energyBox = FindControls<TextBox>(form)
                    .FirstOrDefault(box => box.Text == result.T1EnergyToTargetKWh.Value.ToString("F3") + " кВт⋅ч");

                Assert.IsNotNull(energyLabel);
                Assert.IsNotNull(energyBox);
            }
        }

        [TestMethod]
        public void Constructor_RendersReachedT8PlusThresholdsInGreen()
        {
            var result = new RecordingInfoResult
            {
                SourceRoot = @"C:\Data\Test",
                T1Min = 4.9,
                T8PlusStats = new T8PlusTemperatureStats
                {
                    HasChannels = true,
                    AverageReached = true,
                    AverageValue = 4.8,
                    AverageElapsedMs = 60_000L,
                    AverageTime = new DateTime(2026, 4, 2, 16, 0, 0, DateTimeKind.Local)
                },
                Meta = new List<KeyValuePair<string, string>>()
            };

            using (var form = new RecordingInfoForm(result))
            {
                TextBox averageBox = FindControls<TextBox>(form)
                    .FirstOrDefault(box => box.Text.Contains(result.T8PlusStats.AverageValue.Value.ToString("F1")));

                Assert.IsNotNull(averageBox);
                Assert.AreEqual(Color.Green, averageBox.ForeColor);
            }
        }

        [TestMethod]
        public void Constructor_WithT8PlusRecalculator_UpdatesRowsWhenThresholdChanges()
        {
            var initial = new RecordingInfoResult
            {
                SourceRoot = @"C:\Data\Test",
                T1Min = 4.9,
                T8PlusStats = new T8PlusTemperatureStats
                {
                    HasChannels = true,
                    AverageReached = false,
                    AverageValue = 8.0,
                    AverageElapsedMs = 120_000L,
                    AverageTime = new DateTime(2026, 4, 2, 16, 1, 0, DateTimeKind.Local)
                },
                Meta = new List<KeyValuePair<string, string>>()
            };
            var recalculated = new RecordingInfoResult
            {
                SourceRoot = initial.SourceRoot,
                T1Min = 4.9,
                T8PlusStats = new T8PlusTemperatureStats
                {
                    HasChannels = true,
                    AverageReached = true,
                    AverageValue = 6.0,
                    AverageElapsedMs = 60_000L,
                    AverageTime = new DateTime(2026, 4, 2, 16, 0, 0, DateTimeKind.Local)
                },
                Meta = new List<KeyValuePair<string, string>>()
            };

            using (var form = new RecordingInfoForm(initial, null, thresholds => recalculated))
            {
                NumericUpDown averageThreshold = FindControls<NumericUpDown>(form)
                    .FirstOrDefault(control => control.Name == "AverageT8PlusThresholdUpDown");

                Assert.IsNotNull(averageThreshold);

                averageThreshold.Value = 7.0m;

                TextBox averageBox = FindControls<TextBox>(form)
                    .FirstOrDefault(box => box.Text == recalculated.T8PlusStats.AverageValue.Value.ToString("F1") + " °C");

                Assert.IsNotNull(averageBox);
                Assert.AreEqual(Color.Green, averageBox.ForeColor);
            }
        }

        [TestMethod]
        public void Constructor_WithT8PlusRecalculator_KeepsExistingControlsWhenThresholdChanges()
        {
            var initial = new RecordingInfoResult
            {
                SourceRoot = @"C:\Data\Test",
                T1Min = 4.9,
                T8PlusStats = new T8PlusTemperatureStats
                {
                    HasChannels = true,
                    AverageReached = false,
                    AverageValue = 8.0,
                    AverageElapsedMs = 120_000L,
                    AverageTime = new DateTime(2026, 4, 2, 16, 1, 0, DateTimeKind.Local)
                },
                Meta = new List<KeyValuePair<string, string>>()
            };
            var recalculated = new RecordingInfoResult
            {
                SourceRoot = initial.SourceRoot,
                T1Min = 4.9,
                T8PlusStats = new T8PlusTemperatureStats
                {
                    HasChannels = true,
                    AverageReached = true,
                    AverageValue = 6.0,
                    AverageElapsedMs = 60_000L,
                    AverageTime = new DateTime(2026, 4, 2, 16, 0, 0, DateTimeKind.Local)
                },
                Meta = new List<KeyValuePair<string, string>>()
            };

            using (var form = new RecordingInfoForm(initial, null, thresholds => recalculated))
            {
                NumericUpDown averageThreshold = FindControls<NumericUpDown>(form)
                    .FirstOrDefault(control => control.Name == "AverageT8PlusThresholdUpDown");
                TextBox originalAverageBox = FindControls<TextBox>(form)
                    .FirstOrDefault(box => box.Name == "AverageT8PlusValueBox");
                int originalControlCount = FindControls<Control>(form).Count();

                Assert.IsNotNull(averageThreshold);
                Assert.IsNotNull(originalAverageBox);

                averageThreshold.Value = 7.0m;

                TextBox updatedAverageBox = FindControls<TextBox>(form)
                    .FirstOrDefault(box => box.Name == "AverageT8PlusValueBox");
                Label updatedAverageLabel = FindControls<Label>(form)
                    .FirstOrDefault(label => label.Text == "Средняя <= 7 °C");

                Assert.AreSame(originalAverageBox, updatedAverageBox);
                Assert.AreEqual(originalControlCount, FindControls<Control>(form).Count());
                Assert.AreEqual(recalculated.T8PlusStats.AverageValue.Value.ToString("F1") + " °C", updatedAverageBox.Text);
                Assert.IsNotNull(updatedAverageLabel);
            }
        }

        [TestMethod]
        public void Constructor_RendersUnreachedT8PlusThresholdsInRedWithValueAndTimes()
        {
            var result = new RecordingInfoResult
            {
                SourceRoot = @"C:\Data\Test",
                T1Min = 4.9,
                T8PlusStats = new T8PlusTemperatureStats
                {
                    HasChannels = true,
                    AverageReached = false,
                    AverageValue = 7.0,
                    AverageElapsedMs = 120_000L,
                    AverageTime = new DateTime(2026, 4, 2, 16, 1, 0, DateTimeKind.Local)
                },
                Meta = new List<KeyValuePair<string, string>>()
            };

            using (var form = new RecordingInfoForm(result))
            {
                TextBox[] valueBoxes = FindControls<TextBox>(form).ToArray();
                TextBox averageValueBox = valueBoxes
                    .FirstOrDefault(box => box.Text.Contains(result.T8PlusStats.AverageValue.Value.ToString("F1") + " °C"));
                TextBox averageElapsedBox = valueBoxes
                    .FirstOrDefault(box => box.Text == "00:02:00");
                TextBox averageDateBox = valueBoxes
                    .FirstOrDefault(box => box.Text == "02.04.26 16:01:00");

                Assert.IsNotNull(averageValueBox);
                Assert.IsNotNull(averageElapsedBox);
                Assert.IsNotNull(averageDateBox);
                Assert.AreEqual(Color.Red, averageValueBox.ForeColor);
                Assert.AreEqual(Color.Red, averageElapsedBox.ForeColor);
                Assert.AreEqual(Color.Red, averageDateBox.ForeColor);
            }
        }

        [TestMethod]
        public void Constructor_RendersT8PlusDropRate_WhenDifferentFromT1DropRate()
        {
            var result = new RecordingInfoResult
            {
                SourceRoot = @"C:\Data\Test",
                T1Min = 4.9,
                T1DropRatePerMinute = -0.02,
                T8PlusStats = new T8PlusTemperatureStats
                {
                    HasChannels = true,
                    AverageDropRatePerMinute = -0.05
                },
                Meta = new List<KeyValuePair<string, string>>()
            };

            using (var form = new RecordingInfoForm(result))
            {
                Label rateLabel = FindControls<Label>(form)
                    .FirstOrDefault(label => label.Text == "Скорость падения T8+");
                string expectedRate = result.T8PlusStats.AverageDropRatePerMinute.Value.ToString("F2") + " °C/мин";
                TextBox rateBox = FindControls<TextBox>(form)
                    .FirstOrDefault(box => box.Text == expectedRate);

                Assert.IsNotNull(rateLabel);
                Assert.IsNotNull(rateBox);
            }
        }

        [TestMethod]
        public void Constructor_HidesT8PlusDropRate_WhenSameAsT1DropRate()
        {
            var result = new RecordingInfoResult
            {
                SourceRoot = @"C:\Data\Test",
                T1Min = 4.9,
                T1DropRatePerMinute = -0.05,
                T8PlusStats = new T8PlusTemperatureStats
                {
                    HasChannels = true,
                    AverageDropRatePerMinute = -0.05
                },
                Meta = new List<KeyValuePair<string, string>>()
            };

            using (var form = new RecordingInfoForm(result))
            {
                Label rateLabel = FindControls<Label>(form)
                    .FirstOrDefault(label => label.Text == "Скорость падения T8+");

                Assert.IsNull(rateLabel);
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
