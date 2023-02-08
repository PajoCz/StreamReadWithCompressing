using System;
using System.Collections.Generic;
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
            LastReadUsedHeaderIdentification = null;

            byte[] intBytes = new byte[4];
            var headerIdentification = _StreamDataForReading.Read(intBytes, 0, intBytes.Length);
            _Position += headerIdentification;
            if (headerIdentification == 0) return 0;    //nothing in input stream
            var module = _StreamReadModules.FindByHeaderIdentification(intBytes);
            if (module == null)
            {
                //not compressed by known headerIdentification - only copy to output
                return ReadToBufferOriginalInput(buffer, count, new List<Tuple<byte[], int>>()
                {
                    new Tuple<byte[], int>(intBytes, headerIdentification)
                });
            }

            // Chunk header - Uncompressed and Compressed size
            byte[] intBytes2 = new byte[4];
            var readedUncompressedChunkSize = _StreamDataForReading.Read(intBytes2, 0, intBytes2.Length);
            _Position += readedUncompressedChunkSize;
            if (readedUncompressedChunkSize != 4)
            {
                return ReadToBufferOriginalInput(buffer, count, new List<Tuple<byte[], int>>()
                {
                    new Tuple<byte[], int>(intBytes, headerIdentification),
                    new Tuple<byte[], int>(intBytes2, readedUncompressedChunkSize)
                });
            }
            var uncompressedChunkSize = BitConverter.ToInt32(intBytes2, 0);

            byte[] intBytes3 = new byte[4];
            var readedCompressedChunkSize = _StreamDataForReading.Read(intBytes3, 0, intBytes3.Length);
            _Position += readedCompressedChunkSize;
            if (readedCompressedChunkSize != 4)
            {
                return ReadToBufferOriginalInput(buffer, count, new List<Tuple<byte[], int>>()
                {
                    new Tuple<byte[], int>(intBytes, headerIdentification),
                    new Tuple<byte[], int>(intBytes2, readedUncompressedChunkSize),
                    new Tuple<byte[], int>(intBytes3, readedCompressedChunkSize)
                });
            }
            var compressedChunkSize = BitConverter.ToInt32(intBytes3, 0);

            if (compressedChunkSize > _StreamDataForReading.Length)
            {
                return ReadToBufferOriginalInput(buffer, count, new List<Tuple<byte[], int>>()
                {
                    new Tuple<byte[], int>(intBytes, headerIdentification),
                    new Tuple<byte[], int>(intBytes2, readedUncompressedChunkSize),
                    new Tuple<byte[], int>(intBytes3, readedCompressedChunkSize)
                });
            }

            //Read Chunk data to _BufferCompressedData
            if (_BufferCompressedData.Length < compressedChunkSize)
                Array.Resize(ref _BufferCompressedData, compressedChunkSize);

            int readed = _StreamDataForReading.ReadMaybeMoreTimes(_BufferCompressedData, 0, compressedChunkSize);
            if (readed == 0)
            {
                return ReadToBufferOriginalInput(buffer, count, new List<Tuple<byte[], int>>()
                {
                    new Tuple<byte[], int>(intBytes, headerIdentification),
                    new Tuple<byte[], int>(intBytes2, readedUncompressedChunkSize),
                    new Tuple<byte[], int>(intBytes3, readedCompressedChunkSize)
                });
            }

            _StreamCompressedData.Position = 0;
            _StreamCompressedData.Write(_BufferCompressedData, 0, readed);
            _StreamCompressedData.Position = 0;

            LastReadUsedHeaderIdentification = module.HeaderIdentification;

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

        private int ReadToBufferOriginalInput(byte[] buffer, int count, List<Tuple<byte[],int>> prefixBytesReaded)
        {
            int byteArraysReaded = 0;
            foreach (var prefixByte in prefixBytesReaded)
            {
                Array.Copy(prefixByte.Item1, 0, buffer, byteArraysReaded, prefixByte.Item2);
                byteArraysReaded += prefixByte.Item2;
            }
            int readedOriginal = _StreamDataForReading.Read(buffer, byteArraysReaded, count - byteArraysReaded);
            _Position += readedOriginal;
            return readedOriginal + byteArraysReaded;
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            throw new NotImplementedException();
        }
    }
}