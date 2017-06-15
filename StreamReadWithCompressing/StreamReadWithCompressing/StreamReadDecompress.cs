using System;
using System.IO;
using System.Text;

namespace StreamReadWithCompressing
{
    public class StreamReadDecompress : Stream
    {
        private readonly Stream _StreamCompressedData;
        private readonly Stream _StreamDataForReading;
        private readonly StreamReadModules _StreamReadModules;
        private byte[] _BufferCompressedData;
        private byte[] _BufferDecompressedData;
        private int _BufferDecompressedDataLength;
        private int _BufferDecompressedDataPosition;
        private long _Position;

        public StreamReadDecompress(Stream p_StreamDataForReading)
        {
            _StreamDataForReading = p_StreamDataForReading;
            _StreamCompressedData = new MemoryStream();
            _BufferCompressedData = new byte[0];
            _BufferDecompressedData = new byte[0];
            _StreamReadModules = new StreamReadModules();
        }

        public string LastReadUsedHeaderIdentification { get; private set; }

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length { get; }

        public override long Position
        {
            get => _Position;
            set
            {
                if (_Position != value) Seek(value, SeekOrigin.Begin);
            }
        }

        public override void Flush()
        {
            throw new NotImplementedException();
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new NotImplementedException();
        }

        public override void SetLength(long value)
        {
            throw new NotImplementedException();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            if (buffer == null)
                throw new ArgumentNullException(nameof(buffer));
            if (offset != 0)
                throw new ArgumentOutOfRangeException(nameof(offset));
            if (count < 0)
                throw new ArgumentOutOfRangeException(nameof(count));
            if (buffer.Length - offset < count)
                throw new ArgumentException("Offset and length were out of bounds for the array");

            if (_BufferDecompressedDataPosition < _BufferDecompressedDataLength)
            {
                var copyCount = Math.Min(_BufferDecompressedDataLength - _BufferDecompressedDataPosition, count);
                Array.Copy(_BufferDecompressedData, _BufferDecompressedDataPosition, buffer, 0, copyCount);
                _BufferDecompressedDataPosition += copyCount;
                _Position += copyCount;
                return copyCount;
            }

            if (count == 0) return 0;

            //Read new compressed chunk
            //Read first 4 bytes - may be known headerIdentification - it means that stream is compressed
            byte[] intBytes = new byte[4];
            var headerIdentification = _StreamDataForReading.Read(intBytes, 0, intBytes.Length);
            if (headerIdentification == 0) return 0;
            var module = _StreamReadModules.FindByHeaderIdentification(intBytes);
            if (module == null)
            {
                //not compressed by known headerIdentification - only copy to output
                LastReadUsedHeaderIdentification = null;
                Array.Copy(intBytes, buffer, intBytes.Length);
                int readedOriginal = _StreamDataForReading.Read(buffer, intBytes.Length, count - intBytes.Length) + intBytes.Length;
                _Position += readedOriginal;
                return readedOriginal;
            }
            LastReadUsedHeaderIdentification = Encoding.UTF8.GetString(intBytes);

            // Chunk header - Uncompressed and Compressed size
            var readedUncompressedChunkSize = _StreamDataForReading.Read(intBytes, 0, intBytes.Length);
            if (readedUncompressedChunkSize == 0) return 0;
            var uncompressedChunkSize = BitConverter.ToInt32(intBytes, 0);

            var readedCompressedChunkSize = _StreamDataForReading.Read(intBytes, 0, intBytes.Length);
            if (readedCompressedChunkSize == 0) return 0;
            var compressedChunkSize = BitConverter.ToInt32(intBytes, 0);

            //Read Chunk data to _BufferCompressedData
            if (_BufferCompressedData.Length < compressedChunkSize)
                Array.Resize(ref _BufferCompressedData, compressedChunkSize);
            var readed = _StreamDataForReading.Read(_BufferCompressedData, 0, compressedChunkSize);
            if (readed == 0) return 0;

            _StreamCompressedData.Position = 0;
            _StreamCompressedData.Write(_BufferCompressedData, 0, readed);
            _StreamCompressedData.Position = 0;

            //unzip data to buffer
            using (var gzipStream = module.ActionCreateDecompressStreamForWriting(_StreamCompressedData))
            {
                int readedUncompressed;
                if (uncompressedChunkSize > buffer.Length)
                {
                    //must use another buffer (_BufferDecompressedData) and copy to buffer
                    if (_BufferDecompressedData.Length < uncompressedChunkSize)
                        Array.Resize(ref _BufferDecompressedData, uncompressedChunkSize);
                    readedUncompressed = gzipStream.Read(_BufferDecompressedData, 0, uncompressedChunkSize);
                    Array.Copy(_BufferDecompressedData, buffer, count);
                    _BufferDecompressedDataPosition = count;
                    _BufferDecompressedDataLength = uncompressedChunkSize;
                    _Position += count;
                    return count;
                }
                //can read direct to buffer
                readedUncompressed = gzipStream.Read(buffer, 0, uncompressedChunkSize);
                _BufferDecompressedDataPosition = 0;
                _BufferDecompressedDataLength = 0;
                _Position += readedUncompressed;
                return readedUncompressed;
            }
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            throw new NotImplementedException();
        }
    }
}