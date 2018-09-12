using System;
using System.IO;
using System.IO.Compression;

namespace Shenmunity
{
    public class SubStream : Stream
    {
        Stream m_stream;
        long m_base;
        long m_length;

        public SubStream(Stream stream, long offset, long length)
        {
            m_stream = stream;
            m_length = length;
            m_base = offset;
            Seek(0, SeekOrigin.Begin);
        }

        public override bool CanRead
        {
            get
            {
                return m_stream.CanRead;
            }
        }

        public override bool CanSeek
        {
            get
            {
                return m_stream.CanSeek;
            }
        }

        public override bool CanWrite
        {
            get
            {
                return m_stream.CanWrite;
            }
        }
        public override long Length
        {
            get
            {
                return m_length;
            }
        }

        public override long Position
        {
            get
            {
                return m_stream.Position - m_base;
            }
            set
            {
                Seek(value, SeekOrigin.Begin);
            }
        }

        public override void Flush()
        {
            m_stream.Flush();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            return m_stream.Read(buffer, offset, count);
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            switch(origin)
            {
                case SeekOrigin.Begin:
                    return m_stream.Seek(m_base + offset, origin) - m_base;
                case SeekOrigin.Current:
                    return m_stream.Seek(offset, origin) - m_base;
                case SeekOrigin.End:
                    return m_stream.Seek(offset + Length + m_base, SeekOrigin.Begin) - m_base;
            }
            throw new NotSupportedException();
        }

        public override void SetLength(long value)
        {
            throw new NotSupportedException();
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            m_stream.Write(buffer, offset, count);
        }
    }
}