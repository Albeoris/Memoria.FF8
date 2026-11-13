using System.Diagnostics;
using Memoria.FF7.Formats;
using Memoria.FF7.Resources;
using Memoria.FF8.Rendering;
using Color = Microsoft.Xna.Framework.Color;
using Matrix4x4 = System.Numerics.Matrix4x4;
using Vector3 = System.Numerics.Vector3;
using Vector4 = System.Numerics.Vector4;

namespace Memoria.FF8.App.Tests;

[Collection("GPU rendering")]
public sealed class Ff7RetainedWorldTerrainTests(ITestOutputHelper output)
{
    [Theory]
    [InlineData(0, false)]
    [InlineData(0, true)]
    [InlineData(1, false)]
    [InlineData(2, false)]
    public void OriginalBridgeAndCurvedTerrainMatchTheCpuReference(int bridgeIndex, bool acrossSeam)
    {
        string? root = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR");
        Assert.SkipWhen(string.IsNullOrWhiteSpace(root), "Select original FFVII resources for retained terrain checks.");
        using GpuTestDevice device = new();
        using GpuTriangleRenderer renderer = new(device.Device);
        Ff7Installation installation = Ff7Installation.Discover(root!);
        using Ff7WorldResources resources = Ff7WorldResources.Load(installation);
        Ff7WorldPresentationData data = Ff7PcExecutableData.LoadWorldPresentation(installation, resources);
        var definitions = Ff7PcExecutableData.LoadWorldBridges(installation);
        Ff7WorldBridgeGeometry bridges = new(definitions, new(data));
        Assert.Equal(3, definitions.Count);
        Ff7WorldBridgeDefinition bridge = definitions[bridgeIndex];
        int block = (bridge.ChunkZ >> 2) * 9 + (bridge.ChunkX >> 2);
        int meshIndex = (bridge.ChunkZ & 3) * 4 + (bridge.ChunkX & 3);
        PcWorldMesh mesh = resources.Map.ReadMesh(block, meshIndex);
        Ff7WorldTextureResources textures = new(installation, resources);
        Ff7WorldTerrainAdapter terrain = new(textures);
        Ff7WorldTerrainReference referenceTerrain = new(textures);
        Ff7WorldTextureAnimationResources cycles = new(installation, resources, textures.Descriptors);
        Vector3 origin = acrossSeam ? new(0x48000, 0, -0x38000) : Vector3.Zero;
        Matrix4x4 rotation = Matrix4x4.CreateRotationX(MathF.PI / 4);
        float centerY = mesh.Vertices.Average(position => position.Y);
        Matrix4x4 camera = Matrix4x4.CreateTranslation(-origin.X - 4096, -centerY, -origin.Z - 4096) * rotation * Matrix4x4.CreateTranslation(0, 0, 20000);
        Ff7WorldView view = new(camera, rotation, 400, 640, 480, 320);

        for (int i = 0; i < 2; i++)
        {
            Ff7WorldBridgeDeformation deformation = new(bridge.Index, 7 + i * 11);
            uint counter = i == 0 ? 0u : 18u;
            int Frame(int texture) => cycles.Frame(texture, counter);
            Vector3 Deform(int vertex, Vector3 position) => bridges.Vertex(bridge.ChunkX, bridge.ChunkZ, vertex, position, deformation);
            void Reference()
            {
                renderer.Begin(view.Width, view.Height, Vector4.UnitW, 0);
                referenceTerrain.DrawNative(renderer, mesh, origin, view, Frame, deformVertex: Deform);
                renderer.End();
            }
            void Retained()
            {
                renderer.Begin(view.Width, view.Height, Vector4.UnitW, 0);
                terrain.DrawRetained(renderer, mesh, origin, view, Frame, 5000, bridge, bridges.RotationAxis(bridge, deformation));
                renderer.End();
            }
            Reference();
            Color[] expected = GpuTestDevice.Read(renderer.Target);
            Assert.True(expected.Count(pixel => pixel != Color.Black) > 200);
            Retained();
            Assert.Equal(i == 0 ? 1 : 0, renderer.FrameTerrainUploads);
            Color[] actual = GpuTestDevice.Read(renderer.Target);
            AssertSimilar(expected, actual);
            if (i == 1 && !acrossSeam)
            {
                for (int warmup = 0; warmup < 8; warmup++)
                {
                    Reference();
                    Retained();
                }
                var reference = Measure(Reference, Retained);
                Assert.Equal(0, renderer.FrameTerrainUploads);
                output.WriteLine($"25 frames: reference {reference.ReferenceMilliseconds:F3} ms/{reference.ReferenceBytes} managed bytes, retained {reference.RetainedMilliseconds:F3} ms/{reference.RetainedBytes} managed bytes, {renderer.CachedTerrainBytes} retained terrain bytes.");
            }
        }
    }

