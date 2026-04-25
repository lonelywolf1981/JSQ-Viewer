using System;
using System.Collections.Generic;
using System.Linq;
using JSQViewer.Core;

namespace JSQViewer.Application.Channels
{
    public static class ProtocolChannelOrder
    {
        private static readonly string[] FixedKeys = new[]
        {
            "Pc", "Pe", "T-sie", "UR-sie", "Tc", "Te",
            "T1", "T2", "T3", "T4", "T5", "T6", "T7",
            "I", "F", "V", "W"
        };

        public static List<string> Build(string[] cols, Dictionary<string, ChannelInfo> channels)
        {
            if (cols == null || cols.Length == 0)
                return new List<string>();

            var channelMap = channels ?? new Dictionary<string, ChannelInfo>(StringComparer.OrdinalIgnoreCase);
            var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var result = new List<string>(cols.Length);

            foreach (string key in FixedKeys)
            {
                foreach (string matched in ResolveKey(key, cols, used))
                {
                    result.Add(matched);
                    used.Add(matched);
                }
            }

            var extras = cols
                .Where(c => !string.IsNullOrWhiteSpace(c) && !used.Contains(c))
                .OrderBy(c => GetDisplayName(c, channelMap), NaturalStringComparer.Instance)
                .ThenBy(c => c, NaturalStringComparer.Instance)
                .ToList();

            result.AddRange(extras);
            return result;
        }

        private static string StripMergeDecorations(string code)
        {
            int colonIndex = code.IndexOf("::", StringComparison.Ordinal);
            string result = colonIndex >= 0 ? code.Substring(colonIndex + 2) : code;
            int hashIndex = result.IndexOf('#');
            return hashIndex > 0 ? result.Substring(0, hashIndex) : result;
        }

        private static List<string> ResolveKey(string key, string[] cols, HashSet<string> used)
        {
            var exact = new List<string>();
            var suffix = new List<string>();
            string suf = "-" + key;

            foreach (string c in cols)
            {
                if (used.Contains(c)) continue;
                string baseCode = StripMergeDecorations(c);
                if (string.Equals(baseCode, key, StringComparison.OrdinalIgnoreCase))
                    exact.Add(c);
                else if (baseCode.EndsWith(suf, StringComparison.OrdinalIgnoreCase))
                    suffix.Add(c);
            }

            var candidates = exact.Concat(suffix).ToList();
            if (candidates.Count == 0) return new List<string>();

            // Группируем по тегу источника (часть до "::", либо "" для одиночного источника).
            // Внутри каждой группы применяем приоритет A-/C-префикса и выбираем одного победителя.
            // Это гарантирует: при одном источнике — один канал на слот (старое поведение),
            // при нескольких источниках — по одному каналу от каждого источника.
            var bySource = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
            var sourceOrder = new List<string>();
            foreach (string c in candidates)
            {
                int sep = c.IndexOf("::", StringComparison.Ordinal);
                string tag = sep >= 0 ? c.Substring(0, sep) : string.Empty;
                if (!bySource.ContainsKey(tag))
                {
                    bySource[tag] = new List<string>();
                    sourceOrder.Add(tag);
                }
                bySource[tag].Add(c);
            }

            var result = new List<string>(sourceOrder.Count);
            foreach (string tag in sourceOrder)
            {
                result.Add(SelectBest(bySource[tag]));
            }
            return result;
        }

        private static string SelectBest(List<string> group)
        {
            foreach (string pref in new[] { "A-", "C-" })
            {
                string byPref = group.FirstOrDefault(
                    c => StripMergeDecorations(c).StartsWith(pref, StringComparison.OrdinalIgnoreCase));
                if (byPref != null) return byPref;
            }
            return group[0];
        }

        private static string GetDisplayName(string code, Dictionary<string, ChannelInfo> channels)
        {
            ChannelInfo ch;
            if (channels.TryGetValue(code, out ch))
            {
                if (!string.IsNullOrWhiteSpace(ch.Name)) return ch.Name.Trim();
                if (!string.IsNullOrWhiteSpace(ch.Label)) return ch.Label.Trim();
            }
            return code ?? string.Empty;
        }
    }
}
