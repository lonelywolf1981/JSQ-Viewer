using System;
using System.Collections.Generic;
using System.IO;
using JSQViewer.Application.Workspace.Ports;
using JSQViewer.Core;

namespace JSQViewer.Infrastructure.DataImport
{
    public sealed class CanaliDefinitionReader : ICanaliDefinitionReader
    {
        public Dictionary<string, ChannelInfo> Read(string root)
        {
            if (string.IsNullOrWhiteSpace(root))
            {
                throw new ArgumentException("Root folder is required.", nameof(root));
            }

            string path = Path.Combine(root, "Set", "Canali.def");
            return CanaliParser.ParseCanaliDef(path);
        }
    }
}
