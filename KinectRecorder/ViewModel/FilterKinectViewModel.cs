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
using Microsoft.Win32;

using KinectRecorder.Multimedia;

using MF = SharpDX.MediaFoundation;
using System.Reactive.Linq;
using System.IO;
using System.Threading;
using System.Collections.ObjectModel;
using System.Reactive.Subjects;

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

                if (VisualizeThresholds)
                {
                    VisualizeThresholds = false;
                }
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

        private bool bVisualizeThresholds = false;
        public bool VisualizeThresholds
        {
            get
            {
                return bVisualizeThresholds;
            }

            set
            {
                if (FilterEnabled)
                {
                    FilterEnabled = false;
                }

                bVisualizeThresholds = value;
                RaisePropertyChanged();
            }
        }

        private bool bUseGPUFiltering = false;
        public bool UseGPUFiltering
        {
            get
            {
                return bUseGPUFiltering;
            }

            set
            {
                bUseGPUFiltering = value;
                RaisePropertyChanged();
            }
        }

        #endregion

        #region Visualization and Control Properties

        private ImageSource filteredVideoFrame;
        public ImageSource FilteredVideoFrame
        {
            get
            {
                return filteredVideoFrame;
            }

            set
            {
                ++videoSamples;

                filteredVideoFrame = value;
                RaisePropertyChanged();

                observableFilteredImage.SafeOnNext(filteredVideoFrame);
            }
        }

        private Subject<ImageSource> observableFilteredImage;

        private byte[] audioFrame;
        private byte[] AudioFrame
        {
            get
            {
                return audioFrame;
            }

            set
            {
                ++audioSamples;

                audioFrame = value;
                RaisePropertyChanged();

                observableAudioFrame.SafeOnNext(audioFrame);
            }
        }

        private Subject<byte[]> observableAudioFrame;

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

                observableIsRunning.SafeOnNext(isRunning);
            }
        }

        private Subject<bool> observableIsRunning = new Subject<bool>();

        private bool isRecording = false;
        public bool IsRecording
        {
            get
            {
                return isRecording;
            }

            set
            {
                isRecording = value;
                RaisePropertyChanged();
            }
        }

        #endregion

        #region FPS Display

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

        #endregion

        public RelayCommand<System.Windows.Controls.Button> ToggleRecordingCommand { get; private set; }

        public RelayCommand OpenRecordingCommand { get; private set; }

        public RelayCommand ResetFilterCommand { get; private set; }

        private MediaFoundationVideoWriter videoWriter;
        private WaveFile debugWaveFile;

        private ushort[] depthData = new ushort[KinectManager.DepthSize];
        private Subject<ushort[]> observableDepthData;

        private DepthSpacePoint[] depthSpaceData = new DepthSpacePoint[KinectManager.ColorSize];
        private Subject<DepthSpacePoint[]> observableDepthSpaceData;

        /// <summary>
        /// Raw color frame data in bgra format
        /// </summary>
        private byte[] colorData = new byte[KinectManager.ColorSize * 4];
        private Subject<byte[]> observableColorData;

        private ObjectFilter objectFilter;

        private IDisposable StartProcessingSubscription;
        private IDisposable StopProcessingSubscription;

        private IDisposable ColorAndDepthSourceSubscription;
        private IDisposable AutoThresholdSubscription;
        private IDisposable FilterFramesSubscription;
        private IDisposable RecordingSubscription;

        private int audioSamples = 0;
        private int videoSamples = 0;
        private int samplesWritten = 0;

        private int numColorAndDepthFrame = 0;
        private int numVideoFrames = 0;
        private int numAudioFrames = 0;

        public FilterKinectViewModel()
        {
            if (!IsInDesignMode)
            {
                Initialize();
            }
            else
            {
                Fps = 42;
            }
        }

        private void Initialize()
        {
            HaloSize = 3;
            NearThreshold = 1589;
            FarThreshold = 1903;

            objectFilter = ObjectFilter.CreateObjectFilterWithGPUSupport();

            ToggleRecordingCommand = new RelayCommand<System.Windows.Controls.Button>(sender =>
            {
                if (!IsRecording)
                {
                    var sfd = new SaveFileDialog();
                    sfd.Filter = "MPEG-4 Video|*.mp4|All files|*.*";
                    sfd.Title = "Save the recording";

                    if (sfd.ShowDialog() == true)
                    {
                        if (videoWriter != null)
                        {
                            videoWriter.Dispose();
                        }

                        videoWriter = new MP4VideoWriter(sfd.FileName, new SharpDX.Size2(1920, 1080), MF.VideoFormatGuids.Argb32, true);

                        debugWaveFile = new WaveFile(WAVEFORMATEX.DefaultPCM);
                        debugWaveFile.Open(Path.Combine(Path.GetDirectoryName(sfd.FileName), Path.GetFileNameWithoutExtension(sfd.FileName)) + ".wav");

                        sender.Content = "Stop recording";
                        IsRecording = true;
                    }
                }
                else
                {
                    if (videoWriter != null)
                    {
                        videoWriter.Dispose();
                        videoWriter = null;
                    }

                    debugWaveFile.Close();

                    sender.Content = "Record";
                    IsRecording = false;
                }
            });

            OpenRecordingCommand = new RelayCommand(OpenRecording);

            ResetFilterCommand = new RelayCommand(objectFilter.Reset);

            var observableIsRecording = ObservableEx.ObservableProperty(() => IsRecording);

            observableIsRecording.Subscribe(e =>
            {
                if (e) ActivateRecording();
                else DeactivateRecording();
            });

            var observableIsAvailable = Observable.FromEventPattern<IsAvailableChangedEventArgs>(KinectManager.Instance, "KinectAvailabilityChanged")
                .Select(e => e.EventArgs.IsAvailable);

            StartProcessingSubscription = observableIsAvailable
                .CombineLatest(observableIsRunning, (available, running) => Tuple.Create(available, running))
                .Where(tuple => tuple.Item1 && tuple.Item2)
                .Subscribe(_ => StartProcessing());

            StopProcessingSubscription = observableIsAvailable
                .CombineLatest(observableIsRunning, (available, running) => Tuple.Create(available, running))
                .Where(tuple => !tuple.Item1 || !tuple.Item2)
                .Subscribe(_ => StopProcessing());

            sw = Stopwatch.StartNew();
        }

        private void OpenRecording()
        {
            var ofd = new Microsoft.Win32.OpenFileDialog();
            ofd.Filter = "Kinect Eventstream|*.xef|All files|*.*";
            ofd.Title = "Open a recording";

            if (ofd.ShowDialog() == true)
            {
                KinectManager.Instance.RecordingStopped += RecordingStopped;
                KinectManager.Instance.OpenRecording(ofd.FileName);

                if (!IsRunning) IsRunning = true;
            }
        }

        private void RecordingStopped()
        {
            IsRunning = false;
        }

        private void CloseRecording()
        {
            KinectManager.Instance.CloseRecording();
        }

        private void StartProcessing()
        {
            if (KinectManager.Instance.Paused)
            {
                KinectManager.Instance.PauseKinect(false);
            }

            var observableColorAndDepth = Observable.FromEventPattern<MultiSourceFrameArrivedEventArgs>(KinectManager.Instance, "ColorAndDepthSourceFrameArrived");

            ColorAndDepthSourceSubscription = observableColorAndDepth.Subscribe(
                e => ColorAndDepthSourceFrameArrived(e.Sender, e.EventArgs)
            );

            observableColorData = new Subject<byte[]>();
            observableDepthData = new Subject<ushort[]>();
            observableDepthSpaceData = new Subject<DepthSpacePoint[]>();

            AutoThresholdSubscription = observableDepthData
                .Sample(TimeSpan.FromSeconds(1))
                .Where(_ => AutomaticThresholds)
                .Select(data => AutomaticThreshold(data))
                .Subscribe(async (thresh) => 
                {
                    var t = await thresh;
                    FarThreshold = t;
                    NearThreshold = t - 500; // Nearthreshold is 50 cm towards the camera.
                });

            FilterFramesSubscription = observableColorData
                .Zip(observableDepthData, observableDepthSpaceData, (c, d, dsp) => Tuple.Create(c, d, dsp))
                .Select((t) => FilterFrames(t.Item1, t.Item2, t.Item3))
                .Subscribe(async (bytes) => FilteredVideoFrame = (await bytes).ToBgr32BitMap());
        }

        private Task<ushort> AutomaticThreshold(ushort[] depthData)
        {
            return Task.Run(() =>
            {
                /* 
                 * 1. method: Just take the maximum depth
                 * Works only if the area in front of the camera is flat
                 */
                var maxDepth = depthData.Max();

                /* 
                 * 2. method: Take the depth value which occurs most often
                 */
                var bins = new Dictionary<ushort, int>();
                foreach (var depth in depthData)
                {
                    if (depth == 0) continue;

                    if (bins.ContainsKey(depth))
                    {
                        ++bins[depth];
                    }
                    else
                    {
                        bins.Add(depth, 1);
                    }
                }

                ushort binWithHighestValue = 0;
                int HighestValue = 0;
                foreach (var bin in bins)
                {
                    if (bin.Value > HighestValue)
                    {
                        binWithHighestValue = bin.Key;
                        HighestValue = bin.Value;
                    }
                }

                maxDepth = binWithHighestValue;

                /*
                 * 3. method: Take the depth in the middle of the frame and apply a 3x3 mean filter.
                 */

                var meanConvolutionKernel = new float[3, 3]
                {
                            {1, 1, 1 }, {1, 1, 1 }, { 1, 1, 1 }
                };

                meanConvolutionKernel.Multiply(1f / 9f);

                var depthMatrix = depthData.Select((s) => (float)s).ToArray().ToMatrix(KinectManager.DepthWidth, KinectManager.DepthHeight);

                var middleMean = depthMatrix.ConvolutePixel(KinectManager.DepthWidth / 2, KinectManager.DepthHeight / 2, meanConvolutionKernel);

                maxDepth = (ushort)middleMean;

                // Clamp the value so it is within the allowed threshold
                maxDepth = MathHelper.Clamp(maxDepth, ThresholdMin, ThresholdMax);

                return maxDepth;
            });
        }

        private Task<byte[]> FilterFrames(byte[] color, ushort[] depth, DepthSpacePoint[] depthSpace)
        {
            Task<byte[]> task;

            if (FilterEnabled)
            {
                if (UseGPUFiltering)
                {
                    var bytes = objectFilter.FilterGPU(colorData, depthData, depthSpaceData, NearThreshold, FarThreshold, HaloSize);
                    task = Task.FromResult(bytes);
                }
                else
                {
                    task = objectFilter.FilterCPUAsync(colorData, depthData, depthSpaceData, NearThreshold, FarThreshold, HaloSize);
                }
            }
            else if (VisualizeThresholds)
            {
                task = Task.Run(() => Threshold(colorData));
            }
            else
            {
                task = Task.FromResult(color);
            }

            return task;
        }

        private void ActivateRecording()
        {
            observableFilteredImage = new Subject<ImageSource>();
            var observablePixels = observableFilteredImage.Select(img =>
                {
                    var pixels = new byte[KinectManager.ColorWidth * KinectManager.ColorHeight * 4];
                    ((img) as BitmapSource).CopyPixels(pixels, KinectManager.ColorWidth * 4, 0);

                    return pixels;
                });

            observableAudioFrame = new Subject<byte[]>();

            var observable = observablePixels
                .Zip(observableAudioFrame, (image, audio) => Tuple.Create(image, audio));

            RecordingSubscription = observable
                .Subscribe(async t =>
            {
                await Task.Run(() =>
                {
                    --audioSamples;
                    --videoSamples;
                    ++samplesWritten;

                    debugWaveFile.Write(t.Item2);

                    videoWriter.AddVideoAndAudioFrame(t.Item1.ToMemoryMappedTexture(), t.Item2);
                });
            });
        }

        private void DeactivateRecording()
        {
            RecordingSubscription.SafeDispose();

            LogConsole.WriteLine($"Video: {videoSamples}, Audio: {audioSamples}, Written: {samplesWritten}");
        }

        private void StopProcessing()
        {
            if (!KinectManager.Instance.Paused)
            {
                KinectManager.Instance.PauseKinect();
            }

            ColorAndDepthSourceSubscription.SafeDispose();
            FilterFramesSubscription.SafeDispose();

            LogConsole.WriteLine($"Videoframes: {numVideoFrames}, Audioframes: {numAudioFrames}, TotalFramesArrived: {numColorAndDepthFrame}");
        }

        public override void Cleanup()
        {
            StopProcessing();

            objectFilter.SafeDispose();

            sw.Stop();

            base.Cleanup();
        }

        private void ColorAndDepthSourceFrameArrived(object sender, MultiSourceFrameArrivedEventArgs e)
        {
            // Get a reference to the multi-frame
            var reference = e.FrameReference.AcquireFrame();

            ++totalFrames;
            ++numColorAndDepthFrame;

            if (sw.ElapsedMilliseconds >= 1000)
            {
                Fps = totalFrames;
                totalFrames = 0;
                sw.Restart();
            }

            // -- Open depth frame --
            using (var frame = reference.DepthFrameReference.AcquireFrame())
            {
                if (frame != null)
                {
                    Debug.Assert(frame.FrameDescription.LengthInPixels == KinectManager.DepthSize);

                    frame.CopyFrameDataToArray(depthData);

                    KinectManager.Instance.CoordinateMapper.MapColorFrameToDepthSpace(depthData, depthSpaceData);

                    // Notify observers
                    observableDepthData.OnNext(depthData);
                    observableDepthSpaceData.OnNext(depthSpaceData);
                }
            }
            // --
            // -- Open color frame --
            using (var frame = reference.ColorFrameReference.AcquireFrame())
            {
                if (frame != null)
                {
                    ++numVideoFrames;

                    Debug.Assert(frame.FrameDescription.LengthInPixels == KinectManager.ColorSize);

                    colorData = KinectManager.Instance.ToByteBuffer(frame);

                    // Notify observers
                    observableColorData.OnNext(colorData);
                }
            }
            // --
            // -- Acquire audio frame --
            using (var beamFrames = KinectManager.Instance.PollAudio())
            {
                if (beamFrames == null)
                {
                    return;
                }

                ++numAudioFrames;

                var subFrame = beamFrames[0].SubFrames[0];
                var audioBuffer = new byte[subFrame.FrameLengthInBytes];
                subFrame.CopyFrameDataToArray(audioBuffer);

                AudioFrame = audioBuffer;
            }
            // --
        }

        private unsafe byte[] Threshold(byte[] frame)
        {
            byte[] result = new byte[KinectManager.ColorSize * 4];

            var sw = Stopwatch.StartNew();

            fixed (byte* bgraPtr = frame)
            {
                var pBGR = (Bgra*)bgraPtr;
                fixed (byte* resultPtr = result)
                {
                    var dst = (Bgra*)resultPtr;
                    var defaultColor = new Bgra() { Blue = 44, Green = 250, Red = 88, Alpha = 255 };
                    var tooNearColor = new Bgra() { Blue = 88, Green = 44, Red = 250, Alpha = 255 };
                    var tooFarColor = new Bgra() { Blue = 250, Green = 44, Red = 88, Alpha = 255 };

                    for (int colorIndex = 0; colorIndex < KinectManager.ColorSize; ++colorIndex)
                    {
                        DepthSpacePoint dsp = depthSpaceData[colorIndex];

                        var src = &defaultColor;

                        if (dsp.X != float.NegativeInfinity && dsp.Y != -float.NegativeInfinity)
                        {
                            int dx = (int)Math.Round(dsp.X);
                            int dy = (int)Math.Round(dsp.Y);

                            if (0 <= dx && dx < KinectManager.DepthWidth && 0 <= dy && dy < KinectManager.DepthHeight)
                            {
                                int depth = depthData[dx + dy * KinectManager.DepthWidth];

                                if (depth < NearThreshold)
                                    src = &tooNearColor;
                                else if (depth > FarThreshold)
                                    src = &tooFarColor;
                                else
                                    src = pBGR + colorIndex;
                            }
                        }

                        dst[colorIndex] = *src;
                    }
                }
            }

            Console.WriteLine($"Thresholding took {sw.ElapsedMilliseconds} ms");

            return result;
        }
    }
}
