using System;

namespace JSQViewer.Application.Database
{
    public sealed class RecordingSummaryItem
    {
        public string Id { get; set; }
        public string PostId { get; set; }
        public string Title { get; set; }
        public string Status { get; set; }
        public DateTime? StartedAt { get; set; }
        public DateTime? StoppedAt { get; set; }
        public string EquipmentModel { get; set; }
        public string ExperimentType { get; set; }

        public bool IsActive
        {
            get { return string.Equals(Status, "recording", StringComparison.OrdinalIgnoreCase); }
        }

        public double DurationHours
        {
            get
            {
                if (!StartedAt.HasValue)
                {
                    return 0.0;
                }

                DateTime end = StoppedAt.HasValue ? StoppedAt.Value : DateTime.Now;
                double hours = (end - StartedAt.Value).TotalHours;
                return hours < 0.0 ? 0.0 : hours;
            }
        }

        public string ToSourceString()
        {
            return RecordingSourceRef.Build(Id);
        }
    }
}
