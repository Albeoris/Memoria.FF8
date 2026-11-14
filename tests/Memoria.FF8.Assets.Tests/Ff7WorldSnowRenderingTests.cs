using System.Numerics;
using Memoria.FF7.Formats;
using Memoria.FF7.Resources;
using Memoria.FF8.Rendering;

namespace Memoria.FF8.Assets.Tests;

public sealed class Ff7WorldSnowRenderingTests
{
    [Fact]
    public void OriginalSnowLayersKeepNativeScreenPixelRepeatsAlphaAndPureDraws()
    {
        string? root = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR");
        Assert.SkipWhen(string.IsNullOrWhiteSpace(root), "Select original FFVII resources for snow rendering.");
        Ff7Installation installation = Ff7Installation.Discover(root!);
        using Ff7WorldResources resources = Ff7WorldResources.Load(installation, 3);
        Ff7WorldSnowAdapter adapter = new(PcTexture.Read(resources.ReadRequired("snow4.tex")), PcTexture.Read(resources.ReadRequired("snow5.tex")));
        Ff7WorldSnowLayer[] layers = [new(1, 128, 255, 128), new(2, 64, 64, 255)];
        Capture first = new();
        Capture repeated = new();
        adapter.Draw(first, 960, 540, layers);
        adapter.Draw(repeated, 960, 540, layers);
        Assert.Equal(first.Triangles, repeated.Triangles);
        Assert.Equal(4, first.Triangles.Count);
        Assert.Equal(new Vector2(255 / 256f, 0.5f), first.Triangles[0].A.Uv);
        Assert.Equal(new Vector2((255 + 960) / 256f, 0.5f), first.Triangles[0].B.Uv);
        Assert.Equal(new Vector2(255 / 256f, (128 + 540) / 256f), first.Triangles[0].C.Uv);
        Assert.Equal(new Vector4(128 / 255f), first.Triangles[0].A.Color);
        Assert.Equal(new Vector4(64 / 255f, 64 / 255f, 64 / 255f, 128 / 255f), first.Triangles[2].A.Color);
        Assert.All(first.Triangles, triangle =>
        {
            Assert.Equal(TriangleBlend.AlphaAdd, triangle.Material.Blend);
            Assert.Equal(TriangleSampler.PointWrap, triangle.Material.Sampler);
            Assert.Equal(TriangleDepthComparison.Always, triangle.Material.DepthComparison);
            Assert.False(triangle.Material.DepthWrite);
        });
        Capture hidden = new();
        adapter.Draw(hidden, 960, 540, [new(1, 0, 0, 0), new(2, 0, 0, 0)]);
        Assert.Empty(hidden.Triangles);
    }

    private sealed class Capture : ITriangleRenderer
    {
        public List<(TriangleVertex A, TriangleVertex B, TriangleVertex C, TriangleMaterial Material)> Triangles { get; } = [];

        public void Begin(int width, int height, Vector4 clearColor, float clearDepth = 1)
        {
        }

        public void Draw(in TriangleVertex a, in TriangleVertex b, in TriangleVertex c, in TriangleMaterial material)
        {
            Triangles.Add((a, b, c, material));
        }

        public void End()
        {
        }
    }
}
