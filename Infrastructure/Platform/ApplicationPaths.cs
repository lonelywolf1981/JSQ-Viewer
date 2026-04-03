using System;
using System.IO;
using JSQViewer.Application.Abstractions;
using JSQViewer.Application.Exporting;

namespace JSQViewer.Infrastructure.Platform
{
    public sealed class ApplicationPaths : IAppPaths
    {
        public ApplicationPaths()
            : this(AppDomain.CurrentDomain.BaseDirectory)
        {
        }

        public ApplicationPaths(string applicationBaseDirectory)
        {
            ApplicationBaseDirectory = string.IsNullOrWhiteSpace(applicationBaseDirectory)
                ? AppDomain.CurrentDomain.BaseDirectory
                : applicationBaseDirectory;
        }

        public string ApplicationBaseDirectory { get; }

        public string ProjectRoot
        {
            get
            {
                DirectoryInfo directory = new DirectoryInfo(ApplicationBaseDirectory);
                for (int i = 0; i < 8 && directory != null; i++)
                {
                    if (File.Exists(Path.Combine(directory.FullName, "template.xlsx")))
                    {
                        return directory.FullName;
                    }

                    directory = directory.Parent;
                }

                return ApplicationBaseDirectory;
            }
        }

        public string LogDirectory
        {
            get { return Path.Combine(ApplicationBaseDirectory, "log"); }
        }

        public string GetProtocolTemplatePath(ProtocolTemplateMode mode)
        {
            string fileName = mode == ProtocolTemplateMode.DoubleCabinet
                ? "template2.xlsx"
                : "template.xlsx";
            return Path.Combine(ProjectRoot, fileName);
        }
    }
}
