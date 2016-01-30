using AForge;
using KinectRecorder.GPGPU;
using Microsoft.Kinect;
using SlimDX;
using SlimDX.D3DCompiler;
using SlimDX.Direct3D11;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

using Buffer = SlimDX.Direct3D11.Buffer;
using Debug = System.Diagnostics.Debug;

namespace KinectRecorder
{
    class ObjectFilter : IDisposed
    {
        public enum FilterMode
        {
            CPU,
            GPU
        }

        struct int2
        {
            public int x, y;

            public int2(int x, int y)
            {
                this.x = x;
                this.y = y;
            }
        }

        struct int4
        {
            public int x, y, z, a;

            public int4(int x, int y, int z, int a)
            {
                this.x = x;
                this.y = y;
                this.z = z;
                this.a = a;
            }
        }

        private bool bLastFrameReset;
        private byte[] lastFramePixels;

        private Device device;
        private ShaderBytecode shaderByteCode;
        private ComputeShader computeShader;

        public static Task<ObjectFilter> CreateObjectFilterWithGPUSupportAsync()
        {
            throw new NotImplementedException();
        }

        public static ObjectFilter CreateObjectFilterWithGPUSupport()
        {
            var filter = new ObjectFilter();
            filter.InitGPUSupport();
            return filter;
        }

        public FilterMode _FilterMode { get; set; }

        public ObjectFilter()
        {
            Reset();
        }

        private Task InitGPUSupportAsync()
        {
            // Make device
            device = new Device(DriverType.Hardware, DeviceCreationFlags.Debug, FeatureLevel.Level_11_0);

            // Compile compute shader
            return Task.Run(() => computeShader = GPGPUHelper.LoadComputeShader(device, "GPGPU/FilterObjects.compute", "Filter", out shaderByteCode));
        }

        private void InitGPUSupport()
        {
            // Make device
            device = new Device(DriverType.Hardware, DeviceCreationFlags.Debug, FeatureLevel.Level_11_0);

            // Compile compute shader  
            computeShader = GPGPUHelper.LoadComputeShader(device, "GPGPU/FilterObjects.compute", "Filter", out shaderByteCode);
        }

        #region IDisposable Support
        private bool disposedValue = false; // To detect redundant calls
        public bool Disposed => disposedValue;

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    shaderByteCode.SafeDispose();
                    computeShader.SafeDispose();
                    device.SafeDispose();
                }

                lastFramePixels = null;

                disposedValue = true;
            }
        }

        public void Dispose()
        {
            Dispose(true);
        }
        #endregion

        public void Reset()
        {
            bLastFrameReset = true;
        }

#if DEBUG
        public void testgpu()
        {
            // Make device
            Device device = new Device(DriverType.Hardware, DeviceCreationFlags.None, FeatureLevel.Level_11_0);

            ShaderBytecode byteCode;
            ComputeShader compute = GPGPUHelper.LoadComputeShader(device, "GPGPU/Simple.compute", "main", out byteCode);

            Texture2D uavTexture;
            UnorderedAccessView computeResult = GPGPUHelper.CreateUnorderedAccessView(device, 1024, 1024, SlimDX.DXGI.Format.R8G8B8A8_UNorm, out uavTexture);

            device.ImmediateContext.ComputeShader.Set(compute);
            device.ImmediateContext.ComputeShader.SetUnorderedAccessView(computeResult, 0);
            device.ImmediateContext.Dispatch(32, 32, 1);

            Texture2D.ToFile(device.ImmediateContext, uavTexture, ImageFileFormat.Png, "uav.png");
        }
