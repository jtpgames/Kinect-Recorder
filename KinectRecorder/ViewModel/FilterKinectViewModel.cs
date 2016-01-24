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
    /// <summary>
    /// http://www.mikeadev.net/2013/06/generic-method/
    /// </summary>
    public static class MathHelper
    {
        /// <summary>
        /// Clamps value within desired range
        /// This is a generic. So use any type you want
        /// </summary>
        /// <param name="value">Value to be clamped</param>
        /// <param name="min">Min range</param>
        /// <param name="max">Max range</param>
        /// <returns>Clamped value within range</returns>
        public static T Clamp<T>(T value, T min, T max)
            where T : IComparable<T>
        {
            T result = value;
            if (result.CompareTo(max) > 0)
                result = max;
            if (result.CompareTo(min) < 0)
                result = min;

            return result;
        }
    }

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
                RaisePropertyChanged("HaloText");
            }
        }

        public string HaloText => $"Size of Halo: {HaloSize}";

        public ushort ThresholdMin => 500;

        public ushort ThresholdMax => 6500;

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
                RaisePropertyChanged("NearThresholdText");

                if (nearThreshold > FarThreshold)
                {
                    FarThreshold = NearThreshold;
                }
            }
        }

        public string NearThresholdText => $"Near Threshold in mm: {NearThreshold}";

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
                RaisePropertyChanged("FarThresholdText");
            }
        }

        public string FarThresholdText => $"Far Threshold in mm: {FarThreshold}";

        private bool bFilterEnabled = false;
        public bool FilterEnabled
        {
            get
            {
                return bFilterEnabled;
            }

            set
            {
                bFilterEnabled = value;
                RaisePropertyChanged();
            }
        }

        private bool bAutomaticThresholds = true;
        public bool AutomaticThresholds
        {
            get
            {
                return bAutomaticThresholds;
            }

            set
            {
                bAutomaticThresholds = value;
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

            var bOneSecondElapsed = false;
            if (sw.ElapsedMilliseconds >= 1000)
            {
                Fps = totalFrames;
                totalFrames = 0;
                sw.Restart();
                bOneSecondElapsed = true;
            }

            // Open depth frame
            using (var frame = reference.DepthFrameReference.AcquireFrame())
            {
                if (frame != null)
                {
                    Debug.Assert(frame.FrameDescription.LengthInPixels == KinectManager.DepthSize);

                    frame.CopyFrameDataToArray(depthData);

                    KinectManager.Instance.CoordinateMapper.MapColorFrameToDepthSpace(depthData, depthSpaceData);

                    if (bOneSecondElapsed && AutomaticThresholds)
                    {
                        var maxDepth = depthData.Max();

                        var bins = new Dictionary<ushort, int>();
                        foreach (var depth in depthData)
                        {
                            if (bins.ContainsKey(depth))
                            {
                                ++bins[depth];
                            }
                            else
                            {
                                bins.Add(depth, 1);
                            }
                        }

                        //var maxDepth2 = bins.Max().Value;

                        maxDepth = MathHelper.Clamp(maxDepth, ThresholdMin, ThresholdMax);

                        FarThreshold = maxDepth;
                        NearThreshold = maxDepth - 500;
                    }
                }
            }

            // Open color frame
            using (var frame = reference.ColorFrameReference.AcquireFrame())
            {
                if (frame != null)
                {
                    Debug.Assert(frame.FrameDescription.LengthInPixels == KinectManager.ColorSize);

                    colorData = KinectManager.Instance.ToByteBuffer(frame);

                    if (FilterEnabled)
                    {
                        var bytes = await objectFilter.FilterAsync(colorData, depthData, depthSpaceData, NearThreshold, FarThreshold, HaloSize);
                        FilteredVideoFrame = bytes.ToBgr32BitMap();
                    }
                    else
                    {
                        FilteredVideoFrame = KinectManager.Instance.ToBitmap(frame);
                    }
                }
            }
        }
    }
}
