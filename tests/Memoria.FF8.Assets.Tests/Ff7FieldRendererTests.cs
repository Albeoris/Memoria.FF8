using System.Numerics;
using System.Text.Json;
using Memoria.FF7.Formats;
using Memoria.FF7.Resources;
using Memoria.FF8.Rendering;

namespace Memoria.FF8.Assets.Tests;

public sealed class Ff7FieldRendererTests
{
    [Theory]
    [InlineData(1u, false)]
    [InlineData(2u, true)]
    public void NativeShadeModePreservesFlatAndGouraudVertexColorOwnership(uint mode, bool interpolated)
    {
        string? root = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR");
        Assert.SkipWhen(string.IsNullOrWhiteSpace(root), "Set MEMORIA_FF7_GAME_DIR for the selected native model pose.");
        Ff7Installation installation = Ff7Installation.Discover(root!);
        Ff7FieldResources field = Ff7FieldResources.Load(installation, "md1stin");
        using LgpArchive archive = LgpArchive.Open(installation.GetRequired(Ff7ResourceRole.CharacterArchive).Path);
        Ff7FieldModelResources original = Ff7FieldModelResources.Load(archive, field.Models.Models[0]);
        byte[] state = new byte[100];
        System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(state.AsSpan(8), 0x20000);
        System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(state.AsSpan(12), 0x20000);
        System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(state.AsSpan(36), mode);
        System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(state.AsSpan(92), 255);
        PcPolygonModel triangle = new([Vector3.Zero, Vector3.UnitX, Vector3.UnitY], [Vector3.UnitZ, Vector3.UnitZ, Vector3.UnitZ], [], [0xFFFF0000, 0xFF00FF00, 0xFF0000FF], [], [new(0, 0, 1, 2, 0, 1, 2, default)], [state], [new(0, 0, 1, 0, 3, 0, 0, 0, default)], default);
        Ff7FieldModelResources resources = original with { Geometry = [original.Geometry[0] with { Polygon = triangle }] };
        Ff7FieldRenderer renderer = new(field, new Dictionary<int, Ff7FieldModelResources> { [0] = resources }, new(50, 32000));
        Ff7ActorVisual actor = new(0, 0, 3800 * 4096, 28500 * 4096, 310 * 4096, 0, 0, 0) { ModelEffects = new(_ => true, null, true) };
        Capture submitted = new();
        renderer.Render(submitted, 320, 240, new(0, Vector2.Zero, [actor], new ushort[256], MovieActive: true));
        Assert.Equal(3, submitted.Colors.Count);
        Assert.Equal(new Vector4(1, 0, 0, 1), submitted.Colors[0]);
        Assert.Equal(interpolated ? new Vector4(0, 1, 0, 1) : submitted.Colors[0], submitted.Colors[1]);
        Assert.Equal(interpolated ? new Vector4(0, 0, 1, 1) : submitted.Colors[0], submitted.Colors[2]);
    }

    [Fact]
    public void ModelRenderOverridesKeepVisibilityAndCachedColorInTheSharedTriangleStream()
    {
        string? root = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR");
        Assert.SkipWhen(string.IsNullOrWhiteSpace(root), "Set MEMORIA_FF7_GAME_DIR for original model submission tests.");
        Ff7Installation installation = Ff7Installation.Discover(root!);
        Ff7FieldResources field = Ff7FieldResources.Load(installation, "md1stin");
        using LgpArchive archive = LgpArchive.Open(installation.GetRequired(Ff7ResourceRole.CharacterArchive).Path);
        Ff7FieldModelResources model = Ff7FieldModelResources.Load(archive, field.Models.Models[0]);
        Ff7FieldRenderer renderer = new(field, new Dictionary<int, Ff7FieldModelResources> { [0] = model }, new(50, 32000));
        int shaded = 0;
        Ff7ActorVisual actor = new(0, 0, 3800 * 4096, 28500 * 4096, 310 * 4096, 0, 0, 0)
        {
            ModelEffects = new(part => part == 0, (_, _, _) =>
            {
                shaded++;
                return 0xFF800040;
            }, true)
        };
        Capture submitted = new();
        renderer.Render(submitted, 320, 240, new(0, Vector2.Zero, [actor], new ushort[256], MovieActive: true));
        Assert.True(shaded > 0);
        Assert.True(submitted.Count > 0 && submitted.Count < model.Geometry.Sum(part => part.Polygon.Polygons.Count));
        Assert.All(submitted.Colors, color => Assert.Equal(new Vector4(128 / 255f, 0, 64 / 255f, 1), color));
        Assert.All(submitted.Clips, clip => Assert.Equal(new Vector4(0, 8, 320, 232), clip));
        Capture hidden = new();
        renderer.Render(hidden, 320, 240, new(0, Vector2.Zero, [actor with { ModelEffects = new(_ => false, null, true) }], new ushort[256], MovieActive: true));
        Assert.Equal(0, hidden.Count);
    }

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
        Capture endedCamera = new();
        PcFieldCamera blankCamera = new(Vector3.Zero, Vector3.Zero, Vector3.Zero, 0, Vector3.Zero, 0, 0);
        renderer.Render(endedCamera, 320, 240, new(0, Vector2.Zero, [new(0, 0, 3800 * 4096, 28500 * 4096, 310 * 4096, 0, 0, 0)], new ushort[256], MovieActive: true, CameraOverride: blankCamera));
        Assert.Equal(0, endedCamera.Count);
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
        public List<Vector4> Colors { get; } = [];
        public List<Vector4?> Clips { get; } = [];

        public void Begin(int width, int height, Vector4 clearColor, float clearDepth = 1)
        {
            BeginCalled = true;
            ClearColor = clearColor;
        }

        public void Draw(in TriangleVertex a, in TriangleVertex b, in TriangleVertex c, in TriangleMaterial material)
        {
            Colors.AddRange([a.Color, b.Color, c.Color]);
            Clips.Add(material.ClipRectangle);
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
