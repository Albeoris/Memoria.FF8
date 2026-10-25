using System.Numerics;
using System.Security.Cryptography;
using Albeoris.Games.FF8.Imaging;
using Albeoris.Games.FF8.Imaging.Map;
using ImageMagick;
using Memoria.FF8.Assets;
using Memoria.FF8.Mods;
using Memoria.FF8.Rendering;
using Memoria.FF8.Resources;
using Color = Microsoft.Xna.Framework.Color;

namespace Memoria.FF8.App.Tests;

[Collection("GPU rendering")]
public sealed class DemasterFieldRenderingTests
{
    private const string Stem = "textures/field_bg/te/testmap/testmap_0";
    private static readonly ResourceKey Owner = new("field/testmap/testmap.mim");
    private static readonly ResourceSourceDescriptor Source = new("synthetic-field", GameDataEdition.Classic, ["native-field-background-page"]);

    [Fact]
    public void PaletteAndPlainPagesKeepMapAnimationSelectionAndIndependentSourceUvs()
    {
        using InMemoryResourceSource source = new(Source);
        ResourceService resources = new(source);
        MapTile[] map = [Map(0, 0, palette: 4), Map(16, 16, parameter: 7, state: 0), Map(16, 32, parameter: 7, state: 1)];
        BackgroundTile[] tiles = Register(resources, map, _ => Solid(new(20, 30, 40)));
        resources.Prepare(Resolver(Page(Stem + ".png", (_, _) => new(20, 40, 220)), Page(Stem + "_3.png", (x, _) => x < 32 ? new(20, 220, 40) : new(220, 40, 20)))).Commit();
        Assert.Contains(Stem + ".png", tiles[0].Texture!.Value.Provenance);
        Assert.All(tiles.Skip(1), tile => Assert.Contains(Stem + "_3.png", tile.Texture!.Value.Provenance));
        Assert.All(tiles, tile => Assert.Equal((64, 64), (tile.ResolvedImage.Width, tile.ResolvedImage.Height)));
        FieldRenderer renderer = Renderer(Field(tiles));
        byte[] first = renderer.Render(new Dictionary<int, int> { [7] = 0 }, [], TimeSpan.Zero).ToArray();
        Assert.Equal(new Rgba32(20, 40, 220), Sample(first, 164, 116));
        Assert.Equal(new Rgba32(20, 220, 40), Sample(first, 180, 116));
        Assert.Equal(first, renderer.Render(new Dictionary<int, int> { [7] = 0 }, [], TimeSpan.FromSeconds(10)));
        byte[] second = renderer.Render(new Dictionary<int, int> { [7] = 1 }, [], TimeSpan.Zero).ToArray();
        Assert.Equal(new Rgba32(220, 40, 20), Sample(second, 180, 116));
        Assert.NotEqual(Hash(first), Hash(second));
        Assert.Equal(new[] { (255, 0), (7, 0), (7, 1) }, tiles.Select(tile => (tile.AnimationId, tile.AnimationState)));
        using GpuTestDevice device = new();
        using GpuTriangleRenderer gpu = new(device.Device);
        renderer.TriangleRenderer = gpu;
        renderer.Render(new Dictionary<int, int> { [7] = 0 }, [], TimeSpan.FromSeconds(10));
        AssertPixels(first, GpuTestDevice.Read(gpu.Target));
        renderer.Render(new Dictionary<int, int> { [7] = 1 }, [], TimeSpan.Zero);
        AssertPixels(second, GpuTestDevice.Read(gpu.Target));
    }

