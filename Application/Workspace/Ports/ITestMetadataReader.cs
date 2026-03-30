using System.Collections.Generic;

namespace JSQViewer.Application.Workspace.Ports
{
    public interface ITestMetadataReader
    {
        Dictionary<string, string> Read(string root);
    }
}
