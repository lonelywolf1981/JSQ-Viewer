using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;

namespace LeMuReViewer.Settings
{
    public static class Persistence
    {
        public static string SanitizeKey(string name, string fallback)
        {
            string value = (name ?? string.Empty).Trim();
            if (value.Length == 0)
            {
                return fallback;
            }

            string cleaned = Regex.Replace(value, @"[\\/:*?""<>|]+", "_");
            cleaned = Regex.Replace(cleaned, @"\s+", " ").Trim();
            return cleaned.Length == 0 ? fallback : cleaned;
        }

        public static bool SaveJson<T>(string folder, string key, T payload)
        {
            string safeKey = SanitizeKey(key, "default");
            string path = Path.Combine(folder, safeKey + ".json");
            return JsonHelper.SaveToFile(path, payload);
        }

        public static T LoadJson<T>(string folder, string key, T fallback)
        {
            string safeKey = SanitizeKey(key, "default");
            string path = Path.Combine(folder, safeKey + ".json");
            return JsonHelper.LoadFromFile(path, fallback);
        }

        public static bool DeleteJson(string folder, string key)
        {
            try
            {
                string safeKey = SanitizeKey(key, "default");
                string path = Path.Combine(folder, safeKey + ".json");
                if (File.Exists(path))
                {
                    File.Delete(path);
                }

                return true;
            }
            catch
            {
                return false;
            }
        }

        public static List<string> ListSavedKeys(string folder)
        {
            var result = new List<string>();
            if (!Directory.Exists(folder))
            {
                return result;
            }

            string[] files = Directory.GetFiles(folder, "*.json", SearchOption.TopDirectoryOnly);
            Array.Sort(files, StringComparer.OrdinalIgnoreCase);
            foreach (string file in files)
            {
                result.Add(Path.GetFileNameWithoutExtension(file));
            }

            return result;
        }
    }
}
