using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using NUnit.Framework;

namespace StreamReadWithCompressing.Test
{
    [TestFixture]
    public class StreamReadPrecompressedChunksTest
    {
        [Test]
        public void CreateTestFile()
        {
            string text = "This is testing content for compressing.";
            using (var streamWithData = new FileStream(@"C:\1\original.txt", FileMode.Create))
            {
                //Create 1MB MemoryStream
                while (streamWithData.Position <= 1024 * 1024 * 1024)
                {
                    streamWithData.Write(Encoding.UTF8.GetBytes(text), 0, text.Length);
                }
                streamWithData.Position = 0;
            }
        }

        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        [TestCase(4)]
        [TestCase(5)]
        [TestCase(6)]
        [TestCase(7)]
        [TestCase(8)]
        public void CompressFile(int p_PrepareChunks)
        {
            Stopwatch sw = Stopwatch.StartNew();
            using (var streamWithData = new FileStream(@"C:\1\original.txt", FileMode.Open))
            using (var compressStream = new StreamReadPrecompressedChunks(streamWithData, StreamReadModules.HeaderIdentificationGzip, p_PreparedChunks: p_PrepareChunks))
            using (var streamCompressed = new FileStream(@"C:\1\compressed-newAlgo.gzip", FileMode.Create))
            {
                compressStream.CopyTo(streamCompressed);
            }
            sw.Stop();
            File.AppendAllText(@"C:\1\log.txt", $"{DateTime.Now} Compress new algo ({p_PrepareChunks} chunks) elapsed {sw.Elapsed}{Environment.NewLine}");

            Assert.AreEqual(4246802, new FileInfo(@"C:\1\compressed-newAlgo.gzip").Length);
            //DecompressFile("compressed-newAlgo.gzip");
            //AssertFileContentsSame(@"C:\1\original.txt", @"C:\1\decompressed.txt");
        }

        [Test]
        public void CompressFile_OldAlgo()
        {
            Stopwatch sw = Stopwatch.StartNew();
            using (var streamWithData = new FileStream(@"C:\1\original.txt", FileMode.Open))
            using (var compressStream = new StreamReadCompress(streamWithData, StreamReadModules.HeaderIdentificationGzip))
            using (var streamCompressed = new FileStream(@"C:\1\compressed-oldAlgo.gzip", FileMode.Create))
            {
                compressStream.CopyTo(streamCompressed);
            }
            sw.Stop();
            File.AppendAllText(@"C:\1\log.txt", $"{DateTime.Now} Compress old algo elapsed {sw.Elapsed}{Environment.NewLine}");

            Assert.AreEqual(4246802, new FileInfo(@"C:\1\compressed-oldAlgo.gzip").Length);
        }

        [TestCase("compressed-newAlgo.gzip")]
        [TestCase("compressed-oldAlgo.gzip")]
        public void DecompressFile(string p_FileName)
        {
            Stopwatch sw = Stopwatch.StartNew();
            using (var streamWithData = new FileStream(@"C:\1\" + p_FileName, FileMode.Open))
            using (var compressStream = new StreamReadDecompress(streamWithData))
            using (var streamCompressed = new FileStream(@"C:\1\decompressed.txt", FileMode.Create))
            {
                compressStream.CopyTo(streamCompressed);
            }
            sw.Stop();
            File.AppendAllText(@"C:\1\log.txt", $"{DateTime.Now} Decompress elapsed {sw.Elapsed}{Environment.NewLine}");
        }

        private void AssertFileContentsSame(string p_FileName1, string p_FileName2)
        {
            if (new FileInfo(p_FileName1).Length != new FileInfo(p_FileName2).Length)
            {
                Assert.Fail("Files length is different");
            }
            int bufLen = 80 * 1024;
            byte[] buf1 = new byte[bufLen];
            byte[] buf2 = new byte[bufLen];
            using (FileStream fs1 = new FileStream(p_FileName1, FileMode.Open))
            using (FileStream fs2 = new FileStream(p_FileName2, FileMode.Open))
            {
                int readed1;
                int readed2;
                while ((readed1 = fs1.Read(buf1, 0, bufLen)) > 0 &&
                       (readed2 = fs2.Read(buf2, 0, bufLen)) > 0)
                {
                    if (readed1 != readed2)
                    {
                        Assert.Fail("Readed another length of bytes");
                    }
                    Assert.IsTrue(buf1.SequenceEqual(buf2), "Files with another content");
                }
            }
        }
    }
}
