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

        private static readonly string[] AllowedPrefixes = new[] { "A-", "B-", "C-" };

        public static List<string> Build(string[] cols, Dictionary<string, ChannelInfo> channels)
        {
            if (cols == null || cols.Length == 0)
                return new List<string>();

            var channelMap = channels ?? new Dictionary<string, ChannelInfo>(StringComparer.OrdinalIgnoreCase);
            var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var result = new List<string>(cols.Length);
            string priorityPrefix = FindPriorityPrefix(cols);

            // 1. FixedKeys: Pc, Pe, T-sie, UR-sie, Tc, Te, T1..T7, I, F, V, W.
            // Для T1..T7, а также для остальных prefixed fixed keys, берем канал с тем
            // A-/B-/C-префиксом, который найден у T1..T7.
            foreach (string key in FixedKeys)
            {
                foreach (string matched in ResolveFixedKey(key, cols, used, priorityPrefix))
                {
                    result.Add(matched);
                    used.Add(matched);
                }
            }

            // 2. extras = все каналы, не вошедшие в FixedKeys.
            var extras = cols
                .Where(c => !string.IsNullOrWhiteSpace(c) && !used.Contains(c))
                .ToList();

            // 3-5. Group1 = каналы с приоритетным префиксом, Group2 = остальные.
            // Внутри групп — натуральная сортировка по display name.
            var group1 = extras
                .Where(c => HasPrefix(c, priorityPrefix))
                .OrderBy(c => GetDisplaySortName(c, channelMap), NaturalStringComparer.Instance)
                .ThenBy(c => GetBaseCode(c), NaturalStringComparer.Instance)
                .ThenBy(c => c, NaturalStringComparer.Instance)
                .ToList();

            var group2 = extras
                .Where(c => !HasPrefix(c, priorityPrefix))
                .OrderBy(c => GetDisplaySortName(c, channelMap), NaturalStringComparer.Instance)
                .ThenBy(c => GetBaseCode(c), NaturalStringComparer.Instance)
                .ThenBy(c => c, NaturalStringComparer.Instance)
                .ToList();

            result.AddRange(group1);
            result.AddRange(group2);
            return result;
        }

        private static string FindPriorityPrefix(string[] cols)
        {
            if (cols == null) return string.Empty;

            for (int i = 1; i <= 7; i++)
            {
                string key = "T" + i.ToString();
                foreach (string col in cols)
                {
                    string baseCode = GetBaseCode(col);
                    foreach (string prefix in AllowedPrefixes)
                    {
                        if (string.Equals(baseCode, prefix + key, StringComparison.OrdinalIgnoreCase))
                            return prefix;
                    }
                }
            }

            return string.Empty;
        }

        private static List<string> ResolveFixedKey(string key, string[] cols, HashSet<string> used, string priorityPrefix)
        {
            if (cols == null) return new List<string>();

            var bySource = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
            var sourceOrder = new List<string>();
            for (int i = 0; i < cols.Length; i++)
            {
                string col = cols[i];
                if (string.IsNullOrWhiteSpace(col) || (used != null && used.Contains(col)))
                    continue;

                string baseCode = GetBaseCode(col);
                if (!IsFixedKeyCandidate(baseCode, key))
                    continue;

                string source = GetSourceTag(col);
                if (!bySource.ContainsKey(source))
                {
                    bySource[source] = new List<string>();
                    sourceOrder.Add(source);
                }

                bySource[source].Add(col);
            }

            var result = new List<string>(sourceOrder.Count);
            for (int i = 0; i < sourceOrder.Count; i++)
            {
                string selected = SelectBestFixedCandidate(bySource[sourceOrder[i]], key, priorityPrefix);
                if (!string.IsNullOrWhiteSpace(selected))
                {
                    result.Add(selected);
                }
            }

            return result;
        }

        private static bool IsFixedKeyCandidate(string baseCode, string key)
        {
            if (string.IsNullOrWhiteSpace(baseCode) || string.IsNullOrWhiteSpace(key))
                return false;

            if (string.Equals(baseCode, key, StringComparison.OrdinalIgnoreCase))
                return true;

            for (int i = 0; i < AllowedPrefixes.Length; i++)
            {
                if (string.Equals(baseCode, AllowedPrefixes[i] + key, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }

        private static string SelectBestFixedCandidate(List<string> candidates, string key, string priorityPrefix)
        {
            if (candidates == null || candidates.Count == 0)
                return null;

            if (!string.IsNullOrWhiteSpace(priorityPrefix))
            {
                string prefixed = FirstCandidateByBaseCode(candidates, priorityPrefix + key);
                if (!string.IsNullOrWhiteSpace(prefixed))
                    return prefixed;
            }

            string aPrefixed = FirstCandidateByBaseCode(candidates, "A-" + key);
            if (!string.IsNullOrWhiteSpace(aPrefixed))
                return aPrefixed;

            string cPrefixed = FirstCandidateByBaseCode(candidates, "C-" + key);
            if (!string.IsNullOrWhiteSpace(cPrefixed))
                return cPrefixed;

            string bPrefixed = FirstCandidateByBaseCode(candidates, "B-" + key);
            if (!string.IsNullOrWhiteSpace(bPrefixed))
                return bPrefixed;

            string exact = FirstCandidateByBaseCode(candidates, key);
            return !string.IsNullOrWhiteSpace(exact) ? exact : candidates[0];
        }

        private static string FirstCandidateByBaseCode(List<string> candidates, string baseCode)
        {
            for (int i = 0; i < candidates.Count; i++)
            {
                if (string.Equals(GetBaseCode(candidates[i]), baseCode, StringComparison.OrdinalIgnoreCase))
                    return candidates[i];
            }

            return null;
        }

        private static bool HasPrefix(string code, string prefix)
        {
            return !string.IsNullOrWhiteSpace(prefix)
                   && GetBaseCode(code).StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
        }

        private static string GetBaseCode(string code)
        {
            if (string.IsNullOrWhiteSpace(code)) return string.Empty;
            int sep = code.IndexOf("::", StringComparison.Ordinal);
            string result = sep >= 0 ? code.Substring(sep + 2) : code;
            int hash = result.IndexOf('#');
            return hash > 0 ? result.Substring(0, hash) : result;
        }

        private static string GetSourceTag(string code)
        {
            if (string.IsNullOrWhiteSpace(code)) return string.Empty;
            int sep = code.IndexOf("::", StringComparison.Ordinal);
            return sep >= 0 ? code.Substring(0, sep) : string.Empty;
        }

        private static string GetDisplaySortName(string code, Dictionary<string, ChannelInfo> channels)
        {
            ChannelInfo ch;
            if (channels != null && channels.TryGetValue(code, out ch))
            {
                if (!string.IsNullOrWhiteSpace(ch.Name)) return ch.Name.Trim();

                if (!string.IsNullOrWhiteSpace(ch.Label) && !string.Equals(ch.Label, ch.Code, StringComparison.OrdinalIgnoreCase))
                    return GetBaseCode(ch.Label).Trim();
            }

            return GetBaseCode(code);
        }
    }
}
