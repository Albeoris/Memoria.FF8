using System.Numerics;
using Albeoris.Games.FF8.Imaging;
using Albeoris.Games.FF8.Imaging.Map;
using Albeoris.Games.FF8.Imaging.Mim;
using Memoria.FF8.Assets;
using Memoria.FF8.Rendering;

namespace Memoria.FF8.Assets.Tests;

public sealed class BackgroundActorBlendTests
{
    [Theory]
    [InlineData(MapBlendMode.Average, 35, 5, 110)]
    [InlineData(MapBlendMode.Add, 70, 10, 220)]
    [InlineData(MapBlendMode.Subtract, 0, 0, 180)]
    [InlineData(MapBlendMode.QuarterAdd, 17, 2, 205)]
    public void NearStpTexelsBlendOverActorsWhileOpaqueAndTransparentTexelsKeepTheirNativeMeaning(MapBlendMode mode, byte red, byte green, byte blue)
    {
        PixelImage texture = Solid(new Rgba32(0, 0, 200));
        FieldModel model = new("triangle", [new(-1, 0)], [new(new Vector3(0, -10, -10), 0), new(new Vector3(0, 10, -10), 0), new(new Vector3(0, 0, 10), 0)], [new([0, 1, 2], [Vector2.Zero, Vector2.Zero, Vector2.Zero], 0, false)], [texture], [new([new(Vector3.Zero, [Vector3.Zero])])]);
        PixelImage fog = Solid(new Rgba32(70, 10, 20, 128));
        FieldAssets field = new(0, "mixed-STP", [], [], [new(0, 0, 40, 255, 0, 0, MapBlendMode.None, false, true, Solid(new Rgba32(20, 40, 60))), new(0, 0, 10, 255, 0, 0, mode, true, true, fog)], new FieldCamera(Vector3.UnitX, Vector3.UnitY, Vector3.UnitZ, Vector3.Zero, Vector2.Zero, 100), new FieldWalkmesh([]), [model]);
        ActorVisual[] actors = [new(0, 0, 0, 0, 100, 192, 0, 0, true)];
        FieldRenderer renderer = new(field);
        Assert.Equal(new byte[] { red, green, blue, 255 }, Sample(renderer.Render(new Dictionary<int, int>(), actors, TimeSpan.Zero)));
        fog[1, 1] = new Rgba32(70, 10, 20);
        Assert.Equal(new byte[] { 70, 10, 20, 255 }, Sample(renderer.Render(new Dictionary<int, int>(), actors, TimeSpan.Zero)));
        fog[1, 1] = default;
        Assert.Equal(new byte[] { 0, 0, 200, 255 }, Sample(renderer.Render(new Dictionary<int, int>(), actors, TimeSpan.Zero)));
        fog[1, 1] = new Rgba32(70, 10, 20, 128);
        Assert.Equal(new byte[] { 0, 0, 200, 255 }, Sample(new FieldRenderer(field with { Background = [field.Background[0], field.Background[1] with { Z = 30 }] }).Render(new Dictionary<int, int>(), actors, TimeSpan.Zero)));
        Assert.Equal(new byte[] { 70, 10, 20, 255 }, Sample(new FieldRenderer(field with { Background = [field.Background[0], field.Background[1] with { Blend = false }] }).Render(new Dictionary<int, int>(), actors, TimeSpan.Zero)));
    }

    [Fact]
    public void BackgroundWithoutActorCoverageIsBlendedOnceAndAtlasDecodingRetainsStp()
    {
        PixelImage fog = Solid(new Rgba32(70, 10, 20, 128));
        FieldAssets field = new(0, "no-actors", [], [], [new(0, 0, 40, 255, 0, 0, MapBlendMode.None, false, true, Solid(new Rgba32(20, 40, 60))), new(0, 0, 10, 255, 0, 0, MapBlendMode.Add, true, true, fog)], new FieldCamera(Vector3.UnitX, Vector3.UnitY, Vector3.UnitZ, Vector3.Zero, Vector2.Zero, 100), new FieldWalkmesh([]), []);
        Assert.Equal(new byte[] { 90, 50, 80, 255 }, Sample(new FieldRenderer(field).Render(new Dictionary<int, int>(), [], TimeSpan.Zero)));
        ushort[] colors = new ushort[4096];
        colors[1] = 0x801F;
        colors[2] = 0x001F;
        byte[] atlas = new byte[MimFile.AtlasStride * MimFile.AtlasHeight];
        atlas[1] = 1;
        atlas[2] = 2;
        MimFile mim = new(new byte[MimFile.HeaderSize], colors, atlas);
        PixelImage decoded = FieldBackgroundTileDecoder.Decode(mim, new MapTile { PackedTexture = 0x80 });
        Assert.Equal(default, decoded[0, 0]);
        Assert.Equal(new Rgba32(255, 0, 0, 128), decoded[1, 0]);
        Assert.Equal(new Rgba32(255, 0, 0), decoded[2, 0]);
    }

    private static PixelImage Solid(Rgba32 color)
    {
        return new PixelImage(16, 16, Enumerable.Repeat(color, 256).ToArray());
    }

    private static byte[] Sample(byte[] pixels)
    {
        return pixels.AsSpan((121 * 320 + 161) * 4, 4).ToArray();
    }
}
