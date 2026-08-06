using System;

namespace JSQViewer.Application.Database
{
    public enum ClimateModeSource
    {
        Unknown,
        FromRecord,
        FromChannels
    }

    public sealed class ClimateModeInfo
    {
        public static readonly ClimateModeInfo Unknown =
            new ClimateModeInfo(string.Empty, ClimateModeSource.Unknown, null, null);

        public ClimateModeInfo(
            string label,
            ClimateModeSource source,
            double? temperatureCelsius,
            double? humidityPercent)
        {
            Label = label ?? string.Empty;
            Source = source;
            TemperatureCelsius = temperatureCelsius;
            HumidityPercent = humidityPercent;
        }

        public string Label { get; private set; }

        public ClimateModeSource Source { get; private set; }

        public double? TemperatureCelsius { get; private set; }

        public double? HumidityPercent { get; private set; }

        public bool IsKnown
        {
            get { return Source != ClimateModeSource.Unknown && Label.Length > 0; }
        }
    }
}
