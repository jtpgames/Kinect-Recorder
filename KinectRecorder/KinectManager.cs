using Microsoft.Kinect;
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
        public static readonly int DepthWidth = 512;
        public static readonly int DepthHeight = 424;
        public static readonly int DepthSize = DepthWidth * DepthHeight;
        public static readonly int IRWidth = 512;
        public static readonly int IRHeight = 424;
        public static readonly int IRSize = IRWidth * IRHeight;
        public static readonly int ColorWidth = 1920;
        public static readonly int ColorHeight = 1080;
        public static readonly int ColorSize = ColorWidth * ColorHeight;

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

        private KinectSensor _sensor;
        private MultiSourceFrameReader _multireader;
        private MultiSourceFrameReader _colordepthReader;
        private ColorFrameReader _colorReader;
        private AudioBeamFrameReader _audioReader;

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

        public CoordinateMapper CoordinateMapper => _sensor.CoordinateMapper;

        public void PauseKinect(bool bPause = true)
        {
            _multireader.IsPaused = bPause;
            _colordepthReader.IsPaused = bPause;
        }

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

        private void Reader_MultiSourceFrameArrived(object sender, MultiSourceFrameArrivedEventArgs e)
        {
            // Get a reference to the multi-frame
            var reference = e.FrameReference.AcquireFrame();

            // Open color frame
            using (var frame = reference.ColorFrameReference.AcquireFrame())
            {
                if (frame != null)
                {
                    
                }
            }

            // Open depth frame
            using (var frame = reference.DepthFrameReference.AcquireFrame())
            {
                if (frame != null)
                {
                    
                }
            }

            // Open infrared frame
            using (var frame = reference.InfraredFrameReference.AcquireFrame())
            {
                if (frame != null)
                {
                    
                }
            }
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
    }
}
