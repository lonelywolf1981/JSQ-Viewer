using System.Text;

namespace JSQViewer.Application.Abstractions
{
    public interface IFileSystem
    {
        void CreateDirectory(string path);

        void AppendAllText(string path, string contents, Encoding encoding);
    }
}
