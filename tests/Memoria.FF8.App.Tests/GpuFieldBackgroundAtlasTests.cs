using System.Numerics;
using Albeoris.Games.FF8.Imaging;
using Albeoris.Games.FF8.Imaging.Map;
using Memoria.FF8.Assets;
using Memoria.FF8.Rendering;
using Memoria.FF8.Resources;
using Color = Microsoft.Xna.Framework.Color;

namespace Memoria.FF8.App.Tests;

[Collection("GPU rendering")]
public sealed class GpuFieldBackgroundAtlasTests
{
    [Theory]
    [InlineData(264)]
    [InlineData(821)]
    public void Installed_large_background_keeps_pixels_and_resident_textures(int fieldId)
    {
        using GameAssets assets = new(TestResources.GameDirectory);
        FieldAssets field = assets.LoadField(fieldId);
        FieldRenderer renderer = new(field, 960, 720);
        Dictionary<int, int> states = field.Background.Where(tile => tile.AnimationId != 255).GroupBy(tile => tile.AnimationId).ToDictionary(group => group.Key, group => (int)group.First().AnimationState);
        using GpuTestDevice device = new();
        using GpuTriangleRenderer gpu = new(device.Device);
        renderer.TriangleRenderer = new SeparateTileTextures(gpu);
        renderer.Render(states, [], TimeSpan.Zero);
        Color[] expected = GpuTestDevice.Read(gpu.Target);
        renderer.TriangleRenderer = gpu;
        renderer.Render(states, [], TimeSpan.Zero);
        Assert.Equal(expected, GpuTestDevice.Read(gpu.Target));
        renderer.Render(states, [], TimeSpan.Zero);
        Assert.Equal(0, gpu.FrameTextureUploads);
        Assert.InRange(gpu.FrameDrawCalls, 1, 2);
        Assert.Equal(expected, GpuTestDevice.Read(gpu.Target));
    }

    [Theory]
    [InlineData(960, 720)]
    [InlineData(1280, 720)]
    [InlineData(1920, 1080)]
    public void Installed_opening_corridor_scrolling_keeps_separate_texture_pixels(int width, int height)
    {
        using GameAssets assets = new(TestResources.GameDirectory);
        FieldAssets field = assets.LoadField(229);
        FieldRenderer renderer = new(field, width, height);
        Dictionary<int, int> states = field.Background.Where(tile => tile.AnimationId != 255).GroupBy(tile => tile.AnimationId).ToDictionary(group => group.Key, group => (int)group.First().AnimationState);
        using GpuTestDevice device = new();
        using GpuTriangleRenderer gpu = new(device.Device);
        SeparateTileTextures separate = new(gpu);
        for (int frame = 0; frame < 32; frame++)
        {
            Dictionary<int, Vector2> offsets = field.Background.Select(tile => tile.Layer).Distinct().ToDictionary(layer => (int)layer, layer => new Vector2(frame - 16, frame - 16));
            renderer.TriangleRenderer = separate;
            renderer.Render(states, [], TimeSpan.Zero, backgroundScrollOffsets: offsets);
            Color[] expected = GpuTestDevice.Read(gpu.Target);
            string? output = Environment.GetEnvironmentVariable("MEMORIA_FF8_ATLAS_OUTPUT");
            if (output is not null && frame == 1)
            {
                Directory.CreateDirectory(output);
                using FileStream image = File.Create(Path.Combine(output, $"separate-{width}x{height}.png"));
                gpu.Target.SaveAsPng(image, width, height);
            }
            renderer.TriangleRenderer = gpu;
            renderer.Render(states, [], TimeSpan.Zero, backgroundScrollOffsets: offsets);
            Color[] actual = GpuTestDevice.Read(gpu.Target);
            if (output is not null && frame == 1)
            {
                using FileStream image = File.Create(Path.Combine(output, $"atlas-{width}x{height}.png"));
                gpu.Target.SaveAsPng(image, width, height);
            }
            int differences = expected.Zip(actual).Count(pair => pair.First != pair.Second);
            Assert.True(differences == 0, $"{width}x{height}, scroll{frame - 16}: {differences} differing pixels.");
        }
    }

