namespace JSQViewer.Application.Exporting.Ports
{
    public interface ITemplateExporter
    {
        byte[] Export(ExportTemplateRequest request);
    }
}
