using System.IO;

namespace StreamReadWithCompressing
{
    public static class StreamReadHelper
    {
        public static int ReadMaybeMoreTimes(this Stream p_StreamDataForReading, byte[] p_Buffer, int p_PreviouslyReadedToBuffer, int p_TotalCount)
        {
            var readed = p_PreviouslyReadedToBuffer;
            var readedNow = int.MaxValue;
            while (readed < p_TotalCount && readedNow > 0)
            {
                readedNow = p_StreamDataForReading.Read(p_Buffer, readed, p_TotalCount - readed);
                readed += readedNow;
            }
            return readed;
        }
    }
}