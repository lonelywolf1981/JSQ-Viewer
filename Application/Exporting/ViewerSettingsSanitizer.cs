using System;
using System.Collections.Generic;
using JSQViewer.Settings;

namespace JSQViewer.Application.Exporting
{
    public sealed class ViewerSettingsSanitizer
    {
        public ViewerSettingsModel Sanitize(ViewerSettingsModel settings)
        {
            ViewerSettingsModel source = settings ?? ViewerSettingsModel.CreateDefault();
            ViewerSettingsModel sanitized = ViewerSettingsModel.CreateDefault();

            sanitized.row_mark.threshold_T = source.row_mark != null ? source.row_mark.threshold_T : sanitized.row_mark.threshold_T;
            sanitized.row_mark.color = NormalizeHex(source.row_mark != null ? source.row_mark.color : null, "#EAD706");
            sanitized.row_mark.intensity = 100;

            sanitized.discharge_mark.threshold = source.discharge_mark != null ? source.discharge_mark.threshold : null;
            sanitized.discharge_mark.color = NormalizeHex(source.discharge_mark != null ? source.discharge_mark.color : null, "#FFC000");

            sanitized.suction_mark.threshold = source.suction_mark != null ? source.suction_mark.threshold : null;
            sanitized.suction_mark.color = NormalizeHex(source.suction_mark != null ? source.suction_mark.color : null, "#00B0F0");

            sanitized.scales = new Dictionary<string, ScaleSettings>(StringComparer.OrdinalIgnoreCase);
            string[] keys = new[] { "W", "X", "Y" };
            for (int i = 0; i < keys.Length; i++)
            {
                string key = keys[i];
                ScaleSettings input = source.scales != null && source.scales.ContainsKey(key) ? source.scales[key] : null;
                ScaleSettings current = ScaleSettings.CreateDefault();
                if (input != null)
                {
                    current.min = input.min;
                    current.opt = input.opt;
                    current.max = input.max;
                    current.colors.min = NormalizeHex(input.colors != null ? input.colors.min : null, "#1CBCF2");
                    current.colors.opt = NormalizeHex(input.colors != null ? input.colors.opt : null, "#00FF00");
                    current.colors.max = NormalizeHex(input.colors != null ? input.colors.max : null, "#F3919B");
                }

                if (current.opt <= current.min)
                {
                    current.opt = current.min + 1;
                }

                if (current.max < current.opt)
                {
                    current.max = current.opt;
                }

                sanitized.scales[key] = current;
            }

            return sanitized;
        }

        private static string NormalizeHex(string value, string fallback)
        {
            string s = (value ?? string.Empty).Trim();
            if (!s.StartsWith("#")) s = "#" + s;
            if (s.Length != 7) return fallback;
            for (int i = 1; i < s.Length; i++)
            {
                char ch = s[i];
                bool ok = (ch >= '0' && ch <= '9') || (ch >= 'A' && ch <= 'F') || (ch >= 'a' && ch <= 'f');
                if (!ok) return fallback;
            }

            return s.ToUpperInvariant();
        }
    }
}
