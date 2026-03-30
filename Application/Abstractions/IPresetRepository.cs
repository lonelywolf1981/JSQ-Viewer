using System.Collections.Generic;
using JSQViewer.Settings;

namespace JSQViewer.Application.Abstractions
{
    public interface IPresetRepository
    {
        List<ViewerPreset> List();

        ViewerPreset Load(string keyOrName);

        bool Exists(string keyOrName);

        ViewerPreset Save(ViewerPreset preset);

        bool Delete(string keyOrName);
    }
}
