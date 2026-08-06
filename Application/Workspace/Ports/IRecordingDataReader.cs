using JSQViewer.Core;

namespace JSQViewer.Application.Workspace.Ports
{
    public interface IRecordingDataReader
    {
        TestData ReadRecording(string recordingId);

        TestData AppendNewWindows(TestData existing, string recordingId);
    }
}
