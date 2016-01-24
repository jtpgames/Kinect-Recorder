using GalaSoft.MvvmLight;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;
using Microsoft.Kinect;
using System.Windows.Media;
using GalaSoft.MvvmLight.CommandWpf;

namespace KinectRecorder.ViewModel
{
    public class FilterKinectViewModel : ViewModelBase
    {
        #region Filter Configurations

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

                if (nearThreshold > FarThreshold)
                {
                    FarThreshold = nearThreshold;
                }
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

        #endregion

        private ImageSource filteredVideoFrame;
        public ImageSource FilteredVideoFrame
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

        private bool isRunning = false;
        public bool IsRunning
        {
            get
            {
                return isRunning;
            }

            set
            {
                isRunning = value;
                RaisePropertyChanged();

                if (isRunning)
                {
                    StartProcessing();
                }
                else
                {
                    StopProcessing();
                }
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

        public RelayCommand ResetFilterCommand { get; private set; }

        private ushort[] depthData = new ushort[KinectManager.DepthSize];

        private DepthSpacePoint[] depthSpaceData = new DepthSpacePoint[KinectManager.ColorSize];

        /// <summary>
        /// Raw color frame data in bgra format
        /// </summary>
        private byte[] colorData = new byte[KinectManager.ColorSize * 4]; // ColorSize * sizeof(bgra)

        private ObjectFilter objectFilter = new ObjectFilter();

        public FilterKinectViewModel()
        {
            if (!IsInDesignMode)
            {
                HaloSize = 3;
                NearThreshold = 1589;
                FarThreshold = 1903;

                ResetFilterCommand = new RelayCommand(objectFilter.Reset);

                sw = Stopwatch.StartNew();
            }
            else
            {
                Fps = 42;
            }
        }

        private void StartProcessing()
        {
            KinectManager.Instance.ColorAndDepthSourceFrameArrived += ColorAndDepthSourceFrameArrived;
        }

        private void StopProcessing()
        {
            KinectManager.Instance.ColorAndDepthSourceFrameArrived -= ColorAndDepthSourceFrameArrived;
        }

        public override void Cleanup()
        {
            StopProcessing();

            sw.Stop();

            base.Cleanup();
        }

        private async void ColorAndDepthSourceFrameArrived(object sender, MultiSourceFrameArrivedEventArgs e)
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

                    //FilteredVideoFrame = KinectManager.Instance.ToBitmap(frame);

                    var bytes = await objectFilter.FilterAsync(colorData, depthData, depthSpaceData, NearThreshold, FarThreshold, HaloSize);
                    FilteredVideoFrame = bytes.ToBgr32BitMap();
                }
            }
        }
    }
}
