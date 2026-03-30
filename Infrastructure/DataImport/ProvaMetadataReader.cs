using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using JSQViewer.Application.Workspace.Ports;
using JSQViewer.Core;

namespace JSQViewer.Infrastructure.DataImport
{
    public sealed class ProvaMetadataReader : ITestMetadataReader
    {
        private static readonly Regex ProvaDatRegex = new Regex(@"Prova\d+\.dat$", RegexOptions.IgnoreCase);

        public Dictionary<string, string> Read(string root)
        {
            if (string.IsNullOrWhiteSpace(root))
            {
                throw new ArgumentException("Root folder is required.", nameof(root));
            }

            foreach (string file in Directory.GetFiles(root))
            {
                if (ProvaDatRegex.IsMatch(Path.GetFileName(file)))
                {
                    return CanaliParser.ParseProvaDat(file);
                }
            }

            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }
    }
}
