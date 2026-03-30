using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using JSQViewer.Application.Workspace.Ports;
using JSQViewer.Core;

namespace JSQViewer.Infrastructure.DataImport
{
    public sealed class DbfTestDataSourceReader : ITestDataSourceReader
    {
        private static readonly Regex ProvaDbfRegex = new Regex(@"Prova(\d+)\.dbf$", RegexOptions.IgnoreCase);
        private static readonly string[] TimeColumns = { "Data", "Ore", "Minuti", "Secondi", "mSecondi" };

        public TestData Read(string root, Dictionary<string, ChannelInfo> channels, Dictionary<string, string> metadata)
        {
            if (string.IsNullOrWhiteSpace(root))
            {
                throw new ArgumentException("Root folder is required.", nameof(root));
            }

            string[] dbfFiles = Directory.GetFiles(root, "Prova*.dbf", SearchOption.TopDirectoryOnly)
                .Where(path => ProvaDbfRegex.IsMatch(Path.GetFileName(path)))
                .OrderBy(path => DbfSortKey(path))
                .ToArray();

            if (dbfFiles.Length == 0)
            {
                throw new FileNotFoundException("No Prova*.dbf files found in test folder.");
            }

            DbfHeader[] headers = new DbfHeader[dbfFiles.Length];
            int totalRecords = 0;
            var columnSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var timeColumnSet = new HashSet<string>(TimeColumns, StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < dbfFiles.Length; i++)
            {
                headers[i] = DbfReader.ReadHeaderFromFile(dbfFiles[i]);
                totalRecords += headers[i].Records;
                for (int fieldIndex = 0; fieldIndex < headers[i].Fields.Count; fieldIndex++)
                {
                    string name = headers[i].Fields[fieldIndex].Name;
                    if (!timeColumnSet.Contains(name))
                    {
                        columnSet.Add(name);
                    }
                }
            }

            string[] columnNames = columnSet.OrderBy(name => name, StringComparer.OrdinalIgnoreCase).ToArray();
            long[] timestamps = new long[totalRecords];
            var columns = new Dictionary<string, double?[]>(StringComparer.OrdinalIgnoreCase);
            foreach (string column in columnNames)
            {
                columns[column] = new double?[totalRecords];
            }

            int[] fileCounts = new int[dbfFiles.Length];
            int[] fileOffsets = new int[dbfFiles.Length];
            int offset = 0;
            for (int i = 0; i < dbfFiles.Length; i++)
            {
                fileOffsets[i] = offset;
                offset += headers[i].Records;
            }

            Parallel.For(0, dbfFiles.Length, i =>
            {
                int[] timeFieldIndices = BuildTimeFieldIndices(headers[i]);
                fileCounts[i] = DbfReader.ReadAllRecordsDirect(dbfFiles[i], headers[i], timestamps, columns, columnNames, fileOffsets[i], timeFieldIndices);
            });

            int totalValid = 0;
            for (int i = 0; i < fileCounts.Length; i++)
            {
                totalValid += fileCounts[i];
            }

            long[] compactTimestamps = new long[totalValid];
            var compactColumns = new Dictionary<string, double?[]>(StringComparer.OrdinalIgnoreCase);
            foreach (string column in columnNames)
            {
                compactColumns[column] = new double?[totalValid];
            }

            int writePosition = 0;
            for (int i = 0; i < dbfFiles.Length; i++)
            {
                int sourceStart = fileOffsets[i];
                int count = fileCounts[i];
                if (count == 0)
                {
                    continue;
                }

                Array.Copy(timestamps, sourceStart, compactTimestamps, writePosition, count);
                foreach (string column in columnNames)
                {
                    Array.Copy(columns[column], sourceStart, compactColumns[column], writePosition, count);
                }
                writePosition += count;
            }

            int[] sortIndices = new int[totalValid];
            for (int i = 0; i < totalValid; i++)
            {
                sortIndices[i] = i;
            }

            Array.Sort(sortIndices, (left, right) => compactTimestamps[left].CompareTo(compactTimestamps[right]));

            long[] sortedTimestamps = new long[totalValid];
            var sortedColumns = new Dictionary<string, double?[]>(StringComparer.OrdinalIgnoreCase);
            foreach (string column in columnNames)
            {
                sortedColumns[column] = new double?[totalValid];
            }

            for (int i = 0; i < totalValid; i++)
            {
                int sourceIndex = sortIndices[i];
                sortedTimestamps[i] = compactTimestamps[sourceIndex];
            }

            foreach (string column in columnNames)
            {
                double?[] sourceArray = compactColumns[column];
                double?[] destinationArray = sortedColumns[column];
                for (int i = 0; i < totalValid; i++)
                {
                    destinationArray[i] = sourceArray[sortIndices[i]];
                }
            }

            return new TestData
            {
                Root = root,
                Meta = metadata ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
                Channels = channels ?? new Dictionary<string, ChannelInfo>(StringComparer.OrdinalIgnoreCase),
                CodeSources = columnNames.ToDictionary(column => column, column => root, StringComparer.OrdinalIgnoreCase),
                SourceStartMs = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase)
                {
                    [root] = totalValid > 0 ? sortedTimestamps[0] : 0L
                },
                SourceEndMs = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase)
                {
                    [root] = totalValid > 0 ? sortedTimestamps[totalValid - 1] : 0L
                },
                TimestampsMs = sortedTimestamps,
                Columns = sortedColumns,
                ColumnNames = columnNames,
                SourceColumns = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
                {
                    [root] = columnNames.ToArray()
                },
                RowCount = totalValid
            };
        }

        private static int[] BuildTimeFieldIndices(DbfHeader header)
        {
            int[] indices = { -1, -1, -1, -1, -1 };
            for (int i = 0; i < header.Fields.Count; i++)
            {
                string name = header.Fields[i].Name;
                if (string.Equals(name, "Data", StringComparison.OrdinalIgnoreCase)) indices[0] = i;
                else if (string.Equals(name, "Ore", StringComparison.OrdinalIgnoreCase)) indices[1] = i;
                else if (string.Equals(name, "Minuti", StringComparison.OrdinalIgnoreCase)) indices[2] = i;
                else if (string.Equals(name, "Secondi", StringComparison.OrdinalIgnoreCase)) indices[3] = i;
                else if (string.Equals(name, "mSecondi", StringComparison.OrdinalIgnoreCase)) indices[4] = i;
            }

            return indices;
        }

        private static int DbfSortKey(string path)
        {
            Match match = ProvaDbfRegex.Match(Path.GetFileName(path));
            int key;
            if (match.Success && int.TryParse(match.Groups[1].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out key))
            {
                return key;
            }

            return 0;
        }
    }
}
