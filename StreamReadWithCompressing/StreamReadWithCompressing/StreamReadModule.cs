using System;
using System.IO;

namespace StreamReadWithCompressing
{
    public class StreamReadModule
    {
        public string HeaderIdentification { get; set; }
        public byte[] HeaderIdentificationBytes { get; set; }
        public Func<Stream, Stream> ActionCreateCompressStreamForWriting { get; set; }
        public Func<Stream, Stream> ActionCreateDecompressStreamForWriting { get; set; }
    }
}