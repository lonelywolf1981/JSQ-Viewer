using System;
using System.Collections.Generic;
using System.IO;
using JSQViewer.Application.Database;
using JSQViewer.Core;

namespace JSQViewer.Application.Workspace
{
    public sealed class SourceDisplayNameResolver
    {
        public IReadOnlyList<string> GetOrderedRoots(TestData data)
        {
            var roots = new List<string>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (data == null)
            {
                return roots;
            }

            AppendRoots(roots, seen, data.SourceOrder);
            if (data.SourceColumns != null)
            {
                AppendRoots(roots, seen, data.SourceColumns.Keys);
            }

            if (roots.Count == 0)
            {
                AppendRoot(roots, seen, data.Root);
            }

            return roots;
        }

        public IReadOnlyDictionary<string, string> ResolveAll(TestData data)
        {
            IReadOnlyList<string> roots = GetOrderedRoots(data);
            var names = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var recordingIds = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var collisionCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

            for (int i = 0; i < roots.Count; i++)
            {
                string root = roots[i];
                string name = ResolveBaseName(data, root, roots.Count == 1);
                names[root] = name;

                int count;
                collisionCounts.TryGetValue(name, out count);
                collisionCounts[name] = count + 1;

                string recordingId;
                if (RecordingSourceRef.TryParse(root, out recordingId))
                {
                    recordingIds[root] = recordingId;
                }
            }

            for (int i = 0; i < roots.Count; i++)
            {
                string root = roots[i];
                string name = names[root];
                string recordingId;
                if (collisionCounts[name] > 1
                    && recordingIds.TryGetValue(root, out recordingId))
                {
                    names[root] = name + " [" + recordingId + "]";
                }
            }

            return names;
        }

        public string Resolve(TestData data, string sourceRoot)
        {
            if (string.IsNullOrWhiteSpace(sourceRoot))
            {
                return string.Empty;
            }

            IReadOnlyDictionary<string, string> names = ResolveAll(data);
            string name;
            if (names.TryGetValue(sourceRoot, out name))
            {
                return name;
            }

            IReadOnlyList<string> orderedRoots = GetOrderedRoots(data);
            bool isOnlyWorkspaceRoot = orderedRoots.Count == 1
                && string.Equals(
                    orderedRoots[0],
                    sourceRoot,
                    StringComparison.OrdinalIgnoreCase);
            return ResolveBaseName(data, sourceRoot, isOnlyWorkspaceRoot);
        }

        private static void AppendRoots(
            ICollection<string> roots,
            ISet<string> seen,
            IEnumerable<string> candidates)
        {
            if (candidates == null)
            {
                return;
            }

            foreach (string candidate in candidates)
            {
                AppendRoot(roots, seen, candidate);
            }
        }

        private static void AppendRoot(
            ICollection<string> roots,
            ISet<string> seen,
            string candidate)
        {
            if (!string.IsNullOrWhiteSpace(candidate) && seen.Add(candidate))
            {
                roots.Add(candidate);
            }
        }

        private static string ResolveBaseName(
            TestData data,
            string sourceRoot,
            bool isOnlyRoot)
        {
            string displayName;
            if (TryGetDisplayName(data, sourceRoot, out displayName))
            {
                return displayName;
            }

            string recordingId;
            if (RecordingSourceRef.TryParse(sourceRoot, out recordingId))
            {
                string legacyTitle;
                if (isOnlyRoot && TryGetLegacyTitle(data, out legacyTitle))
                {
                    return legacyTitle;
                }

                return recordingId;
            }

            string pathName = TryGetPathName(sourceRoot);
            return string.IsNullOrWhiteSpace(pathName) ? sourceRoot : pathName;
        }

        private static bool TryGetDisplayName(
            TestData data,
            string sourceRoot,
            out string displayName)
        {
            displayName = null;
            if (data == null || data.SourceDisplayNames == null)
            {
                return false;
            }

            string directValue;
            if (data.SourceDisplayNames.TryGetValue(sourceRoot, out directValue)
                && !string.IsNullOrWhiteSpace(directValue))
            {
                displayName = directValue.Trim();
                return true;
            }

            foreach (KeyValuePair<string, string> pair in data.SourceDisplayNames)
            {
                if (string.Equals(pair.Key, sourceRoot, StringComparison.OrdinalIgnoreCase)
                    && !string.IsNullOrWhiteSpace(pair.Value))
                {
                    displayName = pair.Value.Trim();
                    return true;
                }
            }

            return false;
        }

        private static bool TryGetLegacyTitle(TestData data, out string title)
        {
            title = null;
            if (data == null || data.Meta == null)
            {
                return false;
            }

            string value;
            if (!data.Meta.TryGetValue("Название", out value)
                || string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            title = value.Trim();
            return true;
        }

        private static string TryGetPathName(string sourceRoot)
        {
            try
            {
                string path = sourceRoot.TrimEnd(
                    Path.DirectorySeparatorChar,
                    Path.AltDirectorySeparatorChar);
                return Path.GetFileName(path);
            }
            catch (ArgumentException)
            {
                return null;
            }
            catch (NotSupportedException)
            {
                return null;
            }
            catch (PathTooLongException)
            {
                return null;
            }
        }
    }
}
