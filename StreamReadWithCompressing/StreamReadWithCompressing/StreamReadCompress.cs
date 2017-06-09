using System;
using System.IO;
using System.Text;

namespace StreamReadWithCompressing
{
    public class StreamReadCompress : Stream
    {
        // _DefaultCopyBufferSize - from MS implementation of Stream.CopyTo method - 80kB
        // We pick a value that is the largest multiple of 4096 that is still smaller than the large object heap threshold (85K).
        // The CopyTo/CopyToAsync buffer is short-lived and is likely to be collected at Gen0, and it offers a significant
        // improvement in Copy performance.
        public const int ReadedChunkSizeBeforeCompressDefaultValue = 80 * 1024;

        private readonly Stream _StreamDataForReading;

        private readonly byte[] _BufferOriginalData;
        private int _BufferOriginalDataLength;
        private int _BufferOriginalDataPosition;

        private readonly StreamReadModule _CompressModule;

        private readonly byte _CompressOnlyRatioToPercent;
        private readonly int _CompressOnlyStreamWithMinimumLength;
        private readonly int _ReadedChunkSizeBeforeCompress;

        private readonly Stream _StreamCompressedData;
        private int _StreamCompressedDataLength;

        private long _Position;
        private ReadStreamSource? _ReadStreamSource;

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
        public StreamReadCompress(Stream p_StreamDataForReading, string p_CompressModuleIdentifier, int p_CompressOnlyStreamWithMinimumLength = 0,
            byte p_CompressOnlyRatioToPercent = 100, int p_ChunkSizeOfStreamDataForCompress = ReadedChunkSizeBeforeCompressDefaultValue)
        {
            _CompressOnlyStreamWithMinimumLength = p_CompressOnlyStreamWithMinimumLength;
            _CompressOnlyRatioToPercent = p_CompressOnlyRatioToPercent;
            _ReadedChunkSizeBeforeCompress = p_ChunkSizeOfStreamDataForCompress > ReadedChunkSizeBeforeCompressDefaultValue
                ? ReadedChunkSizeBeforeCompressDefaultValue
                : p_ChunkSizeOfStreamDataForCompress;
            _StreamDataForReading = p_StreamDataForReading ?? throw new ArgumentNullException(nameof(p_StreamDataForReading));
            _StreamCompressedData = new MemoryStream();
            _BufferOriginalData = new byte[_ReadedChunkSizeBeforeCompress];
            byte[] compressModuleIdentifier = Encoding.UTF8.GetBytes(p_CompressModuleIdentifier);
            if (compressModuleIdentifier.Length > 0)
            {
                //Defined, must compress
                if (compressModuleIdentifier.Length != 4)
                    throw new ArgumentOutOfRangeException(nameof(p_CompressModuleIdentifier), p_CompressModuleIdentifier,
                        "p_CompressModuleIdentifier must be 4 chars length");
                _CompressModule = new StreamReadModules().FindByHeaderIdentification(compressModuleIdentifier);
                if (_CompressModule == null)
                    throw new Exception($"StreamReadModule with HeaderIdentification = {Encoding.UTF8.GetString(compressModuleIdentifier)} not found");
            }
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
            //http://faithlife.codes/blog/2012/06/always-wrap-gzipstream-with-bufferedstream/ - write buffer small

            if (buffer == null)
                throw new ArgumentNullException(nameof(buffer));
            if (offset != 0)
                throw new ArgumentOutOfRangeException(nameof(offset));
            if (count < 0)
                throw new ArgumentOutOfRangeException(nameof(count));
            if (buffer.Length - offset < count)
                throw new ArgumentException("Offset and length were out of bounds for the array");

            if (!CheckReadStreamSourceAndPrepareBufferData())
                return 0;

            if (_ReadStreamSource == ReadStreamSource.BufferOriginalData)
                return ProcessBufferOriginalData(buffer, count);

            if (_ReadStreamSource == ReadStreamSource.StreamCompressedData)
                return ProcessStreamCompressedData(buffer, count);

            throw new Exception("Unknown ReadStreamSource in internal algo");
        }

