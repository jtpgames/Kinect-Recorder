using SharpDX;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

using MF = SharpDX.MediaFoundation;

namespace KinectRecorder.Multimedia
{
    public unsafe class MemoryMappedTexture32bpp : IDisposable
    {
        #region The native structure, where we store all ObjectIDs uploaded from graphics hardware
        private IntPtr m_pointer;
        private int* m_pointerNative;
        private Size2 m_size;
        private int m_countInts;
        #endregion

        /// <summary>
        /// Initializes a new instance of the <see cref="MemoryMappedTexture32bpp"/> class.
        /// </summary>
        /// <param name="size">The total size of the texture.</param>
        public MemoryMappedTexture32bpp(Size2 size)
        {
            m_pointer = Marshal.AllocHGlobal(size.Width * size.Height * 4);
            m_pointerNative = (int*)m_pointer.ToPointer();
            m_size = size;
            m_countInts = m_size.Width * m_size.Height;
        }

        /// <summary>
        /// Converts the underlying buffer to a managed byte array.
        /// </summary>
        public byte[] ToArray()
        {
            byte[] result = new byte[this.SizeInBytes];
            Marshal.Copy(m_pointer, result, 0, (int)this.SizeInBytes);
            return result;
        }

        /// <summary>
        /// Führt anwendungsspezifische Aufgaben aus, die mit dem Freigeben, Zurückgeben oder Zurücksetzen von nicht verwalteten Ressourcen zusammenhängen.
        /// </summary>
        public void Dispose()
        {
            Marshal.FreeHGlobal(m_pointer);
            m_pointer = IntPtr.Zero;
            m_pointerNative = (int*)0;
            m_size = new Size2(0, 0);
        }

        /// <summary>
        /// Gets the value at the given (pixel) location.
        /// </summary>
        /// <param name="xPos">The x position.</param>
        /// <param name="yPos">The y position.</param>
        public int GetValue(int xPos, int yPos)
        {
            return m_pointerNative[xPos + (yPos * m_size.Width)];
        }

        /// <summary>
        /// Sets all alpha values to one.
        /// </summary>
        public void SetAllAlphaValuesToOne_ARGB()
        {
            uint alphaByteValue = 0xFF000000;
            uint* pointerUInt = (uint*)m_pointerNative;
            for (int loopIndex = 0; loopIndex < m_countInts; loopIndex++)
            {
                pointerUInt[loopIndex] |= alphaByteValue;
            }
        }

        public void FillWith(IEnumerable<byte> data)
        {
            FillWith(data.ToArray());
        }

        public void FillWith(byte[] data)
        {
            Marshal.Copy(data, 0, m_pointer, data.Length);
        }

        /// <summary>
        /// Gets the total size of the buffer in bytes.
        /// </summary>
        public uint SizeInBytes
        {
            get
            {
                return (uint)(m_size.Width * m_size.Height * 4);
            }
        }

        public int CountInts
        {
            get { return m_countInts; }
        }

        /// <summary>
        /// Gets the width of the buffer.
        /// </summary>
        public int Width
        {
            get { return m_size.Width; }
        }

        /// <summary>
        /// Gets the pitch of the underlying texture data.
        /// (pitch = stride, see https://msdn.microsoft.com/en-us/library/windows/desktop/aa473780(v=vs.85).aspx )
        /// </summary>
        public int Pitch
        {
            get { return m_size.Width * 4; }
        }

        /// <summary>
        /// Gets the pitch of the underlying texture data.
        /// (pitch = stride, see https://msdn.microsoft.com/en-us/library/windows/desktop/aa473780(v=vs.85).aspx )
        /// </summary>
        public int Stride
        {
            get { return m_size.Width * 4; }
        }

        /// <summary>
        /// Gets the height of the buffer.
        /// </summary>
        public int Height
        {
            get { return m_size.Height; }
        }

        /// <summary>
        /// Gets the pixel size of this texture.
        /// </summary>
        public Size2 PixelSize
        {
            get
            {
                return m_size;
            }
        }

        /// <summary>
        /// Gets the pointer of the buffer.
        /// </summary>
        public IntPtr Pointer
        {
            get
            {
                if (m_pointer == IntPtr.Zero) { throw new ObjectDisposedException("MemoryMappedTextureFloat"); }
                return m_pointer;
            }
        }
    }

