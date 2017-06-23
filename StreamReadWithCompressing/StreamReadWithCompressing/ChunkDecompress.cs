using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace StreamReadWithCompressing
{
    public class ChunkDecompress
    {
        private byte[] _BufferCompressedData;
        private byte[] _BufferDecompressedData;
        private readonly Stream _StreamCompressedData;

        private int _BufferDecompressedDataLength;
        private int _BufferDecompressedDataPosition;

        public ManualResetEvent _ManualResetEvent = new ManualResetEvent(false);

        public Stopwatch TotalBlockedTime = new Stopwatch();

        public ChunkDecompress(byte[] p_BufferCompressedData, byte[] p_BufferDecompressedData, Stream p_StreamCompressedData)
        {
            _BufferCompressedData = p_BufferCompressedData;
            _BufferDecompressedData = p_BufferDecompressedData;
            _StreamCompressedData = p_StreamCompressedData;
        }

        public int BlockingReadFromDecompressedChunk(byte[] buffer, int count, out bool chunkReadedToEnd)
        {
            TotalBlockedTime.Start();
            _ManualResetEvent.WaitOne();
            TotalBlockedTime.Stop();

            chunkReadedToEnd = true;
            if (_BufferDecompressedDataPosition >= _BufferDecompressedDataLength) return 0;

            var copyCount = Math.Min(_BufferDecompressedDataLength - _BufferDecompressedDataPosition, count);
            Array.Copy(_BufferDecompressedData, _BufferDecompressedDataPosition, buffer, 0, copyCount);
            _BufferDecompressedDataPosition += copyCount;
            chunkReadedToEnd = _BufferDecompressedDataPosition == _BufferDecompressedDataLength;
            return copyCount;
        }

        public void ReadDataAndStartDecompressingInTask(Stream p_StreamDataForReading, StreamReadModules p_StreamReadModules, int p_Count)
        {
            _ManualResetEvent.Set();        
            //Read new compressed chunk
            //Read first 4 bytes - may be known headerIdentification - it means that stream is compressed
            byte[] intBytes = new byte[4];
            var headerIdentification = p_StreamDataForReading.Read(intBytes, 0, intBytes.Length);
            if (headerIdentification == 0)
            {
                _BufferDecompressedDataLength = 0;
                return;
            }
            var module = p_StreamReadModules.FindByHeaderIdentification(intBytes);
            if (module == null)
            {
                //not compressed by known headerIdentification - only copy to output
                if (_BufferDecompressedData.Length < p_Count)
                {
                    Array.Resize(ref _BufferDecompressedData, p_Count);
                }
                Array.Copy(intBytes, _BufferDecompressedData, intBytes.Length);

                int readedOriginal = p_StreamDataForReading.ReadMaybeMoreTimes(_BufferDecompressedData, intBytes.Length, p_Count);
                _BufferDecompressedDataLength = readedOriginal;
                _BufferDecompressedDataPosition = 0;
                return;
            }

            // Chunk header - Uncompressed and Compressed size
            var readedUncompressedChunkSize = p_StreamDataForReading.Read(intBytes, 0, intBytes.Length);
            if (readedUncompressedChunkSize == 0)
            {
                _BufferDecompressedDataLength = 0;
                return;
            }
            var uncompressedChunkSize = BitConverter.ToInt32(intBytes, 0);

            var readedCompressedChunkSize = p_StreamDataForReading.Read(intBytes, 0, intBytes.Length);
            if (readedCompressedChunkSize == 0)
            {
                _BufferDecompressedDataLength = 0;
                return;
            }
            var compressedChunkSize = BitConverter.ToInt32(intBytes, 0);

            //Read Chunk data to _BufferCompressedData
            if (_BufferCompressedData.Length < compressedChunkSize)
                Array.Resize(ref _BufferCompressedData, compressedChunkSize);

            int readed = p_StreamDataForReading.ReadMaybeMoreTimes(_BufferCompressedData, 0, compressedChunkSize);
            if (readed == 0)
            {
                _BufferDecompressedDataLength = 0;
                return;
            }

            _ManualResetEvent.Reset();
            Task.Factory.StartNew(() => DecompressData(readed, module, uncompressedChunkSize));
        }

        private void DecompressData(int readed, StreamReadModule module, int uncompressedChunkSize)
        {
            _StreamCompressedData.Position = 0;
            _StreamCompressedData.Write(_BufferCompressedData, 0, readed);
            _StreamCompressedData.Position = 0;

            //decompress data to buffer
            using (var gzipStream = module.ActionCreateDecompressStreamForWriting(_StreamCompressedData))
            {
                if (_BufferDecompressedData.Length < uncompressedChunkSize)
                    Array.Resize(ref _BufferDecompressedData, uncompressedChunkSize);
                var readedUncompressed = gzipStream.Read(_BufferDecompressedData, 0, uncompressedChunkSize);
                if (uncompressedChunkSize != readedUncompressed)
                {
                    throw new Exception($"UncompressedChunkSize must be {uncompressedChunkSize}B but stream returns {readedUncompressed}B");
                }
                _BufferDecompressedDataPosition = 0;
                _BufferDecompressedDataLength = uncompressedChunkSize;
            }
            _ManualResetEvent.Set();
        }
    }
}
