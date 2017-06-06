using System;
using System.Collections.Generic;
using System.IO.Compression;
using System.Linq;
using System.Text;

namespace StreamReadWithCompressing
{
    public class StreamReadModules
    {
        /// <summary>
        ///     4 chars identifiers
        /// </summary>
        public const string HeaderIdentificationGzip = "gzip";

        public const string HeaderIdentificationDeflate = "defl";

        private readonly Dictionary<int, StreamReadModule> _Modules;

        public StreamReadModules()
        {
            _Modules = new List<StreamReadModule>
            {
                new StreamReadModule
                {
                    HeaderIdentification = HeaderIdentificationGzip,
                    HeaderIdentificationBytes = Encoding.UTF8.GetBytes(HeaderIdentificationGzip),
                    ActionCreateCompressStreamForWriting = inputStream => new GZipStream(inputStream, CompressionMode.Compress, true),
                    ActionCreateDecompressStreamForWriting = inputStream => new GZipStream(inputStream, CompressionMode.Decompress, true)
                },
                new StreamReadModule
                {
                    HeaderIdentification = HeaderIdentificationDeflate,
                    HeaderIdentificationBytes = Encoding.UTF8.GetBytes(HeaderIdentificationDeflate),
                    ActionCreateCompressStreamForWriting = inputStream => new DeflateStream(inputStream, CompressionMode.Compress, true),
                    ActionCreateDecompressStreamForWriting = inputStream => new DeflateStream(inputStream, CompressionMode.Decompress, true)
                }
            }.ToDictionary(m => BitConverter.ToInt32(Encoding.UTF8.GetBytes(m.HeaderIdentification), 0));
        }

        public StreamReadModule FindByHeaderIdentification(byte[] p_HeaderIdentification)
        {
            StreamReadModule module;
            if (_Modules.TryGetValue(BitConverter.ToInt32(p_HeaderIdentification, 0), out module))
                return module;
            return null;
        }
    }
}