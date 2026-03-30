using System;
using JSQViewer.Application.Exporting;
using JSQViewer.Application.Exporting.Ports;
using JSQViewer.Export;
using AppTemplateValidationResult = JSQViewer.Application.Exporting.TemplateValidationResult;

namespace JSQViewer.Infrastructure.Exporting
{
    public sealed class OpenXmlTemplateExportValidator : ITemplateExportValidator
    {
        public AppTemplateValidationResult Validate(byte[] xlsxBytes)
        {
            Export.TemplateValidationResult result = TemplateExportValidator.Validate(xlsxBytes);
            return new AppTemplateValidationResult
            {
                Ok = result.Ok,
                Message = result.Message
            };
        }
    }
}
