namespace JSQViewer.Application.Abstractions
{
    public interface IAppPaths
    {
        string ApplicationBaseDirectory { get; }

        string ProjectRoot { get; }

        string LogDirectory { get; }
    }
}
