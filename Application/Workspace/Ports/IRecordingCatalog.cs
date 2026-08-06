using System.Collections.Generic;
using JSQViewer.Application.Database;

namespace JSQViewer.Application.Workspace.Ports
{
    public interface IRecordingCatalog
    {
        IList<RecordingSummaryItem> List(RecordingCatalogFilter filter);

        IList<string> ListPosts();

        IList<string> ListExperimentTypes();

        string GetStatus(string recordingId);
    }
}
