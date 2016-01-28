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
using SharpDX.MediaFoundation;

using MF = SharpDX.MediaFoundation;

namespace KinectRecorder.Multimedia
{
    struct WAVEFORMATEX
    {
        public SharpDX.Multimedia.WaveFormatEncoding wFormatTag;
        public ushort nChannels;
        public uint nSamplesPerSec;
        public uint nAvgBytesPerSec;
        public ushort nBlockAlign;
        public ushort wBitsPerSample;
        public ushort cbSize;

        public static WAVEFORMATEX DefaultPCM
        {
            get
            {
                var WaveFormatEx = new WAVEFORMATEX();
                WaveFormatEx.wFormatTag = SharpDX.Multimedia.WaveFormatEncoding.Pcm;
                WaveFormatEx.nChannels = 1;
                WaveFormatEx.nSamplesPerSec = 16000;
                WaveFormatEx.wBitsPerSample = 16;
                WaveFormatEx.nBlockAlign = (ushort)(WaveFormatEx.nChannels * WaveFormatEx.wBitsPerSample / 8);
                WaveFormatEx.nAvgBytesPerSec = WaveFormatEx.nSamplesPerSec * WaveFormatEx.nBlockAlign;
                WaveFormatEx.cbSize = 0;

                return WaveFormatEx;
            }
        }

        public static WAVEFORMATEX DefaultIEEE
        {
            get
            {
                var WaveFormatEx = new WAVEFORMATEX();
                WaveFormatEx.wFormatTag = SharpDX.Multimedia.WaveFormatEncoding.IeeeFloat;
                WaveFormatEx.nChannels = 1;
                WaveFormatEx.nSamplesPerSec = 16000;
                WaveFormatEx.wBitsPerSample = 32;
                WaveFormatEx.nBlockAlign = (ushort)(WaveFormatEx.nChannels * WaveFormatEx.wBitsPerSample / 8);
                WaveFormatEx.nAvgBytesPerSec = WaveFormatEx.nSamplesPerSec * WaveFormatEx.nBlockAlign;
                WaveFormatEx.cbSize = 0;

                return WaveFormatEx;
            }
        }

        public SharpDX.Multimedia.WaveFormat ToSharpDX()
        {
            return SharpDX.Multimedia.WaveFormat.CreateCustomFormat(
                wFormatTag,
                (int)nSamplesPerSec,
                nChannels,
                (int)nAvgBytesPerSec,
                nBlockAlign,
                wBitsPerSample
            );
        }
    }

    abstract class MediaFoundationAudioWriter
    {
        /// <summary>
        /// Gets all the available media types for a particular 
        /// </summary>
        /// <param name="audioSubtype">Audio subtype - a value from the AudioSubtypes class</param>
        /// <returns>An array of available media types that can be encoded with this subtype</returns>
        public static MF.MediaType[] GetOutputMediaTypes(Guid audioSubtype)
        {
            MF.Collection availableTypes;
            try
            {
                availableTypes = MF.MediaFactory.TranscodeGetAudioOutputAvailableTypes(audioSubtype, MF.TransformEnumFlag.All, null);
            }
            catch (SharpDXException c)
            {
                if (c.ResultCode.Code == MF.ResultCode.NotFound.Code)
                {
                    // Don't worry if we didn't find any - just means no encoder available for this type
                    return new MF.MediaType[0];
                }
                throw;
            }

            int count = availableTypes.ElementCount;
            var mediaTypes = new List<MF.MediaType>(count);
            for (int n = 0; n < count; n++)
            {
                var mediaTypeObject = availableTypes.GetElement(n);
                mediaTypes.Add(new MF.MediaType(mediaTypeObject.NativePointer));

            }
            availableTypes.Dispose();
            return mediaTypes.ToArray();
        }

