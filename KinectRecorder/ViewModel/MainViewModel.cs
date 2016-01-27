using GalaSoft.MvvmLight;
using GalaSoft.MvvmLight.CommandWpf;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;

namespace KinectRecorder.ViewModel
{
    public class MainViewModel : ViewModelBase
    {
        public ObservableCollection<string> OutputLog => LogConsole.Logs;

        public RelayCommand OpenRawStreamRecWindowCommand { get; private set; }

        public RelayCommand<Window> ExitCommand { get; private set; }

        /// <summary>
        /// Initializes a new instance of the MainViewModel class.
        /// </summary>
        public MainViewModel()
        {
            if (IsInDesignMode)
            {
                OutputLog.Add("Initializing kinect ...");
                OutputLog.Add("Kinect up and running");
                OutputLog.Add("Calculating the answer to the Ultimate Question of Life, the Universe, and Everything!!");
            }
            else
            {
                OpenRawStreamRecWindowCommand = new RelayCommand(() => new KStudioRawStreamRecorder().Show());
                ExitCommand = new RelayCommand<Window>(w => w.Close());
            }
        }
    }
}