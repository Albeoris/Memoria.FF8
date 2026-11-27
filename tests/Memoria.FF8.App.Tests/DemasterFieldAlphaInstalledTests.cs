using System.Text;
using Albeoris.Games.FF8.Imaging;
using Albeoris.Games.FF8.Imaging.Map;
using Memoria.FF8.Assets;
using Memoria.FF8.Mods;
using Memoria.FF8.Rendering;
using Memoria.FF8.Resources;
using Color = Microsoft.Xna.Framework.Color;

namespace Memoria.FF8.App.Tests;

[Collection("GPU rendering")]
public sealed class DemasterFieldAlphaInstalledTests
{
    [Theory]
    [InlineData("field-page-lamp-dds", "bghall_1", 7, 1017, 96, 224, 8, 5)]
    [InlineData("field-page-haze-dds", "bgmon_2", 11, 1540, 0, 0, 8, 9)]
    public void FullyCoveredArtworkAddsLightAtNativeTranslucentTexel(string fixture, string map, int page, int tileIndex, int sourceX, int sourceY, int x, int y)
    {
        string path = TestResources.FixturePath(fixture);
        string game = TestResources.GameDirectory;
        using IResourceSource source = GameDataSourceFactory.Open(game);
        ResourceService resources = new(source);
        using GameAssets assets = new(game, resources: resources);
        string[] maps = Encoding.ASCII.GetString(assets.ReadResource(new("field/mapdata/maplist"))).Replace("\r", "").Split('\n');
        int fieldId = Array.IndexOf(maps, map);
        Assert.True(fieldId >= 0);
        FieldAssets field = assets.LoadField(fieldId);
        BackgroundTile tile = field.Background[tileIndex];
        Assert.True(tile.Draw && tile.Blend);
        Assert.Equal(MapBlendMode.Add, tile.BlendMode);
        Assert.Equal(128, tile.Image[x, y].A);

        string relativePath = $"textures/field_bg/{map[..2]}/{map}/{map}_{page}.dds";
        ModFile file = new("Demaster/FieldPages", ModFormat.Demaster, relativePath, 1, () => File.ReadAllBytes(path));
        PixelImage artwork = ModImageDecoder.Decode(file).Image;
        Assert.Equal((1024, 1024), (artwork.Width, artwork.Height));
        const int scale = 4;
        int sampleX = x * scale + scale / 2;
        int sampleY = y * scale + scale / 2;
        Rgba32 raw = artwork[sourceX * scale + sampleX, sourceY * scale + sampleY];
        Assert.Equal(255, raw.A);
        Assert.True(raw.R > 0 && raw.G > 0 && raw.B > 0);
        MapFile layout = MapFile.Read(assets.ReadResource(new($"field/{map}/{map}.map")));
        MapTile[] additive = layout.Tiles.Where(record => record.BlendMode == MapBlendMode.Add).ToArray();
        Dictionary<int, ModFile> pages = [];
        foreach (int usedPage in additive.Select(record => record.TextureId).Distinct())
        {
            string filename = $"{map}_{usedPage}.dds";
            string sibling = Path.Combine(Path.GetDirectoryName(path)!, filename);
            pages.Add(usedPage, usedPage == page ? file : new("Demaster/FieldPages", ModFormat.Demaster, $"textures/field_bg/{map[..2]}/{map}/{filename}", 1, () => File.ReadAllBytes(sibling)));
        }
        resources.Prepare(new DemasterTextureResolver(new(1, [], pages.Values.ToArray()))).Commit();
        AssertAdditiveCoverage(field, layout, pages);
        Assert.True(tile.Texture!.Value.IsReplacement);

        PixelImage rawCrop = new(16 * scale, 16 * scale);
        for (int row = 0; row < rawCrop.Height; row++)
        {
            artwork.Pixels.AsSpan((sourceY * scale + row) * artwork.Width + sourceX * scale, rawCrop.Width).CopyTo(rawCrop.Pixels.AsSpan(row * rawCrop.Width));
        }
        Rgba32 backdrop = new(24, 48, 64);
        Rgba32 added = new((byte)Math.Min(255, backdrop.R + raw.R), (byte)Math.Min(255, backdrop.G + raw.G), (byte)Math.Min(255, backdrop.B + raw.B));
        using GpuTestDevice device = new();
        using GpuTriangleRenderer gpu = new(device.Device);
        AssertRendered(field, tile with { Image = rawCrop, Texture = null }, sampleX, sampleY, scale, backdrop, raw, gpu);
        AssertRendered(field, tile, sampleX, sampleY, scale, backdrop, added, gpu);
    }

    private static void AssertAdditiveCoverage(FieldAssets field, MapFile layout, Dictionary<int, ModFile> pages)
    {
        var images = pages.ToDictionary(pair => pair.Key, pair => ModImageDecoder.Decode(pair.Value).Image);
        for (int i = 0; i < layout.Tiles.Count; i++)
        {
            MapTile record = layout.Tiles[i];
            if (record.BlendMode != MapBlendMode.Add)
            {
                continue;
            }
            BackgroundTile tile = field.Background[i];
            Assert.True(tile.Blend && tile.Texture!.Value.IsReplacement);
            Assert.DoesNotContain(tile.Image.Pixels, color => color.A == 255);
            PixelImage image = images[record.TextureId];
            int scale = image.Width / 256;
            PixelImage resolved = tile.ResolvedImage;
            Assert.Equal((16 * scale, 16 * scale), (resolved.Width, resolved.Height));
            for (int y = 0; y < resolved.Height; y++)
            {
                for (int x = 0; x < resolved.Width; x++)
                {
                    Rgba32 raw = image[record.SourceX * scale + x, record.SourceY * scale + y];
                    Rgba32 expected = raw.A == 0 ? raw : raw with { A = 128 };
                    Assert.Equal(expected, resolved[x, y]);
                }
            }
        }
    }

    private static void AssertRendered(FieldAssets field, BackgroundTile tile, int sampleX, int sampleY, int scale, Rgba32 backdrop, Rgba32 expected, GpuTriangleRenderer gpu)
    {
        // Center the original MAP primitive over a known destination and sample one output pixel per DDS texel.
        FieldAssets isolated = field with { Background = [tile with { X = 0, Y = 0 }], Information = null };
        FieldRenderer renderer = new(isolated, 320 * scale, 224 * scale, nativeMovieViewport: true) { ClearColor = backdrop };
        Dictionary<int, int> states = new() { [tile.AnimationId] = tile.AnimationState };
        int pixel = (112 * scale + sampleY) * renderer.OutputWidth + 160 * scale + sampleX;
        byte[] cpu = renderer.Render(states, [], TimeSpan.Zero);
        Assert.Equal(expected, new Rgba32(cpu[pixel * 4], cpu[pixel * 4 + 1], cpu[pixel * 4 + 2], cpu[pixel * 4 + 3]));

        renderer.TriangleRenderer = gpu;
        renderer.Render(states, [], TimeSpan.Zero);
        Color actual = GpuTestDevice.Read(gpu.Target)[pixel];
        Assert.InRange(Math.Abs(expected.R - actual.R), 0, 1);
        Assert.InRange(Math.Abs(expected.G - actual.G), 0, 1);
        Assert.InRange(Math.Abs(expected.B - actual.B), 0, 1);
        Assert.Equal(expected.A, actual.A);
    }
}
