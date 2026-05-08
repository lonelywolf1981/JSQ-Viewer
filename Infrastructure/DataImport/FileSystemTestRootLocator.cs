using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using JSQViewer.Application.Workspace.Ports;

namespace JSQViewer.Infrastructure.DataImport
{
    public sealed class FileSystemTestRootLocator : ITestRootLocator
    {
        private static readonly Regex ProvaDbfRegex = new Regex(@"^Prova\d+\.dbf$", RegexOptions.IgnoreCase);

        public string FindRoot(string folder)
        {
            string absolutePath = Path.GetFullPath(folder);
            if (!Directory.Exists(absolutePath))
            {
                throw new DirectoryNotFoundException("Folder not found: " + absolutePath);
            }

            string[] localDbf = Directory.GetFiles(absolutePath, "Prova*.dbf", SearchOption.TopDirectoryOnly)
                .Where(path => ProvaDbfRegex.IsMatch(Path.GetFileName(path)))
                .ToArray();
            if (localDbf.Length > 0)
            {
                return absolutePath;
            }

            var found = new List<string>();
            SearchDbfRecursive(absolutePath, 0, 8, found);
            if (found.Count == 0)
            {
                throw new FileNotFoundException("No Prova*.dbf files found in selected folder: " + absolutePath);
            }

            return found.Select(Path.GetDirectoryName)
                .Where(path => !string.IsNullOrEmpty(path))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(path => GetRelativeDepth(absolutePath, path))
                .ThenBy(path => path, StringComparer.OrdinalIgnoreCase)
                .First();
        }

        private static void SearchDbfRecursive(string directory, int depth, int maxDepth, List<string> results)
        {
            if (depth > maxDepth)
            {
                return;
            }

            try
            {
                string[] files = Directory.GetFiles(directory, "Prova*.dbf", SearchOption.TopDirectoryOnly);
                for (int i = 0; i < files.Length; i++)
                {
                    if (ProvaDbfRegex.IsMatch(Path.GetFileName(files[i])))
                    {
                        results.Add(files[i]);
                    }
                }

                string[] subDirectories = Directory.GetDirectories(directory)
                    .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                    .ToArray();
                for (int i = 0; i < subDirectories.Length; i++)
                {
                    SearchDbfRecursive(subDirectories[i], depth + 1, maxDepth, results);
                }
            }
            catch (UnauthorizedAccessException)
            {
            }
        }

        private static int GetRelativeDepth(string root, string path)
        {
            string normalizedRoot = root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string normalizedPath = path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            if (normalizedPath.Length <= normalizedRoot.Length)
            {
                return 0;
            }

            string relative = normalizedPath.Substring(normalizedRoot.Length)
                .TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            if (string.IsNullOrEmpty(relative))
            {
                return 0;
            }

            return relative.Split(new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar }, StringSplitOptions.RemoveEmptyEntries).Length;
        }
    }
}
