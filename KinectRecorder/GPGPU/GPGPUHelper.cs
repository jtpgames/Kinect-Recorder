using SlimDX;
using SlimDX.D3DCompiler;
using SlimDX.Direct3D11;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

using Buffer = SlimDX.Direct3D11.Buffer;
using Debug = System.Diagnostics.Debug;

namespace KinectRecorder.GPGPU
{
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
}
