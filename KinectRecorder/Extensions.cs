using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace KinectRecorder
{
    static class ByteArrayExtensions
    {
        public static BitmapSource ToBgr32BitMap(this byte[] pixels)
        {
            var format = PixelFormats.Bgr32;

            int stride = KinectManager.ColorWidth * format.BitsPerPixel / 8;

            var image = BitmapSource.Create(KinectManager.ColorWidth, KinectManager.ColorHeight,
                96, 96,
                format, null,
                pixels, stride);

            return image;
        }
    }
}
