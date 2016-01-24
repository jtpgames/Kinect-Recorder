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
    /// <summary>
    /// Interaction logic for RawStreamRecorder.xaml
    /// </summary>
    public partial class RawStreamRecorder : Window
    {
        private bool bRecordWithAudio = true;

        private BlockingCollection<Bitmap> bitmapsToWrite = new BlockingCollection<Bitmap>();
        private VideoFileWriter videoWriter = null;

        private Stream audioRecordingStream = null;
        private WavRecorder wavRecorder = null;

        private WaveFile waveFile = null;

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

        public RawStreamRecorder()
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

                        if (videoWriter != null && videoWriter.IsOpen && !bitmapsToWrite.IsAddingCompleted)
                        {
                            var bitmap = KinectManager.Instance.ImageToBitmap(frame);
                            bitmapsToWrite.Add(bitmap);
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
                sfd.Filter = "AVI Video|*.avi|MPEG-4 Video|*.mp4|All files|*.*";
                sfd.Title = "Save the recording";

                if (sfd.ShowDialog() == true)
                {
                    RecordVideo(sfd.FileName);

                    if (bRecordWithAudio)
                    {
                        var path = Path.GetDirectoryName(sfd.FileName);
                        var wavFileName = Path.Combine(path, Path.GetFileNameWithoutExtension(sfd.FileName) + ".wav");
                        
                        RecordAudio2(wavFileName);
                    }
                }
            }
            else
            {
                bitmapsToWrite.CompleteAdding();

                if (wavRecorder != null)
                {
                    wavRecorder.StopRecording();
                }

                if (waveFile != null)
                {
                    KinectManager.Instance.AudioSourceFrameArrived -= Reader_AudioSoureFrameArrived2;
                    waveFile.Close();
                }
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

        private void RecordVideo(string filename)
        {
            videoWriter = new VideoFileWriter();

            videoWriter.Open(filename, 1920, 1080, 30, VideoCodec.MPEG4, 7 * 1000 * 1000);

            Task.Factory.StartNew(RecordFrames, TaskCreationOptions.LongRunning);
        }

        private void RecordFrames()
        {
            foreach (var bitmap in bitmapsToWrite.GetConsumingEnumerable())
            {
                if (videoWriter != null && videoWriter.IsOpen)
                {
                    videoWriter.WriteVideoFrame(bitmap);
                }
            }

            videoWriter.Close();
            videoWriter = null;
        }

        private async void RecordAudio(string filename)
        {
            wavRecorder = new WavRecorder();

            KinectManager.Instance.AudioSourceFrameArrived += Reader_AudioSoureFrameArrived;

            audioRecordingStream = new BlockingMemoryStream();
            await wavRecorder.RecordAsync(filename, audioRecordingStream);

            KinectManager.Instance.AudioSourceFrameArrived -= Reader_AudioSoureFrameArrived;
        }

        private void RecordAudio2(string filename)
        {
            waveFile = new WaveFile();
            waveFile.Open(filename);

            KinectManager.Instance.AudioSourceFrameArrived += Reader_AudioSoureFrameArrived2;
        }

        private async void Reader_AudioSoureFrameArrived(object sender, AudioBeamFrameArrivedEventArgs e)
        {
            using (var beamFrames = e.FrameReference.AcquireBeamFrames())
            {
                if (beamFrames == null)
                {
                    return;
                }

                var subFrame = beamFrames[0].SubFrames[0];
                var audioBuffer = new byte[subFrame.FrameLengthInBytes];
                subFrame.CopyFrameDataToArray(audioBuffer);

                audioRecordingStream.Write(audioBuffer, 0, audioBuffer.Length);
                //await audioRecordingStream.WriteAsync(audioBuffer, 0, audioBuffer.Length);
            }
        }

        private void Reader_AudioSoureFrameArrived2(object sender, AudioBeamFrameArrivedEventArgs e)
        {
            using (var beamFrames = e.FrameReference.AcquireBeamFrames())
            {
                if (beamFrames == null)
                {
                    return;
                }

                var subFrame = beamFrames[0].SubFrames[0];
                var audioBuffer = new byte[subFrame.FrameLengthInBytes];
                subFrame.CopyFrameDataToArray(audioBuffer);

                waveFile.Write(audioBuffer);
            }
        }
    }
}
