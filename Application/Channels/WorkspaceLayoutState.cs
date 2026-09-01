using System;
using System.Collections.Generic;
using System.Linq;

namespace JSQViewer.Application.Channels
{
    public sealed class WorkspaceLayoutState
    {
        public string MainSelectedOrderKey { get; set; }

        public Dictionary<string, string> SourceSelectedOrderKeys { get; set; }

        public Dictionary<string, List<string>> SourceEffectiveOrders { get; set; }

        public Dictionary<string, T8PlusLineSelection> SourceT8PlusLines { get; set; }

        public WorkspaceLayoutState()
        {
            SourceSelectedOrderKeys = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            SourceEffectiveOrders = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
            SourceT8PlusLines = new Dictionary<string, T8PlusLineSelection>(StringComparer.OrdinalIgnoreCase);
        }

        public void EnsureInitialized()
        {
            SourceSelectedOrderKeys = NormalizeSelectedOrderKeys(SourceSelectedOrderKeys);
            SourceEffectiveOrders = NormalizeEffectiveOrders(SourceEffectiveOrders);
            SourceT8PlusLines = NormalizeT8PlusLines(SourceT8PlusLines);
        }

        public static string NormalizeSourceRoot(string sourceRoot)
        {
            string normalized = (sourceRoot ?? string.Empty).Trim().Replace('/', '\\');
            while (normalized.Length > 3 && normalized.EndsWith("\\", StringComparison.Ordinal))
            {
                normalized = normalized.Substring(0, normalized.Length - 1);
            }

            return normalized;
        }

        private static Dictionary<string, string> NormalizeSelectedOrderKeys(Dictionary<string, string> source)
        {
            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (source == null)
            {
                return result;
            }

            foreach (KeyValuePair<string, string> pair in source)
            {
                string key = NormalizeSourceRoot(pair.Key);
                if (key.Length == 0)
                {
                    continue;
                }

                string value = string.IsNullOrWhiteSpace(pair.Value) ? null : pair.Value.Trim();
                result[key] = value;
            }

            return result;
        }

        private static Dictionary<string, List<string>> NormalizeEffectiveOrders(Dictionary<string, List<string>> source)
        {
            var result = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
            if (source == null)
            {
                return result;
            }

            foreach (KeyValuePair<string, List<string>> pair in source)
            {
                string key = NormalizeSourceRoot(pair.Key);
                if (key.Length == 0)
                {
                    continue;
                }

                List<string> order = pair.Value == null
                    ? new List<string>()
                    : pair.Value
                        .Where(code => !string.IsNullOrWhiteSpace(code))
                        .Select(code => code.Trim())
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .ToList();
                if (order.Count == 0)
                {
                    continue;
                }

                result[key] = order;
            }

            return result;
        }

        private static Dictionary<string, T8PlusLineSelection> NormalizeT8PlusLines(
            Dictionary<string, T8PlusLineSelection> source)
        {
            var result = new Dictionary<string, T8PlusLineSelection>(StringComparer.OrdinalIgnoreCase);
            if (source == null)
            {
                return result;
            }

            foreach (KeyValuePair<string, T8PlusLineSelection> pair in source)
            {
                string key = NormalizeSourceRoot(pair.Key);
                if (key.Length == 0 || pair.Value == null || !pair.Value.HasAny)
                {
                    continue;
                }

                result[key] = pair.Value;
            }

            return result;
        }
    }

    public sealed class T8PlusLineSelection
    {
        public static readonly T8PlusLineSelection None = new T8PlusLineSelection(false, false, false);

        public T8PlusLineSelection()
        {
        }

        public T8PlusLineSelection(bool showMinimum, bool showAverage, bool showMaximum)
        {
            ShowMinimum = showMinimum;
            ShowAverage = showAverage;
            ShowMaximum = showMaximum;
        }

        public bool ShowMinimum { get; set; }

        public bool ShowAverage { get; set; }

        public bool ShowMaximum { get; set; }

        public bool HasAny
        {
            get { return ShowMinimum || ShowAverage || ShowMaximum; }
        }
    }
}
