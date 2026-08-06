using System;

namespace JSQViewer.Application.Database
{
    public sealed class ClimateModeResolver
    {
        public const double ToleranceCelsius = 3.0;

        private static readonly string[] ModeIds = { "25_60", "32_65", "40_40" };
        private static readonly double[] ModeTemperatures = { 25.0, 32.0, 40.0 };

        public ClimateModeInfo Resolve(
            string climateModeId,
            double? temperatureCelsius,
            double? humidityPercent)
        {
            string recordLabel = TryGetLabel(climateModeId);
            if (recordLabel != null)
            {
                return new ClimateModeInfo(
                    recordLabel,
                    ClimateModeSource.FromRecord,
                    temperatureCelsius,
                    humidityPercent);
            }

            if (!temperatureCelsius.HasValue)
            {
                return ClimateModeInfo.Unknown;
            }

            int bestIndex = -1;
            double bestDistance = double.MaxValue;
            for (int i = 0; i < ModeTemperatures.Length; i++)
            {
                double distance = Math.Abs(ModeTemperatures[i] - temperatureCelsius.Value);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    bestIndex = i;
                }
            }

            if (bestIndex < 0 || bestDistance > ToleranceCelsius)
            {
                return ClimateModeInfo.Unknown;
            }

            return new ClimateModeInfo(
                ToLabel(ModeIds[bestIndex]),
                ClimateModeSource.FromChannels,
                temperatureCelsius,
                humidityPercent);
        }

        private static string TryGetLabel(string climateModeId)
        {
            if (string.IsNullOrWhiteSpace(climateModeId))
            {
                return null;
            }

            string id = climateModeId.Trim();
            for (int i = 0; i < ModeIds.Length; i++)
            {
                if (string.Equals(ModeIds[i], id, StringComparison.OrdinalIgnoreCase))
                {
                    return ToLabel(ModeIds[i]);
                }
            }

            return null;
        }

        private static string ToLabel(string modeId)
        {
            return modeId.Replace('_', '/');
        }
    }
}
