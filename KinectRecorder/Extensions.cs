using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Windows.Media.Imaging;

using KinectRecorder.Multimedia;

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

        public static MemoryMappedTexture32bpp BGRAToMemoryMappedTextureARGB(this byte[] bgra)
        {
            var tex = new MemoryMappedTexture32bpp(new SharpDX.Size2(KinectManager.ColorWidth, KinectManager.ColorHeight));

            tex.FillWith(bgra.Reverse());

            return tex;
        }

        public static MemoryMappedTexture32bpp ToMemoryMappedTexture(this byte[] pixels)
        {
            var tex = new MemoryMappedTexture32bpp(new SharpDX.Size2(KinectManager.ColorWidth, KinectManager.ColorHeight));

            tex.FillWith(pixels);

            return tex;
        }

        public static string ToFormattedString(this byte[] pixels, int start = 0, int count = 42)
        {
            var sb = new StringBuilder();

            const int lineWidth = 20;
            byte[] arr = new byte[lineWidth];

            int len = start + count;
            len = len.Clamp(len, pixels.Length);
            for (int i = start; i < len; i += lineWidth)
            {
                Array.Copy(pixels, i, arr, 0, lineWidth);
                foreach (var item in arr)
                {
                    sb.AppendFormat(" {0} ", item);
                }
                sb.AppendLine();
            }

            return sb.ToString();
        }
    }

    static class MathExtensions
    {
        public static int RoundUp(this int numToRound, int multiple)
        {
            if (multiple == 0)
            {
                return numToRound;
            }

            int remainder = numToRound % multiple;
            if (remainder == 0)
            {
                return numToRound;
            }

            return numToRound + multiple - remainder;
        }

        /// <summary>
        /// Clamps value within desired range
        /// This is a generic. So use any type you want
        /// </summary>
        /// <param name="value">Value to be clamped</param>
        /// <param name="min">Min range</param>
        /// <param name="max">Max range</param>
        /// <returns>Clamped value within range</returns>
        public static T Clamp<T>(this T value, T min, T max)
            where T : IComparable<T>
        {
            T result = value;
            if (result.CompareTo(max) > 0)
                result = max;
            if (result.CompareTo(min) < 0)
                result = min;

            return result;
        }

        public static T Matrix<T>(this T[] array, int x, int y, int rowLength)
        {
            return array[x + y * rowLength];
        }

        public static T[,] ToMatrix<T>(this T[] array, int columns, int rows)
        {
            Debug.Assert(columns * rows == array.Length);

            var matrix = new T[columns, rows];

            for (int y = 0; y < rows; ++y)
            {
                for (int x = 0; x < columns; ++x)
                {
                    matrix[x, y] = array.Matrix(x, y, columns);
                }
            }

            return matrix;
        }

        public static void Multiply(this float[,] matrix, float scalar)
        {
            int width = matrix.GetLength(0);
            int height = matrix.GetLength(1);

            for (int x = 0; x < width; ++x)
            {
                for (int y = 0; y < height; ++y)
                {
                    matrix[x, y] *= scalar;
                }
            }
        }

        public static float ConvolutePixel(this float[,] matrix, int x, int y, float[,] convolutionKernel)
        {
            // Assert dimensions, Kernel must be odd and square
            Debug.Assert((convolutionKernel.GetLength(0) % 2) != 0);
            Debug.Assert((convolutionKernel.GetLength(1) % 2) != 0);
            Debug.Assert(convolutionKernel.GetLength(0) == convolutionKernel.GetLength(0));

            /*
             * I*(x,y) = sum_i=0^n-1 (sum_j=0^n-1 (I[x + i - a, y + j - a] * K[i, j]))
             * where n is size of kernel
             * and a is the middle index of the kernel
             * Example: 3x3 kernel --> n=3, a=1
             */

            int n = convolutionKernel.GetLength(0);
            int a = n / 2;

            float I = 0;
            for (int i = 0; i < n; ++i)
            {
                for (int j = 0; j < n; ++j)
                {
                    float I_x_y_star;

                    int xm = x + i - a;
                    int ym = y + i - a;

                    // handle boundaries
                    if (xm < 0 || ym < 0)
                        I_x_y_star = 0;
                    else if (xm >= matrix.GetLength(0) || ym >= matrix.GetLength(1))
                        I_x_y_star = 0;
                    else
                        I_x_y_star = matrix[xm, ym];

                    var I_x_y = I_x_y_star * convolutionKernel[i, j];
                    I += I_x_y;
                }
            }

            return I;
        }
    }
}
