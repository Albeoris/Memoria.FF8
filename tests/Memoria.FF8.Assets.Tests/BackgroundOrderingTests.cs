using System.Numerics;
using System.Collections.Immutable;
using Albeoris.Games.FF8.Imaging;
using Albeoris.Games.FF8.Imaging.Map;
using Memoria.FF8.Assets;
using Memoria.FF8.Rendering;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class BackgroundOrderingTests
{
    [Theory]
    [InlineData(-192, 0, false)]
    [InlineData(176, 0, false)]
    [InlineData(0, -144, false)]
    [InlineData(0, 128, false)]
    [InlineData(-191, -143, true)]
    [InlineData(175, 127, true)]
    public void DisabledWrappingStillUsesStrictNativeOriginCulling(short x, short y, bool visible)
    {
        FieldNativeBackgroundWrapFrame frame = new(0, "", -192, 176, -144, 128, ImmutableArray<FieldNativeBackgroundWrapLayer>.Empty);
        FieldNativeBackgroundWrapLayer layer = new(0, 0, 0, 0, 0);
        Assert.Equal(visible, FieldNativeBackgroundTileProbe.Relocate(x, y, layer, frame) is not null);
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(1, 0)]
    [InlineData(0, 0)]
    public void EqualDepthUsesNativeHeadInsertionAcrossLayers(int foregroundLayer, int skyLayer)
    {
        PixelImage foreground = Solid(new Rgba32(40, 60, 80));
        foreground[2, 1] = default;
        FieldAssets field = Field([
            Tile(4094, foregroundLayer, foreground),
            Tile(4094, skyLayer, Solid(new Rgba32(220, 180, 120)))
        ]);
        FieldRenderer renderer = new(field, 320, 224);
        byte[] pixels = renderer.Render(new Dictionary<int, int>(), [], TimeSpan.Zero);
        Assert.Equal(new byte[] { 40, 60, 80, 255 }, Sample(pixels, 161, 113));
        Assert.Equal(new byte[] { 220, 180, 120, 255 }, Sample(pixels, 162, 113));
        Dictionary<int, Vector2> offsets = field.Background.Select(tile => tile.Layer).Distinct().ToDictionary(layer => layer, _ => new Vector2(3, -2));
        pixels = renderer.Render(new Dictionary<int, int>(), [], TimeSpan.Zero, backgroundScrollOffsets: offsets);
        Assert.Equal(new byte[] { 40, 60, 80, 255 }, Sample(pixels, 164, 111));
        Assert.Equal(new byte[] { 220, 180, 120, 255 }, Sample(pixels, 165, 111));
    }

    [Fact]
    public void TranslucentClutComposesAfterOpaquePrimitivesWithoutWritingDepth()
    {
        PixelImage cutout = Solid(default);
        cutout[1, 1] = new Rgba32(10, 20, 30);
        FieldAssets field = Field([
            Tile(4094, 0, cutout) with { PackedPalette = 0x100 },
            Tile(4094, 1, Solid(new Rgba32(100, 120, 140))) with { PackedPalette = 0x101 },
            Tile(4094, 0, Solid(new Rgba32(20, 30, 40, 128))) with { PackedPalette = 0x100, Blend = true, BlendMode = MapBlendMode.Add },
            Tile(4094, 0, Solid(new Rgba32(10, 20, 30, 128))) with { PackedPalette = 0x100, Blend = true, BlendMode = MapBlendMode.Add }
        ]);
        byte[] pixels = new FieldRenderer(field, 320, 224).Render(new Dictionary<int, int>(), [], TimeSpan.Zero);
        Assert.Equal(new byte[] { 40, 70, 100, 255 }, Sample(pixels, 161, 113));
        Assert.Equal(new byte[] { 130, 170, 210, 255 }, Sample(pixels, 162, 113));
    }

    [Fact]
    public void FirstEncounteredPackedClutIsNearerEvenWhenItsLaterTilePrecedesAnotherClutInTheOt()
    {
        PixelImage cutout = Solid(default);
        cutout[1, 1] = new Rgba32(10, 20, 30);
        FieldAssets field = Field([
            Tile(4094, 0, cutout) with { PackedPalette = 0x100 },
            Tile(4094, 1, Solid(new Rgba32(220, 180, 120))) with { PackedPalette = 0x101 },
            Tile(4094, 0, Solid(new Rgba32(40, 60, 80))) with { PackedPalette = 0x100 }
        ]);
        byte[] pixels = new FieldRenderer(field, 320, 224).Render(new Dictionary<int, int>(), [], TimeSpan.Zero);
        Assert.Equal(new byte[] { 10, 20, 30, 255 }, Sample(pixels, 161, 113));
        Assert.Equal(new byte[] { 40, 60, 80, 255 }, Sample(pixels, 162, 113));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void InitialRawClutIsSeededBeforeCullingAndOnlyCullPassingDepthChangesResetIt(bool resetDepth)
    {
        FieldAssets field = Field([
            Tile(4094, 0, Solid(default)) with { X = -1000, PackedPalette = 0x100 },
            Tile(20, 0, Solid(default)) with { X = resetDepth ? -191 : -192, PackedPalette = 0x200 },
            Tile(4094, 0, Solid(new Rgba32(40, 60, 80))) with { PackedPalette = 0x101 },
            Tile(4094, 1, Solid(new Rgba32(220, 180, 120))) with { PackedPalette = 0x100 }
        ]);
        byte[] pixels = new FieldRenderer(field, 320, 224).Render(new Dictionary<int, int>(), [], TimeSpan.Zero);
        byte[] expected = resetDepth ? [40, 60, 80, 255] : [220, 180, 120, 255];
        Assert.Equal(expected, Sample(pixels, 161, 113));
    }

    [Theory]
    [InlineData(-192, 0, false)]
    [InlineData(-191, 0, true)]
    [InlineData(0, -144, false)]
    [InlineData(0, -143, true)]
    [InlineData(176, 0, false)]
    [InlineData(175, 0, true)]
    [InlineData(0, 128, false)]
    [InlineData(0, 127, true)]
    public void CullPassingHiddenModulesRegisterClutsBeforeAnimationRejection(int x, int y, bool inWindow)
    {
        FieldAssets field = Field([
            Tile(4094, 0, Solid(default)) with { X = -1000, PackedPalette = 0x200 },
            Tile(4094, 0, Solid(default)) with { X = x, Y = y, PackedPalette = 0x100, AnimationId = 7, AnimationState = 1 },
            Tile(4094, 0, Solid(new Rgba32(40, 60, 80))) with { PackedPalette = 0x101 },
            Tile(4094, 0, Solid(new Rgba32(220, 180, 120))) with { PackedPalette = 0x100 }
        ]);
        byte[] pixels = new FieldRenderer(field, 320, 224).Render(new Dictionary<int, int>(), [], TimeSpan.Zero);
        byte[] expected = inWindow ? [220, 180, 120, 255] : [40, 60, 80, 255];
        Assert.Equal(expected, Sample(pixels, 161, 113));
    }

    [Fact]
    public void UnequalDepthStillUsesFarToNearOrderingAndInactiveModulesDoNotOcclude()
    {
        FieldAssets field = Field([
            Tile(5, 0, Solid(new Rgba32(40, 60, 80))),
            Tile(1, 0, Solid(new Rgba32(200, 0, 0))) with { AnimationId = 7, AnimationState = 1 },
            Tile(4094, 1, Solid(new Rgba32(220, 180, 120)))
        ]);
        FieldRenderer renderer = new(field, 320, 224);
        Assert.Equal(new byte[] { 40, 60, 80, 255 }, Sample(renderer.Render(new Dictionary<int, int>(), [], TimeSpan.Zero), 161, 113));
        Assert.Equal(new byte[] { 200, 0, 0, 255 }, Sample(renderer.Render(new Dictionary<int, int> { [7] = 1 }, [], TimeSpan.Zero), 161, 113));
    }

    [Theory]
    [InlineData(321, 500, 120, 57, 66, 66)]
    [InlineData(323, 300, 110, 24, 16, 16)]
    [InlineData(327, 320, 240, 115, 99, 90)]
    [InlineData(328, 320, 240, 41, 49, 41)]
    public void OriginalDolletGeometryRemainsVisibleInFrontOfEqualDepthSky(int id, int x, int y, byte red, byte green, byte blue)
    {
        string directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR") ?? LaunchOptions.DefaultGameDirectory;
        Assert.SkipUnless(Directory.Exists(directory), "Installed original Dollet resources are required.");
        using GameAssets assets = new(directory);
        FieldAssets field = assets.LoadField(id);
        Assert.Contains(field.Background, tile => tile.Layer == 0 && tile.Z == 4094);
        Assert.Contains(field.Background, tile => tile.Layer == 1 && tile.Z == 4094);
        Dictionary<int, Vector2> offsets = id == 321 ? new() { [0] = new(-440, 0), [1] = new(440, 0) } : new();
        byte[] pixels = new FieldRenderer(field, 640, 448).Render(new Dictionary<int, int>(), [], TimeSpan.Zero, backgroundScrollOffsets: offsets);
        Assert.Equal(new byte[] { red, green, blue, 255 }, pixels.AsSpan((y * 640 + x) * 4, 4).ToArray());
    }

    private static FieldAssets Field(BackgroundTile[] tiles)
    {
        return new FieldAssets(0, "native-OT-order", [], [], tiles, new FieldCamera(Vector3.UnitX, Vector3.UnitY, Vector3.UnitZ, Vector3.Zero, Vector2.Zero, 100), new FieldWalkmesh([]), []);
    }

    private static BackgroundTile Tile(int depth, int layer, PixelImage image)
    {
        return new BackgroundTile(0, 0, depth, 255, 0, layer, MapBlendMode.None, false, true, image);
    }

    private static PixelImage Solid(Rgba32 color)
    {
        return new PixelImage(16, 16, Enumerable.Repeat(color, 256).ToArray());
    }

    private static byte[] Sample(byte[] pixels, int x, int y)
    {
        return pixels.AsSpan((y * 320 + x) * 4, 4).ToArray();
    }
}
