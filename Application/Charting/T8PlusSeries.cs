namespace JSQViewer.Application.Charting
{
    /// <summary>
    /// Три временных ряда по массиву термопар источника, выровненные
    /// по <see cref="JSQViewer.Core.TestData.TimestampsMs"/>.
    /// </summary>
    public sealed class T8PlusSeries
    {
        public static readonly T8PlusSeries Empty = new T8PlusSeries(false, new double?[0], new double?[0], new double?[0]);

        public T8PlusSeries(bool hasChannels, double?[] minimum, double?[] average, double?[] maximum)
        {
            HasChannels = hasChannels;
            Minimum = minimum ?? new double?[0];
            Average = average ?? new double?[0];
            Maximum = maximum ?? new double?[0];
        }

        public bool HasChannels { get; private set; }

        public double?[] Minimum { get; private set; }

        public double?[] Average { get; private set; }

        public double?[] Maximum { get; private set; }
    }
}
