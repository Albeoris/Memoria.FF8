using System.Numerics;
using Memoria.FF7.Resources;
using Memoria.FF8.Rendering;

namespace Memoria.FF8.Tests;

public sealed class Ff7WorldEnvironmentTests
{
    [Fact]
    public void OriginalSurfaceEnvironmentUsesNativeChunksLightingRadarAndPureDraws()
    {
        string? root = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR");
        Assert.SkipWhen(string.IsNullOrWhiteSpace(root), "Select original FFVII resources for the world environment check.");
        Ff7Installation installation = Ff7Installation.Discover(root);
        using Ff7WorldResources world = Ff7WorldResources.Load(installation);
        Ff7WorldPresentationData presentation = Ff7PcExecutableData.LoadWorldPresentation(installation, world);
        Ff7WorldEnvironmentResources resources = Ff7WorldEnvironmentResources.Load(installation, world);
        Assert.Equal(new Vector4(0, 0, 0, 1), resources.ClearColor);
        Assert.Equal(new Vector3(0, 0, 255), resources.DefaultSkyColors.Upper);
        Assert.Equal(new Vector3(250, 255, 255), resources.DefaultSkyColors.Lower);
        Assert.Equal(128, resources.Radar.Width);
        Assert.Equal(64, resources.Radar.Height);
        Assert.Equal(256, resources.Clouds.Width);
        Assert.Equal(64, resources.Clouds.Height);
        Ff7WorldSkyZone zone = new(0, 185605, 120033, 1, 10, 10, new(0, 0, 65), new(145, 255, 190));
        Ff7WorldSkyColors colors = resources.SkyColorsAt(new(185636, 0, 123325), [zone]);
        Assert.Equal(new Vector3(0, 0, 65), colors.Upper);
        Assert.Equal(new Vector3(145, 255, 190), colors.Lower);
        Ff7WorldSkyColors halfway = resources.SkyColorsAt(new(zone.X + 15360, 0, zone.Z), [zone]);
        Assert.Equal(new Vector3(0, 0, 160), halfway.Upper);
        Assert.Equal(new Vector3(197, 255, 222), halfway.Lower);
        Ff7WorldVisibility visibility = new(presentation);
        IReadOnlyList<Ff7WorldChunkPlacement> chunks = visibility.RequiredChunks(185636, 123325, 0);
        Assert.Equal(16, chunks.Count);
        Assert.Contains(chunks, value => value.ChunkX == 22 && value.ChunkZ == 15 && value.BlockIndex == 32 && value.MeshIndex == 14);
        Assert.All(chunks, value => Assert.InRange(value.BlockIndex, 0, 62));
        Assert.Equal(63, Ff7WorldVisibility.ResolveBlockIndex(0x32, 1));
        Assert.Equal(64, Ff7WorldVisibility.ResolveBlockIndex(0x29, 2));
        Assert.Equal(65, Ff7WorldVisibility.ResolveBlockIndex(0x2A, 2));
        Assert.Equal(66, Ff7WorldVisibility.ResolveBlockIndex(0x3C, 3));
        Assert.Equal(67, Ff7WorldVisibility.ResolveBlockIndex(0x2F, 4));
        Assert.Equal(68, Ff7WorldVisibility.ResolveBlockIndex(0x30, 4));
        IReadOnlyList<Ff7WorldChunkPlacement> seam = visibility.RequiredChunks(0, 0, 0);
        Assert.Contains(seam, value => value.ChunkX == 35 && value.ChunkZ == 27 && value.Origin.X == -8192 && value.Origin.Z == -8192);
        Assert.Null(visibility.VisiblePosition(new(185636, 0, 123325), Vector3.Zero, 2, 0));
        Assert.Null(visibility.VisiblePosition(new(185636, 0, 123325), new(185636, 0, 123325), 10, 0));
        Assert.Equal(new Vector3(0x48000, 0, 8191), visibility.VisiblePosition(new(0x48000 - 1, 0, 8191), new(0, 0, 8191), 2, 0));
        Ff7WorldEnvironmentAdapter adapter = new(resources, presentation);
        Ff7WorldView view = new Ff7WorldCameraProjection(presentation).Create(new(185636, 0, 123325), 0, 1500, 10000, 0, 0, 400, 320, 240, 0);
        Capture first = new();
        Capture second = new();
        adapter.DrawSky(first, view, new(185636, 0, 123325), 0, 10000, 0, 0, true, 0, colors.Upper, colors.Lower);
        adapter.DrawSky(second, view, new(185636, 0, 123325), 0, 10000, 0, 0, true, 0, colors.Upper, colors.Lower);
        Assert.Equal(first.Triangles, second.Triangles);
        Assert.Equal(new Vector4(colors.Upper / 255, 1), first.Triangles[0].A.Color);
        Assert.All(first.Triangles.Skip(2), triangle => Assert.Equal(TriangleBlend.AlphaAdd, triangle.Material.Blend));
        Capture radar = new();
        adapter.DrawRadar(radar, 320, 240, 0, 0, 0, 0, [new(0, 185636, 123325, true), new(25, 92960, 179731)]);
        Assert.Equal(6, radar.Triangles.Count);
        Assert.Equal(new Vector2(228, 156), radar.Triangles[0].A.Position);
        Assert.Equal(new Vector2(300, 212), radar.Triangles[1].C.Position);
        Assert.All(radar.Triangles, triangle =>
        {
            Assert.Equal(TriangleBlend.Alpha, triangle.Material.Blend);
            Assert.Equal(128 / 255f, triangle.A.Color.W);
        });
        Capture cone = new();
        adapter.DrawRadar(cone, 320, 240, 0, 2, 0, 0, [new(0, 185636, 123325, true)]);
        Assert.Equal(TriangleBlend.AlphaAdd, cone.Triangles[0].Material.Blend);
        Capture hidden = new();
        adapter.DrawRadar(hidden, 320, 240, 2, 0, 0, 0, []);
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
