namespace JSQViewer.Application.Exporting
{
    public sealed class ExportTemplateResult
    {
        public byte[] Payload { get; set; }

        public TemplateValidationResult Validation { get; set; }
    }
}