    /// <summary>
    /// A helper class containing utility methods used when working with Media Foundation.
    /// Source: https://github.com/RolandKoenig/SeeingSharp/blob/master/SeeingSharp.Multimedia/Core/_Util/MFHelper.cs
    /// </summary>
    internal class MFHelper
    {
        /// <summary>
        /// Gets the Guid from the given type.
        /// </summary>
        /// <typeparam name="T">The type to get the guid from.</typeparam>
        internal static Guid GetGuidOf<T>()
        {
            GuidAttribute guidAttribute = typeof(T).GetTypeInfo().GetCustomAttribute<GuidAttribute>();
            if (guidAttribute == null)
            {
                throw new Exception(string.Format("No Guid found on type {0}!", typeof(T).FullName));
            }

            return new Guid(guidAttribute.Value);
        }

        /// <summary>
        /// Builds a Guid for a video subtype for the given format id (see MFRawFormats).
        /// </summary>
        /// <param name="rawFormatID">The raw format id.</param>
        internal static Guid BuildVideoSubtypeGuid(int rawFormatID)
        {
            return new Guid(
                rawFormatID,
                0x0000, 0x0010, 0x80, 0x00, 0x00, 0xaa, 0x00, 0x38, 0x9b, 0x71);
        }

        /// <summary>
        /// Helper function that builds the Guid for a video subtype using the given FOURCC value
        /// </summary>
        /// <param name="fourCCString">The FOURCC string to convert to a guid.</param>
        internal static Guid BuildVideoSubtypeGuid(string fourCCString)
        {
            return new Guid(
                GetFourCCValue(fourCCString),
                0x0000, 0x0010, 0x80, 0x00, 0x00, 0xaa, 0x00, 0x38, 0x9b, 0x71);
        }

        /// <summary>
        /// Gets the FourCC value for the given string.
        /// More infos about FourCC:
        ///  see: http://msdn.microsoft.com/en-us/library/windows/desktop/bb970509(v=vs.85).aspx,
        ///  see: http://msdn.microsoft.com/en-us/library/windows/desktop/aa370819(v=vs.85).aspx#creating_subtype_guids_from_fourccs_and_d3dformat_values,
        ///  see: http://de.wikipedia.org/wiki/FourCC
        /// </summary>
        /// <param name="fourCCString">The FourCC string to be converted into an unsigned integer value.</param>
#if DESKTOP
        internal static uint GetFourCCValue(string fourCCString)
#else
        internal static int GetFourCCValue(string fourCCString)
#endif
        {
            if (string.IsNullOrEmpty(fourCCString)) { throw new ArgumentNullException("subtype"); }
            if (fourCCString.Length > 4) { throw new ArgumentException("Given value too long!"); }

            // Build fcc value
            byte[] asciiBytes = Encoding.UTF8.GetBytes(fourCCString);
            byte[] fccValueBytes = new byte[4];
            for (int loop = 0; loop < 4; loop++)
            {
                if (asciiBytes.Length > loop) { fccValueBytes[loop] = asciiBytes[loop]; }
                else { fccValueBytes[loop] = 0x20; }
            }

            // Return guid
#if DESKTOP
            return BitConverter.ToUInt32(fccValueBytes, 0);
#else 
            return BitConverter.ToInt32(fccValueBytes, 0);
#endif
        }

        /// <summary>
        /// Encodes the given values to a single long.
        /// Example usage: Size attribute.
        /// </summary>
        /// <param name="valueA">The first value.</param>
        /// <param name="valueB">The second value.</param>
        internal static long GetMFEncodedIntsByValues(int valueA, int valueB)
        {
            byte[] valueXBytes = BitConverter.GetBytes(valueA);
            byte[] valueYBytes = BitConverter.GetBytes(valueB);

            byte[] resultBytes = new byte[8];
            if (BitConverter.IsLittleEndian)
            {
                resultBytes[0] = valueYBytes[0];
                resultBytes[1] = valueYBytes[1];
                resultBytes[2] = valueYBytes[2];
                resultBytes[3] = valueYBytes[3];
                resultBytes[4] = valueXBytes[0];
                resultBytes[5] = valueXBytes[1];
                resultBytes[6] = valueXBytes[2];
                resultBytes[7] = valueXBytes[3];
            }
            else
            {
                resultBytes[0] = valueXBytes[0];
                resultBytes[1] = valueXBytes[1];
                resultBytes[2] = valueXBytes[2];
                resultBytes[3] = valueXBytes[3];
                resultBytes[4] = valueYBytes[0];
                resultBytes[5] = valueYBytes[1];
                resultBytes[6] = valueYBytes[2];
                resultBytes[7] = valueYBytes[3];
            }

            return BitConverter.ToInt64(resultBytes, 0);
        }

