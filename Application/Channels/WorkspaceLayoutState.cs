using System.Collections.Generic;

namespace JSQViewer.Application.Channels
{
    public sealed class WorkspaceLayoutState
    {
        public string MainSelectedOrderKey { get; set; }

        public Dictionary<string, string> SourceSelectedOrderKeys { get; set; }

        public WorkspaceLayoutState()
        {
            SourceSelectedOrderKeys = new Dictionary<string, string>();
        }
    }
}