    [Theory]
    [InlineData(229)]
    [InlineData(264)]
    [InlineData(821)]
    public void Installed_background_batched_submission_stays_resident(int fieldId)
    {
        using GameAssets assets = new(TestResources.GameDirectory);
        FieldAssets field = assets.LoadField(fieldId);
        FieldRenderer renderer = new(field, 960, 720);
        Dictionary<int, int> states = field.Background.Where(tile => tile.AnimationId != 255).GroupBy(tile => tile.AnimationId).ToDictionary(group => group.Key, group => (int)group.First().AnimationState);
        using GpuTestDevice device = new();
        using GpuTriangleRenderer gpu = new(device.Device);
        renderer.TriangleRenderer = gpu;
        for (int warmup = 0; warmup < 30; warmup++)
        {
            renderer.Render(states, [], TimeSpan.Zero);
        }
        List<double> milliseconds = [];
        for (int sample = 0; sample < 5; sample++)
        {
            System.Diagnostics.Stopwatch timer = System.Diagnostics.Stopwatch.StartNew();
            for (int frame = 0; frame < 120; frame++)
            {
                renderer.Render(states, [], TimeSpan.Zero);
            }
            GpuTestDevice.Read(gpu.Target);
            milliseconds.Add(timer.Elapsed.TotalMilliseconds / 120);
        }
        Assert.Equal(0, gpu.FrameTextureUploads);
        Assert.InRange(gpu.FrameDrawCalls, 1, 2);
        string? output = Environment.GetEnvironmentVariable("MEMORIA_FF8_ATLAS_OUTPUT");
        if (output is not null)
        {
            Directory.CreateDirectory(output);
            File.WriteAllText(Path.Combine(output, $"submission-{fieldId}.json"), System.Text.Json.JsonSerializer.Serialize(new { Field = fieldId, Milliseconds = milliseconds, gpu.FrameDrawCalls, gpu.FrameTextureUploads }));
        }
    }

    [Theory]
    [InlineData(320, 240)]
    [InlineData(960, 720)]
    public void Scrolling_source_pixels_match_software_away_from_exact_boundary_ties(int width, int height)
    {
        BackgroundTile[] tiles = Enumerable.Range(0, 280).Select(index => Tile(index % 20 * 16 - 160, index / 20 * 16 - 112, new PixelImage(16, 16, Enumerable.Range(0, 256).Select(texel => new Rgba32((byte)(texel % 16 * 17), (byte)(texel / 16 * 17), (byte)index, 255)).ToArray()))).ToArray();
        FieldRenderer renderer = new(Field(tiles), width, height);
        using GpuTestDevice device = new();
        using GpuTriangleRenderer gpu = new(device.Device);
        foreach (int offset in new[] { -15, -1, 0, 1, 15 })
        {
            Dictionary<int, Vector2> offsets = new() { [0] = new(offset, offset) };
            renderer.TriangleRenderer = null;
            byte[] expected = renderer.Render(new Dictionary<int, int>(), [], TimeSpan.Zero, backgroundScrollOffsets: offsets).ToArray();
            renderer.TriangleRenderer = gpu;
            renderer.Render(new Dictionary<int, int>(), [], TimeSpan.Zero, backgroundScrollOffsets: offsets);
            Color[] actual = GpuTestDevice.Read(gpu.Target);
            for (int pixel = 0; pixel < actual.Length; pixel++)
            {
                // Exact source-edge ties may round to either adjacent source
                // pixel in the independent CPU and hardware arithmetic.
                double sourceX = (pixel % width + 0.5) * 320 / width - offset;
                double sourceY = (pixel / width + 0.5) * 224 / height - offset;
                bool sourceEdge = Math.Abs(sourceX - Math.Round(sourceX)) < 0.0001 || Math.Abs(sourceY - Math.Round(sourceY)) < 0.0001;
                if (!sourceEdge)
                {
                    Assert.Equal(expected[pixel * 4], actual[pixel].R);
                    Assert.Equal(expected[pixel * 4 + 1], actual[pixel].G);
                    Assert.Equal(expected[pixel * 4 + 2], actual[pixel].B);
                    Assert.Equal(expected[pixel * 4 + 3], actual[pixel].A);
                }
            }
        }
    }

    [Fact]
    public void Patterned_tiles_keep_point_sampling_boundaries_at_fractional_vertical_scale()
    {
        BackgroundTile[] tiles = Enumerable.Range(0, 2131).Select(index => Tile(index % 20 * 16 - 160, index / 20 % 14 * 16 - 112, new PixelImage(16, 16, Enumerable.Range(0, 256).Select(texel => new Rgba32((byte)(texel * 31 + index), (byte)(texel * 19), (byte)(texel * 7), 255)).ToArray()))).ToArray();
        FieldAssets field = Field(tiles);
        FieldRenderer renderer = new(field, 960, 720);
        Dictionary<int, Vector2> offsets = new() { [0] = Vector2.Zero };
        using GpuTestDevice device = new();
        using GpuTriangleRenderer gpu = new(device.Device);
        renderer.TriangleRenderer = new SeparateTileTextures(gpu);
        renderer.Render(new Dictionary<int, int>(), [], TimeSpan.Zero, backgroundScrollOffsets: offsets);
        Color[] expected = GpuTestDevice.Read(gpu.Target);
        renderer.TriangleRenderer = gpu;
        renderer.Render(new Dictionary<int, int>(), [], TimeSpan.Zero, backgroundScrollOffsets: offsets);
        Assert.Equal(expected, GpuTestDevice.Read(gpu.Target));
        Assert.Equal(1, gpu.FrameTextureUploads);
    }

