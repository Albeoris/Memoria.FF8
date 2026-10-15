using System.Numerics;
using Albeoris.Games.FF8.Imaging;
using Albeoris.Games.FF8.Imaging.Map;
using Memoria.FF8.Assets;
using Memoria.FF8.Rendering;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeActorTransparencyEdgeTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public void SharedEdgesAddExactlyOnceForQuadsAndAdjacentFaces(bool separateFaces, bool movie)
    {
        ModelFace[] faces = separateFaces
            ? [new([0, 1, 2], [Vector2.Zero, Vector2.Zero, Vector2.Zero], 0, false), new([1, 3, 2], [Vector2.Zero, Vector2.Zero, Vector2.Zero], 0, false)]
            : [new([0, 1, 2, 3], [Vector2.Zero, Vector2.Zero, Vector2.Zero, Vector2.Zero], 0, true)];
        FieldModel model = new("additive-square", [new(-1, 0)], [new(new Vector3(0, -16, -16), 0), new(new Vector3(0, -16, 16), 0), new(new Vector3(0, 16, -16), 0), new(new Vector3(0, 16, 16), 0)], faces, [Solid(new Rgba32(20, 40, 60))], [new([new(Vector3.Zero, [Vector3.Zero])])]);
        PixelImage ground = Solid(new Rgba32(10, 20, 30));
        BackgroundTile[] tiles = [Tile(-16, -16, 1000, ground), Tile(0, -16, 1000, ground), Tile(-16, 0, 1000, ground), Tile(0, 0, 1000, ground)];
        FieldCamera camera = new(Vector3.UnitX, Vector3.UnitY, Vector3.UnitZ, new Vector3(0, 0, 256), Vector2.Zero, 256);
        FieldAssets field = new(0, "additive-shared-edge", [], [], tiles, camera, new FieldWalkmesh([]), [model]);
        FieldRenderer renderer = new(field, 320, 224, ModelTextureSampling.Nearest, nativeMovieViewport: true);
        ActorVisual actor = new(0, 0, 0, 0, 0, 192, 0, 0, true, PolygonColor: new Vector3(128), PrimitiveTransparencyEnabled: true);
        byte[] pixels;
        if (movie)
        {
            byte[] moviePixels = Enumerable.Range(0, 320 * 224).SelectMany(_ => new byte[] { 10, 20, 30, 255 }).ToArray();
            pixels = renderer.RenderMovieFrame(moviePixels, 320, 224, camera, [actor], null);
        }
        else
        {
            pixels = renderer.Render(new Dictionary<int, int>(), [actor], TimeSpan.Zero);
        }
        // The square projects to [144,176] x [96,128]. Its shared diagonal
        // crosses sample centers, so every interior sample must add once.
        for (int y = 97; y < 127; y++)
        {
            for (int x = 145; x < 175; x++)
            {
                Assert.Equal(new byte[] { 30, 60, 90, 255 }, Sample(pixels, x, y));
            }
        }
    }

    [Fact]
    public void SameZTranslucentBackgroundRetainsReverseFileOrderAcrossCluts()
    {
        BackgroundTile[] tiles =
        [
            Tile(0, 0, 100, Solid(new Rgba32(10, 20, 30))) with { PackedPalette = 0x200 },
            Tile(0, 0, 10, Solid(new Rgba32(8, 12, 16, 128))) with { PackedPalette = 0x100, Blend = true, BlendMode = MapBlendMode.Add },
            Tile(0, 0, 10, Solid(new Rgba32(100, 120, 140, 128))) with { PackedPalette = 0x101, Blend = true, BlendMode = MapBlendMode.Average },
            Tile(0, 0, 10, Solid(new Rgba32(20, 40, 60, 128))) with { PackedPalette = 0x100, Blend = true, BlendMode = MapBlendMode.Add }
        ];
        FieldAssets field = new(0, "translucent-OT-order", [], [], tiles, new FieldCamera(Vector3.UnitX, Vector3.UnitY, Vector3.UnitZ, Vector3.Zero, Vector2.Zero, 100), new FieldWalkmesh([]), []);
        byte[] pixels = new FieldRenderer(field, 320, 224, nativeMovieViewport: true).Render(new Dictionary<int, int>(), [], TimeSpan.Zero);
        // Native head insertion draws the last Add, then Average, then first Add:
        // ((10,20,30) + (20,40,60) + (100,120,140)) / 2 + (8,12,16).
        Assert.Equal(new byte[] { 73, 102, 131, 255 }, Sample(pixels, 161, 113));
    }

    private static BackgroundTile Tile(int x, int y, int depth, PixelImage image)
    {
        return new BackgroundTile(x, y, depth, 255, 0, 0, MapBlendMode.None, false, true, image);
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
