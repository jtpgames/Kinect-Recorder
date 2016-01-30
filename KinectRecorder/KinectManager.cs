using Microsoft.Kinect;
using Microsoft.Kinect.Tools;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Timers;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace KinectRecorder
{
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct Bgra
    {
        public byte Blue, Green, Red, Alpha;
    }

    public class KinectManager
    {
        #region Constants

        public static readonly int DepthWidth = 512;
        public static readonly int DepthHeight = 424;
        public static readonly int DepthSize = DepthWidth * DepthHeight;
        public static readonly int IRWidth = 512;
        public static readonly int IRHeight = 424;
        public static readonly int IRSize = IRWidth * IRHeight;
        public static readonly int ColorWidth = 1920;
        public static readonly int ColorHeight = 1080;
        public static readonly int ColorSize = ColorWidth * ColorHeight;

        #endregion

        #region Events

        public event EventHandler<IsAvailableChangedEventArgs> KinectAvailabilityChanged;
        private void OnKinectAvailabilityChanged(object sender, IsAvailableChangedEventArgs e)
        {
            KinectAvailabilityChanged?.Invoke(sender, e);
        }

        /// <summary>
        /// Event gets called, when the Color-, Depth-, and IR-Frames arrived.
        /// </summary>
        public event EventHandler<MultiSourceFrameArrivedEventArgs> MultiSourceFrameArrived;
        private void OnMultiSourceFrameArrived(object sender, MultiSourceFrameArrivedEventArgs e)
        {
            MultiSourceFrameArrived?.Invoke(sender, e);
        }

        /// <summary>
        /// Event gets called, when the Color and the Depth Frames have arrived.
        /// </summary>
        public event EventHandler<MultiSourceFrameArrivedEventArgs> ColorAndDepthSourceFrameArrived;
        private void OnColorAndDepthSourceFrameArrived(object sender, MultiSourceFrameArrivedEventArgs e)
        {
            ColorAndDepthSourceFrameArrived?.Invoke(sender, e);
        }

        /// <summary>
        /// Event gets called, when SetUpCustomMultiSourceReader was calles and the requested frames have arrived.
        /// </summary>
        public event EventHandler<MultiSourceFrameArrivedEventArgs> CustomMultiSourceFrameArrived;
        private void OnCustomMultiSourceFrameArrived(object sender, MultiSourceFrameArrivedEventArgs e)
        {
            CustomMultiSourceFrameArrived?.Invoke(sender, e);
        }

        public event EventHandler<ColorFrameArrivedEventArgs> ColorSourceFrameArrived;
        private void OnColorSourceFrameArrived(object sender, ColorFrameArrivedEventArgs e)
        {
            ColorSourceFrameArrived?.Invoke(sender, e);
        }

        public event EventHandler<AudioBeamFrameArrivedEventArgs> AudioSourceFrameArrived;
        private void OnAudioSourceFrameArrived(object sender, AudioBeamFrameArrivedEventArgs e)
        {
            AudioSourceFrameArrived?.Invoke(sender, e);
        }

        public event Action RecordingStopped;
        private void OnRecordingStopped()
        {
            RecordingStopped?.Invoke();
        }

        #endregion

        private KinectSensor _sensor;

        private MultiSourceFrameReader _customMultireader;
        private MultiSourceFrameReader _multireader;
        private MultiSourceFrameReader _colordepthReader;
        private ColorFrameReader _colorReader;
        private AudioBeamFrameReader _audioReader;

        private KStudioClient client;
        private KStudioRecording recording;
        private KStudioPlayback playback;

        private static readonly object syncRoot = new object();

        private static KinectManager instance = null;
        public static KinectManager Instance
        {
            get
            {
                // double checked lock --> Better performance
                if (instance == null)
                {
                    lock (syncRoot)
                    {
                        if (instance == null)
                        {
                            instance = new KinectManager();
                        }
                    }
                }

                return instance;
            }
        }

        public CoordinateMapper CoordinateMapper => _sensor.CoordinateMapper;

        public bool Paused { get; private set; } = false;

        private KinectManager()
        {
            InitializeKinect();
        }

        private void InitializeKinect()
        {
            _sensor = KinectSensor.GetDefault();

            if (_sensor != null)
            {
                _sensor.Open();

                if (_sensor.IsOpen)
                {
                    _sensor.IsAvailableChanged += OnKinectAvailabilityChanged;

                    _multireader = _sensor.OpenMultiSourceFrameReader(FrameSourceTypes.Color |
                                                 FrameSourceTypes.Depth |
                                                 FrameSourceTypes.Infrared);

                    _colordepthReader = _sensor.OpenMultiSourceFrameReader(FrameSourceTypes.Color |
                                                 FrameSourceTypes.Depth);

                    _colorReader = _sensor.ColorFrameSource.OpenReader();

                    _audioReader = _sensor.AudioSource.OpenReader();

                    _multireader.MultiSourceFrameArrived += OnMultiSourceFrameArrived;

                    _colordepthReader.MultiSourceFrameArrived += OnColorAndDepthSourceFrameArrived;

                    _colorReader.FrameArrived += OnColorSourceFrameArrived;

                    _audioReader.FrameArrived += OnAudioSourceFrameArrived;
                }
            }
        }

        #region Public methods

        public AudioBeamFrameList PollAudio()
        {
            return _audioReader.AcquireLatestBeamFrames();
        }

        /// <summary>
        /// Set up a custom multi source reader who is serving the given frameSourceTypes.
        /// 
        /// </summary>
        /// <param name="frameSourceTypes">FrameSourceTypes to deliver.</param>
        public void SetUpCustomMultiSourceReader(FrameSourceTypes frameSourceTypes)
        {
            _customMultireader = _sensor.OpenMultiSourceFrameReader(frameSourceTypes);
            _customMultireader.MultiSourceFrameArrived += OnCustomMultiSourceFrameArrived;
        }

        public void StartRecording(string filePath)
        {
            client = KStudio.CreateClient();

            client.ConnectToService();

            KStudioEventStreamSelectorCollection streamCollection = new KStudioEventStreamSelectorCollection();
            streamCollection.Add(KStudioEventStreamDataTypeIds.UncompressedColor);
            streamCollection.Add(KStudioEventStreamDataTypeIds.Depth);
            // The enum value for Audio is missing. The GUID below was taken from Kinect Studio.
            var Audio = new Guid(0x787c7abd, 0x9f6e, 0x4a85, 0x8d, 0x67, 0x63, 0x65, 0xff, 0x80, 0xcc, 0x69);
            streamCollection.Add(Audio);

            recording = client.CreateRecording(filePath, streamCollection, KStudioRecordingFlags.IgnoreOptionalStreams);
            recording.Start();

            LogConsole.WriteLine("File opened and recording ...");
        }

        public void StopRecording()
        {
            if (recording != null)
            {
                recording.Stop();
                recording.Dispose();
                recording = null;
            }
            if (client != null)
            {
                client.DisconnectFromService();
                client.Dispose();
                client = null;
            }

            LogConsole.WriteLine("Recording stopped");
        }

        public void OpenRecording(string filePath)
        {
            client = KStudio.CreateClient();

            client.ConnectToService();

            KStudioEventStreamSelectorCollection streamCollection = new KStudioEventStreamSelectorCollection();
            streamCollection.Add(KStudioEventStreamDataTypeIds.UncompressedColor);
            streamCollection.Add(KStudioEventStreamDataTypeIds.Depth);
            Guid Audio = new Guid(0x787c7abd, 0x9f6e, 0x4a85, 0x8d, 0x67, 0x63, 0x65, 0xff, 0x80, 0xcc, 0x69);
            streamCollection.Add(Audio);

            playback = client.CreatePlayback(filePath, streamCollection);
            playback.StateChanged += KStudioClient_Playback_StateChanged;
            playback.Start();

            LogConsole.WriteLine("Recording opened and playing ...");
        }

        public void CloseRecording()
        {
            OnRecordingStopped();

            if (playback != null)
            {
                playback.Stop();
                playback.Dispose();
                playback = null;
            }
            if (client != null)
            {
                client.DisconnectFromService();
                client.Dispose();
                client = null;
            }

            LogConsole.WriteLine("Recording stopped");
        }

        private void KStudioClient_Playback_StateChanged(object sender, EventArgs e)
        {
            LogConsole.WriteLine("Playback state: {0}", playback.State.ToString());

            if (playback.State == KStudioPlaybackState.Stopped)
            {
                CloseRecording();
            }
        }

        public void PauseKinect(bool bPause = true)
        {
            _multireader.IsPaused = bPause;
            _colordepthReader.IsPaused = bPause;
            _colorReader.IsPaused = bPause;
            _audioReader.IsPaused = bPause;

            Paused = bPause;
        }

        public byte[] ToByteBuffer(ColorFrame frame)
        {
            int width = frame.FrameDescription.Width;
            int height = frame.FrameDescription.Height;

            byte[] pixels = new byte[width * height * 4];

            if (frame.RawColorImageFormat == ColorImageFormat.Bgra)
            {
                frame.CopyRawFrameDataToArray(pixels);
            }
            else
            {
                frame.CopyConvertedFrameDataToArray(pixels, ColorImageFormat.Bgra);
            }

            return pixels;
        }

        public Bitmap ImageToBitmap(ColorFrame Image)
        {
            try
            {
                var PixelDataLength = Image.FrameDescription.LengthInPixels * 4;
                int width = Image.FrameDescription.Width;
                int height = Image.FrameDescription.Height;

                byte[] pixeldata = new byte[PixelDataLength];

                if (Image.RawColorImageFormat == ColorImageFormat.Bgra)
                {
                    Image.CopyRawFrameDataToArray(pixeldata);
                }
                else
                {
                    Image.CopyConvertedFrameDataToArray(pixeldata, ColorImageFormat.Bgra);
                }

                Bitmap bmap = new Bitmap(width, height, System.Drawing.Imaging.PixelFormat.Format32bppRgb);
                BitmapData bmapdata = bmap.LockBits(
                    new System.Drawing.Rectangle(0, 0, width, height),
                    ImageLockMode.WriteOnly,
                    bmap.PixelFormat);
                IntPtr ptr = bmapdata.Scan0;
                Marshal.Copy(pixeldata, 0, ptr, (int)PixelDataLength);
                bmap.UnlockBits(bmapdata);
                return bmap;
            }
            catch (Exception e)
            {
                return null;
            }
        }

        public ImageSource ToBitmap(ColorFrame frame)
        {
            int width = frame.FrameDescription.Width;
            int height = frame.FrameDescription.Height;

            byte[] pixels = new byte[width * height * ((PixelFormats.Bgr32.BitsPerPixel + 7) / 8)];

            if (frame.RawColorImageFormat == ColorImageFormat.Bgra)
            {
                frame.CopyRawFrameDataToArray(pixels);
            }
            else
            {
                frame.CopyConvertedFrameDataToArray(pixels, ColorImageFormat.Bgra);
            }

            var format = PixelFormats.Bgr32;

            int stride = width * format.BitsPerPixel / 8;

            return BitmapSource.Create(width, height, 96, 96, format, null, pixels, stride);
        }

        public ImageSource ToBitmap(DepthFrame frame)
        {
            int width = frame.FrameDescription.Width;
            int height = frame.FrameDescription.Height;

            ushort minDepth = frame.DepthMinReliableDistance;
            ushort maxDepth = frame.DepthMaxReliableDistance;

            ushort[] depthData = new ushort[width * height];
            byte[] pixelData = new byte[width * height * (PixelFormats.Bgr32.BitsPerPixel + 7) / 8];

            frame.CopyFrameDataToArray(depthData);

            int colorIndex = 0;
            for (int depthIndex = 0; depthIndex < depthData.Length; ++depthIndex)
            {
                ushort depth = depthData[depthIndex];
                byte intensity = (byte)(depth >= minDepth && depth <= maxDepth ? depth : 0);

                pixelData[colorIndex++] = intensity; // Blue
                pixelData[colorIndex++] = intensity; // Green
                pixelData[colorIndex++] = intensity; // Red

                ++colorIndex;
            }

            var format = PixelFormats.Bgr32;

            int stride = width * format.BitsPerPixel / 8;

            return BitmapSource.Create(width, height, 96, 96, format, null, pixelData, stride);
        }

        public ImageSource ToBitmap(InfraredFrame frame)
        {
            int width = frame.FrameDescription.Width;
            int height = frame.FrameDescription.Height;

            ushort[] infraredData = new ushort[width * height];
            byte[] pixelData = new byte[width * height * (PixelFormats.Bgr32.BitsPerPixel + 7) / 8];

            frame.CopyFrameDataToArray(infraredData);

            int colorIndex = 0;
            for (int infraredIndex = 0; infraredIndex < infraredData.Length; ++infraredIndex)
            {
                ushort ir = infraredData[infraredIndex];
                byte intensity = (byte)(ir >> 8);

                pixelData[colorIndex++] = intensity; // Blue
                pixelData[colorIndex++] = intensity; // Green   
                pixelData[colorIndex++] = intensity; // Red

                ++colorIndex;
            }

            var format = PixelFormats.Bgr32;

            int stride = width * format.BitsPerPixel / 8;

            return BitmapSource.Create(width, height, 96, 96, format, null, pixelData, stride);
        }

        #endregion
    }
}