        /// <summary>
        /// Decodes two integer values from the given long.
        /// Example usage: Size attribute.
        /// </summary>
        /// <param name="encodedInts">The long containing both encoded ints.</param>
        internal static Tuple<int, int> GetValuesByMFEncodedInts(long encodedInts)
        {
            byte[] rawBytes = BitConverter.GetBytes(encodedInts);

            if (BitConverter.IsLittleEndian)
            {
                return Tuple.Create(
                    BitConverter.ToInt32(rawBytes, 4),
                    BitConverter.ToInt32(rawBytes, 0));
            }
            else
            {
                return Tuple.Create(
                    BitConverter.ToInt32(rawBytes, 0),
                    BitConverter.ToInt32(rawBytes, 4));
            }
        }

        /// <summary>
        /// Converts the given duration value from media foundation to a TimeSpan structure.
        /// </summary>
        /// <param name="durationLong">The duration value.</param>
        internal static TimeSpan DurationLongToTimeSpan(long durationLong)
        {
            return TimeSpan.FromMilliseconds(durationLong / 10000);
        }

        /// <summary>
        /// Converts the given TimeSpan value to a duration value for media foundation
        /// </summary>
        /// <param name="timespan">The timespan.</param>
        internal static long TimeSpanToDurationLong(TimeSpan timespan)
        {
            return (long)(timespan.TotalMilliseconds * 10000);
        }
    }

    abstract class MediaFoundationVideoWriter : IDisposable
    {
        private static readonly Guid VIDEO_INPUT_FORMAT = MF.VideoFormatGuids.Rgb32;

        private static bool MFInitialized = false;

        #region Configuration
        private int bitrate;
        private int framerate;
        #endregion

        #region Resources for MediaFoundation video rendering
        private Stream inputStream;
        private MF.ByteStream outStream;
        private MF.SinkWriter sinkWriter;
        private SharpDX.Size2 videoPixelSize;
        private int frameIndex;
        private int streamIndex;
        #endregion

        public MediaFoundationVideoWriter(string filePath, Size2 videoPixelSize)
            : this(filePath, videoPixelSize, VIDEO_INPUT_FORMAT)
        {

        }

        public MediaFoundationVideoWriter(string filePath, Size2 videoPixelSize, Guid videoInputFormat)
        {
            bitrate = 1500000;
            framerate = 15;

            if (!MFInitialized)
            {
                // Initialize MF library
                MF.MediaFactory.Startup(MF.MediaFactory.Version, 0);
            }

            sinkWriter = MF.MediaFactory.CreateSinkWriterFromURL(filePath, IntPtr.Zero, null);

            this.videoPixelSize = videoPixelSize;
            CreateMediaTarget(sinkWriter, videoPixelSize, out streamIndex);

            // Configure input
            using (MF.MediaType mediaTypeIn = new MF.MediaType())
            {
                mediaTypeIn.Set<Guid>(MF.MediaTypeAttributeKeys.MajorType, MF.MediaTypeGuids.Video);
                mediaTypeIn.Set<Guid>(MF.MediaTypeAttributeKeys.Subtype, videoInputFormat);
                mediaTypeIn.Set<int>(MF.MediaTypeAttributeKeys.InterlaceMode, (int)MF.VideoInterlaceMode.Progressive);
                mediaTypeIn.Set<long>(MF.MediaTypeAttributeKeys.FrameSize, MFHelper.GetMFEncodedIntsByValues(videoPixelSize.Width, videoPixelSize.Height));
                mediaTypeIn.Set<long>(MF.MediaTypeAttributeKeys.FrameRate, MFHelper.GetMFEncodedIntsByValues(framerate, 1));
                sinkWriter.SetInputMediaType(streamIndex, mediaTypeIn, null);
            }

            // Start writing the video file
            sinkWriter.BeginWriting();

            // Set initial frame index
            frameIndex = -1;
        }

