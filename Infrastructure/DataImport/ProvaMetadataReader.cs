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

            string fallbackDat = null;
            foreach (string file in Directory.GetFiles(root, "*.dat"))
            {
                if (ProvaDatRegex.IsMatch(Path.GetFileName(file)))
                {
                    return CanaliParser.ParseProvaDat(file);
                }

                if (fallbackDat == null)
                {
                    fallbackDat = file;
                }
            }

            if (fallbackDat != null)
            {
                return CanaliParser.ParseProvaDat(fallbackDat);
            }

            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }
    }
}
