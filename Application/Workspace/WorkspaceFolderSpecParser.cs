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

                string normalized = NormalizeFolder(folder);
                if (!folders.Any(existing => string.Equals(NormalizeFolder(existing), normalized, StringComparison.Ordinal)))
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

        public string CreateWorkspaceKey(IEnumerable<string> folders)
        {
            string[] normalized = NormalizeFolders(folders).ToArray();
            if (normalized.Length == 0)
            {
                return string.Empty;
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

        public IReadOnlyList<string> NormalizeFolders(IEnumerable<string> folders)
        {
            return (folders ?? Array.Empty<string>())
                .Select(NormalizeFolder)
                .Where(value => value.Length > 0)
                .Distinct(StringComparer.Ordinal)
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray();
        }

        private static string NormalizeFolder(string folder)
        {
            string value = (folder ?? string.Empty).Trim().Trim('"');
            if (value.Length == 0)
            {
                return string.Empty;
            }

            value = value.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);
            while (value.Length > 1 && value.EndsWith(Path.DirectorySeparatorChar.ToString(), StringComparison.Ordinal))
            {
                string root = Path.GetPathRoot(value);
                if (!string.IsNullOrEmpty(root) && value.Length <= root.Length)
                {
                    break;
                }

                value = value.Substring(0, value.Length - 1);
            }

            return value.ToLowerInvariant();
        }
    }
}
