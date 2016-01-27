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
    class ObjectFilter
    {
        public enum FilterMode
        {
            CPU,
            GPU
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

        static class GPGPUHelper
        {
            public static ComputeShader LoadComputeShader(Device device, string filename, string entrypoint)
            {
                // Compile compute shader
                ShaderBytecode shaderBytecode = null;
                try
                {
                    shaderBytecode = ShaderBytecode.CompileFromFile(filename, entrypoint, "cs_5_0", ShaderFlags.None, EffectFlags.None);
                }
                catch (Exception ex)
                {
                    LogConsole.WriteLine(ex.Message);
                }

                System.Diagnostics.Debug.Assert(shaderBytecode != null);

                return new ComputeShader(device, shaderBytecode);
            }

            public static Buffer CreateConstantBuffer<T>(Device device, T[] data)
                where T : struct
            {
                var elementCount = data.Length;
                var bufferSizeInBytes = Marshal.SizeOf(typeof(T)) * elementCount;

                bufferSizeInBytes = bufferSizeInBytes.RoundUp(16);

                BufferDescription inputBufferDescription = new BufferDescription
                {
                    BindFlags = BindFlags.ConstantBuffer,
                    CpuAccessFlags = CpuAccessFlags.Write,
                    OptionFlags = ResourceOptionFlags.None,
                    SizeInBytes = bufferSizeInBytes,
                    StructureByteStride = 0,
                    Usage = ResourceUsage.Dynamic,
                };

                Buffer inputBuffer = null;
                try
                {
                    inputBuffer = new Buffer(device, inputBufferDescription);
                    DataBox input = device.ImmediateContext.MapSubresource(inputBuffer, MapMode.WriteDiscard, MapFlags.None);
                    input.Data.WriteRange(data);
                    device.ImmediateContext.UnmapSubresource(inputBuffer, 0);
                }
                catch (Exception e)
                {
                    LogConsole.WriteLine(e.Message);
                }

                System.Diagnostics.Debug.Assert(inputBuffer != null);

                return inputBuffer;
            }

            public static UnorderedAccessView CreateUnorderedAccessView(Device device, int width, int height, SlimDX.DXGI.Format format, out Texture2D texture)
            {
                var desc = new UnorderedAccessViewDescription()
                {
                    Dimension = UnorderedAccessViewDimension.Texture2D,
                    ArraySize = 1,
                    ElementCount = width * height,
                    Format = format
                };

                texture = new Texture2D(device, new Texture2DDescription()
                {
                    ArraySize = 1,
                    MipLevels = 1,
                    OptionFlags = ResourceOptionFlags.None,
                    SampleDescription = new SlimDX.DXGI.SampleDescription(1, 0),
                    Usage = ResourceUsage.Default,
                    CpuAccessFlags = CpuAccessFlags.None,
                    BindFlags = BindFlags.ShaderResource | BindFlags.UnorderedAccess,
                    Width = width,
                    Height = height,
                    Format = format
                });

                return new UnorderedAccessView(device, texture);
            }

            public static UnorderedAccessView CreateUnorderedAccessView<T>(Device device, T[] data, int width, int height, SlimDX.DXGI.Format format, out Texture2D texture)
                where T : struct
            {
                throw new NotImplementedException();

                texture = new Texture2D(device, new Texture2DDescription()
                {
                    ArraySize = 1,
                    MipLevels = 1,
                    OptionFlags = ResourceOptionFlags.None,
                    SampleDescription = new SlimDX.DXGI.SampleDescription(1, 0),
                    Usage = ResourceUsage.Dynamic,
                    CpuAccessFlags = CpuAccessFlags.Write,
                    BindFlags = BindFlags.ShaderResource | BindFlags.UnorderedAccess,
                    Width = width,
                    Height = height,
                    Format = format
                });

                var context = device.ImmediateContext;
                DataBox box = context.MapSubresource(texture, 0, 0, MapMode.WriteDiscard, MapFlags.None);
                box.Data.WriteRange(data);
                context.UnmapSubresource(texture, 0);

                return new UnorderedAccessView(device, texture);
            }
        }

        private bool bLastFrameReset;
        private byte[] lastFramePixels;

        private Device device;
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
            return Task.Run(() => computeShader = GPGPUHelper.LoadComputeShader(device, "GPGPU/FilterObjects.compute", "Filter"));
        }

        private void InitGPUSupport()
        {
            // Make device
            device = new Device(DriverType.Hardware, DeviceCreationFlags.Debug, FeatureLevel.Level_11_0);

            // Compile compute shader  
            computeShader = GPGPUHelper.LoadComputeShader(device, "GPGPU/FilterObjects.compute", "Filter");
        }

        ~ObjectFilter()
        {
            computeShader.Dispose();
            device.Dispose();
        }

        public void Reset()
        {
            bLastFrameReset = true;
        }

        public void testgpu()
        {
            // Make device
            Device device = new Device(DriverType.Hardware, DeviceCreationFlags.None, FeatureLevel.Level_11_0);

            ComputeShader compute = GPGPUHelper.LoadComputeShader(device, "GPGPU/Simple.compute", "main");

            Texture2D uavTexture;
            UnorderedAccessView computeResult = GPGPUHelper.CreateUnorderedAccessView(device, 1024, 1024, SlimDX.DXGI.Format.R8G8B8A8_UNorm, out uavTexture);

            device.ImmediateContext.ComputeShader.Set(compute);
            device.ImmediateContext.ComputeShader.SetUnorderedAccessView(computeResult, 0);
            device.ImmediateContext.Dispatch(32, 32, 1);

            Texture2D.ToFile(device.ImmediateContext, uavTexture, ImageFileFormat.Png, "uav.png");
        }

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

            var sw = Stopwatch.StartNew();

            // Create a constant buffer to pass the filter configuration
            var cbuffer = GPGPUHelper.CreateConstantBuffer(device, new int[] { nearThresh, farThresh, haloSize });

            // -- Create GPULists using the immediate context and pass the data --
            GPUList<int4> bgraData = new GPUList<int4>(device.ImmediateContext);
            var bgraDataTransformed = new int4[1920 * 1080];
            for (int i = 0, j = 0;  i < bgra.Length; i += 4, ++j)
            {
                bgraDataTransformed[j] = new int4(bgra[i], bgra[i+1], bgra[i+2], bgra[i+3]);
            }
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

            // Initialize last frame with current color frame, if it was reset
            if (bLastFrameReset)
            {
                lastFramePixels = bgra;
                bLastFrameReset = false;
            }

            GPUList<int4> lastFrameData = new GPUList<int4>(device.ImmediateContext);
            var lastFrameDataTransformed = new int4[1920 * 1080];
            for (int i = 0, j = 0; i < bgra.Length; i += 4, ++j)
            {
                lastFrameDataTransformed[j] = new int4(lastFramePixels[i], lastFramePixels[i + 1], lastFramePixels[i + 2], lastFramePixels[i + 3]);
            }
            lastFrameData.AddRange(lastFrameDataTransformed);

            var resultArray = new int4[1920 * 1080];
            GPUList<int4> resultData = new GPUList<int4>(device.ImmediateContext, resultArray);
            // --

            // Run the compute shader
            device.ImmediateContext.ComputeShader.Set(computeShader);
            device.ImmediateContext.ComputeShader.SetConstantBuffer(cbuffer, 0);
            device.ImmediateContext.ComputeShader.SetUnorderedAccessView(bgraData.UnorderedAccess, 0);
            device.ImmediateContext.ComputeShader.SetUnorderedAccessView(depthData.UnorderedAccess, 1);
            device.ImmediateContext.ComputeShader.SetUnorderedAccessView(depthSpacePointData.UnorderedAccess, 2);
            device.ImmediateContext.ComputeShader.SetUnorderedAccessView(lastFrameData.UnorderedAccess, 3);
            device.ImmediateContext.ComputeShader.SetUnorderedAccessView(resultData.UnorderedAccess, 4);
            // gcd(1920, 1080) = 120
            // 120 * 120 > 1024 --> throws shader compilation exception
            // so we reduce it to 30
            //device.ImmediateContext.Dispatch(1920 / 30, 1080 / 30, 1);
            device.ImmediateContext.Dispatch(1920 * 1080 / 256, 1, 1);

            var result = resultData.ToArray();

            // -- Clean up --
            device.ImmediateContext.ComputeShader.SetConstantBuffer(null, 0);
            device.ImmediateContext.ComputeShader.SetUnorderedAccessView(null, 0);
            device.ImmediateContext.ComputeShader.SetUnorderedAccessView(null, 1);
            device.ImmediateContext.ComputeShader.SetUnorderedAccessView(null, 2);
            device.ImmediateContext.ComputeShader.SetUnorderedAccessView(null, 3);
            device.ImmediateContext.ComputeShader.SetUnorderedAccessView(null, 4);

            cbuffer.Dispose();
            bgraData.Dispose();
            depthData.Dispose();
            depthSpacePointData.Dispose();
            lastFrameData.Dispose();
            resultData.Dispose();
            // --

            var resultBytes = new byte[1920 * 1080 * 4];

            for (int i = 0, j = 0; i < resultBytes.Length; i += 4, ++j)
            {
                resultBytes[i] = (byte)result[j].x;
                resultBytes[i+1] = (byte)result[j].y;
                resultBytes[i+2] = (byte)result[j].z;
                resultBytes[i+3] = (byte)result[j].a;
            }

            lastFramePixels = resultBytes;

            Debug.WriteLine($"Filtering took {sw.ElapsedMilliseconds} ms");

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
