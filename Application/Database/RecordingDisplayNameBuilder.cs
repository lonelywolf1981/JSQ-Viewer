using System;
using System.Collections.Generic;

namespace JSQViewer.Application.Database
{
    /// <summary>
    /// Composes the name a recording is shown under everywhere in the application: the workspace
    /// title, the channel and source windows, chart legends. The parts follow the naming order the
    /// laboratory already uses for measurement folders — name, equipment model, compressor,
    /// experiment type, climate mode — so a database recording reads like a folder does.
    /// </summary>
    public static class RecordingDisplayNameBuilder
    {
        public const string Separator = " · ";

        /// <summary>Marks a recording that is still running, so live data is recognisable at a glance.</summary>
        public const string ActiveMarker = "●";

        private const string StatusKey = "Статус";
        private const string ActiveStatus = "recording";

        private static readonly string[] AppendedKeys =
        {
            "Модель оборудования",
            "Компрессор",
            "Тип испытания",
            "Климатический режим"
        };

        public static string Build(IDictionary<string, string> metadata, string fallback)
        {
            string title = ReadValue(metadata, "Название");
            var parts = new List<string>();
            parts.Add(title.Length > 0 ? title : (fallback ?? string.Empty).Trim());

            for (int i = 0; i < AppendedKeys.Length; i++)
            {
                string key = AppendedKeys[i];
                string value = ReadValue(metadata, key);
                if (value.Length == 0)
                {
                    continue;
                }

                parts.Add(string.Equals(key, "Компрессор", StringComparison.Ordinal)
                    ? StripCompressorSuffix(value)
                    : value);
            }

            string name = string.Join(Separator, parts.ToArray());
            return IsActive(metadata) ? ActiveMarker + " " + name : name;
        }

        private static bool IsActive(IDictionary<string, string> metadata)
        {
            return string.Equals(ReadValue(metadata, StatusKey), ActiveStatus, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Drops the voltage and frequency suffix the database carries on compressor models
        /// ("NPT14RA (220-240V/50Hz)" -> "NPT14RA"). The full value stays in the metadata panel.
        /// </summary>
        private static string StripCompressorSuffix(string compressor)
        {
            int suffixStart = compressor.IndexOf('(');
            if (suffixStart <= 0)
            {
                return compressor;
            }

            string stripped = compressor.Substring(0, suffixStart).Trim();
            return stripped.Length == 0 ? compressor : stripped;
        }

        private static string ReadValue(IDictionary<string, string> metadata, string key)
        {
            string value;
            if (metadata == null || !metadata.TryGetValue(key, out value) || value == null)
            {
                return string.Empty;
            }

            return value.Trim();
        }
    }
}
