using System.Numerics;
using System.Text.Json;
using Memoria.FF7.Formats;
using Memoria.FF7.Resources;
using Memoria.FF8.Rendering;

namespace Memoria.FF8.Assets.Tests;

public sealed class Ff7FieldRendererTests
{
    [Fact]
    public void IndexedTilePreservesPaletteZeroAndDirectCameraProjection()
    {
        PcFieldTexturePage page = new(0, 0, 1, new byte[65536]);
        PcFieldBackground background = new(320, 240, new byte[] { 1 }, [], new[] { page });
        PcFieldPalettes palettes = new(0, 0, 1, new IReadOnlyList<ushort>[] { new ushort[] { 31 } });
        PcFieldTile tile = new(0, 0, 0, 0, 0, 0, 0, 0, 16, 16, 0, 4095, 0, 0, 0, 0, 0, 0, 1, 0, 0, 0, ReadOnlyMemory<byte>.Empty);
        var image = Ff7FieldRenderer.DecodeTile(background, palettes, tile);
        Assert.Equal((byte)255, image.Pixels[0].R);
        Assert.Equal((byte)0, image.Pixels[0].A);
        Assert.Equal(0, new Ff7ProjectionDepth(50, 32000).Project(50), 5);
        Assert.Equal(1, new Ff7ProjectionDepth(50, 32000).Project(32000), 5);
    }

    [Fact]
    public void OptionalInstalledOpeningSubmitsOriginalLayeredBackgroundAndActorTriangles()
    {
        string? root = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR");
        Assert.SkipWhen(string.IsNullOrWhiteSpace(root), "Set MEMORIA_FF7_GAME_DIR for read-only original render-contract tests.");
        Ff7Installation installation = Ff7Installation.Discover(root);
        Ff7FieldResources field = Ff7FieldResources.Load(installation, "md1stin");
        using LgpArchive characters = LgpArchive.Open(installation.GetRequired(Ff7ResourceRole.CharacterArchive).Path);
        var resources = new Dictionary<int, Ff7FieldModelResources> { [0] = Ff7FieldModelResources.Load(characters, field.Models.Models[0]) };
        Ff7FieldRenderer renderer = new(field, resources, new(50, 32000));
        Capture triangles = new();
        renderer.Render(triangles, 320, 240, new(0, Vector2.Zero, [new(0, 0, 3800 * 4096, 28500 * 4096, 310 * 4096, 0, 0, 0)], new ushort[256]));
        Assert.True(triangles.Count > field.Background.Layers[0].Count * 2);
        Assert.True(triangles.ModelTriangles > 0);
        Assert.All(triangles.Depths, depth => Assert.True(float.IsFinite(depth)));
        Assert.True(triangles.BeginCalled && triangles.EndCalled);
        Capture movie = new();
        renderer.Render(movie, 320, 240, new(0, Vector2.Zero, [new(0, 0, 3800 * 4096, 28500 * 4096, 310 * 4096, 0, 0, 0)], new ushort[256], MovieActive: true, CameraOverride: field.Cameras[0]));
        Assert.Equal(0, movie.ClearColor.W);
        Assert.True(movie.ModelTriangles > 0 && movie.Count < triangles.Count);
        Assert.All(movie.PreservesAlpha, preserve => Assert.False(preserve));
        string? report = Environment.GetEnvironmentVariable("MEMORIA_FF7_RENDER_CONTRACT_REPORT");
        if (!string.IsNullOrWhiteSpace(report))
        {
            File.WriteAllText(report, JsonSerializer.Serialize(new { States = resources[0].Geometry.SelectMany(part => part.Polygon.RenderStates.Select(raw => PcRenderState.Read(raw.Span))).Select(state => new { state.Options, state.Features, state.ShadeMode, state.AmbientLight, state.AlphaReference, state.BlendMode, state.SourceBlend, state.DestinationBlend, state.VertexAlpha }).Distinct(), Textures = resources[0].Geometry.SelectMany(part => part.Textures).Select(texture => new { texture.BytesPerPixel, texture.RenderInfo }).Distinct() }));
        }
    }

    private sealed class Capture : ITriangleRenderer
    {
        public int Count { get; private set; }
        public int ModelTriangles { get; private set; }
        public bool BeginCalled { get; private set; }
        public bool EndCalled { get; private set; }
        public List<float> Depths { get; } = [];
        public Vector4 ClearColor { get; private set; }
        public List<bool> PreservesAlpha { get; } = [];

        public void Begin(int width, int height, Vector4 clearColor, float clearDepth = 1)
        {
            BeginCalled = true;
            ClearColor = clearColor;
        }

        public void Draw(in TriangleVertex a, in TriangleVertex b, in TriangleVertex c, in TriangleMaterial material)
        {
            Count++;
            PreservesAlpha.Add(material.PreserveTargetAlpha);
            if (a.ReciprocalW != 1)
            {
                ModelTriangles++;
            }
            Depths.Add(a.Depth);
            Depths.Add(b.Depth);
            Depths.Add(c.Depth);
        }

        public void End()
        {
            EndCalled = true;
        }
    }
}
