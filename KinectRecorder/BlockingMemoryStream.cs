using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace KinectRecorder
{
    public sealed class BlockingMemoryStream : Stream
    {
        private readonly MemoryStream underlyingStream = new MemoryStream();
        private readonly AutoResetEvent waitHandle = new AutoResetEvent(false);

        public int Timeout { get; set; }

        private bool bClosed = false;

        private long readPosition = 0;
        private long writePosition = 0;

        public override bool CanRead => underlyingStream.CanRead;

        public override bool CanSeek => underlyingStream.CanSeek;

        public override bool CanWrite => underlyingStream.CanWrite;

        public override long Length => underlyingStream.Length;

        public override long Position
        {
            get
            {
                return underlyingStream.Position;
            }

            set
            {
                underlyingStream.Position = value;
                readPosition = value;
                writePosition = value;
            }
        }

        public override void Flush() => underlyingStream.Flush();

        public override int Read(byte[] buffer, int offset, int count)
        {
            int bytes;

            while ((bytes = ReadFromUnderlyingStream(buffer, offset, count)) == 0)
            {
                // 0 bytes read (end of stream), wait Timeout ms for someone to write
                if (!waitHandle.WaitOne(Timeout))
                {
                    throw new TimeoutException();
                }

                if (bClosed)
                    return 0;
            }

            return bytes;
        }

        private int ReadFromUnderlyingStream(byte[] buffer, int offset, int count)
        {
            int bytes;

            underlyingStream.Position = readPosition;
            try
            {
                bytes = underlyingStream.Read(buffer, offset, count);
            }
            finally
            {
                readPosition = underlyingStream.Position;
            }

            return bytes;
        }

        public override long Seek(long offset, SeekOrigin origin) => underlyingStream.Seek(offset, origin);

        public override void SetLength(long value) => underlyingStream.SetLength(value);

        public override void Write(byte[] buffer, int offset, int count)
        {
            // Write to the stream and notify any waiting threads
            underlyingStream.Position = writePosition;
            underlyingStream.Write(buffer, offset, count);
            writePosition = underlyingStream.Position;

            waitHandle.Set();
        }

        public override void Close()
        {
            bClosed = true;
            waitHandle.Set();

            base.Close();
        }

        public BlockingMemoryStream()
        {
            Timeout = 5000;
        }
    }
}
