//#define log
using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace StreamReadWithCompressing
{
    public class Chunk
    {
        public readonly byte[] _BufferOriginalData;

        public readonly int _Key;
        private readonly Action<string> _LogAction;
        public readonly Stream _StreamCompressedData;
        public int _BufferOriginalDataLength;
        public int _BufferOriginalDataPosition;
        public ManualResetEvent _ManualResetEvent = new ManualResetEvent(false);
        public int _StreamCompressedDataLength;
        private StreamReadCompressSourceEnum StreamReadCompressSource;
        public Stopwatch TotalBlockedTime = new Stopwatch();

        public Chunk(int p_Key, byte[] p_BufferOriginalData, Stream p_StreamCompressedData, Action<string> p_LogAction)
        {
            _Key = p_Key;
            _BufferOriginalData = p_BufferOriginalData;
            _StreamCompressedData = p_StreamCompressedData;
            _LogAction = p_LogAction;
        }

#if log
        public void Log(string p_Text)
        {
            _LogAction($"Chunk[{_Key}] {p_Text}");
        }
#endif

        public int BlockingReadFromCompressedChunk(byte[] p_Buffer, int p_Count,
            byte[] p_CompressModuleHeaderIdentificationBytes, out int p_ReadedBytesFromOriginalStream)
        {
#if log
            Log("ManualResetEvent.WaitOne before");
#endif
            TotalBlockedTime.Start();
            _ManualResetEvent.WaitOne();
            TotalBlockedTime.Stop();
#if log
            Log("ManualResetEvent.WaitOne after");
#endif
            p_ReadedBytesFromOriginalStream = 0;
            var result = 0;

#if log
            if (StreamReadCompressSource == StreamReadCompressSourceEnum.None)
                Log("Read without data, returns 0");
#endif
            if (StreamReadCompressSource == StreamReadCompressSourceEnum.BufferOriginalData)
            {
                result = ProcessBufferOriginalData(p_Buffer, p_Count, out p_ReadedBytesFromOriginalStream);
#if log
                Log($"Read reads from original data {result} B");
#endif
            }

            if (StreamReadCompressSource == StreamReadCompressSourceEnum.StreamCompressedData)
            {
                result = ProcessStreamCompressedData(p_Buffer, p_Count, p_CompressModuleHeaderIdentificationBytes,
                    out p_ReadedBytesFromOriginalStream);
#if log
                Log($"Read reads from compressed data {result} B");
#endif
            }
            return result;
        }

        private int ProcessStreamCompressedData(byte[] buffer, int count,
            byte[] p_CompressModuleHeaderIdentificationBytes, out int p_ReadedBytesFromOriginalStream)
        {
            if (_StreamCompressedData.Position == 0)
            {
                //write header bytes
                Array.Copy(p_CompressModuleHeaderIdentificationBytes, 0, buffer, 0, 4);
                var bytes = BitConverter.GetBytes(_BufferOriginalDataLength);
                Array.Copy(bytes, 0, buffer, 4, 4);
                bytes = BitConverter.GetBytes(_StreamCompressedDataLength);
                Array.Copy(bytes, 0, buffer, 8, 4);
                //write compressed data to buffer
                var readedCompressed =
                    _StreamCompressedData.Read(buffer, 12, Math.Min(_StreamCompressedDataLength, count - 12));
                p_ReadedBytesFromOriginalStream = 12 + readedCompressed;
#if log
                Log($"ReadedCompressed 12B Header + {readedCompressed} B = {p_ReadedBytesFromOriginalStream} B");
#endif
                return 12 + readedCompressed;
            }
            var readedCompressed2 = _StreamCompressedData.Read(buffer, 0,
                Math.Min(_StreamCompressedDataLength - (int) _StreamCompressedData.Position, count));
            p_ReadedBytesFromOriginalStream = readedCompressed2;
#if log
            Log($"ReadedCompressed {p_ReadedBytesFromOriginalStream} B");
#endif

            return readedCompressed2;
        }

        private int ProcessBufferOriginalData(byte[] buffer, int count, out int p_ReadedBytesFromOriginalStream)
        {
            var copyCount = Math.Min(count, _BufferOriginalDataLength - _BufferOriginalDataPosition);
            Buffer.BlockCopy(_BufferOriginalData, _BufferOriginalDataPosition, buffer, 0, copyCount);
            _BufferOriginalDataPosition += copyCount;

            p_ReadedBytesFromOriginalStream = count;
#if log
            Log($"ReadedOriginal {p_ReadedBytesFromOriginalStream} B");
#endif

            return copyCount;
        }

        public void ReadDataAndStartCompressingInTask(Stream p_StreamDataForReading, StreamReadModule p_CompressModule,
            int p_ReadedChunkSizeBeforeCompress, int p_CompressOnlyStreamWithMinimumLength,
            int p_CompressOnlyRatioToPercent)
        {
#if log
            Log("ManualResetEvent.WaitOne Reset");
#endif
            _ManualResetEvent.Reset();
            var readedUncompressedChunkSize =
                p_StreamDataForReading.Read(_BufferOriginalData, 0, p_ReadedChunkSizeBeforeCompress);
#if log
            Log(
                $"Readed {readedUncompressedChunkSize} from StreamDataForReading (Position = {p_StreamDataForReading.Position})");
#endif
            Task.Factory.StartNew(() => CompressData(readedUncompressedChunkSize, p_CompressModule,
                p_CompressOnlyStreamWithMinimumLength, p_CompressOnlyRatioToPercent));
        }

        public void CompressData(int p_ReadedUncompressedChunkSize, StreamReadModule p_CompressModule,
            int p_CompressOnlyStreamWithMinimumLength, int p_CompressOnlyRatioToPercent)
        {
            if (p_ReadedUncompressedChunkSize == 0)
            {
                StreamReadCompressSource = StreamReadCompressSourceEnum.None;
#if log
                Log($"Compress[{_Key}] returns StreamReadCompressSource=None");
#endif
                ManualResetEventSetAndLog();
                return;
            }

            _BufferOriginalDataLength = p_ReadedUncompressedChunkSize;
            _BufferOriginalDataPosition = 0;

            //Check if compression needed            
            if (p_CompressModule == null || p_ReadedUncompressedChunkSize <= p_CompressOnlyStreamWithMinimumLength)
            {
#if log
                StreamReadCompressSource = StreamReadCompressSourceEnum.BufferOriginalData;
                Log(
                    $"Compress[{_Key}] returns StreamReadCompressSource=BufferOriginalData (CompressOnlyStreamWithMinimumLength)");
#endif
                ManualResetEventSetAndLog();
                return;
            }

            //Compression needed
            _StreamCompressedData.Position = 0;
            using (var streamCompressForWriting =
                p_CompressModule.ActionCreateCompressStreamForWriting(_StreamCompressedData))
            {
#if log
                Log("Compress begin");
#endif
                streamCompressForWriting.Write(_BufferOriginalData, 0, p_ReadedUncompressedChunkSize);
#if log
                Log("Compress end");
#endif
            }

            var compressRatioPercent = _StreamCompressedData.Position / (decimal) p_ReadedUncompressedChunkSize * 100m;
            var compressedLargerThanOriginal = _StreamCompressedData.Position + 12 > p_ReadedUncompressedChunkSize;
            if (compressedLargerThanOriginal || compressRatioPercent >= p_CompressOnlyRatioToPercent)
            {
                //Compressed data is larger then configurable limits, use original data
                StreamReadCompressSource = StreamReadCompressSourceEnum.BufferOriginalData;
#if log
                Log(
                    "Compress returns StreamReadCompressSource=BufferOriginalData (compressed larger then configurable limit)");
#endif
                ManualResetEventSetAndLog();
                return;
            }

            _StreamCompressedDataLength = (int) _StreamCompressedData.Position;
            _StreamCompressedData.Position = 0;
            StreamReadCompressSource = StreamReadCompressSourceEnum.StreamCompressedData;
#if log
            Log(
                $"Compress returns CompressedData ({_StreamCompressedDataLength} B) (First buffer of this chain will be {_StreamCompressedDataLength} B + 12 B Header={_StreamCompressedDataLength + 12} B)");
#endif

            ManualResetEventSetAndLog();
        }

        private void ManualResetEventSetAndLog()
        {
#if log
            Log("ManualResetEvent.Set");
#endif
            _ManualResetEvent.Set();
        }

        private enum StreamReadCompressSourceEnum
        {
            None,
            BufferOriginalData,
            StreamCompressedData
        }
    }
}