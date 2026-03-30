using System;
using JSQViewer.Application.Exporting;
using JSQViewer.Application.Exporting.Ports;
using JSQViewer.Export;

namespace JSQViewer.Infrastructure.Exporting
{
    public sealed class OpenXmlTemplateExporter : ITemplateExporter
    {
        public byte[] Export(ExportTemplateRequest request)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));

            return TemplateExporter.Export(
                request.TemplatePath,
                request.LoadedFolder,
                request.Data,
                request.SelectedChannels == null ? null : new System.Collections.Generic.List<string>(request.SelectedChannels),
                request.IncludeExtra,
                request.Refrigerant,
                request.ViewerSettings,
                request.RangeStartMs,
                request.RangeEndMs);
        }
    }
}
