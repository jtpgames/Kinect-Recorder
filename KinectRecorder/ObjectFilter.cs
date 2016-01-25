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

        public unsafe byte[] Filter(byte[] bgra, ushort[] depth, DepthSpacePoint[] depthSpaceData,
            int nearThresh, int farThresh, int haloSize)
        {
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

            var sw = Stopwatch.StartNew();

            fixed (byte* bgraPtr = bgra)
            {
                var pBGR = (Bgra*)bgraPtr;
                fixed (byte* resultPtr = result)
                {
                    var dst = (Bgra*)resultPtr;

                    fixed (byte* lastFramePtr = lastFramePixels)
                    {
                        var pLastframe = (Bgra*)lastFramePtr;

                        for (int colorIndex = 0; colorIndex < KinectManager.ColorSize; ++colorIndex)
                        {
                            DepthSpacePoint dsp = depthSpaceData[colorIndex];

                            // show last frame by default
                            var src = pLastframe + colorIndex;

                            if (dsp.X != float.NegativeInfinity && dsp.Y != -float.NegativeInfinity)
                            {
                                int dx = (int)Math.Round(dsp.X);
                                int dy = (int)Math.Round(dsp.Y);

                                if (0 <= dx && dx < KinectManager.DepthWidth && 0 <= dy && dy < KinectManager.DepthHeight
                                        && AllDepthsValidWithinHalo(dx, dy, haloArray, depth,
                                                                    nearThresh, farThresh, actualHaloSize))
                                {
                                    // show video
                                    src = pBGR + colorIndex;
                                }
                            }

                            dst[colorIndex] = *src;
                        }
                    }
                }
            }

            lastFramePixels = result;

            Console.WriteLine($"Filtering took {sw.ElapsedMilliseconds} ms");

            return result;
        }

        private bool AllDepthsValidWithinHalo(int coordx, int coordy, IntPoint[] halo, ushort[] depthData,
            int nearThresh, int farThresh, int haloSize)
        {
            for (int i = 0; i < haloSize; ++i)
            {
                int neighborx = coordx + halo[i].X;
                int neighbory = coordy + halo[i].Y;
                int depth = depthData[(neighborx + neighbory * KinectManager.DepthWidth)];
                if (depth < nearThresh || depth > farThresh)
                {
                    return false;
                }
            }

            return true;
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
