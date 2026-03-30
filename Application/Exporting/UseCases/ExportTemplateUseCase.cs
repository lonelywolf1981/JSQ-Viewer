using System;
using JSQViewer.Application.Exporting.Ports;

namespace JSQViewer.Application.Exporting
{
    public sealed class ExportTemplateUseCase
    {
        private readonly ITemplateExporter _templateExporter;
        private readonly ITemplateExportValidator _templateExportValidator;

        public ExportTemplateUseCase(ITemplateExporter templateExporter, ITemplateExportValidator templateExportValidator)
        {
            _templateExporter = templateExporter ?? throw new ArgumentNullException(nameof(templateExporter));
            _templateExportValidator = templateExportValidator ?? throw new ArgumentNullException(nameof(templateExportValidator));
        }

        public ExportTemplateResult Execute(ExportTemplateRequest request)
        {
            byte[] payload = _templateExporter.Export(request);
            return new ExportTemplateResult
            {
                Payload = payload,
                Validation = _templateExportValidator.Validate(payload)
            };
        }
    }
}
