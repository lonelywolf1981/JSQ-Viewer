using System.Collections.Generic;
using JSQViewer.Core;
using JSQViewer.Settings;

namespace JSQViewer.Application.Exporting
{
    public sealed class ExportTemplateRequest
    {
        public ProtocolTemplateMode TemplateMode { get; set; }

        public string LoadedFolder { get; set; }

        public TestData Data { get; set; }

        public IReadOnlyList<string> SelectedChannels { get; set; }

        public bool IncludeExtra { get; set; }

        public string Refrigerant { get; set; }

        public ViewerSettingsModel ViewerSettings { get; set; }

        public long? RangeStartMs { get; set; }

        public long? RangeEndMs { get; set; }
    }
}
