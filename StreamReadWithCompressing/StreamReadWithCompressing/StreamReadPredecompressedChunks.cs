using System;
using System.Collections.Generic;
using System.IO;

namespace StreamReadWithCompressing
{
    public class StreamReadPredecompressedChunks : Stream
    {
        private readonly List<ChunkDecompress> _Chunks;
        private readonly Stream _StreamDataForReading;
        private readonly StreamReadModules _StreamReadModules;
        private int _ChunkBufferIndexForRead;
        private long _Position;

        public StreamReadPredecompressedChunks(Stream p_StreamDataForReading, int p_PreparedChunks = 1)
        {
            _StreamDataForReading = p_StreamDataForReading;
            _StreamReadModules = new StreamReadModules();

            _Chunks = new List<ChunkDecompress>(p_PreparedChunks);
            for (var i = 0; i < p_PreparedChunks; i++)
            {
                var chunk = new ChunkDecompress(new byte[0], new byte[0], new MemoryStream());
                chunk.ReadDataAndStartDecompressingInTask(_StreamDataForReading, _StreamReadModules, 80 * 1024);
                _Chunks.Add(chunk);
            }
        }

        private ChunkDecompress ActiveChunk => _Chunks[_ChunkBufferIndexForRead];

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

            if (count == 0) return 0;

            bool chunkReadedToEnd;
            var result = ActiveChunk.BlockingReadFromDecompressedChunk(buffer, count, out chunkReadedToEnd);
            _Position += result;

            if (result == 0) return 0;

            if (chunkReadedToEnd)
            {
                ActiveChunk.ReadDataAndStartDecompressingInTask(_StreamDataForReading, _StreamReadModules, count);
                SwitchToNextChunk();
            }

            return result;
        }

        private void SwitchToNextChunk()
        {
            _ChunkBufferIndexForRead++;
            if (_ChunkBufferIndexForRead >= _Chunks.Count)
                _ChunkBufferIndexForRead = 0;
        }


        public override void Write(byte[] buffer, int offset, int count)
        {
            throw new NotImplementedException();
        }
    }
}