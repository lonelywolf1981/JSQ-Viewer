using JSQViewer.Application.Exporting;

namespace JSQViewer.Application.Abstractions
{
    public interface IAppPaths
    {
        string ApplicationBaseDirectory { get; }

        string ProjectRoot { get; }

        string LogDirectory { get; }

        string GetProtocolTemplatePath(ProtocolTemplateMode mode);
    }
}
