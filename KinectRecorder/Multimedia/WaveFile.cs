using System;
using System.IO;
using System.Runtime.InteropServices;

namespace KinectRecorder.Multimedia
{
    class WaveFile : IDisposable
    {
        [StructLayout(LayoutKind.Sequential)]
        struct WAVEHEADER
        {
            public uint dwRiff;                     // "RIFF"
            public uint dwSize;                     // Size
            public uint dwWave;                     // "WAVE"
            public uint dwFmt;                      // "fmt "
            public uint dwFmtSize;                  // Wave Format Size
        };

        const int SizeofWaveFormatEx = 18;

        WAVEHEADER WaveHeder;
        WAVEFORMATEX WaveFormatEx;

        public WaveFile(WAVEFORMATEX format)
        {
            WaveHeder = new WAVEHEADER();
            WaveHeder.dwRiff = BitConverter.ToUInt32(RIFF, 0);
            WaveHeder.dwWave = BitConverter.ToUInt32(WAVE, 0);
            WaveHeder.dwFmt = BitConverter.ToUInt32(fmt, 0);

            WaveFormatEx = format;
        }

        readonly byte[] RIFF = new byte[]
        {
            (byte)'R', (byte)'I', (byte)'F', (byte)'F',
        };

        readonly byte[] WAVE = new byte[]
        {
            (byte)'W', (byte)'A', (byte)'V', (byte)'E',
        };

        readonly byte[] fmt = new byte[]
        {
            (byte)'f', (byte)'m', (byte)'t', (byte)' ',
        };

        readonly byte[] data = new byte[]
        {
            (byte)'d', (byte)'a', (byte)'t', (byte)'a',
        };

        FileStream file;
        BinaryWriter binaryWriter;

        uint dataSize = 0;

        void WriteWaveHeader(uint dataSize)
        {
            binaryWriter.Seek(0, SeekOrigin.Begin);

            int waveHeaderSize = Marshal.SizeOf(WaveHeder) + SizeofWaveFormatEx + /*WaveFormatEx.cbSize +*/ data.Length + sizeof(uint);

            // Update the sizes in the header
            WaveHeder.dwSize = (uint)(waveHeaderSize + dataSize - (2 * sizeof(uint)));
            WaveHeder.dwFmtSize = (uint)(SizeofWaveFormatEx/* + WaveFormatEx.cbSize*/);

            // Write the file header
            binaryWriter.Write(WaveHeder.dwRiff);
            binaryWriter.Write(WaveHeder.dwSize);
            binaryWriter.Write(WaveHeder.dwWave);
            binaryWriter.Write(WaveHeder.dwFmt);
            binaryWriter.Write(WaveHeder.dwFmtSize);

            // Write the format
            binaryWriter.Write((ushort)WaveFormatEx.wFormatTag);
            binaryWriter.Write(WaveFormatEx.nChannels);
            binaryWriter.Write(WaveFormatEx.nSamplesPerSec);
            binaryWriter.Write(WaveFormatEx.nAvgBytesPerSec);
            binaryWriter.Write(WaveFormatEx.nBlockAlign);
            binaryWriter.Write(WaveFormatEx.wBitsPerSample);
            binaryWriter.Write(WaveFormatEx.cbSize);

            // Write the data header
            binaryWriter.Write(data);
            binaryWriter.Write(dataSize);
        }

        public void Open(string fileName)
        {
            file = new FileStream(fileName, FileMode.Create, FileAccess.Write);
            binaryWriter = new BinaryWriter(file);

            dataSize = 0;
            WriteWaveHeader(0);

            disposed = false;
        }

        public void Close()
        {
            Dispose();
        }

        public void Write(byte[] data)
        {
            if (binaryWriter == null)
            {
                return;
            }

            binaryWriter.Write(data);
            dataSize += (uint)data.Length;
        }

        // Flag: Has Dispose already been called?
        bool disposed = false;

        // Public implementation of Dispose pattern callable by consumers.
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        // Protected implementation of Dispose pattern.
        protected virtual void Dispose(bool disposing)
        {
            if (disposed)
            {
                return;
            }

            if (disposing)
            {
                // Free any other managed objects here.
                //

                if (binaryWriter != null)
                {
                    WriteWaveHeader(dataSize);
                    file.Dispose();
                    binaryWriter.Dispose();
                    binaryWriter = null;
                }
            }

            // Free any unmanaged objects here.
            //
            disposed = true;
        }
    }
}
