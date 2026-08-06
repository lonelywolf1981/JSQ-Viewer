using System;
using System.Collections.Generic;
using System.Globalization;
using JSQViewer.Application.Workspace;
using JSQViewer.Core;
using JSQViewer.Presentation.WinForms.ViewModels;

namespace JSQViewer.Presentation.WinForms.Presenters
{
    public sealed class DynamicsForecastRoleItemBuilder
    {
        private readonly SourceDisplayNameResolver _sourceDisplayNameResolver;

        public DynamicsForecastRoleItemBuilder()
            : this(new SourceDisplayNameResolver())
        {
        }

        public DynamicsForecastRoleItemBuilder(SourceDisplayNameResolver sourceDisplayNameResolver)
        {
            _sourceDisplayNameResolver = sourceDisplayNameResolver
                ?? throw new ArgumentNullException(nameof(sourceDisplayNameResolver));
        }

        public IReadOnlyList<DynamicsForecastRoleItemViewModel> Build(
            TestData data,
            IEnumerable<string> selectedCodes)
        {
            var result = new List<DynamicsForecastRoleItemViewModel>();
            if (data == null || selectedCodes == null)
            {
                return result;
            }

            foreach (string code in selectedCodes)
            {
                if (string.IsNullOrWhiteSpace(code))
                {
                    continue;
                }

                string source = ResolveSource(data, code);
                string sourceLabel = _sourceDisplayNameResolver.Resolve(data, source);
                string displayCode = NormalizeChannelCodeForDisplay(code);
                string label = string.IsNullOrWhiteSpace(sourceLabel)
                    ? displayCode
                    : string.Format(CultureInfo.InvariantCulture, "[{0}] {1}", sourceLabel, displayCode);
                result.Add(new DynamicsForecastRoleItemViewModel(
                    code,
                    label,
                    ResolveDurationHours(data, source)));
            }

            return result;
        }

        private static string ResolveSource(TestData data, string code)
        {
            string source;
            if (data.CodeSources != null && data.CodeSources.TryGetValue(code, out source))
            {
                return source ?? string.Empty;
            }

            return string.Empty;
        }

        private static double ResolveDurationHours(TestData data, string source)
        {
            if (string.IsNullOrWhiteSpace(source)
                || data.SourceStartMs == null
                || data.SourceEndMs == null)
            {
                return double.PositiveInfinity;
            }

            long startMs;
            long endMs;
            if (!data.SourceStartMs.TryGetValue(source, out startMs)
                || !data.SourceEndMs.TryGetValue(source, out endMs))
            {
                return double.PositiveInfinity;
            }

            return Math.Max(0d, (endMs - startMs) / 3600000d);
        }

        private static string NormalizeChannelCodeForDisplay(string code)
        {
            string result = (code ?? string.Empty).Trim();
            int separator = result.IndexOf("::", StringComparison.Ordinal);
            if (separator >= 0)
            {
                result = result.Substring(separator + 2);
            }

            int hash = result.IndexOf('#');
            if (hash > 0)
            {
                result = result.Substring(0, hash);
            }

            return result;
        }
    }
}