        /// <summary>
        /// Queries the available bitrates for a given encoding output type, sample rate and number of channels
        /// </summary>
        /// <param name="audioSubtype">Audio subtype - a value from the AudioSubtypes class</param>
        /// <param name="sampleRate">The sample rate of the PCM to encode</param>
        /// <param name="channels">The number of channels of the PCM to encode</param>
        /// <returns>An array of available bitrates in average bits per second</returns>
        public static int[] GetEncodeBitrates(Guid audioSubtype, int sampleRate, int channels)
        {
            return GetOutputMediaTypes(audioSubtype)
                .Where(mt => mt.Get(MF.MediaTypeAttributeKeys.AudioSamplesPerSecond) == sampleRate &&
                    mt.Get(MF.MediaTypeAttributeKeys.AudioNumChannels) == channels)
                .Select(mt => mt.Get(MF.MediaTypeAttributeKeys.AudioAvgBytesPerSecond) * 8)
                .Distinct()
                .OrderBy(br => br)
                .ToArray();
        }

        /// <summary>
        /// Tries to find the encoding media type with the closest bitrate to that specified
        /// </summary>
        /// <param name="audioSubtype">Audio subtype, a value from AudioSubtypes</param>
        /// <param name="inputFormat">Your encoder input format (used to check sample rate and channel count)</param>
        /// <param name="desiredBitRate">Your desired bitrate</param>
        /// <returns>The closest media type, or null if none available</returns>
        public static MF.MediaType SelectMediaType(Guid audioSubtype, SharpDX.Multimedia.WaveFormat inputFormat, int desiredBitRate)
        {
            return GetOutputMediaTypes(audioSubtype)
                .Where(mt => mt.Get(MF.MediaTypeAttributeKeys.AudioSamplesPerSecond) == inputFormat.SampleRate &&
                    mt.Get(MF.MediaTypeAttributeKeys.AudioNumChannels) == inputFormat.Channels)
                .Select(mt => new { MediaType = mt, Delta = Math.Abs(desiredBitRate - mt.Get(MF.MediaTypeAttributeKeys.AudioAvgBytesPerSecond) * 8) })
                .OrderBy(mt => mt.Delta)
                .Select(mt => mt.MediaType)
                .FirstOrDefault();
        }

        private int streamIndex;
        public int StreamIndex => streamIndex;

        public abstract Guid AudioFormat { get; }

        public MediaFoundationAudioWriter(MF.SinkWriter sinkWriter, ref WAVEFORMATEX waveFormat, int desiredBitRate = 192000)
        {
            var sharpWf = waveFormat.ToSharpDX();

            // Information on configuring an AAC media type can be found here:
            // http://msdn.microsoft.com/en-gb/library/windows/desktop/dd742785%28v=vs.85%29.aspx
            var outputMediaType = SelectMediaType(AudioFormat, sharpWf, desiredBitRate);
            if (outputMediaType == null) throw new InvalidOperationException("No suitable encoders available");

            var inputMediaType = new MF.MediaType();
            var size = 18 + sharpWf.ExtraSize;

            sinkWriter.AddStream(outputMediaType, out streamIndex);

            MF.MediaFactory.InitMediaTypeFromWaveFormatEx(inputMediaType, new[] { sharpWf }, size);
            sinkWriter.SetInputMediaType(streamIndex, inputMediaType, null);
        }

