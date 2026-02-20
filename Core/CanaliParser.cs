using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace JSQViewer.Core
{
    public static class CanaliParser
    {
        public static Dictionary<string, ChannelInfo> ParseCanaliDef(string path)
        {
            var result = new Dictionary<string, ChannelInfo>(StringComparer.OrdinalIgnoreCase);
            if (!File.Exists(path))
            {
                return result;
            }

            string text = SafeReadText(path);
            string[] lines = text.Split(new[] { "\r\n", "\n", "\r" }, StringSplitOptions.RemoveEmptyEntries);
            var cleaned = new List<string>(lines.Length);
            foreach (string line in lines)
            {
                string trimmed = line.Trim();
                if (trimmed.Length > 0)
                {
                    cleaned.Add(trimmed);
                }
            }

            if (cleaned.Count == 0)
            {
                return result;
            }

            if (Regex.IsMatch(cleaned[0], @"^\d+$"))
            {
                cleaned.RemoveAt(0);
            }

            foreach (string line in cleaned)
            {
                string[] parts = line.Split(';');
                if (parts.Length < 3)
                {
                    continue;
                }

                string code = parts[0].Trim();
                if (code.Length == 0)
                {
                    continue;
                }

                result[code] = new ChannelInfo
                {
                    Code = code,
                    Name = parts[1].Trim(),
                    Unit = parts[2].Trim()
                };
            }

            return result;
        }

        public static Dictionary<string, string> ParseProvaDat(string path)
        {
            var meta = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (!File.Exists(path))
            {
                return meta;
            }

            string text = SafeReadText(path);
            string[] lines = text.Split(new[] { "\r\n", "\n", "\r" }, StringSplitOptions.RemoveEmptyEntries);
            foreach (string line in lines)
            {
                string trimmed = line.Trim();
                int sep = trimmed.IndexOf(';');
                if (trimmed.Length == 0 || sep <= 0)
                {
                    continue;
                }

                string key = trimmed.Substring(0, sep).Trim();
                string value = trimmed.Substring(sep + 1).Trim();
                if (key.Length > 0)
                {
                    meta[key] = value;
                }
            }

            return meta;
        }

        internal static string SafeReadText(string path)
        {
            byte[] raw = File.ReadAllBytes(path);
            if (raw.Length == 0)
            {
                return string.Empty;
            }

            // BOM detection
            if (raw.Length >= 3 && raw[0] == 0xEF && raw[1] == 0xBB && raw[2] == 0xBF)
            {
                return Encoding.UTF8.GetString(raw, 3, raw.Length - 3);
            }

            // Try strict UTF-8 (throws on invalid sequences)
            try
            {
                var strictUtf8 = new UTF8Encoding(false, true);
                return strictUtf8.GetString(raw);
            }
            catch
            {
            }

            // Fallback to CP1251 (Cyrillic Windows)
            return Encoding.GetEncoding(1251).GetString(raw);
        }
    }
}
