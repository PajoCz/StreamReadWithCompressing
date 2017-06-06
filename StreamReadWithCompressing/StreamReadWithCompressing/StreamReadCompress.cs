using System;
using System.IO;
using System.Text;

namespace StreamReadWithCompressing
{
    public class StreamReadCompress : Stream
    {
        private readonly byte[] _CompressModuleIdentifier;

        private readonly byte _CompressOnlyRatioToPercent;

        private readonly int _CompressOnlyStreamWithMinimumLength;

        //private readonly Func<Stream, StreamWithHeaderIdentification> _CreateNewCompressWriteStream;
        private readonly Stream _StreamCompressedData;

        private readonly Stream _StreamDataForReading;

        private long _Position;
        public StreamReadModules StreamReadModules;

        /// <summary>
        /// </summary>
        /// <param name="p_StreamDataForReading"></param>
        /// <param name="p_CompressModuleIdentifier"></param>
        /// <param name="p_CompressOnlyStreamWithMinimumLength"></param>
        /// <param name="p_CompressOnlyRatioToPercent">
        ///     Calculate Compress/Decompress*100. Compress only if calculated value is
        ///     smaller than this setting
        /// </param>
        public StreamReadCompress(Stream p_StreamDataForReading, string p_CompressModuleIdentifier, int p_CompressOnlyStreamWithMinimumLength = 0,
            byte p_CompressOnlyRatioToPercent = 100)
        {
            _CompressOnlyStreamWithMinimumLength = p_CompressOnlyStreamWithMinimumLength;
            _CompressOnlyRatioToPercent = p_CompressOnlyRatioToPercent;
            _StreamDataForReading = p_StreamDataForReading ?? throw new ArgumentNullException(nameof(p_StreamDataForReading));
            _StreamCompressedData = new MemoryStream();
            _CompressModuleIdentifier = Encoding.UTF8.GetBytes(p_CompressModuleIdentifier);
            if (_CompressModuleIdentifier.Length != 4)
                throw new ArgumentOutOfRangeException(nameof(p_CompressModuleIdentifier), p_CompressModuleIdentifier,
                    "p_CompressModuleIdentidier must be 4 chars length");
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

            var readedUncompressedChunkSize = _StreamDataForReading.Read(buffer, 0, count);
            if (readedUncompressedChunkSize == 0) return 0;
            if (readedUncompressedChunkSize <= _CompressOnlyStreamWithMinimumLength)
                return readedUncompressedChunkSize;
            //cleaned only if any data. Last read always reads empty buffer - it means no another data
            LastReadUsedHeaderIdentification = null;

            //http://faithlife.codes/blog/2012/06/always-wrap-gzipstream-with-bufferedstream/ - Read buffer small
            _StreamCompressedData.Position = 0;
            var module = StreamReadModules.FindByHeaderIdentification(_CompressModuleIdentifier);
            if (module == null)
                throw new Exception($"StreamReadModule with HeaderIdentification = {Encoding.UTF8.GetString(_CompressModuleIdentifier)} not found");
            using (var streamCompressForWriting = module.ActionCreateCompressStreamForWriting(_StreamCompressedData))
            {
                streamCompressForWriting.Write(buffer, 0, readedUncompressedChunkSize);
            }
            var writtenCompressed = (int) _StreamCompressedData.Position;

            var compressRatioPercent = _StreamCompressedData.Position / (decimal) readedUncompressedChunkSize * 100m;
            var compressedLargerThanOriginal = writtenCompressed + 12 > count;
            if (compressedLargerThanOriginal || compressRatioPercent >= _CompressOnlyRatioToPercent)
                return readedUncompressedChunkSize;

            //write _StreamCompressedData with any headers info to buffer
            _StreamCompressedData.Position = 0;

            //write header bytes
            Array.Copy(module.HeaderIdentificationBytes, 0, buffer, 0, 4);
            LastReadUsedHeaderIdentification = module.HeaderIdentification;
            byte[] bytes = BitConverter.GetBytes(readedUncompressedChunkSize);
            Array.Copy(bytes, 0, buffer, 4, 4);
            bytes = BitConverter.GetBytes(writtenCompressed);
            Array.Copy(bytes, 0, buffer, 8, 4);
            //write compressed data to buffer
            var readedCompressed = _StreamCompressedData.Read(buffer, 12, writtenCompressed);

            _Position += 12 + readedCompressed;
            return 12 + readedCompressed;
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            throw new NotImplementedException();
        }

        protected override void Dispose(bool disposing)
        {
            _StreamDataForReading?.Dispose();
            _StreamCompressedData?.Dispose();
            base.Dispose(disposing);
        }
    }
}