        public MF.Sample CreateSampleFromFrame(byte[] data)
        {
            MF.MediaBuffer mediaBuffer = MF.MediaFactory.CreateMemoryBuffer(data.Length);

            // Write all contents to the MediaBuffer for media foundation
            int cbMaxLength = 0;
            int cbCurrentLength = 0;
            IntPtr mediaBufferPointer = mediaBuffer.Lock(out cbMaxLength, out cbCurrentLength);
            try
            {

                Marshal.Copy(data, 0, mediaBufferPointer, data.Length);
            }
            finally
            {
                mediaBuffer.Unlock();
                mediaBuffer.CurrentLength = data.Length;
            }

            // Create the sample (includes image and timing information)
            MF.Sample sample = MF.MediaFactory.CreateSample();
            sample.AddBuffer(mediaBuffer);

            return sample;
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

        public int StreamIndex => streamIndex;

        public MF.SinkWriter SinkWriter => sinkWriter;

        private MediaFoundationAudioWriter audioWriter;

        private static SinkWriter CreateSinkWriter(string outputFile)
        {
            SinkWriter writer;
            using (var attributes = new MediaAttributes())
            {
                MediaFactory.CreateAttributes(attributes, 1);
                attributes.Set(SinkWriterAttributeKeys.ReadwriteEnableHardwareTransforms.Guid, (UInt32)1);
                try
                {
                    writer = MediaFactory.CreateSinkWriterFromURL(outputFile, IntPtr.Zero, attributes);
                }
                catch (COMException e)
                {
                    if (e.ErrorCode == unchecked((int)0xC00D36D5))
                    {
                        throw new ArgumentException("Was not able to create a sink writer for this file extension");
                    }
                    throw;
                }
            }
            return writer;
        }

        public MediaFoundationVideoWriter(string filePath, Size2 videoPixelSize)
            : this(filePath, videoPixelSize, VIDEO_INPUT_FORMAT)
        {

        }

        public MediaFoundationVideoWriter(string filePath, Size2 videoPixelSize, Guid videoInputFormat, bool supportAudio = false)
        {
            bitrate = 1500000;
            framerate = 15;

            if (!MFInitialized)
            {
                // Initialize MF library. MUST be called before any MF related operations.
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

            if (supportAudio)
            {
                // initialize audio writer
                var waveFormat = WAVEFORMATEX.DefaultPCM;
                audioWriter = new MP3AudioWriter(sinkWriter, ref waveFormat);
            }

            // Start writing the video file. MUST be called before write operations.
            sinkWriter.BeginWriting();

            // Set initial frame index
            frameIndex = -1;
        }

        public MF.Sample CreateSampleFromFrame(MemoryMappedTexture32bpp frame)
        {
            MF.MediaBuffer mediaBuffer = MF.MediaFactory.CreateMemoryBuffer((int)frame.SizeInBytes);

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
                        int* targetBufferPointerNative = (int*)frame.Pointer.ToPointer();
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
                        int* targetBufferPointerNative = (int*)frame.Pointer.ToPointer();
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
                mediaBuffer.CurrentLength = (int)frame.SizeInBytes;
            }

            // Create the sample (includes image and timing information)
            MF.Sample sample = MF.MediaFactory.CreateSample();
            sample.AddBuffer(mediaBuffer);

            return sample;
        }

        public void AddVideoFrame(MemoryMappedTexture32bpp texture)
        {
            ++frameIndex;

            // Create the sample (includes image and timing information)
            MF.Sample sample = CreateSampleFromFrame(texture);
            try
            {
                //long frameDuration = 10 * 1000 * 1000 / framerate;
                long frameDuration;
                MF.MediaFactory.FrameRateToAverageTimePerFrame(framerate, 1, out frameDuration);
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

            //mediaBuffer.Dispose();
        }

        public void AddVideoAndAudioFrame(MemoryMappedTexture32bpp frame, byte[] audioFrame)
        {
            Debug.Assert(frame != null && frame.SizeInBytes != 0
                && audioFrame != null && audioFrame.Length != 0);

            var videoSample = CreateSampleFromFrame(frame);
            var audioSample = audioWriter.CreateSampleFromFrame(audioFrame);
            try
            {

                var samples = new Dictionary<int, Sample>();
                samples.Add(StreamIndex, videoSample);
                samples.Add(audioWriter.StreamIndex, audioSample);

                WriteSamples(samples);
            }
            finally
            {
                videoSample.Dispose();
                audioSample.Dispose();
            }
        }

        private void WriteSamples(Dictionary<int, Sample> samples)
        {
            ++frameIndex;

            long frameDuration;
            MediaFactory.FrameRateToAverageTimePerFrame(framerate, 1, out frameDuration);

            try
            {
                foreach (var item in samples)
                {
                    var streamIndex = item.Key;
                    var sample = item.Value;

                    sample.SampleTime = frameDuration * frameIndex;
                    sample.SampleDuration = frameDuration;

                    sinkWriter.WriteSample(streamIndex, sample);
                }
            }
            catch (SharpDXException e)
            {
                Debug.WriteLine(e.Message);
            }
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
            get { return bitrate; }
            set
            {
                bitrate = value;
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
                    if (frameIndex > 0)
                    {
                        sinkWriter.Finalize();
                    }

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
