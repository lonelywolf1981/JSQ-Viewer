using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using JSQViewer.Core;

namespace JSQViewer.Tests
{
    [TestClass]
    public class DbfReaderGuardTests
    {
        private static byte[] MakeMinimalDbfStream(int recordCount, ushort recordLength)
        {
            ushort headerLength = 33;
            byte[] header = new byte[headerLength];
            header[0] = 3;
            byte[] countBytes = BitConverter.GetBytes(recordCount);
            Array.Copy(countBytes, 0, header, 4, 4);
            header[8] = (byte)(headerLength & 0xFF);
            header[9] = (byte)(headerLength >> 8);
            header[10] = (byte)(recordLength & 0xFF);
            header[11] = (byte)(recordLength >> 8);
            header[32] = 0x0D;
            return header;
        }

        [TestMethod]
        public void ReadAllRecordsDirect_RecordLengthZero_ThrowsInvalidDataException()
        {
            byte[] dbfBytes = MakeMinimalDbfStream(recordCount: 10, recordLength: 0);
            DbfHeader header = DbfReader.ReadHeader(dbfBytes);
            Assert.AreEqual(0, header.RecordLength);

            var timestamps = new long[10];
            var columns = new Dictionary<string, double?[]>();
            var colNames = new string[0];

            Assert.ThrowsException<InvalidDataException>(() =>
            {
                using (var ms = new MemoryStream(dbfBytes))
                {
                    ms.Seek(header.HeaderLength, SeekOrigin.Begin);
                    DbfReader.ReadAllRecordsDirect(ms, header, timestamps, columns, colNames, 0);
                }
            });
        }
    }
}
