using System.Collections.Generic;
using JSQViewer.Core;

namespace JSQViewer.Application.Workspace.Ports
{
    public interface ICanaliDefinitionReader
    {
        Dictionary<string, ChannelInfo> Read(string root);
    }
}