    [Fact]
    public void More_than_512_distinct_tiles_share_an_upload_and_remain_resident_while_scrolling()
    {
        BackgroundTile[] tiles = Enumerable.Range(0, 600).Select(index => Tile(index % 20 * 16 - 160, index / 20 % 14 * 16 - 112, new PixelImage(2, 2, Enumerable.Repeat(new Rgba32((byte)index, (byte)(index >> 1), (byte)(255 - index), 255), 4).ToArray()))).ToArray();
        using GpuTestDevice device = new();
        using GpuTriangleRenderer gpu = new(device.Device);
        FieldRenderer renderer = Renderer(Field(tiles));
        renderer.TriangleRenderer = gpu;
        for (int frame = 0; frame < 5; frame++)
        {
            renderer.Render(new Dictionary<int, int>(), [], TimeSpan.Zero, backgroundScrollOffsets: new Dictionary<int, Vector2> { [0] = new(frame, 0) });
            Assert.Equal(frame == 0 ? 1 : 0, gpu.FrameTextureUploads);
            Assert.Equal(1, gpu.FrameDrawCalls);
        }
        Assert.Contains(GpuTestDevice.Read(gpu.Target), pixel => pixel.R != 0 || pixel.G != 0 || pixel.B != 0);
    }

    [Theory]
    [InlineData(MapBlendMode.Average)]
    [InlineData(MapBlendMode.Add)]
    [InlineData(MapBlendMode.Subtract)]
    [InlineData(MapBlendMode.QuarterAdd)]
    public void Atlas_regions_preserve_distinct_texels_alpha_and_equal_depth_blend_order(MapBlendMode blend)
    {
        FieldAssets field = Field([
            Tile(0, 0, Image(new(20, 40, 60, 255), new(80, 100, 120, 255), new(140, 160, 180, 255), new(200, 220, 240, 255))) with { Z = 40 },
            Tile(0, 0, Image(new(120, 20, 40, 128), new(0, 0, 0, 0), new(40, 80, 120, 255), new(80, 40, 20, 128))) with { Blend = true, BlendMode = blend },
            Tile(0, 0, Image(new(10, 30, 50, 128), new(100, 120, 140, 128), new(0, 0, 0, 0), new(50, 30, 10, 128))) with { Blend = true, BlendMode = blend },
            Tile(16, 0, Image(new(255, 0, 0, 255), new(0, 255, 0, 255), new(0, 0, 255, 255), new(255, 255, 0, 255)))
        ]);
        FieldRenderer renderer = Renderer(field);
        byte[] software = renderer.Render(new Dictionary<int, int>(), [], TimeSpan.Zero).ToArray();
        using GpuTestDevice device = new();
        using GpuTriangleRenderer gpu = new(device.Device);
        renderer.TriangleRenderer = gpu;
        renderer.Render(new Dictionary<int, int>(), [], TimeSpan.Zero);
        AssertPixels(software, GpuTestDevice.Read(gpu.Target));
        Assert.Equal(1, gpu.FrameTextureUploads);
    }

    [Fact]
    public void Switching_software_hardware_software_rebuilds_scrolled_tile_coverage()
    {
        FieldAssets field = Field([Tile(-16, -16, Image(new(20, 40, 60, 255), new(80, 100, 120, 255), new(140, 160, 180, 255), new(200, 220, 240, 255)))]);
        FieldRenderer renderer = Renderer(field);
        Dictionary<int, Vector2> offsets = new() { [0] = new(7, 5) };
        byte[] first = renderer.Render(new Dictionary<int, int>(), [], TimeSpan.Zero, backgroundScrollOffsets: offsets).ToArray();
        using GpuTestDevice device = new();
        using GpuTriangleRenderer gpu = new(device.Device);
        renderer.TriangleRenderer = gpu;
        renderer.Render(new Dictionary<int, int>(), [], TimeSpan.Zero, backgroundScrollOffsets: offsets);
        AssertPixels(first, GpuTestDevice.Read(gpu.Target));
        renderer.TriangleRenderer = null;
        Assert.Equal(first, renderer.Render(new Dictionary<int, int>(), [], TimeSpan.Zero, backgroundScrollOffsets: offsets));
        offsets = new() { [0] = new(-9, 13) };
        byte[] moved = renderer.Render(new Dictionary<int, int>(), [], TimeSpan.Zero, backgroundScrollOffsets: offsets).ToArray();
        Assert.NotEqual(first, moved);
        Assert.Equal(Renderer(field).Render(new Dictionary<int, int>(), [], TimeSpan.Zero, backgroundScrollOffsets: offsets), moved);
    }

