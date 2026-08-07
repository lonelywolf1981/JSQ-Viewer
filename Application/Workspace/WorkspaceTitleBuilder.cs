using System;
using System.Collections.Generic;
using System.Globalization;
using JSQViewer.Core;

namespace JSQViewer.Application.Workspace
{
    public sealed class WorkspaceTitleBuilder
    {
        private readonly SourceDisplayNameResolver _resolver;

        public WorkspaceTitleBuilder(SourceDisplayNameResolver resolver)
        {
            _resolver = resolver ?? throw new ArgumentNullException(nameof(resolver));
        }

        public string Build(TestData data, string fallback)
        {
            IReadOnlyList<string> roots = _resolver.GetOrderedRoots(data);
            IReadOnlyDictionary<string, string> names = _resolver.ResolveAll(data);
            var titles = new List<string>();

            for (int i = 0; i < roots.Count; i++)
            {
                string title;
                if (names.TryGetValue(roots[i], out title)
                    && !string.IsNullOrWhiteSpace(title))
                {
                    titles.Add(title);
                }
            }

            if (titles.Count == 0)
            {
                return fallback ?? string.Empty;
            }

            if (titles.Count > 1)
            {
                return string.Join("; ", titles);
            }

            var parts = new List<string> { titles[0] };
            AppendMetaPart(parts, data, WorkspaceMetadataKeys.EquipmentModel);
            AppendMetaPart(parts, data, WorkspaceMetadataKeys.ExperimentType);
            AppendMetaPart(parts, data, WorkspaceMetadataKeys.ClimateMode);
            return string.Join(" · ", parts.ToArray());
        }

        private static void AppendMetaPart(ICollection<string> parts, TestData data, string metaKey)
        {
            if (data == null || data.Meta == null)
            {
                return;
            }

            string value;
            if (data.Meta.TryGetValue(metaKey, out value) && !string.IsNullOrWhiteSpace(value))
            {
                parts.Add(value.Trim());
            }
        }

        public string BuildCaption(TestData data, string fallback, string format)
        {
            return string.Format(
                CultureInfo.CurrentCulture,
                string.IsNullOrWhiteSpace(format) ? "{0}" : format,
                Build(data, fallback));
        }
    }
}
