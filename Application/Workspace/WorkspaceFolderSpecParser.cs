using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace JSQViewer.Application.Workspace
{
    public sealed class WorkspaceFolderSpecParser
    {
        public const int MaxFolderCount = 6;

        public IReadOnlyList<string> Parse(string spec)
        {
            var folders = new List<string>();
            string raw = spec ?? string.Empty;
            string[] parts = raw.Split(new[] { ';', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < parts.Length; i++)
            {
                string folder = parts[i].Trim().Trim('"');
                if (folder.Length == 0)
                {
                    continue;
                }

                if (!folders.Any(existing => string.Equals(existing, folder, StringComparison.OrdinalIgnoreCase)))
                {
                    folders.Add(folder);
                }
            }

            return folders;
        }

        public string Join(IEnumerable<string> folders)
        {
            return string.Join(" ; ", folders ?? Array.Empty<string>());
        }

        public string BuildWorkspaceKey(IEnumerable<string> folders)
        {
            string[] normalized = (folders ?? Array.Empty<string>())
                .Where(folder => !string.IsNullOrWhiteSpace(folder))
                .Select(NormalizeFolderPath)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(folder => folder, StringComparer.OrdinalIgnoreCase)
                .ToArray();

            if (normalized.Length == 0)
            {
                return "workspace-empty";
            }

            string payload = string.Join("|", normalized);
            using (var sha = SHA256.Create())
            {
                byte[] hash = sha.ComputeHash(Encoding.UTF8.GetBytes(payload));
                var builder = new StringBuilder(hash.Length * 2);
                for (int i = 0; i < hash.Length; i++)
                {
                    builder.Append(hash[i].ToString("x2"));
                }

                return builder.ToString();
            }
        }

        private static string NormalizeFolderPath(string folder)
        {
            string path = (folder ?? string.Empty).Trim().Trim('"');
            path = path.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);
            while (path.EndsWith(Path.DirectorySeparatorChar.ToString(), StringComparison.Ordinal))
            {
                path = path.Substring(0, path.Length - 1);
            }

            return path.ToLowerInvariant();
        }
    }
}
