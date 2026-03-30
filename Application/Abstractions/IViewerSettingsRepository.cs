using JSQViewer.Settings;

namespace JSQViewer.Application.Abstractions
{
    public interface IViewerSettingsRepository
    {
        ViewerSettingsModel Load();

        bool Save(ViewerSettingsModel settings);
    }
}
