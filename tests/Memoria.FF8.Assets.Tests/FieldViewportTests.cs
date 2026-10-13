using System.Numerics;
using Albeoris.Games.FF8.Imaging;
using Albeoris.Games.FF8.Imaging.Map;
using Memoria.FF8.Assets;
using Memoria.FF8.Rendering;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class FieldViewportTests
{
    [Theory]
    [InlineData(320, 240, 0, 0, 320, 240)]
    [InlineData(640, 360, 80, 0, 480, 360)]
    [InlineData(320, 480, 0, 120, 320, 240)]
    public void NativeDrawingRectangleFillsThePcViewportAndClipsEveryPrimitive(int width, int height, int left, int top, int viewportWidth, int viewportHeight)
    {
        PixelImage texture = Solid(new Rgba32(200, 20, 10));
        List<BackgroundTile> tiles = [];
        for (int y = -144; y < 144; y += 16)
        {
            for (int x = -192; x < 192; x += 16)
            {
                tiles.Add(new BackgroundTile(x, y, 100, 255, 0, 0, MapBlendMode.None, false, true, texture));
            }
        }
        // Root pre-rotation maps this local Y/Z plane to screen X/Y. Its
        // oversized quad extends through all four native DRAWENV edges.
        FieldModel model = new("oversized", [new(-1, 0)], [new(new Vector3(0, -300, -300), 0), new(new Vector3(0, -300, 300), 0), new(new Vector3(0, 300, -300), 0), new(new Vector3(0, 300, 300), 0)], [new([0, 1, 2, 3], [Vector2.Zero, Vector2.Zero, Vector2.Zero, Vector2.Zero], 0, true)], [Solid(new Rgba32(10, 20, 200))], [new([new(Vector3.Zero, [Vector3.Zero])])]);
        FieldAssets field = new(0, "viewport", [], [], tiles, new FieldCamera(Vector3.UnitX, Vector3.UnitY, Vector3.UnitZ, Vector3.Zero, Vector2.Zero, 100), new FieldWalkmesh([]), [model]);
        FieldRenderer renderer = new(field, width, height);
        byte[] background = renderer.Render(new Dictionary<int, int>(), [], TimeSpan.Zero).ToArray();
        byte[] actor = renderer.Render(new Dictionary<int, int>(), [new ActorVisual(0, 0, 0, 0, 100, 192, 0, 0, true)], TimeSpan.Zero, backgroundVisible: false).ToArray();
        FieldAssets shadowField = field with { NativeExecutableHash = NativeFieldRuntimeProfile.FF8EnExecutableHash, Models = [model with { Bones = [] }] };
        ActorVisual shadowActor = new(0, 0, 0, 0, 100, 192, 0, 0, true, GroundShadowRadius601To608: 255, GroundShadowLevel609: 128, GroundShadowCentre: new Vector3(0, 0, 100));
        byte[] shadow = new FieldRenderer(shadowField, width, height).Render(new Dictionary<int, int>(), [shadowActor], TimeSpan.Zero).ToArray();
        Assert.NotEqual(background, shadow);
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                bool inside = x >= left && x < left + viewportWidth && y >= top && y < top + viewportHeight;
                int index = (y * width + x) * 4;
                Assert.Equal(inside ? 200 : 0, background[index]);
                Assert.Equal(inside ? 200 : 0, actor[index + 2]);
                if (!inside)
                {
                    Assert.Equal(0, shadow[index]);
                    Assert.Equal(0, shadow[index + 1]);
                    Assert.Equal(0, shadow[index + 2]);
                }
            }
        }
    }

    [Theory]
    [InlineData(180)]
    [InlineData(181)]
    public void InstalledInfirmaryViewsFillTheTopAndBottomOfTheNativePicture(int fieldId)
    {
        string directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR") ?? @"C:\Steam\steamapps\common\FINAL FANTASY VIII";
        Assert.SkipUnless(Directory.Exists(directory), "Installed original infirmary resources are required.");
        using GameAssets assets = new(directory);
        FieldAssets field = assets.LoadField(fieldId);
        Assert.Equal(new FieldBounds(0, 224, 320, 0), field.Information!.ScreenBounds[0]);
        FieldRenderer renderer = new(field, 960, 720);
        Dictionary<int, int> states = field.Background.Where(tile => tile.AnimationId != 255).Select(tile => tile.AnimationId).Distinct().ToDictionary(id => id, _ => 0);
        ActorVisual[] actors = fieldId == 180 ? [new(1, 1, -141, 802, 0, 192, 3, 2, true), new(8, 8, -150, 650, 0, 64, 0, 1, true)] : [];
        byte[] pixels = renderer.Render(states, actors, TimeSpan.Zero).ToArray();
        foreach (int row in new[] { 0, 719 })
        {
            Assert.Contains(pixels.AsSpan(row * 960 * 4, 960 * 4).ToArray().Where((_, index) => index % 4 != 3), value => value != 0);
        }
        string? evidence = Environment.GetEnvironmentVariable("FF8_RENDER_EVIDENCE");
        if (evidence is not null)
        {
            Directory.CreateDirectory(evidence);
            File.WriteAllBytes(Path.Combine(evidence, $"native-viewport-infirmary-{fieldId}-960x720.rgba"), pixels);
        }
    }

    private static PixelImage Solid(Rgba32 color)
    {
        return new PixelImage(16, 16, Enumerable.Repeat(color, 256).ToArray());
    }
}
