using System;
using System.Collections.Generic;
using JSQViewer.Core;

namespace JSQViewer.Application.Charting
{
    /// <summary>
    /// Отбор каналов массива термопар источника и разбор имён T-каналов.
    /// Единственное место в приложении, где имя канала разбирается на префикс,
    /// суффикс источника и номер: до выделения этого класса разбор существовал
    /// в двух дословных копиях.
    /// </summary>
    public static class T8PlusChannelSelector
    {
        public const int DefaultMinimumNumber = 8;

        public static List<string> SelectColumns(TestData data, string sourceRoot, int minimumNumber)
        {
            var result = new List<string>();
            if (data == null)
            {
                return result;
            }

            string[] columns;
            if (data.SourceColumns != null
                && sourceRoot != null
                && data.SourceColumns.TryGetValue(sourceRoot, out columns)
                && columns != null)
            {
                AddColumns(result, columns, minimumNumber);
                return result;
            }

            if (data.ColumnNames != null && (data.SourceColumns == null || data.SourceColumns.Count <= 1))
            {
                AddColumns(result, data.ColumnNames, minimumNumber);
            }

            return result;
        }

        public static bool TryGetChannelNumber(string columnName, out int number)
        {
            number = 0;
            if (string.IsNullOrEmpty(columnName))
            {
                return false;
            }

            string name = NormalizeChannelName(columnName);
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

        public static string NormalizeChannelName(string columnName)
        {
            if (columnName == null)
            {
                return string.Empty;
            }

            string name = columnName.Trim();
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

        private static void AddColumns(List<string> result, string[] columns, int minimumNumber)
        {
            for (int i = 0; i < columns.Length; i++)
            {
                int number;
                if (TryGetChannelNumber(columns[i], out number) && number >= minimumNumber)
                {
                    result.Add(columns[i]);
                }
            }
        }
    }
}