    [Fact]
    public void Multiple_atlas_pages_and_oversized_replacements_preserve_pixels_and_residency()
    {
        FieldAssets field = Field([
            Tile(-32, 0, Solid(600, 600, new(220, 20, 40, 255))),
            Tile(-16, 0, Solid(600, 600, new(40, 220, 20, 255))),
            Tile(0, 0, Solid(600, 600, new(20, 40, 220, 255))),
            Tile(16, 0, Solid(1025, 2, new(180, 100, 60, 255)))
        ]);
        byte[] software = Renderer(field).Render(new Dictionary<int, int>(), [], TimeSpan.Zero).ToArray();
        using GpuTestDevice device = new();
        using GpuTriangleRenderer gpu = new(device.Device);
        FieldRenderer renderer = Renderer(field);
        renderer.TriangleRenderer = gpu;
        renderer.Render(new Dictionary<int, int>(), [], TimeSpan.Zero);
        Assert.Equal(4, gpu.FrameTextureUploads);
        AssertPixels(software, GpuTestDevice.Read(gpu.Target));
        renderer.Render(new Dictionary<int, int>(), [], TimeSpan.Zero);
        Assert.Equal(0, gpu.FrameTextureUploads);
        AssertPixels(software, GpuTestDevice.Read(gpu.Target));
    }

    [Fact]
    public void Shared_publication_refreshes_same_size_and_resized_images_before_zero_scroll_software_switch()
    {
        using InMemoryResourceSource source = new(new ResourceSourceDescriptor("atlas-validation", GameDataEdition.Classic, []));
        ResourceService resources = new(source);
        PixelImage first = Image(new(20, 40, 60, 255), new(80, 100, 120, 255), new(140, 160, 180, 255), new(200, 220, 240, 255));
        PixelImage second = Image(new(240, 220, 200, 255), new(180, 160, 140, 255), new(120, 100, 80, 255), new(60, 40, 20, 255));
        AssetHandle<TextureValue> firstHandle = resources.GetTexture(new TextureBuildContext(new(new ResourceKey("field/atlas"), "first"), source.Descriptor, 1, "rgba", 16, 16, () => first));
        AssetHandle<TextureValue> secondHandle = resources.GetTexture(new TextureBuildContext(new(new ResourceKey("field/atlas"), "second"), source.Descriptor, 1, "rgba", 16, 16, () => second));
        FieldAssets field = Field([Tile(0, 0, first) with { Texture = firstHandle }, Tile(16, 0, second) with { Texture = secondHandle }]);
        FieldRenderer renderer = Renderer(field);
        using GpuTestDevice device = new();
        using GpuTriangleRenderer gpu = new(device.Device);
        renderer.TriangleRenderer = gpu;
        renderer.Render(new Dictionary<int, int>(), [], TimeSpan.Zero);
        byte[] previous = Renderer(field).Render(new Dictionary<int, int>(), [], TimeSpan.Zero).ToArray();
        foreach (int size in new[] { 2, 4 })
        {
            PixelImage replacementFirst = new(size, size, Enumerable.Range(0, size * size).Select(index => new Rgba32((byte)(30 + index * 7), 170, (byte)(size * 30), 255)).ToArray());
            PixelImage replacementSecond = new(size, size, Enumerable.Range(0, size * size).Select(index => new Rgba32(190, (byte)(40 + index * 5), (byte)(size * 20), 255)).ToArray());
            long oldGeneration = firstHandle.Generation;
            resources.Prepare(new ReplacementResolver(replacementFirst, replacementSecond)).Commit();
            Assert.True(firstHandle.Generation > oldGeneration);
            Assert.Equal(firstHandle.Generation, secondHandle.Generation);
            byte[] expected = Renderer(field).Render(new Dictionary<int, int>(), [], TimeSpan.Zero).ToArray();
            Assert.NotEqual(previous, expected);
            renderer.TriangleRenderer = gpu;
            renderer.Render(new Dictionary<int, int>(), [], TimeSpan.Zero);
            Assert.Equal(1, gpu.FrameTextureUploads);
            AssertPixels(expected, GpuTestDevice.Read(gpu.Target));
            renderer.Render(new Dictionary<int, int>(), [], TimeSpan.Zero);
            Assert.Equal(0, gpu.FrameTextureUploads);
            renderer.TriangleRenderer = null;
            Assert.Equal(expected, renderer.Render(new Dictionary<int, int>(), [], TimeSpan.Zero));
            previous = expected;
        }
    }

