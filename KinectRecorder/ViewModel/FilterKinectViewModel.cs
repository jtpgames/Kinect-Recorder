using GalaSoft.MvvmLight;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;
using Microsoft.Kinect;

namespace KinectRecorder.ViewModel
{
    public class FilterKinectViewModel : ViewModelBase
    {
        private int haloSize;
        public int HaloSize
        {
            get
            {
                return haloSize;
            }

            set
            {
                haloSize = value;
                RaisePropertyChanged();
            }
        }

        private int nearThreshold;
        public int NearThreshold
        {
            get
            {
                return nearThreshold;
            }

            set
            {
                nearThreshold = value;
                RaisePropertyChanged();
            }
        }

        private int farThreshold;
        public int FarThreshold
        {
            get
            {
                return farThreshold;
            }

            set
            {
                farThreshold = value;
                RaisePropertyChanged();
            }
        }

        private BitmapSource filteredVideoFrame;
        public BitmapSource FilteredVideoFrame
        {
            get
            {
                return filteredVideoFrame;
            }

            set
            {
                filteredVideoFrame = value;
                RaisePropertyChanged();
            }
        }

        private Stopwatch sw;
        private int totalFrames = 0;
        private int fps;
        public int Fps
        {
            get
            {
                return fps;
            }

            set
            {
                fps = value;
                RaisePropertyChanged();
            }
        }

        private ushort[] depthData = new ushort[KinectManager.DepthSize];

        private DepthSpacePoint[] depthSpaceData = new DepthSpacePoint[KinectManager.ColorSize];

        private byte[] colorData = new byte[KinectManager.ColorSize * 4]; // ColorSize * sizeof(bgra)

        public FilterKinectViewModel()
        {
            KinectManager.Instance.ColorAndDepthSourceFrameArrived += ColorAndDepthSourceFrameArrived;

            sw = Stopwatch.StartNew();
        }

        public override void Cleanup()
        {
            KinectManager.Instance.ColorAndDepthSourceFrameArrived -= ColorAndDepthSourceFrameArrived;

            sw.Stop();

            base.Cleanup();
        }

        private void ColorAndDepthSourceFrameArrived(object sender, MultiSourceFrameArrivedEventArgs e)
        {
            // Get a reference to the multi-frame
            var reference = e.FrameReference.AcquireFrame();

            ++totalFrames;

            if (sw.ElapsedMilliseconds >= 1000)
            {
                Fps = totalFrames;
                totalFrames = 0;
                sw.Restart();
            }

            // Open depth frame
            using (var frame = reference.DepthFrameReference.AcquireFrame())
            {
                if (frame != null)
                {
                    Debug.Assert(frame.FrameDescription.LengthInPixels == KinectManager.DepthSize);

                    frame.CopyFrameDataToArray(depthData);

                    KinectManager.Instance.CoordinateMapper.MapColorFrameToDepthSpace(depthData, depthSpaceData);
                }
            }

            // Open color frame
            using (var frame = reference.ColorFrameReference.AcquireFrame())
            {
                if (frame != null)
                {
                    Debug.Assert(frame.FrameDescription.LengthInPixels == KinectManager.ColorSize);

                    colorData = KinectManager.Instance.ToByteBuffer(frame);
                }
            }
        }
    }
}
