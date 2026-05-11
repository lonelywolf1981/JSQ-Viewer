using System;
using System.Collections.Generic;

namespace JSQViewer.Application.Recording
{
    public sealed class RecordingInfoResult
    {
        public string SourceRoot { get; set; }

        // null если канал T1 не найден в данном источнике
        public double? T1Min { get; set; }
        public DateTime? T1MinTime { get; set; }
        public double? T1DropRatePerMinute { get; set; }

        // Все пары ключ-значение из .dat, в порядке перечисления Meta
        public IReadOnlyList<KeyValuePair<string, string>> Meta { get; set; }
    }
}
