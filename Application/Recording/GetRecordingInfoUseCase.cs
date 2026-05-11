using System;
using System.Collections.Generic;
using System.Linq;
using JSQViewer.Application.Charting;
using JSQViewer.Application.Workspace.Ports;
using JSQViewer.Core;

namespace JSQViewer.Application.Recording
{
    public sealed class GetRecordingInfoUseCase
    {
        private readonly TimestampRangeService _timestampRangeService;
        private readonly ITestMetadataReader _metadataReader;

        public GetRecordingInfoUseCase(
            TimestampRangeService timestampRangeService,
            ITestMetadataReader metadataReader = null)
        {
            if (timestampRangeService == null)
                throw new ArgumentNullException(nameof(timestampRangeService));
            _timestampRangeService = timestampRangeService;
            _metadataReader = metadataReader;
        }

        public RecordingInfoResult Execute(TestData data, string sourceRoot)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));
            if (sourceRoot == null) throw new ArgumentNullException(nameof(sourceRoot));

            // Читаем метаданные напрямую из .dat-файла источника, если reader доступен.
            // data.Meta — слитый словарь всех источников (первый выигрывает), поэтому
            // при N>=2 записях он содержит не те данные для источника 2, 3, ...
            IReadOnlyList<KeyValuePair<string, string>> meta;
            if (_metadataReader != null)
            {
                try
                {
                    meta = _metadataReader.Read(sourceRoot).ToList();
                }
                catch
                {
                    meta = data.Meta != null
                        ? data.Meta.ToList()
                        : new List<KeyValuePair<string, string>>();
                }
            }
            else
            {
                meta = data.Meta != null
                    ? data.Meta.ToList()
                    : new List<KeyValuePair<string, string>>();
            }

            var result = new RecordingInfoResult
            {
                SourceRoot = sourceRoot,
                Meta = meta
            };

            string t1Column = FindT1Column(data, sourceRoot);
            if (t1Column == null)
                return result;

            double?[] values;
            if (!data.Columns.TryGetValue(t1Column, out values) || values == null)
                return result;

            long startMs, endMs;
            if (!data.SourceStartMs.TryGetValue(sourceRoot, out startMs))
                startMs = data.TimestampsMs.Length > 0 ? data.TimestampsMs[0] : 0;
            if (!data.SourceEndMs.TryGetValue(sourceRoot, out endMs))
                endMs = data.TimestampsMs.Length > 0 ? data.TimestampsMs[data.TimestampsMs.Length - 1] : 0;

            var slice = _timestampRangeService.SliceByTime(data.TimestampsMs, startMs, endMs);
            int i0 = slice.Item1;
            int i1 = slice.Item2;
            if (i1 <= i0)
                return result;

            int minIdx = -1;
            double minVal = double.MaxValue;
            double? firstVal = null;
            long firstValMs = -1; // timestamp первого ненулевого значения T1
            for (int i = i0; i < i1; i++)
            {
                if (!values[i].HasValue) continue;
                if (firstVal == null)
                {
                    firstVal = values[i];
                    firstValMs = data.TimestampsMs[i];
                }
                if (values[i].Value < minVal)
                {
                    minVal = values[i].Value;
                    minIdx = i;
                }
            }

            if (minIdx < 0) return result;

            result.T1Min = minVal;
            result.T1MinTime = _timestampRangeService.UnixMsToLocalDateTime(
                data.TimestampsMs[minIdx]);
            // Отсчёт от первого ненулевого T1 — совпадает с t=0 графика в режиме наложения
            result.T1MinElapsedMs = firstValMs >= 0
                ? data.TimestampsMs[minIdx] - firstValMs
                : data.TimestampsMs[minIdx] - startMs;

            // Скорость падения: от первого значения до минимума, за всё время источника
            double durationMin = (endMs - startMs) / 60_000.0;
            if (durationMin > 0 && firstVal.HasValue)
                result.T1DropRatePerMinute = (minVal - firstVal.Value) / durationMin;

            return result;
        }

        private static string FindT1Column(TestData data, string sourceRoot)
        {
            string[] cols;
            if (data.SourceColumns.TryGetValue(sourceRoot, out cols) && cols != null)
            {
                string found = FindT1InArray(cols);
                if (found != null) return found;
            }

            // Fallback: search global column list
            if (data.ColumnNames != null)
            {
                string found = FindT1InArray(data.ColumnNames);
                if (found != null) return found;
            }

            return null;
        }

        private static string FindT1InArray(string[] cols)
        {
            foreach (string col in cols)
            {
                if (IsT1ColumnName(col)) return col;
            }
            return null;
        }

        // Определяет, является ли имя колонки "T1" в любом из возможных форматов:
        //   "T1"                  — одиночный источник, прямое имя
        //   "C-T1"                — одиночный источник, однобуквенный префикс (C.T1 → C-T1)
        //   "T1#2"                — слияние без split, суффикс дубликата
        //   "C-T1#2"              — слияние без split, суффикс дубликата с префиксом
        //   "foldername::T1"      — слияние с split, полное имя папки как префикс
        //   "foldername::C-T1"    — слияние с split + однобуквенный префикс
        private static bool IsT1ColumnName(string col)
        {
            if (string.IsNullOrEmpty(col)) return false;

            // Снимаем prefix "basename::" (BuildSourceTags использует имя папки + "::")
            string name = col;
            int sep = col.IndexOf("::", StringComparison.Ordinal);
            if (sep >= 0)
                name = col.Substring(sep + 2);

            // Снимаем суффикс дубликата "#N"
            int hash = name.LastIndexOf('#');
            if (hash > 0)
            {
                string hashPart = name.Substring(hash + 1);
                bool allDigits = hashPart.Length > 0;
                foreach (char c in hashPart)
                {
                    if (!char.IsDigit(c)) { allDigits = false; break; }
                }
                if (allDigits)
                    name = name.Substring(0, hash);
            }

            // Прямое совпадение: "T1"
            if (string.Equals(name, "T1", StringComparison.OrdinalIgnoreCase))
                return true;

            // Однобуквенный префикс: "C-T1", "A-T1", "B-T1" и т.д.
            if (name.Length >= 4 && name[1] == '-' &&
                string.Equals(name.Substring(2), "T1", StringComparison.OrdinalIgnoreCase))
                return true;

            return false;
        }
    }
}
