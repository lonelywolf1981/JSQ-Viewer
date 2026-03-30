using System.IO;
using System.Text;
using JSQViewer.Application.Abstractions;

namespace JSQViewer.Infrastructure.Platform
{
    public sealed class FileSystemAdapter : IFileSystem
    {
        public bool FileExists(string path)
        {
            return File.Exists(path);
        }

        public bool DirectoryExists(string path)
        {
            return Directory.Exists(path);
        }

        public void WriteAllBytes(string path, byte[] contents)
        {
            File.WriteAllBytes(path, contents);
        }

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
