using System;
using System.IO;
using System.IO.Compression;

namespace Shenmunity
{
    public class GzipWithSeek : Stream
    {
        long _Position;
        byte[] _Bytes = new byte[256];
        GZipStream m_gzip;
        long m_start;
        Stream m_stream;

        public GzipWithSeek(Stream compressedStream, CompressionMode mode)
        {
            if (mode != CompressionMode.Decompress)
                throw new NotSupportedException();

            m_start = compressedStream.Position;
            m_stream = compressedStream;
            RestartStream();
        }
        public GzipWithSeek(Stream compressedStream, CompressionMode mode, bool leaveOpen)
        {
            if (mode != CompressionMode.Decompress)
                throw new NotSupportedException();

            m_start = compressedStream.Position;
            m_stream = compressedStream;
            RestartStream();
        }

        void RestartStream()
        {
            m_stream.Seek(m_start, SeekOrigin.Begin);
            m_gzip = new GZipStream(m_stream, CompressionMode.Decompress);
            _Position = 0;
        }

        public override bool CanWrite { get { return false; } }
        public override bool CanSeek { get { return true; } }
        public override bool CanRead { get { return true; } }
        public override long Length { get { return 0; } }
        public override long Position { get { return _Position; } set { throw new NotSupportedException(); } }

        public override IAsyncResult BeginRead(byte[] buffer, int offset, int count, AsyncCallback cback, object state)
        {
            return m_gzip.BeginRead(buffer, offset, count, cback, state);
        }
        public override IAsyncResult BeginWrite(byte[] buffer, int offset, int count, AsyncCallback cback, object state)
        {
            throw new NotSupportedException();
        }
        public override int EndRead(IAsyncResult async_result)
        {
            int read = m_gzip.EndRead(async_result);
            _Position += read;
            return read;
        }

        public override void EndWrite(IAsyncResult async_result)
        {
            throw new NotSupportedException();
        }

        public override void Flush()
        {
            m_gzip.Flush();
        }
        public override int Read(byte[] dest, int dest_offset, int count)
        {
            int read = m_gzip.Read(dest, dest_offset, count);
            _Position += read;
            return read;
        }
        public override long Seek(long offset, SeekOrigin origin)
        {
            switch(origin)
            {
                case SeekOrigin.Begin:
                    Advance(offset - _Position);
                    break;
                case SeekOrigin.Current:
                    Advance(offset);
                    break;
                case SeekOrigin.End:
                    throw new NotSupportedException();
            }
            return _Position;
        }

        void Advance(long dist)
        {
            if (dist < 0)
            {
                long target = _Position + dist;
                RestartStream();
                Advance(target);
                return;
            }
            while(dist > 0)
            {
                long adv = System.Math.Min(dist, _Bytes.Length);
                dist -= Read(_Bytes, 0, (int)adv);
            }
        }

        public override void SetLength(long value)
        {
            throw new NotSupportedException();
        }
        public override void Write(byte[] src, int src_offset, int count)
        {
            throw new NotSupportedException();
        }
        protected override void Dispose(bool disposing)
        {
            m_gzip.Dispose();
        }
    }
}