using System;
using System.Collections.Generic;
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
        [TestCase(StreamReadModules.HeaderIdentificationBrotli)]
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
        [TestCase(StreamReadModules.HeaderIdentificationBrotli)]
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
        [TestCase(StreamReadModules.HeaderIdentificationBrotli)]
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
        [TestCase(StreamReadModules.HeaderIdentificationBrotli)]
        public void Compress_Decompress_1MB_RandomData_CompressedBufferBiggerThanOriginalBufferData_ReturnNotCompressedData_DecompressMustReturnOriginalData(string p_HeaderIdentification)
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
                        for (int i = 0; i < streamWithData.Length; i++)
                        {
                            Assert.AreEqual(streamWithData.ReadByte(), streamWithUncompressedData.ReadByte(), "Content of decompressed stream is different");
                        }
                    }
                }
            }
        }

        [TestCase(StreamReadModules.HeaderIdentificationDeflate)]
        [TestCase(StreamReadModules.HeaderIdentificationGzip)]
        [TestCase(StreamReadModules.HeaderIdentificationBrotli)]
        public void Compress_Decompress_1MB_TextData_ReturnCompressedData_DecompressMustReturnOriginalData(string p_HeaderIdentification)
        {
            string text = "This is testing content for compressing";
            //using (var streamWithData = new FileStream(@"D:\1original.txt", FileMode.Create))
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
                //using (var streamWithCompressedData = new FileStream(@"compressed.dat", FileMode.Create))
                using (var streamWithCompressedData = new MemoryStream())
                {
                    streamReadCompress.CopyTo(streamWithCompressedData);
                    //not compressed data - same as original
                    Assert.AreEqual(p_HeaderIdentification, streamReadCompress.LastReadUsedHeaderIdentification);

                    //Decompress
                    streamWithCompressedData.Position = 0;
                    using (StreamReadDecompress streamReadDecompress = new StreamReadDecompress(streamWithCompressedData))
                    //using (var streamWithUncompressedData = new FileStream(@"D:\1decompress.txt", FileMode.Create))
                    using (var streamWithUncompressedData = new MemoryStream())
                    {
                        streamReadDecompress.CopyTo(streamWithUncompressedData);

                        //Assert streamWithData = streamWithUncompressedData
                        streamWithData.Position = 0;
                        streamWithUncompressedData.Position = 0;
                        Assert.AreEqual(streamWithData.Length, streamWithUncompressedData.Length, "Decompressed stream must be same length as original data");
                        for (int i = 0; i < streamWithData.Length; i++)
                        {
                            Assert.AreEqual(streamWithData.ReadByte(), streamWithUncompressedData.ReadByte(), "Content of decompressed stream is different");
                        }
                    }
                }
            }
        }

        //Compress/Decompress ReadBufferSize and Chunk size same
        [TestCase(255, 255, 255)]
        [TestCase(1024, 1024, 1024)]
        [TestCase(80 * 1024, 80 * 1024, 80 * 1024)]
        [TestCase(100 * 1024, 100 * 1024, 100 * 1024)]
        //Compress ReadBufferSize small
        [TestCase(20, 80 * 1024, 80 * 1024)]
        [TestCase(255, 80 * 1024, 80 * 1024)]
        [TestCase(20, 1024, 1024)]
        [TestCase(255, 1024, 1024)]
        //Compress ReadBufferSize large
        [TestCase(85 * 1024, 80 * 1024, 80 * 1024)]
        [TestCase(80 * 1024, 1024, 1024)]
        //Decompress ReadBufferSize small
        [TestCase(80 * 1024, 20, 80 * 1024)]
        [TestCase(80 * 1024, 255, 80 * 1024)]
        [TestCase(1024, 20, 1024)]
        [TestCase(1024, 255, 1024)]
        //Decompress ReadBufferSize large
        [TestCase(80 * 1024, 85 * 1024, 80 * 1024)]
        [TestCase(1024, 8 * 1024, 1024)]
        [TestCase(1024, 85 * 1024, 1024)]
        public void Compress_Decompress_1MB_TextData_Variables_CompressBufferSize_DecompressBufferSize_ChunkSizeOfStreamDataForCompress(int p_StreamReadCompressReadBufferSize, int p_StreamReadDecompressReadBufferSize, int p_StreamReadCompressChunkSizeForCompress)
        {
            string text = "This is testing content for compressing";

            List<string> headerIdentification = new List<string> {StreamReadModules.HeaderIdentificationGzip, StreamReadModules.HeaderIdentificationDeflate, StreamReadModules.HeaderIdentificationBrotli};
            headerIdentification.ForEach(hi =>
            {
                for (int preparedChunksSettings = 0; preparedChunksSettings < 8; preparedChunksSettings++)
                {
                    using (var streamWithData = new MemoryStream())
                    {
                        //Create 1MB MemoryStream
                        while (streamWithData.Position <= 1024 * 1024)
                        {
                            streamWithData.Write(Encoding.UTF8.GetBytes(text), 0, text.Length);
                        }
                        streamWithData.Position = 0;

                        Stream streamCompress = preparedChunksSettings == 0
                            ? (Stream) new StreamReadCompress(streamWithData, hi, p_ChunkSizeOfStreamDataForCompress: p_StreamReadCompressChunkSizeForCompress)
                            : new StreamReadPrecompressedChunks(streamWithData, hi, p_ChunkSizeOfStreamDataForCompress: p_StreamReadCompressChunkSizeForCompress,
                                p_PreparedChunks: preparedChunksSettings);
                        using (var streamReadCompress = streamCompress)
                        using (var streamWithCompressedData = new MemoryStream())
                        {
                            StreamCopyToStreamWithBufferSize(streamReadCompress, streamWithCompressedData, p_StreamReadCompressReadBufferSize);
                            streamReadCompress.CopyTo(streamWithCompressedData);

                            //todo: check that stream contains only one chunk with gzip header
                            //small header means that compressed is bigger and returns original without compression
                            string streamWithCompressedDataContent = Encoding.UTF8.GetString(streamWithCompressedData.ToArray());
                            Assert.AreNotEqual(text, streamWithCompressedDataContent, "Content must be compressed");

                            //Decompress
                            streamWithCompressedData.Position = 0;
                            Stream streamDecompress = preparedChunksSettings == 0
                                ? (Stream)new StreamReadDecompress(streamWithCompressedData)
                                : new StreamReadPredecompressedChunks(streamWithCompressedData, preparedChunksSettings);
                            using (var streamReadDecompress = streamDecompress)
                                //using (var streamWithUncompressedData = new FileStream(@"D:\1decompress.txt", FileMode.Create))
                            using (var streamWithUncompressedData = new MemoryStream())
                            {
                                streamReadDecompress.CopyTo(streamWithUncompressedData, p_StreamReadDecompressReadBufferSize);

                                //Assert streamWithData = streamWithUncompressedData
                                Assert.AreEqual(streamWithData.Length, streamWithUncompressedData.Length, "Decompressed stream must be same length as original data");
                                streamWithData.Position = 0;
                                streamWithUncompressedData.Position = 0;
                                for (int i = 0; i < streamWithData.Length; i++)
                                {
                                    Assert.AreEqual(streamWithData.ReadByte(), streamWithUncompressedData.ReadByte(), "Content of decompressed stream is different");
                                }
                            }
                        }
                    }
                }
            });
        }

        [TestCase("A")] //returns only 1 original char
        [TestCase("AA")] 
        [TestCase("AAA")] 
        [TestCase("AAAA")] 
        [TestCase("AAAAA")] 
        [TestCase("gzip")]  //start with gzip identifier but without other header int numbers - it's only original uncompressed input
        [TestCase("gzip.")]
        [TestCase("gzip..")]
        [TestCase("gzip...")]
        [TestCase("gzip....")]  //end first int with Uncompressed size
        [TestCase("gzip.....")]
        [TestCase("gzip......")]
        [TestCase("gzip.......")]
        [TestCase("gzip........")]  //end second int with Compressed size
        [TestCase("gzip.........")] //after 2 integers only one char (shorter as compressed size in second integer  )
        public void NotCompressed_TooShortInput_DecompressThisOriginal(string text)
        {
            using (var streamWithData = new MemoryStream())
            { 
                streamWithData.Write(Encoding.UTF8.GetBytes(text), 0, text.Length);
                streamWithData.Position = 0;

                Stream streamCompress = new StreamReadCompress(streamWithData, StreamReadModules.HeaderIdentificationGzip);
                using (var streamReadCompress = streamCompress)
                using (var streamWithCompressedData = new MemoryStream())
                {
                    StreamCopyToStreamWithBufferSize(streamReadCompress, streamWithCompressedData, text.Length);
                    streamReadCompress.CopyTo(streamWithCompressedData);

                    //Decompress
                    streamWithCompressedData.Position = 0;

                    //Test data
                    Stream streamDecompress = new StreamReadDecompress(streamWithCompressedData);
                    using (var streamReadDecompress = streamDecompress)
                    using (var streamWithUncompressedData = new MemoryStream())
                    {
                        streamReadDecompress.CopyTo(streamWithUncompressedData);

                        Assert.AreEqual(text.Length, streamWithData.Position);
                        Assert.AreEqual(text.Length, streamWithCompressedData.Position);
                        Assert.AreEqual(text.Length, streamWithUncompressedData.Position);
                        //Assert streamWithData = streamWithCompressedData
                        Assert.AreEqual(streamWithData.Length, streamWithCompressedData.Length, "Compressed stream must be same length as original data (too short to compress)");
                        streamWithData.Position = 0;
                        streamWithCompressedData.Position = 0;
                        for (int i = 0; i < streamWithData.Length; i++)
                        {
                            Assert.AreEqual(streamWithData.ReadByte(), streamWithCompressedData.ReadByte(), "Content of decompressed stream is different");
                        }

                        //Assert streamWithData = streamWithUncompressedData
                        Assert.AreEqual(streamWithData.Length, streamWithUncompressedData.Length, "Decompressed stream must be same length as original data");
                        streamWithData.Position = 0;
                        streamWithUncompressedData.Position = 0;
                        for (int i = 0; i < streamWithData.Length; i++)
                        {
                            Assert.AreEqual(streamWithData.ReadByte(), streamWithUncompressedData.ReadByte(), "Content of decompressed stream is different");
                        }
                    }
                }
            }
        }


        private void StreamCopyToStreamWithBufferSize(Stream p_Source, Stream p_Destination, int p_BufferSize)
        {
            byte[] buffer = new byte[p_BufferSize];
            int read;
            while ((read = p_Source.Read(buffer, 0, buffer.Length)) != 0)
                p_Destination.Write(buffer, 0, read);
        }
    }
}
