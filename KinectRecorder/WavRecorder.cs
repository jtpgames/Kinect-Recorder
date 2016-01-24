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

    public class WavRecorder
    {
        struct WAVEFORMATEX
        {
            public ushort wFormatTag;
            public ushort nChannels;
            public uint nSamplesPerSec;
            public uint nAvgBytesPerSec;
            public ushort nBlockAlign;
            public ushort wBitsPerSample;
            public ushort cbSize;
        }

        private CancellationTokenSource cancelToken;
        private CancellationTokenRegistration cancelRegistration;

        private int totalBytesRecorded = 0;

        public WavRecorder()
        {

        }

        public async Task RecordAsync(string filename, Stream audioStream)
        {
            cancelToken = new CancellationTokenSource();
            cancelRegistration = cancelToken.Token.Register(() => audioStream.Close());

            using (var fs = new FileStream(filename, FileMode.Create))
            {
                // Write header with data lenght 0 --> will be corrected at the end by FinalizeWav.
                WriteWavHeader(fs, 0);

                // Simply copy the data from the stream down to the file
                byte[] buffer = new byte[1024];
                int count;
                try
                {
                    while ((count = await audioStream.ReadAsync(buffer, 0, buffer.Length)) > 0)
                    {
                        fs.Write(buffer, 0, count);
                        totalBytesRecorded += count;
                    }
                }
                catch (Exception e)
                {
                    Console.WriteLine("Ignoring " + e.Message);
                }
                finally
                {
                    FinalizeWav(fs);
                }
            }
        }

        public void StopRecording()
        {
            cancelToken.Cancel();
        }

        private void FinalizeWav(FileStream fs)
        {
            // Rewind stream position and overwrite header with the actual data lenght
            fs.Seek(0, SeekOrigin.Begin);
            WriteWavHeader(fs, totalBytesRecorded);
        }

        private void WriteString(Stream stream, string s)
        {
            byte[] bytes = Encoding.ASCII.GetBytes(s);
            stream.Write(bytes, 0, bytes.Length);
        }

        /// <summary>
        /// A bare bones WAV file header writer
        /// </summary>        
        private void WriteWavHeader(Stream stream, int dataLength)
        {
            // We need to use a memory stream because the BinaryWriter will close the underlying stream when it is closed
            using (var memStream = new MemoryStream(64))
            {
                int cbFormat = 18; //sizeof(WAVEFORMATEX)
                WAVEFORMATEX format = new WAVEFORMATEX()
                {
                    wFormatTag = 1,
                    nChannels = 1,
                    nSamplesPerSec = 16000,
                    nAvgBytesPerSec = 32000,
                    nBlockAlign = 2,
                    wBitsPerSample = 16,
                    cbSize = 0
                };

                using (var bw = new BinaryWriter(memStream))
                {
                    //RIFF header
                    WriteString(memStream, "RIFF");
                    bw.Write(dataLength + cbFormat + 4); //File size - 8
                    WriteString(memStream, "WAVE");
                    WriteString(memStream, "fmt ");
                    bw.Write(cbFormat);

                    //WAVEFORMATEX
                    bw.Write(format.wFormatTag);
                    bw.Write(format.nChannels);
                    bw.Write(format.nSamplesPerSec);
                    bw.Write(format.nAvgBytesPerSec);
                    bw.Write(format.nBlockAlign);
                    bw.Write(format.wBitsPerSample);
                    bw.Write(format.cbSize);

                    //data header
                    WriteString(memStream, "data");
                    bw.Write(dataLength);
                    memStream.WriteTo(stream);
                }
            }
        }
    }
}
