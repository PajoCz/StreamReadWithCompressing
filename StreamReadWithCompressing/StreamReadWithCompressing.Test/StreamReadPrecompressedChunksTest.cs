using System;
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
        private const string FileNameOriginal = "original.txt";
        private const string FileNameCompressed = "compressed.gzip";
        private const string FileNameDecompressed = "decompressed.txt";

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        [TestCase(4)]
        [TestCase(5)]
        [TestCase(6)]
        [TestCase(7)]
        [TestCase(8)]
        public void CompressDecompressFile_LogInfoAboutCompressDecompressTime_CheckOriginalContentWithDecompressed(int p_PrepareChunks)
        {
            CreateTestFileIfNotExists();

            var sw = Stopwatch.StartNew();

            using (var streamWithData = new FileStream(FileNameOriginal, FileMode.Open))
            {
                Stream compressStreamCreated = p_PrepareChunks == 0
                    ? (Stream)new StreamReadCompress(streamWithData, StreamReadModules.HeaderIdentificationBrotli)
                    : new StreamReadPrecompressedChunks(streamWithData, StreamReadModules.HeaderIdentificationBrotli, p_PreparedChunks: p_PrepareChunks);
                using (var compressStream = compressStreamCreated)
                using (var streamCompressed = new FileStream(FileNameCompressed, FileMode.Create))
                {
                    compressStream.CopyTo(streamCompressed);
                }
            }
            sw.Stop();

            string oldNewString = p_PrepareChunks == 0 ? "old" : "new";
            var logText = $"{DateTime.Now} Compress {oldNewString} algo ({p_PrepareChunks} chunks) elapsed {sw.Elapsed}{Environment.NewLine}";
            Log(logText);

            //Assert.AreEqual(4246802, new FileInfo(FileNameCompressed).Length);    //gzip
            Assert.AreEqual(852011, new FileInfo(FileNameCompressed).Length); //brotli

            sw = Stopwatch.StartNew();
            using (var streamWithData = new FileStream(FileNameCompressed, FileMode.Open))
            {
                Stream decompressStreamCreated = p_PrepareChunks == 0
                    ? (Stream)new StreamReadDecompress(streamWithData)
                    : new StreamReadPredecompressedChunks(streamWithData, p_PrepareChunks);
                using (var compressStream = decompressStreamCreated)
                using (var streamCompressed = new FileStream(FileNameDecompressed, FileMode.Create))
                {
                    compressStream.CopyTo(streamCompressed);
                }
            }
            sw.Stop();

            oldNewString = p_PrepareChunks == 0 ? "old" : "new";
            logText = $"{DateTime.Now} Decompress {oldNewString} algo ({p_PrepareChunks} chunks) elapsed {sw.Elapsed}{Environment.NewLine}";
            Log(logText);

            AssertFileContentsSame(FileNameOriginal, FileNameDecompressed);
        }

        private void CreateTestFileIfNotExists()
        {
            var text = "This is testing content for compressing.";
            if (!File.Exists(FileNameOriginal))
            {
                using (var streamWithData = new FileStream(FileNameOriginal, FileMode.Create))
                {
                    while (streamWithData.Position <= 1024 * 1024 * 1024)
                        streamWithData.Write(Encoding.UTF8.GetBytes(text), 0, text.Length);
                }
            }
        }

        private void AssertFileContentsSame(string p_FileName1, string p_FileName2)
        {
            if (new FileInfo(p_FileName1).Length != new FileInfo(p_FileName2).Length)
                Assert.Fail("Files length is different");
            var bufLen = 80 * 1024;
            byte[] buf1 = new byte[bufLen];
            byte[] buf2 = new byte[bufLen];
            using (var fs1 = new FileStream(p_FileName1, FileMode.Open))
            using (var fs2 = new FileStream(p_FileName2, FileMode.Open))
            {
                int readed1;
                int readed2;
                while ((readed1 = fs1.Read(buf1, 0, bufLen)) > 0 &&
                       (readed2 = fs2.Read(buf2, 0, bufLen)) > 0)
                {
                    if (readed1 != readed2)
                        Assert.Fail("Readed another length of bytes");
                    Assert.IsTrue(buf1.SequenceEqual(buf2), "Files with another content");
                }
            }
        }

        private static void Log(string logText)
        {
            Console.Write(logText);
            File.AppendAllText("log.txt", logText);
        }
    }
}