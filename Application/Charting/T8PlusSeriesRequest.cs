namespace JSQViewer.Application.Charting
{
    public sealed class T8PlusSeriesRequest
    {
        public T8PlusSeriesRequest(string sourceRoot, bool showMinimum, bool showAverage, bool showMaximum)
        {
            SourceRoot = sourceRoot ?? string.Empty;
            ShowMinimum = showMinimum;
            ShowAverage = showAverage;
            ShowMaximum = showMaximum;
        }

        public string SourceRoot { get; private set; }

        public bool ShowMinimum { get; private set; }

        public bool ShowAverage { get; private set; }

        public bool ShowMaximum { get; private set; }

        public bool HasAny
        {
            get { return ShowMinimum || ShowAverage || ShowMaximum; }
        }
    }
}
