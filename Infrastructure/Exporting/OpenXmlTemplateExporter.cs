using System;
using JSQViewer.Application.Abstractions;
using JSQViewer.Application.Exporting;
using JSQViewer.Application.Exporting.Ports;
using JSQViewer.Export;

namespace JSQViewer.Infrastructure.Exporting
{
    public sealed class OpenXmlTemplateExporter : ITemplateExporter
    {
        private readonly IAppPaths _appPaths;

        public OpenXmlTemplateExporter(IAppPaths appPaths)
        {
            _appPaths = appPaths ?? throw new ArgumentNullException(nameof(appPaths));
        }

        public byte[] Export(ExportTemplateRequest request)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));

            return TemplateExporter.Export(
                _appPaths.GetProtocolTemplatePath(request.TemplateMode),
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
