using System;
using System.Collections.Generic;

namespace JSQViewer.Application.Recording
{
    public sealed class RecordingInfoResult
    {
        public string SourceRoot { get; set; }
        public DateTime? SourceStartTime { get; set; }

        // null если канал T1 не найден в данном источнике
        public double? T1InitialTemperature { get; set; }
        public double? T1Min { get; set; }
        public DateTime? T1MinTime { get; set; }
        // Прошедшее время от старта записи до достижения минимума T1 (в миллисекундах)
        public long? T1MinElapsedMs { get; set; }
        public double? T1FirstCoolingMin { get; set; }
        public DateTime? T1FirstCoolingMinTime { get; set; }
        public long? T1FirstCoolingMinElapsedMs { get; set; }
        public double? T1DropRatePerMinute { get; set; }
        public double? T1EnergyToTargetKWh { get; set; }
        public long? T1EnergyTargetElapsedMs { get; set; }

        public T8PlusTemperatureStats T8PlusStats { get; set; }

        // Все пары ключ-значение из .dat, в порядке перечисления Meta
        public IReadOnlyList<KeyValuePair<string, string>> Meta { get; set; }
    }

    public sealed class T8PlusTemperatureStats
    {
        public bool HasChannels { get; set; }

        public bool AverageReached { get; set; }
        public double? AverageValue { get; set; }
        public long? AverageElapsedMs { get; set; }
        public DateTime? AverageTime { get; set; }

        public bool MinimumReached { get; set; }
        public double? MinimumValue { get; set; }
        public long? MinimumElapsedMs { get; set; }
        public DateTime? MinimumTime { get; set; }

        public bool MaximumReached { get; set; }
        public double? MaximumValue { get; set; }
        public long? MaximumElapsedMs { get; set; }
        public DateTime? MaximumTime { get; set; }

        public double? AverageDropRatePerMinute { get; set; }
    }

    public sealed class T8PlusTemperatureThresholds
    {
        public const double DefaultAverageThreshold = 5.0;
        public const double DefaultMinimumThreshold = 1.0;
        public const double DefaultMaximumThreshold = 9.0;

        public static readonly T8PlusTemperatureThresholds Default =
            new T8PlusTemperatureThresholds(
                DefaultAverageThreshold,
                DefaultMinimumThreshold,
                DefaultMaximumThreshold);

        public T8PlusTemperatureThresholds(
            double averageThreshold,
            double minimumThreshold,
            double maximumThreshold)
        {
            AverageThreshold = averageThreshold;
            MinimumThreshold = minimumThreshold;
            MaximumThreshold = maximumThreshold;
        }

        public double AverageThreshold { get; private set; }
        public double MinimumThreshold { get; private set; }
        public double MaximumThreshold { get; private set; }
    }
}
