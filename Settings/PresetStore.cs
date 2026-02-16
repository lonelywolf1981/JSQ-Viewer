using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace LeMuReViewer.Settings
{
    public sealed class ViewerPreset
    {
        public string key { get; set; }
        public string name { get; set; }
        public string saved_at { get; set; }
        public List<string> channels { get; set; }
        public string sort_mode { get; set; }
        public bool? auto_step { get; set; }
        public int? target_points { get; set; }
        public int? manual_step { get; set; }
        public bool? include_extra { get; set; }
        public string refrigerant { get; set; }
    }

    public static class PresetStore
    {
        public static string PresetsDir(string baseDir)
        {
            return Path.Combine(baseDir, "saved_presets");
        }

        public static List<ViewerPreset> List(string baseDir)
        {
            string dir = PresetsDir(baseDir);
            var result = new List<ViewerPreset>();
            if (!Directory.Exists(dir))
            {
                return result;
            }

            foreach (string file in Directory.GetFiles(dir, "*.json", SearchOption.TopDirectoryOnly))
            {
                ViewerPreset p = JsonHelper.LoadFromFile(file, (ViewerPreset)null);
                if (p == null)
                {
                    continue;
                }
                if (string.IsNullOrWhiteSpace(p.key))
                {
                    p.key = Path.GetFileNameWithoutExtension(file);
                }
                if (string.IsNullOrWhiteSpace(p.name))
                {
                    p.name = p.key;
                }
                if (p.channels == null)
                {
                    p.channels = new List<string>();
                }
                result.Add(p);
            }

            return result.OrderBy(p => p.name, StringComparer.OrdinalIgnoreCase).ToList();
        }

        public static ViewerPreset Load(string baseDir, string keyOrName)
        {
            string key = Persistence.SanitizeKey(keyOrName, "preset");
            string path = Path.Combine(PresetsDir(baseDir), key + ".json");
            return JsonHelper.LoadFromFile(path, (ViewerPreset)null);
        }

        public static bool Exists(string baseDir, string keyOrName)
        {
            string key = Persistence.SanitizeKey(keyOrName, "preset");
            string path = Path.Combine(PresetsDir(baseDir), key + ".json");
            return File.Exists(path);
        }

        public static bool Delete(string baseDir, string keyOrName)
        {
            try
            {
                string key = Persistence.SanitizeKey(keyOrName, "preset");
                string path = Path.Combine(PresetsDir(baseDir), key + ".json");
                if (!File.Exists(path))
                {
                    return false;
                }
                File.Delete(path);
                return true;
            }
            catch
            {
                return false;
            }
        }

        public static ViewerPreset Save(string baseDir, string name, IList<string> channels)
        {
            string key = Persistence.SanitizeKey(name, "preset");
            string dir = PresetsDir(baseDir);
            Directory.CreateDirectory(dir);

            var p = new ViewerPreset
            {
                key = key,
                name = name,
                saved_at = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                channels = channels == null ? new List<string>() : channels.ToList()
            };

            string path = Path.Combine(dir, key + ".json");
            JsonHelper.SaveToFile(path, p);
            return p;
        }

        public static ViewerPreset Save(string baseDir, ViewerPreset preset)
        {
            if (preset == null)
            {
                throw new ArgumentNullException("preset");
            }

            string key = Persistence.SanitizeKey(preset.name, "preset");
            string dir = PresetsDir(baseDir);
            Directory.CreateDirectory(dir);

            preset.key = key;
            preset.saved_at = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            if (preset.channels == null)
            {
                preset.channels = new List<string>();
            }

            string path = Path.Combine(dir, key + ".json");
            JsonHelper.SaveToFile(path, preset);
            return preset;
        }
    }
}
