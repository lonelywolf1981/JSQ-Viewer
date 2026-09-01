namespace JSQViewer.Application.Charting
{
    /// <summary>
    /// Поиск последнего отсчёта, попадающего в видимый участок графика.
    /// Отдельный класс, потому что правый край задаётся тремя разными путями
    /// (ползунок диапазона, ручные границы оси, конец данных), а правило выбора
    /// отсчёта у них общее.
    /// </summary>
    public static class VisibleRangeEdgeResolver
    {
        public static int ResolveIndex(long[] timestampsMs, long edgeMs)
        {
            if (timestampsMs == null || timestampsMs.Length == 0)
            {
                return -1;
            }

            int low = 0;
            int high = timestampsMs.Length - 1;
            int result = -1;

            while (low <= high)
            {
                int middle = low + ((high - low) / 2);
                if (timestampsMs[middle] <= edgeMs)
                {
                    result = middle;
                    low = middle + 1;
                }
                else
                {
                    high = middle - 1;
                }
            }

            return result;
        }
    }
}