    private static FieldRenderer Renderer(FieldAssets field)
    {
        return new FieldRenderer(field, 320, 224, nativeMovieViewport: true);
    }

    private static BackgroundTile Tile(int x, int y, PixelImage image)
    {
        return new BackgroundTile(x, y, 10, 255, 0, 0, MapBlendMode.None, false, true, image);
    }

    private static FieldAssets Field(IReadOnlyList<BackgroundTile> tiles)
    {
        return new FieldAssets(0, "atlas-validation", [], [], tiles, new FieldCamera(Vector3.UnitX, Vector3.UnitY, Vector3.UnitZ, Vector3.Zero, Vector2.Zero, 100), new FieldWalkmesh([]), []);
    }

    private static PixelImage Image(Rgba32 topLeft, Rgba32 topRight, Rgba32 bottomLeft, Rgba32 bottomRight)
    {
        return new PixelImage(2, 2, [topLeft, topRight, bottomLeft, bottomRight]);
    }

    private static PixelImage Solid(int width, int height, Rgba32 color)
    {
        return new PixelImage(width, height, Enumerable.Repeat(color, width * height).ToArray());
    }

    private sealed class ReplacementResolver(PixelImage first, PixelImage second) : ITextureReplacementResolver
    {
        public TextureReplacement? Resolve(TextureBuildContext context)
        {
            return new TextureReplacement(context.Key.Subresource == "first" ? first : second, "atlas-validation", 16, 16);
        }
    }

    private sealed class SeparateTileTextures(ITriangleRenderer target) : ITriangleRenderer
    {
        private readonly Dictionary<(PixelImage, int, int, int, int), PixelImage> textures = [];

        public void Begin(int width, int height, Vector4 clearColor, float clearDepth = 1)
        {
            target.Begin(width, height, clearColor, clearDepth);
        }

        public void Draw(in TriangleVertex a, in TriangleVertex b, in TriangleVertex c, in TriangleMaterial material)
        {
            PixelImage atlas = material.Texture!;
            Vector2 minimum = Vector2.Min(a.Uv, Vector2.Min(b.Uv, c.Uv));
            Vector2 maximum = Vector2.Max(a.Uv, Vector2.Max(b.Uv, c.Uv));
            int x = (int)MathF.Round(minimum.X * atlas.Width);
            int y = (int)MathF.Round(minimum.Y * atlas.Height);
            int width = (int)MathF.Round((maximum.X - minimum.X) * atlas.Width);
            int height = (int)MathF.Round((maximum.Y - minimum.Y) * atlas.Height);
            var key = (atlas, x, y, width, height);
            if (!textures.TryGetValue(key, out PixelImage? image))
            {
                image = new PixelImage(width, height);
                for (int row = 0; row < height; row++)
                {
                    Array.Copy(atlas.Pixels, (y + row) * atlas.Width + x, image.Pixels, row * width, width);
                }
                textures.Add(key, image);
            }
            Vector2 size = maximum - minimum;
            Vector4 texelRectangle = new(0, 0, width, height);
            target.Draw(a with { Uv = (a.Uv - minimum) / size, TextureTexelRectangle = texelRectangle }, b with { Uv = (b.Uv - minimum) / size, TextureTexelRectangle = texelRectangle }, c with { Uv = (c.Uv - minimum) / size, TextureTexelRectangle = texelRectangle }, material with { Texture = image });
        }

        public void End()
        {
            target.End();
        }
    }

    private static void AssertPixels(byte[] software, Color[] hardware)
    {
        Assert.Equal(software.Length / 4, hardware.Length);
        for (int index = 0; index < hardware.Length; index++)
        {
            Assert.InRange(Math.Abs(software[index * 4] - hardware[index].R), 0, 2);
            Assert.InRange(Math.Abs(software[index * 4 + 1] - hardware[index].G), 0, 2);
            Assert.InRange(Math.Abs(software[index * 4 + 2] - hardware[index].B), 0, 2);
            Assert.Equal(software[index * 4 + 3], hardware[index].A);
        }
    }
}
