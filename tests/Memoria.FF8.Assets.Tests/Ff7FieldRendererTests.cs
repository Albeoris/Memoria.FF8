using System.Numerics;
using System.Text.Json;
using Memoria.FF7.Formats;
using Memoria.FF7.Resources;
using Memoria.FF8.Rendering;

namespace Memoria.FF8.Assets.Tests;

public sealed class Ff7FieldRendererTests
{
    [Theory]
    [InlineData(320, 240)]
    [InlineData(960, 720)]
    public void NativeBlackFadeUsesTheSharedAlphaQuadAfterFieldGeometry(int width, int height)
    {
        string? root = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR");
        Assert.SkipWhen(string.IsNullOrWhiteSpace(root), "Set MEMORIA_FF7_GAME_DIR for original field fade submission.");
        Ff7FieldResources field = Ff7FieldResources.Load(Ff7Installation.Discover(root!), "md1stin");
        Ff7FieldRenderer renderer = new(field, new Dictionary<int, Ff7FieldModelResources>(), new(1, 10000));
        Ff7FieldRenderFrame frame = new(0, Vector2.Zero, [], new ushort[256]);
        Capture baseline = new();
        renderer.Render(baseline, width, height, frame);
        Capture faded = new();
        renderer.Render(faded, width, height, frame with { QuadColorArgb = 0x80000000 });
        Assert.Equal(baseline.Count + 2, faded.Count);
        Assert.Equal(baseline.Colors, faded.Colors.Take(baseline.Colors.Count));
        Assert.All(faded.Colors.TakeLast(6), color => Assert.Equal(new Vector4(0, 0, 0, 128 / 255f), color));
        Assert.All(faded.Materials.TakeLast(2), material =>
        {
            Assert.Equal(TriangleBlend.Alpha, material.Blend);
            Assert.Equal(TriangleDepthComparison.Always, material.DepthComparison);
            Assert.False(material.DepthWrite);
            Assert.Null(material.Texture);
        });
        Assert.Contains(new Vector2(0, height * 8f / 240), faded.Positions.TakeLast(6));
        Assert.Contains(new Vector2(width, height * 232f / 240), faded.Positions.TakeLast(6));
        Capture clear = new();
        renderer.Render(clear, width, height, frame with { QuadColorArgb = 0 });
        Assert.Equal(baseline.Count, clear.Count);
    }

    [Fact]
    public void ActorFailureClosesTheSharedFrameBeforeHostErrorPresentation()
    {
        string? root = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR");
        Assert.SkipWhen(string.IsNullOrWhiteSpace(root), "Set MEMORIA_FF7_GAME_DIR for original field frame cleanup.");
        Ff7FieldResources field = Ff7FieldResources.Load(Ff7Installation.Discover(root!), "md1stin");
        Ff7FieldRenderer renderer = new(field, new Dictionary<int, Ff7FieldModelResources>(), new(50, 32000));
        Capture submitted = new();
        Ff7ActorVisual missing = new(0, 0, 0, 0, 0, 0, 0, 0);
        Assert.Throws<NotSupportedException>(() => renderer.Render(submitted, 320, 240, new(0, Vector2.Zero, [missing], new ushort[256], MovieActive: true)));
        Assert.True(submitted.BeginCalled);
        Assert.True(submitted.EndCalled);
    }

    [Fact]
    public void NativeFieldTintUsesSharedVertexColorsAndDoesNotPaintMoviePixels()
    {
        string? root = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR");
        Assert.SkipWhen(string.IsNullOrWhiteSpace(root), "Set MEMORIA_FF7_GAME_DIR for original field tint submission.");
        Ff7FieldResources field = Ff7FieldResources.Load(Ff7Installation.Discover(root!), "md1stin");
        Ff7FieldRenderer renderer = new(field, new Dictionary<int, Ff7FieldModelResources>(), new(50, 32000));
        Ff7FieldRenderFrame frame = new(0, Vector2.Zero, [], new ushort[256]) { FieldColorArgb = 0x80800D0D };
        Capture submitted = new();
        renderer.Render(submitted, 320, 240, frame);
        Assert.NotEmpty(submitted.Colors);
        Assert.All(submitted.Colors, color => Assert.Equal(new Vector4(128 / 255f, 13 / 255f, 13 / 255f, 1), color));
        Capture movie = new();
        renderer.Render(movie, 320, 240, frame with { MovieActive = true });
        Assert.Empty(movie.Colors);
        Assert.Equal(0, movie.ClearColor.W);
    }

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
    public void CachedTileReservesPaletteZeroAndPreservesDirectCameraProjection()
    {
        PcFieldTexturePage page = new(0, 0, 1, new byte[65536]);
        PcFieldBackground background = new(320, 240, new byte[] { 1 }, [], new[] { page });
        PcFieldPalettes palettes = new(0, 0, 1, new IReadOnlyList<ushort>[] { new ushort[] { 31 } });
        PcFieldTile tile = new(0, 0, 0, 0, 0, 0, 0, 0, 16, 16, 0, 4095, 0, 0, 0, 0, 0, 0, 1, 0, 0, 0, ReadOnlyMemory<byte>.Empty);
        var image = Ff7FieldRenderer.DecodeTile(background, palettes, tile);
        Assert.Equal((byte)0, image.Pixels[0].R);
        Assert.Equal((byte)0, image.Pixels[0].A);
        Assert.Equal(0, new Ff7ProjectionDepth(50, 32000).Project(50), 5);
        Assert.Equal(1, new Ff7ProjectionDepth(50, 32000).Project(32000), 5);
    }

