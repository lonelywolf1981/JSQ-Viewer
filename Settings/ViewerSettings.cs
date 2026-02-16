using System.Collections.Generic;

namespace LeMuReViewer.Settings
{
    public sealed class ViewerSettingsModel
    {
        public RowMarkSettings row_mark { get; set; }
        public ThresholdColorSettings discharge_mark { get; set; }
        public ThresholdColorSettings suction_mark { get; set; }
        public Dictionary<string, ScaleSettings> scales { get; set; }

        public static ViewerSettingsModel CreateDefault()
        {
            return new ViewerSettingsModel
            {
                row_mark = new RowMarkSettings { threshold_T = 150.0, color = "#EAD706", intensity = 100 },
                discharge_mark = new ThresholdColorSettings { threshold = null, color = "#FFC000" },
                suction_mark = new ThresholdColorSettings { threshold = null, color = "#00B0F0" },
                scales = new Dictionary<string, ScaleSettings>
                {
                    { "W", ScaleSettings.CreateDefault() },
                    { "X", ScaleSettings.CreateDefault() },
                    { "Y", ScaleSettings.CreateDefault() }
                }
            };
        }
    }

    public sealed class RowMarkSettings
    {
        public double threshold_T { get; set; }
        public string color { get; set; }
        public int intensity { get; set; }
    }

    public sealed class ThresholdColorSettings
    {
        public double? threshold { get; set; }
        public string color { get; set; }
    }

    public sealed class ScaleSettings
    {
        public double min { get; set; }
        public double opt { get; set; }
        public double max { get; set; }
        public ScaleColors colors { get; set; }

        public static ScaleSettings CreateDefault()
        {
            return new ScaleSettings
            {
                min = -1,
                opt = 1,
                max = 2,
                colors = new ScaleColors
                {
                    min = "#1CBCF2",
                    opt = "#00FF00",
                    max = "#F3919B"
                }
            };
        }
    }

    public sealed class ScaleColors
    {
        public string min { get; set; }
        public string opt { get; set; }
        public string max { get; set; }
    }

    public static class ViewerSettingsStore
    {
        public static ViewerSettingsModel Load(string filePath)
        {
            return JsonHelper.LoadFromFile(filePath, ViewerSettingsModel.CreateDefault());
        }

        public static bool Save(string filePath, ViewerSettingsModel settings)
        {
            return JsonHelper.SaveToFile(filePath, settings ?? ViewerSettingsModel.CreateDefault());
        }
    }
}