        public void AddFrame(MemoryMappedTexture32bpp texture)
        {
            frameIndex++;
            MF.MediaBuffer mediaBuffer = MF.MediaFactory.CreateMemoryBuffer((int)texture.SizeInBytes);

            // Write all contents to the MediaBuffer for media foundation
            int cbMaxLength = 0;
            int cbCurrentLength = 0;
            IntPtr mediaBufferPointer = mediaBuffer.Lock(out cbMaxLength, out cbCurrentLength);

            try
            {
                if (FlipY)
                {
                    unsafe
                    {
                        int stride = videoPixelSize.Width;
                        int* mediaBufferPointerNative = (int*)mediaBufferPointer.ToPointer();
                        int* targetBufferPointerNative = (int*)texture.Pointer.ToPointer();
                        for (int loopY = 0; loopY < videoPixelSize.Height; loopY++)
                        {
                            for (int loopX = 0; loopX < videoPixelSize.Width; loopX++)
                            {
                                int actIndexTarget = loopX + (loopY * videoPixelSize.Width);
                                int actIndexSource = loopX + ((videoPixelSize.Height - (1 + loopY)) * videoPixelSize.Width);
                                mediaBufferPointerNative[actIndexTarget] = targetBufferPointerNative[actIndexSource];
                            }
                        }
                    }
                }
                else
                {
                    unsafe
                    {
                        int stride = videoPixelSize.Width;
                        int* mediaBufferPointerNative = (int*)mediaBufferPointer.ToPointer();
                        int* targetBufferPointerNative = (int*)texture.Pointer.ToPointer();
                        for (int loopY = 0; loopY < videoPixelSize.Height; loopY++)
                        {
                            for (int loopX = 0; loopX < videoPixelSize.Width; loopX++)
                            {
                                int actIndex = loopX + (loopY * videoPixelSize.Width);
                                mediaBufferPointerNative[actIndex] = targetBufferPointerNative[actIndex];
                            }
                        }
                    }
                }
            }
            finally
            {
                mediaBuffer.Unlock();
                mediaBuffer.CurrentLength = (int)texture.SizeInBytes;
            }


            // Create the sample (includes image and timing information)
            MF.Sample sample = MF.MediaFactory.CreateSample();
            try
            {
                sample.AddBuffer(mediaBuffer);

                long frameDuration = 10 * 1000 * 1000 / framerate;
                sample.SampleTime = frameDuration * frameIndex;
                sample.SampleDuration = frameDuration;

                sinkWriter.WriteSample(streamIndex, sample);
            }
            catch (SharpDXException e)
            {
                Debug.WriteLine(e.Message);
            }
            finally
            {
                sample.Dispose();
            }

            mediaBuffer.Dispose();
        }

        /// <summary>
        /// Creates a media target.
        /// </summary>
        /// <param name="sinkWriter">The previously created SinkWriter.</param>
        /// <param name="videoPixelSize">The pixel size of the video.</param>
        /// <param name="streamIndex">The stream index for the new target.</param>
        protected abstract void CreateMediaTarget(MF.SinkWriter sinkWriter, SharpDX.Size2 videoPixelSize, out int streamIndex);

        /// <summary>
        /// Internal use: FlipY during rendering?
        /// </summary>
        protected virtual bool FlipY
        {
            get { return false; }
        }

        public int Bitrate
        {
            get { return bitrate / 1000; ; }
            set
            {
                bitrate = value * 1000;
            }
        }

        public int Framerate
        {
            get { return framerate; }
            set
            {
                framerate = value;
            }
        }

        #region IDisposable Support
        private bool disposedValue = false; // To detect redundant calls

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    sinkWriter.NotifyEndOfSegment(streamIndex);
                    sinkWriter.Finalize();

                    sinkWriter.Dispose();
                    sinkWriter = null;
                }

                disposedValue = true;
            }
        }

        // This code added to correctly implement the disposable pattern.
        public void Dispose()
        {
            Dispose(true);
        }
        #endregion
    }
}