    [Fact]
    public void CachedCpuAndGpuRenderersRefreshOnEnableSameSizeReplacementAndDisable()
    {
        using InMemoryResourceSource source = new(Source);
        ResourceService resources = new(source);
        BackgroundTile[] tiles = Register(resources, [Map(0, 16)], _ => Solid(new(20, 40, 220)));
        FieldRenderer renderer = Renderer(Field(tiles));
        using GpuTestDevice device = new();
        using GpuTriangleRenderer gpu = new(device.Device);
        string? originalHash = null;
        string? previousHash = null;
        Rgba32[] colors = [new(20, 40, 220), new(220, 40, 20), new(20, 220, 40), new(20, 40, 220)];
        for (int step = 0; step < colors.Length; step++)
        {
            long oldGeneration = resources.Generation;
            if (step is 1 or 2)
            {
                resources.Prepare(Resolver(Page(Stem + "_3.png", (_, _) => colors[step]))).Commit();
                Assert.Equal((64, 64), (tiles[0].ResolvedImage.Width, tiles[0].ResolvedImage.Height));
            }
            else if (step == 3)
            {
                resources.Prepare(null).Commit();
                Assert.Equal((16, 16), (tiles[0].ResolvedImage.Width, tiles[0].ResolvedImage.Height));
            }
            if (step > 0)
            {
                Assert.True(resources.Generation > oldGeneration);
            }
            renderer.TriangleRenderer = null;
            byte[] software = renderer.Render(new Dictionary<int, int>(), [], TimeSpan.Zero).ToArray();
            Assert.Equal(colors[step], Sample(software, 164, 116));
            string currentHash = Hash(software);
            originalHash ??= currentHash;
            if (previousHash is not null)
            {
                Assert.NotEqual(previousHash, currentHash);
            }
            if (step == 3)
            {
                Assert.Equal(originalHash, currentHash);
            }
            renderer.TriangleRenderer = gpu;
            renderer.Render(new Dictionary<int, int>(), [], TimeSpan.Zero);
            Assert.Equal(1, gpu.FrameTextureUploads);
            AssertPixels(software, GpuTestDevice.Read(gpu.Target));
            renderer.Render(new Dictionary<int, int>(), [], TimeSpan.Zero);
            Assert.Equal(0, gpu.FrameTextureUploads);
            AssertPixels(software, GpuTestDevice.Read(gpu.Target));
            previousHash = currentHash;
        }
    }

    [Fact]
    public void HdPageCropsPreserveTransparentTexelsNativeBlendModesAndEqualDepthOrder()
    {
        using GpuTestDevice device = new();
        using GpuTriangleRenderer gpu = new(device.Device);
        foreach (MapBlendMode blend in new[] { MapBlendMode.Average, MapBlendMode.Add, MapBlendMode.Subtract, MapBlendMode.QuarterAdd })
        {
            using InMemoryResourceSource source = new(Source);
            ResourceService resources = new(source);
            MapTile[] map = [Map(0, 0, z: 40), Map(0, 16, blend: blend), Map(0, 32, blend: blend)];
            BackgroundTile[] tiles = Register(resources, map, tile => Pattern(tile.SourceX, 16));
            FieldRenderer renderer = Renderer(Field(tiles));
            byte[] native = renderer.Render(new Dictionary<int, int>(), [], TimeSpan.Zero).ToArray();
            resources.Prepare(Resolver(Page(Stem + "_3.png", Artwork))).Commit();
            Assert.All(tiles, tile => Assert.Equal(64, tile.ResolvedImage.Width));
            byte[] replaced = renderer.Render(new Dictionary<int, int>(), [], TimeSpan.Zero).ToArray();
            Assert.Equal(Hash(native), Hash(replaced));
            // Both overlaid tiles are transparent in their upper-right quadrant.
            Assert.Equal(new Rgba32(80, 100, 120), Sample(replaced, 172, 116));
            renderer.TriangleRenderer = gpu;
            renderer.Render(new Dictionary<int, int>(), [], TimeSpan.Zero);
            AssertPixels(replaced, GpuTestDevice.Read(gpu.Target));
            Assert.Equal(blend, tiles[1].BlendMode);
            Assert.True(tiles[1].Blend);
            Assert.True(tiles[1].Draw);
        }
    }