        private int ProcessStreamCompressedData(byte[] buffer, int count)
        {
            LastReadUsedHeaderIdentification = _CompressModule.HeaderIdentification;

            if (_StreamCompressedData.Position == 0)
            {
                //write header bytes
                Array.Copy(_CompressModule.HeaderIdentificationBytes, 0, buffer, 0, 4);
                byte[] bytes = BitConverter.GetBytes(_BufferOriginalDataLength);
                Array.Copy(bytes, 0, buffer, 4, 4);
                bytes = BitConverter.GetBytes(_StreamCompressedDataLength);
                Array.Copy(bytes, 0, buffer, 8, 4);
                //write compressed data to buffer
                var readedCompressed = _StreamCompressedData.Read(buffer, 12, Math.Min(_StreamCompressedDataLength, count - 12));
                _Position += 12 + readedCompressed;
                return 12 + readedCompressed;
            }
            var readedCompressed2 = _StreamCompressedData.Read(buffer, 0, Math.Min(_StreamCompressedDataLength - (int) _StreamCompressedData.Position, count));
            _Position += readedCompressed2;
            return readedCompressed2;
        }

        private int ProcessBufferOriginalData(byte[] buffer, int count)
        {
            LastReadUsedHeaderIdentification = null;

            var copyCount = Math.Min(count, _BufferOriginalDataLength - _BufferOriginalDataPosition);
            Buffer.BlockCopy(_BufferOriginalData, _BufferOriginalDataPosition, buffer, 0, copyCount);
            _BufferOriginalDataPosition += copyCount;
            return copyCount;
        }

        private bool CheckReadStreamSourceAndPrepareBufferData()
        {
            if (_ReadStreamSource == ReadStreamSource.BufferOriginalData && _BufferOriginalDataPosition < _BufferOriginalDataLength)
                return true;
            if (_ReadStreamSource == ReadStreamSource.StreamCompressedData && _StreamCompressedData.Position < _StreamCompressedDataLength)
                return true;

            var readedUncompressedChunkSize = _StreamDataForReading.Read(_BufferOriginalData, 0, _ReadedChunkSizeBeforeCompress);
            if (readedUncompressedChunkSize == 0) return false;

            _BufferOriginalDataLength = readedUncompressedChunkSize;
            _BufferOriginalDataPosition = 0;

            //Check if compression needed            
            if (_CompressModule == null || readedUncompressedChunkSize <= _CompressOnlyStreamWithMinimumLength)
            {
                _ReadStreamSource = ReadStreamSource.BufferOriginalData;
                return true;
            }

            //Compression needed
            _StreamCompressedData.Position = 0;
            using (var streamCompressForWriting = _CompressModule.ActionCreateCompressStreamForWriting(_StreamCompressedData))
            {
                streamCompressForWriting.Write(_BufferOriginalData, 0, readedUncompressedChunkSize);
            }

            var compressRatioPercent = _StreamCompressedData.Position / (decimal) readedUncompressedChunkSize * 100m;
            var compressedLargerThanOriginal = _StreamCompressedData.Position + 12 > readedUncompressedChunkSize;
            if (compressedLargerThanOriginal || compressRatioPercent >= _CompressOnlyRatioToPercent)
            {
                //Compressed data is larger then configurable limits, use original data
                _ReadStreamSource = ReadStreamSource.BufferOriginalData;
                return true;
            }

            _StreamCompressedDataLength = (int) _StreamCompressedData.Position;
            _StreamCompressedData.Position = 0;
            _ReadStreamSource = ReadStreamSource.StreamCompressedData;
            return true;
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

        private enum ReadStreamSource
        {
            BufferOriginalData,
            StreamCompressedData
        }
    }
}