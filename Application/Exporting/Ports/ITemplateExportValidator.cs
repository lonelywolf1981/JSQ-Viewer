namespace JSQViewer.Application.Exporting.Ports
{
    public interface ITemplateExportValidator
    {
        TemplateValidationResult Validate(byte[] xlsxBytes);
    }
}
