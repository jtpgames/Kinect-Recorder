﻿using GalaSoft.MvvmLight;
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
using ReactiveUI;
using System.Reactive;
using System.Collections.Concurrent;
using System.Reactive.Concurrency;

namespace KinectRecorder.ViewModel
{
    public class FilterKinectViewModel : ReactiveObject, IDisposed
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
                this.RaisePropertyChanged();
                this.RaisePropertyChanged("HaloText");
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
                this.RaisePropertyChanged();
                this.RaisePropertyChanged("NearThresholdText");

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
                this.RaisePropertyChanged();
                this.RaisePropertyChanged("FarThresholdText");
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
                this.RaisePropertyChanged();

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
                this.RaisePropertyChanged();
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
                this.RaisePropertyChanged();
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
                this.RaisePropertyChanged();
            }
        }

        #endregion

        #region Visualization and Control Properties

        private ObservableAsPropertyHelper<ImageSource> filteredVideoFrame;
        public ImageSource FilteredVideoFrame
        {
            get
            {
                return filteredVideoFrame.Value;
            }
        }

        private BlockingCollection<byte[]> videoFramesQueue = null;

        private byte[] audioFrame;
        private byte[] AudioFrame
        {
            get
            {
                return audioFrame;
            }

            set
            {
                audioFrame = value;
                this.RaisePropertyChanged();
            }
        }

        private BlockingCollection<byte[]> audioFramesQueue = null;

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
                this.RaisePropertyChanged();

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
                this.RaisePropertyChanged();
            }
        }

        private bool isRecordingPendingFrames = false;

        public bool IsKinectPaused
        {
            get
            {
                return KinectManager.Instance.Paused;
            }

            set
            {
                KinectManager.Instance.PauseKinect(value);
                this.RaisePropertyChanged();
                this.RaisePropertyChanged("IsKinectUnpaused");
            }
        }

        public bool IsKinectUnpaused
        {
            get
            {
                return !KinectManager.Instance.Paused;
            }
        }

        #endregion

        #region FPS Display

        private Stopwatch fpsTimer;
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
                this.RaisePropertyChanged();
            }
        }

        #endregion

        #region Relay Commands

        public RelayCommand<System.Windows.Controls.Button> ToggleRecordingCommand { get; private set; }

        public RelayCommand OpenRecordingCommand { get; private set; }

        public RelayCommand ResetFilterCommand { get; private set; }

        public RelayCommand PauseKinectCommand { get; private set; }

        public RelayCommand ContinueKinectCommand { get; private set; }

        #endregion

        private ReactiveCommand<byte[]> ExecuteFilterVideoFrame { get; set; }
        private ReactiveCommand<Unit> ExecuteWriteVideoAndAudioFrame { get; set; }

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

        private IDisposable observablePixelsSubscription;
        private IDisposable observableAudioSubscription;

        private IDisposable RecordingSubscription;

        private int audioSamples = 0;
        private int videoSamples = 0;
        private int samplesWritten = 0;

        private Stopwatch colorAndDepthFPSTimer;
        private int colorAndDepthFrames = 0;
        private int colorAndDepthFramesPerSec = 0;
        public int ColorAndDepthFramesPerSec
        {
            get
            {
                return colorAndDepthFramesPerSec;
            }

            set
            {
                colorAndDepthFramesPerSec = value;
                this.RaisePropertyChanged();
            }
        }

        public bool Disposed => disposedValue;

        private int totalColorAndDepthFrames = 0;
        private int totalVideoFrames = 0;
        private int totalAudioFrames = 0;

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

        private bool IsInDesignMode
        {
            get
            {
                return !(System.Windows.Application.Current is App);
            }
        }

        private void Initialize()
        {
            HaloSize = 3;
            NearThreshold = 1589;
            FarThreshold = 1903;

            objectFilter = ObjectFilter.CreateObjectFilterWithGPUSupport();

            // -- Initialize Relay commands --

            InitializeRelayCommands();

            // --
            // -- Create observables to activate / deactivate recording and to start and stop processing  --

            var observableIsRecording = ObservableEx.ObservableProperty(() => IsRecording);

            observableIsRecording
                .Throttle(TimeSpan.FromMilliseconds(150))
                .Subscribe(e =>
                {
                    if (e) ActivateRecording();
                    else DeactivateRecording();
                });

            var observableIsAvailable = Observable.FromEventPattern<bool>(KinectManager.Instance, "KinectAvailabilityChanged")
                .Select(e => e.EventArgs);

            StartProcessingSubscription = observableIsAvailable
                .CombineLatest(observableIsRunning, (available, running) => Tuple.Create(available, running))
                .Where(tuple => tuple.Item1 && tuple.Item2)
                .Subscribe(_ => StartProcessing());

            StopProcessingSubscription = observableIsAvailable
                .CombineLatest(observableIsRunning, (available, running) => Tuple.Create(available, running))
                .Where(tuple => !tuple.Item1 || !tuple.Item2)
                .Subscribe(_ => StopProcessing());

            InitializeReactiveCommands();
            InitializeReactiveProperties();

            // --

            fpsTimer = Stopwatch.StartNew();
            colorAndDepthFPSTimer = Stopwatch.StartNew();
        }

        private void InitializeRelayCommands()
        {
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
                    sender.Content = "Record";
                    IsRecording = false;
                }
            });

            OpenRecordingCommand = new RelayCommand(OpenRecording);

            ResetFilterCommand = new RelayCommand(objectFilter.Reset);

            PauseKinectCommand = new RelayCommand(() => IsKinectPaused = true);
            ContinueKinectCommand = new RelayCommand(() => IsKinectPaused = false);
        }

        private void InitializeReactiveCommands()
        {
            ExecuteFilterVideoFrame = ReactiveCommand.CreateAsyncTask(param =>
            {
                var input = param as Tuple<byte[], ushort[], DepthSpacePoint[]>;

                return FilterFrames(input.Item1, input.Item2, input.Item3);
            });

            ExecuteFilterVideoFrame.ThrownExceptions
                .Subscribe(x => Debug.WriteLine(x.Message));

            ExecuteWriteVideoAndAudioFrame = ReactiveCommand.CreateAsyncTask(async param =>
            {
                var input = param as Tuple<byte[], byte[]>;

                await WriteVideoAndAudioFrame(input);

                if (isRecordingPendingFrames && (videoSamples == 0 || audioSamples == 0))
                    CleanUpAfterPendingFramesWereRecorded();
            });
        }

        private void InitializeReactiveProperties()
        {
            filteredVideoFrame = ExecuteFilterVideoFrame
                .Select(bytes => bytes.ToBgr32BitMap())
                .ToProperty(this, x => x.FilteredVideoFrame);

            this.ObservableForProperty(x => x.FilteredVideoFrame)
                .Subscribe(x =>
            {
                ++totalFrames;

                if (fpsTimer.ElapsedMilliseconds >= 1000)
                {
                    Fps = totalFrames;
                    totalFrames = 0;
                    fpsTimer.Restart();
                }
            });

            //var observableVideoFramesQueue = videoFramesQueue
            //    .GetConsumingEnumerable()
            //    .ToObservable(TaskPoolScheduler.Default);

            //var observableAudioFramesQueue = audioFramesQueue
            //    .GetConsumingEnumerable()
            //    .ToObservable(TaskPoolScheduler.Default);

            //var observable = observableVideoFramesQueue
            //    .Zip(observableAudioFramesQueue, (image, audio) => Tuple.Create(image, audio));

            //RecordingSubscription = observable
            //    .InvokeCommand(ExecuteWriteVideoAndAudioFrame);
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
            if (IsKinectPaused)
            {
                IsKinectPaused = false;
            }

            var observableColorAndDepth = Observable.FromEventPattern<MultiSourceFrameArrivedEventArgs>(KinectManager.Instance, "ColorAndDepthSourceFrameArrived");

            ColorAndDepthSourceSubscription = observableColorAndDepth
                .Do(_ =>
                {
                    ++totalColorAndDepthFrames;
                    ++colorAndDepthFrames;

                    if (colorAndDepthFPSTimer.ElapsedMilliseconds >= 1000)
                    {
                        ColorAndDepthFramesPerSec = colorAndDepthFrames;
                        colorAndDepthFrames = 0;
                        colorAndDepthFPSTimer.Restart();
                    }
                })
                .ObserveOn(NewThreadScheduler.Default)
                .Subscribe(
                e => ColorAndDepthSourceFrameArrived(e.Sender, e.EventArgs)
            );

            observableColorData = new Subject<byte[]>();
            observableDepthData = new Subject<ushort[]>();
            observableDepthSpaceData = new Subject<DepthSpacePoint[]>();

            // -- Set up Automatic thresholding --

            var ExecuteAutomaticThreshold = ReactiveCommand.CreateAsyncTask(param =>
            {
                var input = param as ushort[];

                return AutomaticThreshold(input);
            });

            AutoThresholdSubscription = observableDepthData
                .Sample(TimeSpan.FromSeconds(1))
                .Where(_ => AutomaticThresholds)
                .InvokeCommand(ExecuteAutomaticThreshold);

            ExecuteAutomaticThreshold
                .Subscribe((t) =>
                {
                    FarThreshold = t;
                    NearThreshold = t - 500; // Nearthreshold is 50 cm towards the camera.
                });

            // --
            // -- Set up Filtering --

            FilterFramesSubscription = observableColorData
                .Zip(observableDepthData, observableDepthSpaceData, (c, d, dsp) => Tuple.Create(c, d, dsp))
                .InvokeCommand(ExecuteFilterVideoFrame);

            // --
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

        private async Task<byte[]> FilterFrames(byte[] color, ushort[] depth, DepthSpacePoint[] depthSpace)
        {
            byte[] result;

            if (FilterEnabled)
            {
                if (UseGPUFiltering)
                {
                    var bytes = objectFilter.FilterGPU(colorData, depthData, depthSpaceData, NearThreshold, FarThreshold, HaloSize);
                    result = bytes;
                }
                else
                {
                    result = await objectFilter.FilterCPUAsync(colorData, depthData, depthSpaceData, NearThreshold, FarThreshold, HaloSize);
                }
            }
            else if (VisualizeThresholds)
            {
                result = await Task.Run(() => Threshold(colorData));
            }
            else
            {
                result = color;
            }

            return result;
        }

        private void ActivateRecording()
        {
            videoFramesQueue = new BlockingCollection<byte[]>();
            audioFramesQueue = new BlockingCollection<byte[]>();

            Task.Factory.StartNew(async () =>
            {
                foreach (var videoFrame in videoFramesQueue.GetConsumingEnumerable())
                {
                    if (isRecordingPendingFrames && audioSamples == 0)
                    {
                        CleanUpAfterPendingFramesWereRecorded();
                        return;
                    }

                    foreach (var audioframe in audioFramesQueue.GetConsumingEnumerable())
                    {
                        await WriteVideoAndAudioFrame(Tuple.Create(videoFrame, audioFrame));

                        if (isRecordingPendingFrames && (videoSamples == 0 || audioSamples == 0))
                        {
                            CleanUpAfterPendingFramesWereRecorded();
                            return;
                        }

                        break;
                    }
                }
            }, TaskCreationOptions.LongRunning);

            var observablePixels = this.WhenAnyValue(x => x.FilteredVideoFrame)
                .Where(img => img != null && IsRecording)
                .Select(img =>
                {
                    var pixels = new byte[KinectManager.ColorWidth * KinectManager.ColorHeight * 4];
                    ((img) as BitmapSource).CopyPixels(pixels, KinectManager.ColorWidth * 4, 0);

                    return pixels;
                });

            observablePixelsSubscription = observablePixels
                .ObserveOn(TaskPoolScheduler.Default)
                .Subscribe(bytes =>
                {
                    Interlocked.Increment(ref videoSamples);
                    videoFramesQueue.Add(bytes);
                });

            var observableAudio = this.WhenAnyValue(x => x.AudioFrame)
                .Where(frame => frame != null && IsRecording);

            observableAudioSubscription = observableAudio
                .ObserveOn(TaskPoolScheduler.Default)
                .Subscribe(bytes =>
                {
                    Interlocked.Increment(ref audioSamples);
                    audioFramesQueue.Add(bytes);
                });
        }

        private Task WriteVideoAndAudioFrame(Tuple<byte[], byte[]> input)
        {
            return Task.Run(() =>
            {
                Interlocked.Decrement(ref audioSamples);
                Interlocked.Decrement(ref videoSamples);
                Interlocked.Increment(ref samplesWritten);

                if (input.Item2 != null)
                {
                    debugWaveFile.Write(input.Item2);

                    videoWriter.AddVideoAndAudioFrame(input.Item1.ToMemoryMappedTexture(), input.Item2);
                }
                else
                {
                    videoWriter.AddVideoFrame(input.Item1.ToMemoryMappedTexture());
                }
            });
        }

        private void DeactivateRecording()
        {
            isRecordingPendingFrames = true;

            videoFramesQueue?.CompleteAdding();
            audioFramesQueue?.CompleteAdding();

            Debug.WriteLine($"Video: {videoSamples}, Audio: {audioSamples}, Written: {samplesWritten}");
            Debug.WriteLine($"Remaining:: Video: {videoFramesQueue?.Count}, Audio: {audioFramesQueue?.Count}");
        }

        private void CleanUpAfterPendingFramesWereRecorded()
        {
            videoWriter.SafeDispose();

            debugWaveFile.SafeDispose();

            isRecordingPendingFrames = false;

            LogConsole.WriteLine($"Video: {videoSamples}, Audio: {audioSamples}, Written: {samplesWritten}");
        }

        private void StopProcessing()
        {
            if (!IsKinectPaused)
            {
                IsKinectPaused = true;
            }

            ColorAndDepthSourceSubscription.SafeDispose();
            FilterFramesSubscription.SafeDispose();

            LogConsole.WriteLine($"Total Videoframes: {totalVideoFrames}, Total Audioframes: {totalAudioFrames}, Total Frames: {totalColorAndDepthFrames}");
        }

        #region IDisposable Support
        private bool disposedValue = false; // To detect redundant calls

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    Cleanup();
                }

                disposedValue = true;
            }
        }

        public void Dispose()
        {
            Dispose(true);
        }
        #endregion

        private void Cleanup()
        {
            StopProcessing();

            DeactivateRecording();
            CleanUpAfterPendingFramesWereRecorded();

            objectFilter.SafeDispose();

            fpsTimer.Stop();
            colorAndDepthFPSTimer.Stop();
        }

        private void ColorAndDepthSourceFrameArrived(object sender, MultiSourceFrameArrivedEventArgs e)
        {
            // Get a reference to the multi-frame
            var reference = e.FrameReference.AcquireFrame();

            //++totalColorAndDepthFrames;
            //++colorAndDepthFrames;

            //if (colorAndDepthFPSTimer.ElapsedMilliseconds >= 1000)
            //{
            //    ColorAndDepthFramesPerSec = colorAndDepthFrames;
            //    colorAndDepthFrames = 0;
            //    colorAndDepthFPSTimer.Restart();
            //}

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
                    ++totalVideoFrames;

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
                if (beamFrames != null)
                {
                    ++totalAudioFrames;

                    var subFrame = beamFrames[0].SubFrames[0];
                    var audioBuffer = new byte[subFrame.FrameLengthInBytes];
                    subFrame.CopyFrameDataToArray(audioBuffer);

                    AudioFrame = audioBuffer;
                }
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

            Debug.WriteLine($"Thresholding took {sw.ElapsedMilliseconds} ms");

            return result;
        }
    }
}
