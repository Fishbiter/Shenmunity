using System;
using System.IO;
using System.IO.Compression;

namespace Shenmunity
{
    public class DebugStream : Stream
    {
        Stream m_stream;

        public DebugStream(Stream stream)
        {
            m_stream = stream;
        }

        string Peek()
        {
            long pos = Position;
            string str;
            using (var br = new BinaryReader(m_stream))
            {
                str = BitConverter.ToString(br.ReadBytes(64));
            }
            Seek(pos, SeekOrigin.Begin);
            return str;
        }

        string PeekBehind()
        {
            long pos = Position;
            Seek(-64, SeekOrigin.Current);
            string str;
            using (var br = new BinaryReader(m_stream))
            {
                str = BitConverter.ToString(br.ReadBytes(64));
            }
            Seek(pos, SeekOrigin.Begin);
            return str;
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
                return m_stream.Length;
            }
        }

        public override long Position
        {
            get
            {
                return m_stream.Position;
            }
            set
            {
                m_stream.Position = value;
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
            return m_stream.Seek(offset, origin);
        }

        public override void SetLength(long value)
        {
            m_stream.SetLength(value);
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            m_stream.Write(buffer, offset, count);
        }
    }
}