using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace JSQViewer.Core
{
    public sealed class DbfField
    {
        public string Name { get; set; }
        public char FieldType { get; set; }
        public int Length { get; set; }
        public int Decimals { get; set; }
    }

    public sealed class DbfHeader
    {
        public int Version { get; set; }
        public int Records { get; set; }
        public int HeaderLength { get; set; }
        public int RecordLength { get; set; }
        public List<DbfField> Fields { get; set; }

        public DbfHeader()
        {
            Fields = new List<DbfField>();
        }
    }

    public static class DbfReader
    {
        public static DbfHeader ReadHeader(byte[] headerBuffer)
        {
            if (headerBuffer == null || headerBuffer.Length < 32)
            {
                throw new InvalidDataException("DBF header is too short.");
            }

            var header = new DbfHeader();
            header.Version = headerBuffer[0];
            header.Records = BitConverter.ToInt32(headerBuffer, 4);
            if (header.Records < 0)
            {
                throw new InvalidDataException("DBF header contains negative record count.");
            }
            header.HeaderLength = BitConverter.ToUInt16(headerBuffer, 8);
            header.RecordLength = BitConverter.ToUInt16(headerBuffer, 10);

            int offset = 32;
            while (offset + 32 <= header.HeaderLength && offset + 32 <= headerBuffer.Length)
            {
                byte marker = headerBuffer[offset];
                if (marker == 0x0D)
                {
                    break;
                }

                string fieldName = ReadAsciiTerminated(headerBuffer, offset, 11);
                char fieldType = (char)headerBuffer[offset + 11];
                int fieldLength = headerBuffer[offset + 16];
                int decimals = headerBuffer[offset + 17];

                header.Fields.Add(new DbfField
                {
                    Name = fieldName,
                    FieldType = fieldType,
                    Length = fieldLength,
                    Decimals = decimals
                });

                offset += 32;
            }

            return header;
        }

        public static IEnumerable<Dictionary<string, object>> IterateRows(string dbfPath)
        {
            using (var fs = new FileStream(dbfPath, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                byte[] head32 = new byte[32];
                int read32 = fs.Read(head32, 0, 32);
                if (read32 < 32)
                {
                    throw new InvalidDataException("DBF file is too short.");
                }

                int headerLength = BitConverter.ToUInt16(head32, 8);
                if (headerLength < 32)
                {
                    throw new InvalidDataException("Invalid DBF header length.");
                }

                fs.Position = 0;
                byte[] headerBuffer = new byte[headerLength];
                int readHeader = fs.Read(headerBuffer, 0, headerLength);
                if (readHeader < headerLength)
                {
                    throw new InvalidDataException("DBF header cannot be read fully.");
                }

                DbfHeader header = ReadHeader(headerBuffer);
                fs.Position = header.HeaderLength;

                byte[] recordBuffer = new byte[header.RecordLength];
                for (int i = 0; i < header.Records; i++)
                {
                    int recRead = fs.Read(recordBuffer, 0, header.RecordLength);
                    if (recRead < header.RecordLength)
                    {
                        yield break;
                    }

                    if (recordBuffer[0] == (byte)'*')
                    {
                        continue;
                    }

                    var row = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
                    int off = 1;
                    for (int f = 0; f < header.Fields.Count; f++)
                    {
                        DbfField field = header.Fields[f];
                        object parsed = ParseDbfValue(recordBuffer, off, field);
                        row[field.Name] = parsed;
                        off += field.Length;
                    }

                    yield return row;
                }
            }
        }

        private static object ParseDbfValue(byte[] record, int offset, DbfField field)
        {
            string s = Encoding.ASCII.GetString(record, offset, field.Length).Trim();
            if (field.FieldType == 'N')
            {
                if (s.Length == 0 || s == ".")
                {
                    return null;
                }

                double num;
                if (double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out num))
                {
                    return num;
                }

                return null;
            }

            if (field.FieldType == 'D')
            {
                if (s.Length != 8)
                {
                    return null;
                }

                DateTime date;
                if (DateTime.TryParseExact(s, "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out date))
                {
                    return date.Date;
                }

                return null;
            }

            return s.Length == 0 ? null : (object)s;
        }

        /// <summary>
        /// Reads only the header from a DBF file without reading any records.
        /// </summary>
        public static DbfHeader ReadHeaderFromFile(string dbfPath)
        {
            using (var fs = new FileStream(dbfPath, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                byte[] head32 = new byte[32];
                if (fs.Read(head32, 0, 32) < 32) throw new InvalidDataException("DBF file is too short.");
                int headerLength = BitConverter.ToUInt16(head32, 8);
                if (headerLength < 32) throw new InvalidDataException("Invalid DBF header length.");
                fs.Position = 0;
                byte[] headerBuffer = new byte[headerLength];
                if (fs.Read(headerBuffer, 0, headerLength) < headerLength) throw new InvalidDataException("DBF header cannot be read fully.");
                return ReadHeader(headerBuffer);
            }
        }

        /// <summary>
        /// Reads all records from a DBF file directly into pre-allocated arrays.
        /// Returns the number of valid (non-deleted) rows written.
        /// </summary>
        /// <param name="dbfPath">Path to the DBF file</param>
        /// <param name="header">Pre-read header</param>
        /// <param name="timestamps">Output timestamp array (segment starting at writeOffset)</param>
        /// <param name="columns">Output column arrays (segment starting at writeOffset)</param>
        /// <param name="columnNames">Column names matching the columns dictionary keys</param>
        /// <param name="writeOffset">Starting index to write into the arrays</param>
        /// <param name="timeFieldIndices">Indices of time-related fields (Data, Ore, Minuti, Secondi, mSecondi) in header.Fields; -1 if not found</param>
        /// <returns>Number of valid rows written</returns>
        public static int ReadAllRecordsDirect(
            string dbfPath,
            DbfHeader header,
            long[] timestamps,
            Dictionary<string, double?[]> columns,
            string[] columnNames,
            int writeOffset,
            int[] timeFieldIndices)
        {
            int written = 0;
            using (var fs = new FileStream(dbfPath, FileMode.Open, FileAccess.Read, FileShare.Read, 65536))
            {
                fs.Position = header.HeaderLength;

                // Pre-compute field offsets for fast access
                int fieldCount = header.Fields.Count;
                int[] fieldOffsets = new int[fieldCount];
                int off = 1; // skip delete flag byte
                for (int f = 0; f < fieldCount; f++)
                {
                    fieldOffsets[f] = off;
                    off += header.Fields[f].Length;
                }

                int dataFieldIdx = timeFieldIndices[0];
                int oreFieldIdx = timeFieldIndices[1];
                int minutiFieldIdx = timeFieldIndices[2];
                int secondiFieldIdx = timeFieldIndices[3];
                int mSecondiFieldIdx = timeFieldIndices[4];

                // Pre-build column-to-field mapping (avoid FindFieldIndex per record)
                int colCount = columnNames.Length;
                int[] colFieldIdx = new int[colCount];
                int[] colFieldLen = new int[colCount];
                double?[][] colArrays = new double?[colCount][];
                for (int c = 0; c < colCount; c++)
                {
                    int fi = FindFieldIndex(header.Fields, columnNames[c]);
                    colFieldIdx[c] = fi;
                    colFieldLen[c] = fi >= 0 ? header.Fields[fi].Length : 0;
                    columns.TryGetValue(columnNames[c], out colArrays[c]);
                }

                byte[] bulkBuffer = new byte[Math.Min(header.Records, 4096) * header.RecordLength];
                DateTime epochUtc = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);

                int remaining = header.Records;
                while (remaining > 0)
                {
                    int batchRecords = Math.Min(remaining, bulkBuffer.Length / header.RecordLength);
                    int bytesToRead = batchRecords * header.RecordLength;
                    int bytesRead = fs.Read(bulkBuffer, 0, bytesToRead);
                    if (bytesRead < header.RecordLength) break;

                    int actualRecords = bytesRead / header.RecordLength;
                    for (int r = 0; r < actualRecords; r++)
                    {
                        int recStart = r * header.RecordLength;
                        if (bulkBuffer[recStart] == (byte)'*') continue;

                        // Parse timestamp
                        DateTime date = default(DateTime);
                        bool hasDate = false;
                        if (dataFieldIdx >= 0)
                        {
                            DbfField df = header.Fields[dataFieldIdx];
                            string ds = Encoding.ASCII.GetString(bulkBuffer, recStart + fieldOffsets[dataFieldIdx], df.Length).Trim();
                            if (ds.Length == 8)
                            {
                                DateTime parsed;
                                if (DateTime.TryParseExact(ds, "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out parsed))
                                {
                                    date = parsed.Date;
                                    hasDate = true;
                                }
                            }
                        }
                        if (!hasDate) continue;

                        int hh = ParseIntField(bulkBuffer, recStart, header.Fields, fieldOffsets, oreFieldIdx);
                        int mm = ParseIntField(bulkBuffer, recStart, header.Fields, fieldOffsets, minutiFieldIdx);
                        int ss = ParseIntField(bulkBuffer, recStart, header.Fields, fieldOffsets, secondiFieldIdx);
                        int ms = ParseIntField(bulkBuffer, recStart, header.Fields, fieldOffsets, mSecondiFieldIdx);

                        DateTime dt;
                        try { dt = new DateTime(date.Year, date.Month, date.Day, hh, mm, ss, DateTimeKind.Local).AddMilliseconds(ms); }
                        catch { continue; }

                        long tsMs = (long)(dt.ToUniversalTime() - epochUtc).TotalMilliseconds;

                        int idx = writeOffset + written;
                        timestamps[idx] = tsMs;

                        // Parse data columns using pre-built mapping
                        for (int c = 0; c < colCount; c++)
                        {
                            if (colFieldIdx[c] < 0 || colArrays[c] == null) continue;
                            DbfField field = header.Fields[colFieldIdx[c]];
                            if (field.FieldType == 'N')
                            {
                                colArrays[c][idx] = ParseDoubleField(bulkBuffer, recStart + fieldOffsets[colFieldIdx[c]], colFieldLen[c]);
                            }
                        }

                        written++;
                    }
                    remaining -= actualRecords;
                }
            }
            return written;
        }

        private static int FindFieldIndex(List<DbfField> fields, string name)
        {
            for (int i = 0; i < fields.Count; i++)
            {
                if (string.Equals(fields[i].Name, name, StringComparison.OrdinalIgnoreCase))
                    return i;
            }
            return -1;
        }

        private static int ParseIntField(byte[] buffer, int recStart, List<DbfField> fields, int[] offsets, int fieldIdx)
        {
            if (fieldIdx < 0) return 0;
            DbfField field = fields[fieldIdx];
            double? val = ParseDoubleField(buffer, recStart + offsets[fieldIdx], field.Length);
            return val.HasValue ? (int)val.Value : 0;
        }

        private static double? ParseDoubleField(byte[] buffer, int offset, int length)
        {
            // Fast inline parsing — trim spaces, parse number
            int start = offset;
            int end = offset + length - 1;
            while (start <= end && buffer[start] == (byte)' ') start++;
            while (end >= start && buffer[end] == (byte)' ') end--;
            int len = end - start + 1;
            if (len <= 0) return null;

            string s = Encoding.ASCII.GetString(buffer, start, len);
            if (s == ".") return null;
            double num;
            if (double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out num))
                return num;
            return null;
        }

        private static string ReadAsciiTerminated(byte[] buffer, int offset, int length)
        {
            int end = offset;
            int max = offset + length;
            while (end < max && buffer[end] != 0)
            {
                end++;
            }

            return Encoding.ASCII.GetString(buffer, offset, end - offset).Trim();
        }
    }
}
