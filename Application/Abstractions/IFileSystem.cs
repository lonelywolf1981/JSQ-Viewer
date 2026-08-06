using System.Text;
using System.IO;
using System;

namespace JSQViewer.Application.Abstractions
{
    public interface IFileSystem
    {
        bool FileExists(string path);

        bool DirectoryExists(string path);

        string[] GetFiles(string path, string searchPattern, SearchOption searchOption);

        DateTime GetLastWriteTime(string path);

        void WriteAllBytes(string path, byte[] contents);

        void CreateDirectory(string path);

        void AppendAllText(string path, string contents, Encoding encoding);
    }
}
