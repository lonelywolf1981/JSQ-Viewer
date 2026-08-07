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

            // Recording names already carry model, compressor, experiment type and climate mode —
            // RecordingDisplayNameBuilder composes them when the recording is read, so that every
            // window showing a source name shows the same text. Nothing is appended here.
            return titles.Count == 0
                ? fallback ?? string.Empty
                : string.Join("; ", titles);
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
