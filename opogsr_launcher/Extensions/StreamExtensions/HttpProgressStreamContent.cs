using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace opogsr_launcher.Extensions.StreamExtensions
{
    public class HttpProgressStreamContent : HttpContent
    {
        private readonly Stream _stream;
        private readonly IProgress<(ulong Sent, ulong Total)>? _progress;
        private const int BufferSize = 32768;

        public HttpProgressStreamContent(Stream stream, IProgress<(ulong Sent, ulong Total)>? progress = null)
        {
            _stream = stream ?? throw new ArgumentNullException(nameof(stream));
            _progress = progress;
        }

        protected override async Task SerializeToStreamAsync(Stream stream, TransportContext? context)
        {
            long total_bytes = _stream.Length;
            long bytes_sent = 0;
            byte[] buffer = new byte[BufferSize];
            int bytesRead;

            while ((bytesRead = await _stream.ReadAsync(buffer, 0, buffer.Length)) > 0)
            {
                await stream.WriteAsync(buffer, 0, bytesRead);
                bytes_sent += bytesRead;
                _progress?.Report((Convert.ToUInt64(bytes_sent), Convert.ToUInt64(total_bytes)));
            }
        }

        protected override bool TryComputeLength(out long length)
        {
            length = _stream.Length;
            return true;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _stream.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
