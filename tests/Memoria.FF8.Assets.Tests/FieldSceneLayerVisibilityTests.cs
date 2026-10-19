using System.Numerics;
using Albeoris.Games.FF8.Imaging;
using Albeoris.Games.FF8.Imaging.Map;
using Memoria.FF8.Assets;
using Memoria.FF8.Rendering;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class FieldSceneLayerVisibilityTests
{
    [Fact]
    public void ExactGroupSelectorDoesNotHideOtherModulesStatesOrLayers()
    {
        BackgroundTile[] tiles =
        [
            Tile(-32, 0, 5, 0, 200),
            Tile(-16, 1, 5, 0, 150),
            Tile(0, 0, 6, 0, 100),
            Tile(16, 0, 5, 1, 50)
        ];
        FieldAssets field = new(1, "layers", [], [], tiles, new FieldCamera(Vector3.UnitX, Vector3.UnitY, Vector3.UnitZ, Vector3.Zero, Vector2.Zero, 100), new FieldWalkmesh([]), []);
        FieldRenderer renderer = new(field);
        Dictionary<int, int> states = new() { [5] = 0, [6] = 0 };
        byte[] baseline = renderer.Render(states, [], TimeSpan.Zero).ToArray();
        byte[] hidden = renderer.Render(states, [], TimeSpan.Zero, backgroundVisibility: new Dictionary<FieldBackgroundKey, bool> { [new(0, 5, 0)] = false }).ToArray();
        Assert.Equal(16 * 17, Count(baseline, 200));
        Assert.Equal(0, Count(hidden, 200));
        Assert.Equal(Count(baseline, 150), Count(hidden, 150));
        Assert.Equal(Count(baseline, 100), Count(hidden, 100));
        Assert.Equal(0, Count(hidden, 50));
        byte[] inactivePreview = renderer.Render(states, [], TimeSpan.Zero, backgroundVisibility: new Dictionary<FieldBackgroundKey, bool> { [new(0, 5, 1)] = true }).ToArray();
        Assert.True(Count(inactivePreview, 50) > 0);
        Assert.Equal(baseline, renderer.Render(states, [], TimeSpan.Zero));
    }

    private static BackgroundTile Tile(int x, int layer, int module, int state, byte red)
    {
        return new(x, 0, 100, module, state, layer, MapBlendMode.None, false, true, new PixelImage(16, 16, Enumerable.Repeat(new Rgba32(red, 20, 30), 256).ToArray()));
    }

    private static int Count(byte[] pixels, byte red)
    {
        int count = 0;
        for (int offset = 0; offset < pixels.Length; offset += 4)
        {
            if (pixels[offset] == red)
            {
                count++;
            }
        }
        return count;
    }
}