    [Fact]
    public void IndexedZeroColorIsTransparentAtEveryPaletteIndexButStpBlackIsOpaque()
    {
        byte[] pixels = new byte[65536];
        pixels[0] = 1;
        pixels[1] = 2;
        PcFieldTexturePage page = new(0, 0, 1, pixels);
        PcFieldBackground background = new(320, 240, new byte[] { 0 }, [], [page]);
        PcFieldPalettes palettes = new(0, 0, 3, new IReadOnlyList<ushort>[] { new ushort[] { 31, 0, 0x8000 } });
        PcFieldTile tile = new(0, 0, 0, 0, 0, 0, 0, 0, 16, 16, 0, 4095, 0, 0, 0, 0, 0, 0, 1, 0, 0, 0, ReadOnlyMemory<byte>.Empty);
        var image = Ff7FieldRenderer.DecodeTile(background, palettes, tile);
        Assert.Equal((byte)0, image.Pixels[0].A);
        Assert.Equal((byte)255, image.Pixels[1].A);
        Assert.Equal((byte)255, image.Pixels[2].A);
    }

    [Fact]
    public void LiveCanonicalPaletteRowsAreNotExchangedAgainAfterScriptEdits()
    {
        byte[] pixels = new byte[65536];
        pixels[1] = 1;
        PcFieldTexturePage page = new(0, 0, 1, pixels);
        PcFieldBackground background = new(320, 240, new byte[] { 1 }, [], [page]);
        PcFieldPalettes raw = new(0, 0, 3, new IReadOnlyList<ushort>[] { new ushort[] { 31, 0, 0x8000 } });
        PcFieldTile tile = new(0, 0, 0, 0, 0, 0, 0, 0, 16, 16, 0, 4095, 0, 0, 0, 0, 0, 0, 1, 0, 0, 0, ReadOnlyMemory<byte>.Empty);
        ushort[][] rows = raw.CreateCachedBackgroundRows(background);
        PcFieldPalettes canonical = raw with { Palettes = rows.Select(row => (IReadOnlyList<ushort>)row).ToArray() };
        var initial = Ff7FieldRenderer.DecodeTile(background, raw, tile);
        var live = Ff7FieldRenderer.DecodeTile(background, canonical, tile, paletteRowsUseCachedIndices: true);
        Assert.Equal(initial.Pixels, live.Pixels);
        rows[0][0] = 31;
        rows[0][1] = 0;
        var changed = Ff7FieldRenderer.DecodeTile(background, canonical, tile, paletteRowsUseCachedIndices: true);
        Assert.Equal((byte)255, changed.Pixels[0].A);
        Assert.Equal((byte)255, changed.Pixels[0].R);
        Assert.Equal((byte)0, changed.Pixels[1].A);
    }

    [Theory]
    [InlineData("md1stin")]
    [InlineData("nmkin_4")]
    [InlineData("md1_1")]
    [InlineData("nrthmk")]
    public void InstalledCachedIndicesUseTheNativeRemappedPaletteColors(string name)
    {
        string? root = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR");
        Assert.SkipWhen(string.IsNullOrWhiteSpace(root), "Set MEMORIA_FF7_GAME_DIR for original background checks.");
        Ff7FieldResources field = Ff7FieldResources.Load(Ff7Installation.Discover(root!), name);
        int checkedPixels = 0;
        foreach (PcFieldTile tile in field.Background.Layers.SelectMany(layer => layer))
        {
            PcFieldTexturePage page = field.Background.Textures.Single(texture => texture.Id == tile.EffectiveTextureId);
            ushort first = field.Palettes.Palettes[tile.PaletteId][0];
            if (page.Depth == 2 || field.Background.PaletteHasTransparentColor[tile.PaletteId] == 0 || first == 0)
            {
                continue;
            }
            var image = Ff7FieldRenderer.DecodeTile(field.Background, field.Palettes, tile);
            for (int y = 0; y < tile.PixelSize; y++)
            {
                for (int x = 0; x < tile.PixelSize; x++)
                {
                    int index = page.GetIndex(tile.EffectiveSourceX + x, tile.EffectiveSourceY + y);
                    if (index != 0 && field.Palettes.Palettes[tile.PaletteId][index] == 0)
                    {
                        Assert.Equal((byte)255, image[x, y].A);
                        Assert.Equal((byte)(((first & 31) << 3) | ((first & 31) >> 2)), image[x, y].R);
                        Assert.Equal((byte)((((first >> 5) & 31) << 3) | (((first >> 5) & 31) >> 2)), image[x, y].G);
                        Assert.Equal((byte)((((first >> 10) & 31) << 3) | (((first >> 10) & 31) >> 2)), image[x, y].B);
                        checkedPixels++;
                    }
                }
            }
        }
        Assert.True(checkedPixels > 0, "The selected original background must exercise indices whose original transparent color was exchanged with palette zero.");
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
        public List<TriangleMaterial> Materials { get; } = [];
        public List<Vector2> Positions { get; } = [];

        public void Begin(int width, int height, Vector4 clearColor, float clearDepth = 1)
        {
            BeginCalled = true;
            ClearColor = clearColor;
        }

        public void Draw(in TriangleVertex a, in TriangleVertex b, in TriangleVertex c, in TriangleMaterial material)
        {
            Colors.AddRange([a.Color, b.Color, c.Color]);
            Materials.Add(material);
            Positions.AddRange([a.Position, b.Position, c.Position]);
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
