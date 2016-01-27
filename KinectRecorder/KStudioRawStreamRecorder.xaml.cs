using Microsoft.Kinect;
using Microsoft.Win32;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

using AForge.Video.FFMPEG;
using System;
using System.Runtime.InteropServices;
using System.Drawing;
using System.Drawing.Imaging;
using System.Diagnostics;
using System.ComponentModel;
using System.Collections.Concurrent;

namespace KinectRecorder
{
    public partial class KStudioRawStreamRecorder : Window
    {
        private bool bRecordWithAudio = true;

        private Stopwatch sw;
        private int totalFrames = 0;
        private int fps = 0;

        enum ECameraSource
        {
            Color,
            Depth,
            Infrared,
        }

        ECameraSource cameraSource = ECameraSource.Color;

        public KStudioRawStreamRecorder()
        {
            InitializeComponent();

            KinectManager.Instance.MultiSourceFrameArrived += Reader_MultiSourceFrameArrived;

            sw = Stopwatch.StartNew();
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            KinectManager.Instance.MultiSourceFrameArrived -= Reader_MultiSourceFrameArrived;

            base.OnClosing(e);
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
                    if (cameraSource == ECameraSource.Color)
                    {
                        var image = KinectManager.Instance.ToBitmap(frame);
                        camera.Source = image;

                        ++totalFrames;

                        if (sw.ElapsedMilliseconds >= 1000)
                        {
                            fps = totalFrames;
                            lbl_fps.Content = fps;
                            totalFrames = 0;
                            sw.Restart();
                        }
                    }
                }
            }

            // Open depth frame
            using (var frame = reference.DepthFrameReference.AcquireFrame())
            {
                if (frame != null)
                {
                    if (cameraSource == ECameraSource.Depth)
                    {
                        camera.Source = KinectManager.Instance.ToBitmap(frame);

                        ++totalFrames;

                        if (sw.ElapsedMilliseconds >= 1000)
                        {
                            fps = totalFrames;
                            lbl_fps.Content = fps;
                            totalFrames = 0;
                            sw.Restart();
                        }
                    }
                }
            }

            // Open infrared frame
            using (var frame = reference.InfraredFrameReference.AcquireFrame())
            {
                if (frame != null)
                {
                    if (cameraSource == ECameraSource.Infrared)
                    {
                        camera.Source = KinectManager.Instance.ToBitmap(frame);

                        ++totalFrames;

                        if (sw.ElapsedMilliseconds >= 1000)
                        {
                            fps = totalFrames;
                            lbl_fps.Content = fps;
                            totalFrames = 0;
                            sw.Restart();
                        }
                    }
                }
            }
        }

        #region Click handlers

        private void Color_Click(object sender, RoutedEventArgs e)
        {
            cameraSource = ECameraSource.Color;
        }

        private void Depth_Click(object sender, RoutedEventArgs e)
        {
            cameraSource = ECameraSource.Depth;
        }

        private void Infrared_Click(object sender, RoutedEventArgs e)
        {
            cameraSource = ECameraSource.Infrared;
        }

        private void Recording_Click(object sender, RoutedEventArgs e)
        {
            if (btn_recording.IsChecked)
            {
                var sfd = new SaveFileDialog();
                sfd.Filter = "Kinect Eventstream|*.xef|All files|*.*";
                sfd.Title = "Save the recording";

                if (sfd.ShowDialog() == true)
                {
                    KinectManager.Instance.StartRecording(sfd.FileName);
                }
            }
            else
            {
                KinectManager.Instance.StopRecording();
            }
        }

        private void Recording_Audio_Click(object sender, RoutedEventArgs e)
        {
            bRecordWithAudio = !bRecordWithAudio;
        }

        private void Exit_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        #endregion
    }
}
