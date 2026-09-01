namespace JSQViewer.Application.Charting
{
    /// <summary>
    /// Горизонтальная опорная линия на графике: значение статистики T8+
    /// на момент последней отображаемой точки одного источника.
    /// </summary>
    public sealed class ChartLevelLine
    {
        public ChartLevelLine(string sourceRoot, int sourceIndex, ChartSeriesRole role, double value, string label)
        {
            SourceRoot = sourceRoot ?? string.Empty;
            SourceIndex = sourceIndex;
            Role = role;
            Value = value;
            Label = label ?? string.Empty;
        }

        public string SourceRoot { get; private set; }

        public int SourceIndex { get; private set; }

        public ChartSeriesRole Role { get; private set; }

        public double Value { get; private set; }

        public string Label { get; private set; }
    }
}
