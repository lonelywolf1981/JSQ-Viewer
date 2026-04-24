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
                string matched = ResolveKey(key, cols, used);
                if (!string.IsNullOrEmpty(matched))
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

        private static string ResolveKey(string key, string[] cols, HashSet<string> used)
        {
            var exact = new List<string>();
            var suffix = new List<string>();
            string suf = "-" + key;

            foreach (string c in cols)
            {
                if (used.Contains(c)) continue;
                if (string.Equals(c, key, StringComparison.OrdinalIgnoreCase))
                    exact.Add(c);
                else if (c.EndsWith(suf, StringComparison.OrdinalIgnoreCase))
                    suffix.Add(c);
            }

            var candidates = exact.Concat(suffix).ToList();
            if (candidates.Count == 0) return string.Empty;

            foreach (string pref in new[] { "A-", "C-" })
            {
                string byPref = candidates.FirstOrDefault(
                    c => c.StartsWith(pref, StringComparison.OrdinalIgnoreCase));
                if (byPref != null) return byPref;
            }

            return candidates[0];
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
