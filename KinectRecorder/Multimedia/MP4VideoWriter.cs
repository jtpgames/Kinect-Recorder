using System;
using SharpDX;
using SharpDX.MediaFoundation;

using MF = SharpDX.MediaFoundation;

namespace KinectRecorder.Multimedia
{
    class MP4VideoWriter : MediaFoundationVideoWriter
    {
        private static readonly Guid VIDEO_ENCODING_FORMAT = MF.VideoFormatGuids.H264;

        public MP4VideoWriter(string filePath, Size2 videoPixelSize)
            : base(filePath, videoPixelSize)
        {

        }

        public MP4VideoWriter(string filePath, Size2 videoPixelSize, Guid videoInputFormat)
            : base(filePath, videoPixelSize, videoInputFormat)
        {

        }

        protected override void CreateMediaTarget(SinkWriter sinkWriter, Size2 videoPixelSize, out int streamIndex)
        {
            using (MF.MediaType mediaTypeOut = new MF.MediaType())
            {
                mediaTypeOut.Set<Guid>(MF.MediaTypeAttributeKeys.MajorType, MF.MediaTypeGuids.Video);
                mediaTypeOut.Set<Guid>(MF.MediaTypeAttributeKeys.Subtype, VIDEO_ENCODING_FORMAT);
                mediaTypeOut.Set<int>(MF.MediaTypeAttributeKeys.AvgBitrate, Bitrate);
                mediaTypeOut.Set<int>(MF.MediaTypeAttributeKeys.InterlaceMode, (int)MF.VideoInterlaceMode.Progressive);
                mediaTypeOut.Set<long>(MF.MediaTypeAttributeKeys.FrameSize, MFHelper.GetMFEncodedIntsByValues(videoPixelSize.Width, videoPixelSize.Height));
                mediaTypeOut.Set<long>(MF.MediaTypeAttributeKeys.FrameRate, MFHelper.GetMFEncodedIntsByValues(Framerate, 1));
                sinkWriter.AddStream(mediaTypeOut, out streamIndex);
            }
        }

        /// <summary>
        /// Internal use: FlipY during rendering?
        /// </summary>
        protected override bool FlipY
        {
            get
            {
                return true;
            }
        }
    }
}