    [Fact]
    public void TextureFrameChangesDoNotUploadGeometry()
    {
        string? root = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR");
        Assert.SkipWhen(string.IsNullOrWhiteSpace(root), "Select original FFVII resources for retained animated terrain checks.");
        using GpuTestDevice device = new();
        using GpuTriangleRenderer renderer = new(device.Device);
        Ff7Installation installation = Ff7Installation.Discover(root!);
        using Ff7WorldResources resources = Ff7WorldResources.Load(installation);
        Ff7WorldTextureResources textures = new(installation, resources);
        Ff7WorldTextureAnimationResources cycles = new(installation, resources, textures.Descriptors);
        Ff7WorldTextureCycle cycle = cycles.Cycles.First(candidate => candidate.FrameCount > 1);
        Ff7WorldTextureDescriptor descriptor = textures.Descriptors[cycle.TextureIndex];
        PcWorldTriangle triangle = new(0, 1, 2, 0, new(descriptor.UOffset, descriptor.VOffset), new(descriptor.UOffset + 15, descriptor.VOffset), new(descriptor.UOffset, descriptor.VOffset + 15), checked((ushort)cycle.TextureIndex));
        PcWorldMesh mesh = new(0, 0, [new(-200, -100, 1000), new(200, -100, 1000), new(0, 100, 1000)], [new(0, 4096, 0), new(0, 4096, 0), new(0, 4096, 0)], [triangle]);
        Ff7WorldTerrainAdapter terrain = new(textures);
        Ff7WorldTerrainReference reference = new(textures);
        Ff7WorldView view = new(Matrix4x4.Identity, Matrix4x4.Identity, 400, 640, 480, 240);
        Color[]? first = null;
        for (uint counter = 0; counter <= cycle.Divisor; counter += (uint)cycle.Divisor)
        {
            int Frame(int texture) => cycles.Frame(texture, counter);
            renderer.Begin(view.Width, view.Height, Vector4.UnitW, 0);
            reference.DrawNative(renderer, mesh, Vector3.Zero, view, Frame);
            renderer.End();
            Color[] expected = GpuTestDevice.Read(renderer.Target);
            Assert.True(expected.Count(pixel => pixel != Color.Black) > 200);
            renderer.Begin(view.Width, view.Height, Vector4.UnitW, 0);
            terrain.DrawRetained(renderer, mesh, Vector3.Zero, view, Frame, 5000);
            renderer.End();
            Assert.Equal(counter == 0 ? 1 : 0, renderer.FrameTerrainUploads);
            Color[] actual = GpuTestDevice.Read(renderer.Target);
            AssertSimilar(expected, actual);
            if (first is null)
            {
                first = actual;
            }
            else
            {
                Assert.False(first.SequenceEqual(actual));
            }
        }
    }

    private static void AssertSimilar(Color[] expected, Color[] actual)
    {
        int count = expected.Zip(actual).Count(pair => Math.Max(Math.Abs(pair.First.R - pair.Second.R), Math.Max(Math.Abs(pair.First.G - pair.Second.G), Math.Abs(pair.First.B - pair.Second.B))) > 3);
        Assert.True(count <= expected.Length / 100, $"{count} pixels exceeded the channel tolerance.");
    }

    private static (double ReferenceMilliseconds, long ReferenceBytes, double RetainedMilliseconds, long RetainedBytes) Measure(Action reference, Action retained)
    {
        TimeSpan referenceTime = TimeSpan.Zero;
        TimeSpan retainedTime = TimeSpan.Zero;
        long referenceBytes = 0;
        long retainedBytes = 0;
        for (int i = 0; i < 25; i++)
        {
            if ((i & 1) == 0)
            {
                MeasureFrame(reference, ref referenceTime, ref referenceBytes);
                MeasureFrame(retained, ref retainedTime, ref retainedBytes);
            }
            else
            {
                MeasureFrame(retained, ref retainedTime, ref retainedBytes);
                MeasureFrame(reference, ref referenceTime, ref referenceBytes);
            }
        }
        return (referenceTime.TotalMilliseconds, referenceBytes, retainedTime.TotalMilliseconds, retainedBytes);
    }

    private static void MeasureFrame(Action render, ref TimeSpan elapsed, ref long bytes)
    {
        long allocation = GC.GetAllocatedBytesForCurrentThread();
        long start = Stopwatch.GetTimestamp();
        render();
        elapsed += Stopwatch.GetElapsedTime(start);
        bytes += GC.GetAllocatedBytesForCurrentThread() - allocation;
    }
}
