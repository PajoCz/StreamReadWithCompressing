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

        [TestCase(true, 0)]
        [TestCase(false, 1)]
        [TestCase(false, 2)]
        [TestCase(false, 3)]
        [TestCase(false, 4)]
        [TestCase(false, 5)]
        [TestCase(false, 6)]
        [TestCase(false, 7)]
        [TestCase(false, 8)]
        public void CompressFile_LogInfoAboutCompressTime_AssertWithDecompressAlgo(bool p_OldAlgo, int p_PrepareChunks)
        {
            CreateTestFileIfNotExists();

            var sw = Stopwatch.StartNew();
            using (var streamWithData = new FileStream(FileNameOriginal, FileMode.Open))
            using (var compressStream = StreamReadCreator(p_OldAlgo, streamWithData, p_PrepareChunks))
            using (var streamCompressed = new FileStream(FileNameCompressed, FileMode.Create))
            {
                compressStream.CopyTo(streamCompressed);
            }
            sw.Stop();

            string oldNewString = p_OldAlgo ? "old" : "new";
            var logText = $"{DateTime.Now} Compress {oldNewString} algo ({p_PrepareChunks} chunks) elapsed {sw.Elapsed}{Environment.NewLine}";
            Log(logText);

            Assert.AreEqual(4246802, new FileInfo(FileNameCompressed).Length);
            DecompressFile(FileNameCompressed, FileNameDecompressed);
            AssertFileContentsSame(FileNameOriginal, FileNameDecompressed);
        }

        private Stream StreamReadCreator(bool p_OldAlgo, Stream p_StreamDataForReading, int p_PrepareChunks = 0)
        {
            return p_OldAlgo 
                ? (Stream)new StreamReadCompress(p_StreamDataForReading, StreamReadModules.HeaderIdentificationGzip) 
                : new StreamReadPrecompressedChunks(p_StreamDataForReading, StreamReadModules.HeaderIdentificationGzip, p_PreparedChunks: p_PrepareChunks);
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

        private void DecompressFile(string p_FileNameSource, string p_FileNameDestination)
        {
            var sw = Stopwatch.StartNew();
            using (var streamWithData = new FileStream(p_FileNameSource, FileMode.Open))
            using (var compressStream = new StreamReadDecompress(streamWithData))
            using (var streamCompressed = new FileStream(p_FileNameDestination, FileMode.Create))
            {
                compressStream.CopyTo(streamCompressed);
            }
            sw.Stop();

            var logText = $"{DateTime.Now} Decompress elapsed {sw.Elapsed}{Environment.NewLine}";
            Log(logText);
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