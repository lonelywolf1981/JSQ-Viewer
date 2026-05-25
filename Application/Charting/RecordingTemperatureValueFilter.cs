using System;

namespace JSQViewer.Application.Charting
{
    internal static class RecordingTemperatureValueFilter
    {
        public static bool IsTemperatureChannel(string channelCode)
        {
            int number;
            return TryGetTemperatureChannelNumber(channelCode, out number);
        }

        public static bool IsValidTemperature(double value)
        {
            return value > -90.0;
        }

        private static bool TryGetTemperatureChannelNumber(string channelCode, out int number)
        {
            number = 0;
            if (string.IsNullOrEmpty(channelCode))
            {
                return false;
            }

            string name = NormalizeChannelName(channelCode);
            if (name.Length < 2 || (name[0] != 'T' && name[0] != 't'))
            {
                return false;
            }

            string digits = name.Substring(1);
            if (digits.Length == 0)
            {
                return false;
            }

            for (int i = 0; i < digits.Length; i++)
            {
                if (!char.IsDigit(digits[i]))
                {
                    return false;
                }
            }

            return int.TryParse(digits, out number);
        }

        private static string NormalizeChannelName(string channelCode)
        {
            string name = channelCode.Trim();
            int separator = name.LastIndexOf("::", StringComparison.Ordinal);
            if (separator >= 0)
            {
                name = name.Substring(separator + 2);
            }

            int hash = name.LastIndexOf('#');
            if (hash > 0)
            {
                string hashPart = name.Substring(hash + 1);
                bool allDigits = hashPart.Length > 0;
                for (int i = 0; i < hashPart.Length; i++)
                {
                    if (!char.IsDigit(hashPart[i]))
                    {
                        allDigits = false;
                        break;
                    }
                }

                if (allDigits)
                {
                    name = name.Substring(0, hash);
                }
            }

            if (name.Length >= 3 && name[1] == '-')
            {
                name = name.Substring(2);
            }

            return name;
        }
    }
}
