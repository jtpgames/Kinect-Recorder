using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace KinectRecorder
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        [DllImport("dwmapi.dll")]
        public static extern int DwmEnableMMCSS(bool fEnable);

        public MainWindow()
        {
            InitializeComponent();

            DwmEnableMMCSS(true);
        }

        private void window_Closed(object sender, EventArgs e)
        {
            ViewModel.ViewModelLocator.Cleanup();
        }
    }
}
