using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace LeMuReViewer.Core
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
