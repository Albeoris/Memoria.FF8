using System.Numerics;
using Albeoris.Games.FF8.Imaging;
using Albeoris.Games.FF8.Imaging.Map;
using Memoria.FF8.Assets;
using Memoria.FF8.Rendering;

namespace Memoria.FF8.Assets.Tests;

public sealed class BackgroundTileCoverageTests
{
    [Theory]
    [InlineData(320, 240)]
    [InlineData(1200, 900)]
    [InlineData(641, 361)]
    [InlineData(97, 121)]
    public void AdjacentTilesBlendEveryCoveredSampleExactlyOnce(int width, int height)
    {
        PixelImage ground = Solid(new Rgba32(80, 100, 120));
        PixelImage overlay = Solid(new Rgba32(24, 32, 40, 128));
        FieldModel model = new("coverage-actor", [new(-1, 0)], [new(new Vector3(0, -300, -300), 0), new(new Vector3(0, -300, 300), 0), new(new Vector3(0, 300, -300), 0), new(new Vector3(0, 300, 300), 0)], [new([0, 1, 2, 3], [Vector2.Zero, Vector2.Zero, Vector2.Zero, Vector2.Zero], 0, true)], [ground], [new([new(Vector3.Zero, [Vector3.Zero])])]);
        byte[] black = [0, 0, 0, 255];
        foreach (MapBlendMode mode in Enum.GetValues<MapBlendMode>())
        {
            List<BackgroundTile> tiles = [];
            for (int y = -160; y < 160; y += 16)
            {
                for (int x = -208; x < 208; x += 16)
                {
                    tiles.Add(new BackgroundTile(x, y, 100, 255, 0, 0, MapBlendMode.None, false, true, ground));
                    tiles.Add(new BackgroundTile(x, y, 10, 255, 0, 1, mode, true, true, overlay));
                }
            }
            FieldAssets field = new(0, "coverage", [], [], tiles, new FieldCamera(Vector3.UnitX, Vector3.UnitY, Vector3.UnitZ, Vector3.Zero, Vector2.Zero, 100), new FieldWalkmesh([]), [model]);
            FieldRenderer renderer = new(field, width, height);
            foreach ((Vector2 scroll, bool withActor) in new[] { (Vector2.Zero, false), (new Vector2(0.25f, -0.375f), false), (new Vector2(0.25f, -0.375f), true) })
            {
                ActorVisual[] actors = withActor ? [new ActorVisual(0, 0, 0, 0, 100, 192, 0, 0, true, PolygonColor: new Vector3(128))] : [];
                byte[] pixels = renderer.Render(new Dictionary<int, int>(), actors, TimeSpan.Zero, backgroundScrollOffsets: new Dictionary<int, Vector2> { [0] = scroll, [1] = scroll });
                float scale = Math.Min(width / 320f, height / 240f);
                float left = (width - 320 * scale) / 2;
                float top = (height - 240 * scale) / 2;
                byte[] expected = mode switch
                {
                    MapBlendMode.Average => [52, 66, 80, 255],
                    MapBlendMode.Add => [104, 132, 160, 255],
                    MapBlendMode.Subtract => [56, 68, 80, 255],
                    MapBlendMode.QuarterAdd => [86, 108, 130, 255],
                    _ => [24, 32, 40, 255]
                };
                for (int y = 0; y < height; y++)
                {
                    for (int x = 0; x < width; x++)
                    {
                        bool covered = x + 0.5f >= left && x + 0.5f < left + 320 * scale && y + 0.5f >= top && y + 0.5f < top + 240 * scale;
                        ReadOnlySpan<byte> expectedPixel = covered ? expected : black;
                        ReadOnlySpan<byte> actualPixel = pixels.AsSpan((y * width + x) * 4, 4);
                        if (!expectedPixel.SequenceEqual(actualPixel))
                        {
                            Assert.Fail($"{mode} at ({x},{y}), scroll {scroll}, actor {withActor}: expected {Convert.ToHexString(expectedPixel)}, actual {Convert.ToHexString(actualPixel)}.");
                        }
                    }
                }
            }
        }
    }

    [Fact]
    public void InstalledReceptionGlassPreservesItsSourceTexelsWithoutTileEdgeStrips()
    {
        string directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR") ?? TestResources.GameDirectory;
        Assert.SkipUnless(Directory.Exists(directory), "Installed original reception resources are required.");
        using GameAssets assets = new(directory);
        FieldAssets original = assets.LoadField(163);
        Assert.Equal("bggate_6", original.Name);
        Assert.Equal(new FieldBounds(0, 224, 320, 0), original.Information!.ScreenBounds[0]);
        BackgroundTile[] glass = original.Background.Where(tile => tile.Blend && tile.Draw && tile.AnimationId == 255).ToArray();
        Assert.Equal(21, glass.Length);
        Assert.All(glass, tile => Assert.Equal(MapBlendMode.Add, tile.BlendMode));
        Assert.Equal(3466, glass.Sum(tile => tile.Image.Pixels.Count(pixel => pixel.A == 128)));
        // Compose the original 16x16 texels in native integer drawing space,
        // then sample this independent atlas once per output sample center.
        PixelImage source = new(320, 224);
        foreach (BackgroundTile tile in glass)
        {
            for (int y = 0; y < 16; y++)
            {
                for (int x = 0; x < 16; x++)
                {
                    Rgba32 color = tile.Image[x, y];
                    if (color.A == 0)
                    {
                        continue;
                    }
                    int sourceX = tile.X + 160 + x;
                    int sourceY = tile.Y + 112 + y;
                    Rgba32 previous = source[sourceX, sourceY];
                    source[sourceX, sourceY] = color.A == 128 ? new Rgba32((byte)Math.Min(255, previous.R + color.R), (byte)Math.Min(255, previous.G + color.G), (byte)Math.Min(255, previous.B + color.B)) : color;
                }
            }
        }
        byte[] actual = new FieldRenderer(original with { Background = glass }, 1200, 900).Render(new Dictionary<int, int>(), [], TimeSpan.Zero);
        for (int y = 0; y < 900; y++)
        {
            for (int x = 0; x < 1200; x++)
            {
                Rgba32 expected = source[(int)((x + 0.5) * 320 / 1200), (int)((y + 0.5) * 224 / 900)];
                int index = (y * 1200 + x) * 4;
                if (actual[index] != expected.R || actual[index + 1] != expected.G || actual[index + 2] != expected.B)
                {
                    Assert.Fail($"Original reception glass differs at ({x},{y}).");
                }
            }
        }
    }

    private static PixelImage Solid(Rgba32 color)
    {
        return new PixelImage(16, 16, Enumerable.Repeat(color, 256).ToArray());
    }
}