#endif

        public Task<byte[]> FilterAsync(byte[] bgra, ushort[] depth, DepthSpacePoint[] depthSpaceData,
            int nearThresh, int farThresh, int haloSize)
        {
            switch (_FilterMode)
            {
                case FilterMode.CPU:
                    return FilterCPUAsync(bgra, depth, depthSpaceData, nearThresh, farThresh, haloSize);
                case FilterMode.GPU:
                    return FilterGPUAsync(bgra, depth, depthSpaceData, nearThresh, farThresh, haloSize);
                default:
                    return Task.FromResult(new byte[0]);
            }
        }

        public byte[] Filter(byte[] bgra, ushort[] depth, DepthSpacePoint[] depthSpaceData,
            int nearThresh, int farThresh, int haloSize)
        {
            switch (_FilterMode)
            {
                case FilterMode.CPU:
                    return FilterCPU(bgra, depth, depthSpaceData, nearThresh, farThresh, haloSize);
                case FilterMode.GPU:
                    return FilterGPU(bgra, depth, depthSpaceData, nearThresh, farThresh, haloSize);
                default:
                    return new byte[0];
            }
        }

        public Task<byte[]> FilterGPUAsync(byte[] bgra, ushort[] depth, DepthSpacePoint[] depthSpaceData,
            int nearThresh, int farThresh, int haloSize)
        {
            return Task.Run(() => FilterGPU(bgra, depth, depthSpaceData, nearThresh, farThresh, haloSize));
        }

        public byte[] FilterGPU(byte[] bgra, ushort[] depth, DepthSpacePoint[] depthSpaceData,
            int nearThresh, int farThresh, int haloSize)
        {
            if (computeShader == null)
            {
                return new byte[0];
            }

            // Initialize last frame with current color frame, if it was reset
            if (bLastFrameReset)
            {
                lastFramePixels = bgra;
                bLastFrameReset = false;
            }

            // -- Create halo array --

            List<int2> halo = new List<int2>();

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
                        halo.Add(new int2(x, y));
                        ++actualHaloSize;
                    }
                }
            }

            // --

            // -- Perform data transformations so the arrays can be passed to the GPU --

            var bgraDataTransformed = new int4[1920 * 1080];
            for (int i = 0, j = 0; i < bgra.Length; i += 4, ++j)
            {
                bgraDataTransformed[j] = new int4(bgra[i], bgra[i + 1], bgra[i + 2], bgra[i + 3]);
            }

            var lastFrameDataTransformed = new int4[1920 * 1080];
            for (int i = 0, j = 0; i < bgra.Length; i += 4, ++j)
            {
                lastFrameDataTransformed[j] = new int4(lastFramePixels[i], lastFramePixels[i + 1], lastFramePixels[i + 2], lastFramePixels[i + 3]);
            }

            // --

            //var sw = Stopwatch.StartNew();

            // Create a constant buffer to pass the filter configuration
            var cbuffer = GPGPUHelper.CreateConstantBuffer(device, new int[] { nearThresh, farThresh, haloSize });

            // -- Create GPULists using the immediate context and pass the data --

            GPUList<int4> bgraData = new GPUList<int4>(device.ImmediateContext);
            bgraData.AddRange(bgraDataTransformed);

            GPUList<uint> depthData = new GPUList<uint>(device.ImmediateContext);
            depthData.AddRange(depth.Select(d => (uint)d));

            GPUList<DepthSpacePoint> depthSpacePointData = new GPUList<DepthSpacePoint>(device.ImmediateContext, depthSpaceData);
            //depthSpacePointData.AddRange(depthSpaceData.Select(dsp => {

            //    if (dsp.X == float.NegativeInfinity || dsp.Y == -float.NegativeInfinity)
            //    {
            //        return new DepthSpacePoint() { X = -1, Y = -1 };
            //    }
            //    else
            //    {
            //        return dsp;
            //    }
            //}));

            GPUList<int4> lastFrameData = new GPUList<int4>(device.ImmediateContext);
            lastFrameData.AddRange(lastFrameDataTransformed);

            var resultArray = new int4[1920 * 1080];
            GPUList<int4> resultData = new GPUList<int4>(device.ImmediateContext, resultArray);

            GPUList<int2> haloData = new GPUList<int2>(device.ImmediateContext, halo);

            // --

            var sw = Stopwatch.StartNew();

            // Set the buffers and uavs
            device.ImmediateContext.ComputeShader.Set(computeShader);
            device.ImmediateContext.ComputeShader.SetConstantBuffer(cbuffer, 0);
            device.ImmediateContext.ComputeShader.SetUnorderedAccessView(bgraData.UnorderedAccess, 0);
            device.ImmediateContext.ComputeShader.SetUnorderedAccessView(depthData.UnorderedAccess, 1);
            device.ImmediateContext.ComputeShader.SetUnorderedAccessView(depthSpacePointData.UnorderedAccess, 2);
            device.ImmediateContext.ComputeShader.SetUnorderedAccessView(lastFrameData.UnorderedAccess, 3);
            device.ImmediateContext.ComputeShader.SetUnorderedAccessView(resultData.UnorderedAccess, 4);
            device.ImmediateContext.ComputeShader.SetUnorderedAccessView(haloData.UnorderedAccess, 5);

            // Run the compute shader
            device.ImmediateContext.Dispatch(1920 * 1080 / 256, 1, 1);

            // Get result. This call blocks, until the result was calculated
            // because the MapSubresource call waits.
            var result = resultData.ToArray();

            sw.Stop();

            // -- Clean up --

            device.ImmediateContext.ComputeShader.SetConstantBuffer(null, 0);
            device.ImmediateContext.ComputeShader.SetUnorderedAccessView(null, 0);
            device.ImmediateContext.ComputeShader.SetUnorderedAccessView(null, 1);
            device.ImmediateContext.ComputeShader.SetUnorderedAccessView(null, 2);
            device.ImmediateContext.ComputeShader.SetUnorderedAccessView(null, 3);
            device.ImmediateContext.ComputeShader.SetUnorderedAccessView(null, 4);
            device.ImmediateContext.ComputeShader.SetUnorderedAccessView(null, 5);

            cbuffer.Dispose();
            bgraData.Dispose();
            depthData.Dispose();
            depthSpacePointData.Dispose();
            lastFrameData.Dispose();
            resultData.Dispose();
            haloData.Dispose();

            // --

            Debug.WriteLine($"Filtering took {sw.ElapsedMilliseconds} ms");

            var resultBytes = new byte[1920 * 1080 * 4];

            for (int i = 0, j = 0; i < resultBytes.Length; i += 4, ++j)
            {
                resultBytes[i] = (byte)result[j].x;
                resultBytes[i+1] = (byte)result[j].y;
                resultBytes[i+2] = (byte)result[j].z;
                resultBytes[i+3] = (byte)result[j].a;
            }

            lastFramePixels = resultBytes;

            return resultBytes;
        }

        public Task<byte[]> FilterCPUAsync(byte[] bgra, ushort[] depth, DepthSpacePoint[] depthSpaceData,
            int nearThresh, int farThresh, int haloSize)
        {
            return Task.Run(() => Filter(bgra, depth, depthSpaceData, nearThresh, farThresh, haloSize));
        }

        public unsafe byte[] FilterCPU(byte[] bgra, ushort[] depth, DepthSpacePoint[] depthSpaceData,
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

            Debug.WriteLine($"Filtering took {sw.ElapsedMilliseconds} ms");

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
