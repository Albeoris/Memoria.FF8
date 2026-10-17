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
