using System;
using System.Collections.Generic;
using System.Linq;

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
    }
}
