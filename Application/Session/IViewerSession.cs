using JSQViewer.Core;

namespace JSQViewer.Application.Session
{
    public interface IViewerSession
    {
        int DataVersion { get; }
        bool IsLoaded { get; }
        string Folder { get; }
        TestData Data { get; }

        void SetData(string folder, TestData data);
    }
}
