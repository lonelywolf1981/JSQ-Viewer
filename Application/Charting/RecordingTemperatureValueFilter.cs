namespace JSQViewer.Application.Charting
{
    internal static class RecordingTemperatureValueFilter
    {
        public static bool IsTemperatureChannel(string channelCode)
        {
            int number;
            return T8PlusChannelSelector.TryGetChannelNumber(channelCode, out number);
        }

        public static bool IsValidTemperature(double value)
        {
            return value > -90.0;
        }
    }
}
