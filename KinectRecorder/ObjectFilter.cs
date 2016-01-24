using AForge;
using Microsoft.Kinect;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace KinectRecorder
{
    class ObjectFilter
    {
        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        struct Rgba
        {
            public byte Red, Green, Blue, Alpha;
        }

        private T FromByteArray<T>(byte[] array, int startIndex)
        {
            int objsize = Marshal.SizeOf(typeof(T));
            IntPtr buff = Marshal.AllocHGlobal(objsize);
            Marshal.Copy(array, startIndex, buff, objsize);
            T retStruct = (T)Marshal.PtrToStructure(buff, typeof(T));
            Marshal.FreeHGlobal(buff);
            return retStruct;
        }

        private Rgba ReadUsingPointer(byte[] data, int startIndex)
        {
            unsafe
            {
                fixed (byte* ptr = &data[startIndex])
                {
                    return *(Rgba*)ptr;
                }
            }
        }

        private bool bLastFrameReset;
        private byte[] lastFramePixels;

        public ObjectFilter()
        {
            Reset();
        }

        public void Reset()
        {
            bLastFrameReset = true;
        }

        public Task<byte[]> FilterAsync(byte[] bgra, ushort[] depth, DepthSpacePoint[] depthSpaceData,
            int nearThresh, int farThresh, int haloSize)
        {
            return Task.Run(() => Filter(bgra, depth, depthSpaceData, nearThresh, farThresh, haloSize));
        }

        public byte[] Filter(byte[] bgra, ushort[] depth, DepthSpacePoint[] depthSpaceData,
            int nearThresh, int farThresh, int haloSize)
        {
            var sw = Stopwatch.StartNew();

            List<IntPoint> halo = new List<IntPoint>();

            int s = haloSize;
            int xd = s;
            int yd = s / 2;
            int S = (xd + yd) / 2;
            int x0 = -xd;
            int x1 = +xd;
            int y0 = -yd;
            int y1 = +yd;
            int actualHaloSize = 0;
            for (int y = y0; y < y1; ++y)
            {
                for (int x = x0; x < x1; ++x)
                {
                    if (Math.Abs(x) + Math.Abs(y) <= S)
                    {
                        halo.Add(new IntPoint(x, y));
                        ++actualHaloSize;
                    }
                }
            }

            var haloArray = halo.ToArray();

            byte[] result = new byte[KinectManager.ColorSize * 4];

            // Initialize last frame with current color frame, if it was reset
            if (bLastFrameReset)
            {
                lastFramePixels = bgra;
                bLastFrameReset = false;
            }

            for (int colorIndex = 0, depthSpaceIndex = 0; depthSpaceIndex < KinectManager.ColorSize; ++depthSpaceIndex, colorIndex += 4)
            {
                DepthSpacePoint dsp = depthSpaceData[depthSpaceIndex];

                // show last frame by default
                AForge.Imaging.RGB src = new AForge.Imaging.RGB(
                    lastFramePixels[colorIndex],
                    lastFramePixels[colorIndex + 1],
                    lastFramePixels[colorIndex + 2],
                    lastFramePixels[colorIndex + 3]);

                //var src = ReadUsingPointer(lastFramePixels, colorIndex);

                if (dsp.X != float.NegativeInfinity && dsp.Y != -float.NegativeInfinity)
                {
                    int dx = (int)Math.Round(dsp.X);
                    int dy = (int)Math.Round(dsp.Y);

                    if (0 <= dx && dx < KinectManager.DepthWidth && 0 <= dy && dy < KinectManager.DepthHeight
                            && AllDepthsValidWithinHalo(new IntPoint(dx, dy), haloArray, depth,
                                                        nearThresh, farThresh, actualHaloSize))
                    {
                        // show video
                        src = new AForge.Imaging.RGB(
                            bgra[colorIndex],
                            bgra[colorIndex + 1],
                            bgra[colorIndex + 2],
                            bgra[colorIndex + 3]);

                        //src = ReadUsingPointer(bgra, colorIndex);
                    }
                }

                result[colorIndex] = src.Red;
                result[colorIndex+1] = src.Green;
                result[colorIndex+2] = src.Blue;
                result[colorIndex+3] = src.Alpha;
            }

            lastFramePixels = result;

            Console.WriteLine($"Filtering took {sw.ElapsedMilliseconds} ms");

            return result;
        }

        private bool AllDepthsValidWithinHalo(IntPoint coord, IntPoint[] halo, ushort[] depthData,
            int nearThresh, int farThresh, int haloSize)
        {
            //int depth = depthData[coord.X + coord.Y * KinectManager.DepthWidth];
            //if (depth < nearThresh || depth > farThresh)
            //    return false;
            //else
            //    return true;

            for (int i = 0; i < haloSize; ++i)
            {
                IntPoint neighbor = coord + halo[i];
                int depth = depthData[(neighbor.X + neighbor.Y * KinectManager.DepthWidth)];
                if (depth < nearThresh || depth > farThresh)
                {
                    return false;
                }
            }

            return true;
        }
    }
}
