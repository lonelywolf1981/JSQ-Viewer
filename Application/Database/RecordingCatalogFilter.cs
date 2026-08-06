using System;

namespace JSQViewer.Application.Database
{
    public sealed class RecordingCatalogFilter
    {
        public RecordingCatalogFilter()
        {
            Limit = 500;
        }

        public string PostId { get; set; }
        public DateTime? From { get; set; }
        public DateTime? To { get; set; }
        public string ExperimentType { get; set; }
        public string TitleContains { get; set; }
        public int Limit { get; set; }
    }
}
