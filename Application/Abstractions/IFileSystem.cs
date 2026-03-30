using System.Text;

namespace JSQViewer.Application.Abstractions
{
    public interface IFileSystem
    {
        bool FileExists(string path);

        bool DirectoryExists(string path);

        void WriteAllBytes(string path, byte[] contents);

        void CreateDirectory(string path);

        void AppendAllText(string path, string contents, Encoding encoding);
    }
}
