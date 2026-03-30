using System.IO;
using System.Text;
using JSQViewer.Application.Abstractions;

namespace JSQViewer.Infrastructure.Platform
{
    public sealed class FileSystemAdapter : IFileSystem
    {
        public void CreateDirectory(string path)
        {
            Directory.CreateDirectory(path);
        }

        public void AppendAllText(string path, string contents, Encoding encoding)
        {
            File.AppendAllText(path, contents, encoding);
        }
    }
}
