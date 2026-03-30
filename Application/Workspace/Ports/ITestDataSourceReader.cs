using System.Collections.Generic;
using JSQViewer.Core;

namespace JSQViewer.Application.Workspace.Ports
{
    public interface ITestDataSourceReader
    {
        TestData Read(string root, Dictionary<string, ChannelInfo> channels, Dictionary<string, string> metadata);
    }
}