    private static Rgba32 Artwork(int x, int y)
    {
        if (x < 16)
        {
            return new(80, 100, 120);
        }
        int localX = x % 16;
        int localY = y % 16;
        if (localX >= 8 && localY < 8)
        {
            return new(0, 0, 0, 0);
        }
        if (x < 32)
        {
            return localY >= 8 && localX < 8 ? new(40, 80, 120) : new(120, 20, 40, 128);
        }
        return localY >= 8 && localX < 8 ? new(0, 0, 0, 0) : new(10, 30, 50, 128);
    }

    private static PixelImage Pattern(int sourceX, int size)
    {
        return new(size, size, Enumerable.Range(0, size * size).Select(index => Artwork(sourceX + index % size * 16 / size, index / size * 16 / size)).ToArray());
    }

    private static MapTile Map(short destinationX, byte sourceX, int palette = 3, byte parameter = 255, byte state = 0, short z = 10, MapBlendMode blend = MapBlendMode.None)
    {
        return new()
        {
            X = destinationX,
            Y = 0,
            Z = z,
            SourceX = sourceX,
            SourceY = 0,
            TextureId = 0,
            PaletteId = palette,
            PackedTexture = (byte)(blend == MapBlendMode.None ? 0x10 : 0x30),
            Parameter = parameter,
            State = state,
            BlendMode = blend
        };
    }

    private static BackgroundTile[] Register(ResourceService resources, MapTile[] map, Func<MapTile, PixelImage> original)
    {
        return map.Select((tile, index) =>
        {
            PixelImage image = original(tile);
            TextureBuildContext context = new(new(Owner, $"tile/{index}", tile.PaletteId), Source, 1, "classic-field-tile", 16, 16, () => image)
            {
                NativeAlias = FieldBackgroundTileDecoder.Address(Owner, tile)
            };
            return new BackgroundTile(tile.X, tile.Y, tile.Z, tile.Parameter, tile.State, tile.PackedLayer >> 1, tile.BlendMode, ((tile.PackedTexture >> 5) & 3) != 0, (tile.PackedTexture & 0x10) != 0, image)
            {
                PackedPalette = tile.PackedPalette,
                Texture = resources.GetTexture(context)
            };
        }).ToArray();
    }

    private static ModFile Page(string path, Func<int, int, Rgba32> pixel)
    {
        byte[] rgba = new byte[1024 * 1024 * 4];
        for (int y = 0; y < 1024; y++)
        {
            for (int x = 0; x < 1024; x++)
            {
                Rgba32 color = pixel(x / 4, y / 4);
                int offset = (y * 1024 + x) * 4;
                rgba[offset] = color.R;
                rgba[offset + 1] = color.G;
                rgba[offset + 2] = color.B;
                rgba[offset + 3] = color.A;
            }
        }
        using MagickImage image = new(rgba, new PixelReadSettings(1024, 1024, StorageType.Char, PixelMapping.RGBA));
        byte[] bytes = image.ToByteArray(MagickFormat.Png);
        return new("Demaster/SyntheticField", ModFormat.Demaster, path, 1, () => bytes);
    }

    private static DemasterTextureResolver Resolver(params ModFile[] files) => new(new(1, [], files));
    private static PixelImage Solid(Rgba32 color) => new(16, 16, Enumerable.Repeat(color, 256).ToArray());
    private static FieldAssets Field(IReadOnlyList<BackgroundTile> tiles) => new(0, "synthetic-field", [], [], tiles, new(Vector3.UnitX, Vector3.UnitY, Vector3.UnitZ, Vector3.Zero, Vector2.Zero, 100), new([]), []);
    private static FieldRenderer Renderer(FieldAssets field) => new(field, 320, 224, nativeMovieViewport: true);
    private static string Hash(byte[] pixels) => Convert.ToHexString(SHA256.HashData(pixels));

    private static Rgba32 Sample(byte[] pixels, int x, int y)
    {
        int offset = (y * 320 + x) * 4;
        return new(pixels[offset], pixels[offset + 1], pixels[offset + 2], pixels[offset + 3]);
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
