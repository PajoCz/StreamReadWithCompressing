//#define log

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace StreamReadWithCompressing
{
    /// <summary>
    ///     Read chunks to memory and compress their data by another Tasks
    ///     Stream.Read can read prepared compressed data from chunks
    ///     When chunk readed compressed data - Read original data from _StreamDataForReading to this chunk and run another
    ///     Task for compressing
    /// </summary>
    public class StreamReadPrecompressedChunks : Stream
    {
        // _DefaultCopyBufferSize - from MS implementation of Stream.CopyTo method - 80kB
        // We pick a value that is the largest multiple of 4096 that is still smaller than the large object heap threshold (85K).
        // The CopyTo/CopyToAsync buffer is short-lived and is likely to be collected at Gen0, and it offers a significant
        // improvement in Copy performance.
        public const int ReadedChunkSizeBeforeCompressDefaultValue = 80 * 1024;

        private readonly StreamReadModule _CompressModule;

        private readonly byte _CompressOnlyRatioToPercent;
        private readonly int _CompressOnlyStreamWithMinimumLength;

        private readonly List<Chunk> _Chunks;
        private readonly int _PreparedChunks;
        private readonly int _ReadedChunkSizeBeforeCompress;

        private readonly Stream _StreamDataForReading;
        private long _Position;
        private int _ChunkBufferIndexForRead;

#if log
        private readonly StringBuilder log = new StringBuilder();
        private readonly object logLock = new object();
#endif

        /// <summary>
        /// </summary>
        /// <param name="p_StreamDataForReading"></param>
        /// <param name="p_CompressModuleIdentifier"></param>
        /// <param name="p_CompressOnlyStreamWithMinimumLength"></param>
        /// <param name="p_CompressOnlyRatioToPercent">
        ///     Calculate Compress/Decompress*100. Compress only if calculated value is
        ///     smaller than this setting
        /// </param>
        /// <param name="p_ChunkSizeOfStreamDataForCompress"></param>
        /// <param name="p_PreparedChunks"></param>
        public StreamReadPrecompressedChunks(Stream p_StreamDataForReading, string p_CompressModuleIdentifier,
            int p_CompressOnlyStreamWithMinimumLength = 0,
            byte p_CompressOnlyRatioToPercent = 100,
            int p_ChunkSizeOfStreamDataForCompress = ReadedChunkSizeBeforeCompressDefaultValue,
            int p_PreparedChunks = 1)
        {
            _CompressOnlyStreamWithMinimumLength = p_CompressOnlyStreamWithMinimumLength;
            _CompressOnlyRatioToPercent = p_CompressOnlyRatioToPercent;
            _PreparedChunks = p_PreparedChunks;
            _ReadedChunkSizeBeforeCompress = p_ChunkSizeOfStreamDataForCompress >
                                             ReadedChunkSizeBeforeCompressDefaultValue
                ? ReadedChunkSizeBeforeCompressDefaultValue
                : p_ChunkSizeOfStreamDataForCompress;
            _StreamDataForReading = p_StreamDataForReading ??
                                    throw new ArgumentNullException(nameof(p_StreamDataForReading));
            _Chunks = new List<Chunk>(p_PreparedChunks);
            for (var i = 0; i < p_PreparedChunks; i++)
#if log
                _Chunks.Add(new Chunk(i, new byte[_ReadedChunkSizeBeforeCompress], new MemoryStream(), Log));
#else
                _Chunks.Add(new Chunk(i, new byte[_ReadedChunkSizeBeforeCompress], new MemoryStream(), null));
#endif
            var compressModuleIdentifier = Encoding.UTF8.GetBytes(p_CompressModuleIdentifier);
            if (compressModuleIdentifier.Length > 0)
            {
                //Defined, must compress
                if (compressModuleIdentifier.Length != 4)
                    throw new ArgumentOutOfRangeException(nameof(p_CompressModuleIdentifier),
                        p_CompressModuleIdentifier,
                        "p_CompressModuleIdentifier must be 4 chars length");
                _CompressModule = new StreamReadModules().FindByHeaderIdentification(compressModuleIdentifier);
                if (_CompressModule == null)
                    throw new Exception(
                        $"StreamReadModule with HeaderIdentification = {Encoding.UTF8.GetString(compressModuleIdentifier)} not found");
            }

            _Chunks.ForEach(ch => ch.ReadDataAndStartCompressingInTask(_StreamDataForReading, _CompressModule,
                _ReadedChunkSizeBeforeCompress, _CompressOnlyStreamWithMinimumLength, _CompressOnlyRatioToPercent));
        }

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

        private Chunk ActiveChunk => _Chunks[_ChunkBufferIndexForRead];

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
            //http://faithlife.codes/blog/2012/06/always-wrap-gzipstream-with-bufferedstream/ - write buffer small

            if (buffer == null)
                throw new ArgumentNullException(nameof(buffer));
            if (offset != 0)
                throw new ArgumentOutOfRangeException(nameof(offset));
            if (count < 0)
                throw new ArgumentOutOfRangeException(nameof(count));
            if (buffer.Length - offset < count)
                throw new ArgumentException("Offset and length were out of bounds for the array");

            int readedBytesFromOriginalStream;
            bool chunkReadedToEnd;
            var result = ActiveChunk.BlockingReadFromCompressedChunk(buffer, count,
                _CompressModule.HeaderIdentificationBytes, out readedBytesFromOriginalStream, out chunkReadedToEnd);
            _Position += readedBytesFromOriginalStream;
#if log
            Log(
                $"Readed {result} B (OriginalStream {readedBytesFromOriginalStream}) from Chunk[{ChunkBufferIndexForRead}]. Position = {_Position}");
#endif
            if (result == 0)
            {
#if log
                _Chunks.ForEach(ch => Log($"INFO Chain[{ch._Key}] TotalBlockedTime={ch.TotalBlockedTime.Elapsed}"));
#endif
                return result;
            }

            if (chunkReadedToEnd)
            {
                ActiveChunk.ReadDataAndStartCompressingInTask(_StreamDataForReading, _CompressModule,
                    _ReadedChunkSizeBeforeCompress, _CompressOnlyStreamWithMinimumLength, _CompressOnlyRatioToPercent);
                SwitchToNextChunk();
            }
            return result;
        }

        private void SwitchToNextChunk()
        {
            _ChunkBufferIndexForRead++;
            if (_ChunkBufferIndexForRead >= _PreparedChunks)
                _ChunkBufferIndexForRead = 0;
#if log
            Log($"ActiveChunk = {ChunkBufferIndexForRead}");
#endif
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            throw new NotImplementedException();
        }

        protected override void Dispose(bool disposing)
        {
            _StreamDataForReading?.Dispose();
            _Chunks.ForEach(ch => ch._StreamCompressedData?.Dispose());
#if log
            Log("Dispose");
#endif
            base.Dispose(disposing);
#if log
            File.AppendAllText(@"C:\1\debug.txt", log.ToString());
#endif
        }

#if log
        private void Log(string p_Text)
        {
            lock (logLock)
            {
                var text =
                    $"{DateTime.Now:HH:mm:ss.ffffff} ; Thread{Thread.CurrentThread.ManagedThreadId.ToString().PadLeft(2, '0')} ; {p_Text}{Environment.NewLine}";
                log.Append(text);

                if (log.Length > 1024 * 1024)
                {
                    File.AppendAllText(@"C:\1\debug.txt", log.ToString());
                    log.Clear();
                }
            }
        }
#endif
    }
}