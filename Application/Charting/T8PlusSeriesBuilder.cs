using System.Collections.Generic;
using JSQViewer.Core;

namespace JSQViewer.Application.Charting
{
    /// <summary>
    /// Считает минимум, среднее и максимум по массиву термопар источника
    /// на каждом отсчёте времени. Без состояния: кэширование — забота вызывающего.
    /// </summary>
    public sealed class T8PlusSeriesBuilder
    {
        public T8PlusSeries Build(TestData data, string sourceRoot)
        {
            if (data == null || data.TimestampsMs == null || data.TimestampsMs.Length == 0)
            {
                return T8PlusSeries.Empty;
            }

            List<string> columns = T8PlusChannelSelector.SelectColumns(
                data, sourceRoot, T8PlusChannelSelector.DefaultMinimumNumber);
            if (columns.Count == 0)
            {
                return T8PlusSeries.Empty;
            }

            var values = new List<double?[]>(columns.Count);
            var codes = new List<string>(columns.Count);
            for (int c = 0; c < columns.Count; c++)
            {
                double?[] column;
                if (data.Columns != null && data.Columns.TryGetValue(columns[c], out column) && column != null)
                {
                    values.Add(column);
                    codes.Add(columns[c]);
                }
            }

            if (values.Count == 0)
            {
                return T8PlusSeries.Empty;
            }

            int length = data.TimestampsMs.Length;
            var minimum = new double?[length];
            var average = new double?[length];
            var maximum = new double?[length];
            var minimumChannel = new string[length];
            var maximumChannel = new string[length];

            for (int i = 0; i < length; i++)
            {
                double sum = 0d;
                double min = 0d;
                double max = 0d;
                int count = 0;
                int minIndex = -1;
                int maxIndex = -1;

                for (int c = 0; c < values.Count; c++)
                {
                    double?[] column = values[c];
                    if (i >= column.Length || !column[i].HasValue)
                    {
                        continue;
                    }

                    double value = column[i].Value;
                    if (!RecordingTemperatureValueFilter.IsValidTemperature(value))
                    {
                        continue;
                    }

                    if (count == 0 || value < min)
                    {
                        min = value;
                        minIndex = c;
                    }

                    if (count == 0 || value > max)
                    {
                        max = value;
                        maxIndex = c;
                    }

                    sum += value;
                    count++;
                }

                if (count == 0)
                {
                    continue;
                }

                minimum[i] = min;
                average[i] = sum / count;
                maximum[i] = max;
                minimumChannel[i] = codes[minIndex];
                maximumChannel[i] = codes[maxIndex];
            }

            return new T8PlusSeries(
                true, minimum, average, maximum, minimumChannel, maximumChannel, values.Count);
        }
    }
}
