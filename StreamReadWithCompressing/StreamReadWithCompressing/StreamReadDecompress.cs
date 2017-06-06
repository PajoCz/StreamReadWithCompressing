using System;
using System.IO;
using System.Text;

namespace StreamReadWithCompressing
{
    public class StreamReadDecompress : Stream
    {
        private readonly Stream _StreamCompressed;
        private byte[] _BufferCompressedData = new byte[0];
        private long _Position;
        public StreamReadModules StreamReadModules;

        public StreamReadDecompress(Stream p_StreamCompressed)
        {
            _StreamCompressed = p_StreamCompressed;
            StreamReadModules = new StreamReadModules();
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

            //Read first 4 bytes - may be known headerIdentification - stream is compressed
            byte[] intBytes = new byte[4];
            var headerIdentification = _StreamCompressed.Read(intBytes, 0, intBytes.Length);
            if (headerIdentification == 0) return 0;
            var module = StreamReadModules.FindByHeaderIdentification(intBytes);
            if (module == null)
            {
                //not compressed by known headerIdentification
                LastReadUsedHeaderIdentification = null;
                Array.Copy(intBytes, buffer, intBytes.Length);
                return _StreamCompressed.Read(buffer, intBytes.Length, count - intBytes.Length) + intBytes.Length;
            }
            LastReadUsedHeaderIdentification = Encoding.UTF8.GetString(intBytes);

            // Chunk header - Uncompressed and Compressed size
            var readedUncompressedChunkSize = _StreamCompressed.Read(intBytes, 0, intBytes.Length);
            if (readedUncompressedChunkSize == 0) return 0;
            var uncompressedChunkSize = BitConverter.ToInt32(intBytes, 0);

            var readedCompressedChunkSize = _StreamCompressed.Read(intBytes, 0, intBytes.Length);
            if (readedCompressedChunkSize == 0) return 0;
            var compressedChunkSize = BitConverter.ToInt32(intBytes, 0);

            //Read Chunk data to streamCompressedChunk
            if (_BufferCompressedData.Length < compressedChunkSize)
                Array.Resize(ref _BufferCompressedData, compressedChunkSize);
            var readed = _StreamCompressed.Read(_BufferCompressedData, 0, compressedChunkSize);
            if (readed == 0) return 0;

            var streamCompressedChunk = new MemoryStream();
            streamCompressedChunk.Write(_BufferCompressedData, 0, readed);
            streamCompressedChunk.Position = 0;

            //unzip data to buffer
            var readedUncompressed = 0;
            using (var gzipStream = module.ActionCreateDecompressStreamForWriting(streamCompressedChunk))
            {
                readedUncompressed = gzipStream.Read(buffer, 0, uncompressedChunkSize);
            }

            _Position += readedUncompressed;
            return readedUncompressed;
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            throw new NotImplementedException();
        }
    }
}