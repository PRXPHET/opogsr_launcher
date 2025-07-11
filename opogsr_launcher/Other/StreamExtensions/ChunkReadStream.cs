using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace opogsr_launcher.Other.StreamExtensions
{
    public class ChunkReadStream : Stream
    {
        Stream _inner;
        long _chunk_size;

        long _position = 0;

        public ChunkReadStream(Stream inner, long chunk_size)
        {
            _inner = inner;
            _chunk_size = chunk_size;
        }

        public bool ReadNext()
        {
            if (_inner.Position < _inner.Length)
            {
                _position = 0;
                return true;
            }

            return false;
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            int bytesRead = _inner.Read(buffer, offset, Math.Min(count, (int)(_chunk_size - _position)));
            _position += bytesRead;
            return bytesRead;
        }

        public override long Length
        {
            get => Math.Min(_chunk_size, _inner.Length - _inner.Position);
        }

        public override long Position
        {
            get { return _position; }
            set { throw new NotSupportedException(); } // not seekable
        }

        public override bool CanRead { get { return true; } }
        public override bool CanSeek { get { return true; } }
        public override bool CanWrite { get { return false; } }
        public override void Flush() { }
        public override long Seek(long offset, SeekOrigin origin) { throw new NotSupportedException(); }
        public override void SetLength(long value) { throw new NotSupportedException(); }
        public override void Write(byte[] buffer, int offset, int count) { throw new NotSupportedException(); }
    }
}
