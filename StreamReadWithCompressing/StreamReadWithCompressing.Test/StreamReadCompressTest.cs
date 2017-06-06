using System;
using System.IO;
using System.Text;
using NUnit.Framework;

namespace StreamReadWithCompressing.Test
{
    [TestFixture]
    public class StreamReadCompressTest
    {
        [TestCase(StreamReadModules.HeaderIdentificationDeflate)]
        [TestCase(StreamReadModules.HeaderIdentificationGzip)]
        public void CompressOnlyStreamWithMinimumLength_LargerValueThanInputStream_ReturnsOriginalDataWithoutCompression(string p_HeaderIdentification)
        {
            string text = "This is testing content for compressing";
            int compressOnlyStreamWithMinimumLength = text.Length + 1;
            using (var streamWithData = new MemoryStream(Encoding.UTF8.GetBytes(text)))
            using (var streamReadCompress = new StreamReadCompress(streamWithData, p_HeaderIdentification, compressOnlyStreamWithMinimumLength))
            using (var streamWithNonCompressedData = new MemoryStream())
            {
                streamReadCompress.CopyTo(streamWithNonCompressedData);
                string streamWithNonCompressedDataContent = Encoding.UTF8.GetString(streamWithNonCompressedData.ToArray());
                Assert.AreEqual(text, streamWithNonCompressedDataContent);
                Assert.AreEqual(null, streamReadCompress.LastReadUsedHeaderIdentification);
            }
        }

        [TestCase(StreamReadModules.HeaderIdentificationDeflate)]
        [TestCase(StreamReadModules.HeaderIdentificationGzip)]
        public void CompressOnlyRatioToPercent_SetZeroRation_ReturnsOriginalDataWithoutCompression(string p_HeaderIdentification)
        {
            string text = "This is testing content for compressing";
            using (var streamWithData = new MemoryStream(Encoding.UTF8.GetBytes(text)))
            using (var streamReadCompress = new StreamReadCompress(streamWithData, p_HeaderIdentification, p_CompressOnlyRatioToPercent: 0))
            using (var streamWithNonCompressedData = new MemoryStream())
            {
                streamReadCompress.CopyTo(streamWithNonCompressedData);
                string streamWithNonCompressedDataContent = Encoding.UTF8.GetString(streamWithNonCompressedData.ToArray());
                Assert.AreEqual(text, streamWithNonCompressedDataContent);
                Assert.AreEqual(null, streamReadCompress.LastReadUsedHeaderIdentification);
            }
        }

        [TestCase(StreamReadModules.HeaderIdentificationDeflate)]
        [TestCase(StreamReadModules.HeaderIdentificationGzip)]
        public void Compress_Decompress_ShortText(string p_HeaderIdentification)
        {
            string text = "This is testing content for compressing";
            using (var streamWithData = new MemoryStream(Encoding.UTF8.GetBytes(text)))
            using (var streamReadCompress = new StreamReadCompress(streamWithData, p_HeaderIdentification))
            using (var streamWithCompressedData = new MemoryStream())
            {
                streamReadCompress.CopyTo(streamWithCompressedData);
                streamWithCompressedData.Position = 0;
                using (StreamReadDecompress streamReadDecompress = new StreamReadDecompress(streamWithCompressedData))
                using (var streamWithUncompressedData = new MemoryStream())
                {
                    streamReadDecompress.CopyTo(streamWithUncompressedData);
                    string uncompressed = Encoding.UTF8.GetString(streamWithUncompressedData.ToArray());
                    Assert.AreEqual(text, uncompressed);
                }
            }
        }

        [TestCase(StreamReadModules.HeaderIdentificationDeflate)]
        [TestCase(StreamReadModules.HeaderIdentificationGzip)]
        public void CreateDeflate_Compress_Decompress_1MB_RandomData_CompressedBufferBiggerThanOriginalBufferData_ReturnNotCompressedData_DecompressMustReturnOriginalData(string p_HeaderIdentification)
        {            
            Random random = new Random();
            using (var streamWithData = new MemoryStream())
            {
                //Create 1MB MemoryStream with random values
                byte[] randomBuffer = new byte[1024];
                for (int i = 0; i < 1024; i++)
                {
                    random.NextBytes(randomBuffer);
                    streamWithData.Write(randomBuffer, 0, randomBuffer.Length);
                }
                streamWithData.Position = 0;

                //Compress
                using (var streamReadCompress = new StreamReadCompress(streamWithData, p_HeaderIdentification))
                using (var streamWithCompressedData = new MemoryStream())
                {
                    streamReadCompress.CopyTo(streamWithCompressedData);
                    //not compressed data - same as original
                    Assert.IsNull(streamReadCompress.LastReadUsedHeaderIdentification);
                    Assert.AreEqual(streamWithData.Length, streamWithCompressedData.Length);

                    //Decompress
                    streamWithCompressedData.Position = 0;
                    using (StreamReadDecompress streamReadDecompress = new StreamReadDecompress(streamWithCompressedData))
                    using (var streamWithUncompressedData = new MemoryStream())
                    {
                        streamReadDecompress.CopyTo(streamWithUncompressedData);

                        //Assert streamWithData = streamWithUncompressedData
                        streamWithData.Position = 0;
                        streamWithUncompressedData.Position = 0;
                        Assert.AreEqual(streamWithData.Length, streamWithUncompressedData.Length, "Decompressed stream must be same length as original data");
                        for (int i = 0; i < streamWithData.Position; i++)
                        {
                            Assert.AreEqual(streamWithData.ReadByte(), streamWithUncompressedData.ReadByte(), "Content of decompressed stream is different");
                        }
                    }
                }
            }
        }

        [TestCase(StreamReadModules.HeaderIdentificationDeflate)]
        [TestCase(StreamReadModules.HeaderIdentificationGzip)]
        public void CreateDeflate_Compress_Decompress_1MB_TextData_ReturnCompressedData_DecompressMustReturnOriginalData(string p_HeaderIdentification)
        {
            Random random = new Random();
            string text = "This is testing content for compressing";
            using (var streamWithData = new MemoryStream())
            {
                //Create 1MB MemoryStream with random values
                while (streamWithData.Position <= 1024 * 1024)
                {
                    streamWithData.Write(Encoding.UTF8.GetBytes(text), 0, text.Length);                    
                }
                streamWithData.Position = 0;

                //Compress
                using (var streamReadCompress = new StreamReadCompress(streamWithData, p_HeaderIdentification))
                using (var streamWithCompressedData = new MemoryStream())
                {
                    streamReadCompress.CopyTo(streamWithCompressedData);
                    //not compressed data - same as original
                    Assert.AreEqual(p_HeaderIdentification, streamReadCompress.LastReadUsedHeaderIdentification);

                    //Decompress
                    streamWithCompressedData.Position = 0;
                    using (StreamReadDecompress streamReadDecompress = new StreamReadDecompress(streamWithCompressedData))
                    using (var streamWithUncompressedData = new MemoryStream())
                    {
                        streamReadDecompress.CopyTo(streamWithUncompressedData);

                        //Assert streamWithData = streamWithUncompressedData
                        streamWithData.Position = 0;
                        streamWithUncompressedData.Position = 0;
                        Assert.AreEqual(streamWithData.Length, streamWithUncompressedData.Length, "Decompressed stream must be same length as original data");
                        for (int i = 0; i < streamWithData.Position; i++)
                        {
                            Assert.AreEqual(streamWithData.ReadByte(), streamWithUncompressedData.ReadByte(), "Content of decompressed stream is different");
                        }
                    }
                }
            }
        }
    }
}
