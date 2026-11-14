using System.Numerics;
using Memoria.FF7.Resources;
using Memoria.FF8.Rendering;

namespace Memoria.FF8.Assets.Tests;

public sealed class Ff7WorldViewportChunksTests
{
    [Fact]
    public void NativeAndPortraitViewportsKeepOriginalChunkSelection()
    {
        Ff7WorldChunkPlacement[] native = [new(0, 0, 0, 0, Vector3.Zero)];
        Ff7WorldView view = new(Matrix4x4.Identity, Matrix4x4.Identity, 400, 640, 480, 240);
        Assert.Same(native, Ff7WorldViewportChunks.Extend(native, Vector3.Zero, view, 25000));
        Assert.Same(native, Ff7WorldViewportChunks.Extend(native, Vector3.Zero, view with { Width = 480, Height = 640 }, 25000));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(0.7f)]
    [InlineData(2.1f)]
    public void WideViewportCoversUnprojectedFrustumAndPreservesNativeSeamPlacements(float heading)
    {
        Vector3 focus = new(4096, 0, 4096);
        Matrix4x4 camera = Matrix4x4.CreateTranslation(-focus) * Matrix4x4.CreateRotationY(heading) * Matrix4x4.CreateTranslation(0, 0, 10000);
        Ff7WorldView view = new(camera, Matrix4x4.CreateRotationY(heading), 400, 1600, 480, 320) { OffsetX = 10 };
        Ff7WorldChunkPlacement[] native = [new(35, 27, 62, 15, new(-8192, 0, -8192))];
        IReadOnlyList<Ff7WorldChunkPlacement> chunks = Ff7WorldViewportChunks.Extend(native, focus, view, 25000);
        Assert.Equal(native[0], chunks[0]);
        Assert.Equal(chunks.Count, chunks.Select(chunk => chunk.Origin).Distinct().Count());
        Assert.True(Matrix4x4.Invert(camera, out Matrix4x4 inverse));
        foreach (float depth in new[] { 1f, 25000f })
        {
            foreach (int x in new[] { 0, 1600 })
            {
                foreach (int y in new[] { 0, 480 })
                {
                    Vector3 corner = Vector3.Transform(new((x - 800 - 10) * depth / 400, (y - 320) * depth / 400, depth), inverse);
                    Vector3 origin = new(MathF.Floor(corner.X / 8192) * 8192, 0, MathF.Floor(corner.Z / 8192) * 8192);
                    Assert.Contains(chunks, chunk => chunk.Origin == origin);
                }
            }
        }
        Assert.All(chunks, chunk =>
        {
            Assert.InRange(chunk.ChunkX, 0, 35);
            Assert.InRange(chunk.ChunkZ, 0, 27);
            Assert.InRange(chunk.BlockIndex, 0, 62);
            Assert.Equal((chunk.ChunkZ & 3) * 4 + (chunk.ChunkX & 3), chunk.MeshIndex);
        });
    }
}
