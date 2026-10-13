using System.Numerics;
using Albeoris.Games.FF8.Imaging;
using Albeoris.Games.FF8.Imaging.Map;
using Memoria.FF8.Assets;
using Memoria.FF8.Rendering;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeActorShadowTests
{
    [Fact]
    public void DegenerateNativeShadowPreservesTexturedAvatarAndEntireFramebuffer()
    {
        PixelImage texture = new(1, 1, [new Rgba32(100, 140, 200, 255)]);
        FieldModel model = new("triangle", [new ModelBone(-1, 0)], [new ModelVertex(new Vector3(0, -16, -16), 0), new ModelVertex(new Vector3(0, 16, -16), 0), new ModelVertex(new Vector3(0, 0, 16), 0)], [new ModelFace([0, 1, 2], [Vector2.Zero, Vector2.Zero, Vector2.Zero], 0, false)], [texture], [new ModelAnimation([new AnimationFrame(Vector3.Zero, [Vector3.Zero])])]);
        FieldAssets field = new(100, "shadow-fixture", [], [], [], new FieldCamera(Vector3.UnitX, Vector3.UnitY, Vector3.UnitZ, new Vector3(0, 0, 256), Vector2.Zero, 256), new FieldWalkmesh([]), [model]);
        FieldRenderer renderer = new(field, modelTextureSampling: ModelTextureSampling.Nearest);
        ActorVisual actor = new(0, 0, 0, 0, 0, 0, 0, 0, true, PolygonColor: new Vector3(128));
        byte[] baseline = renderer.Render(new Dictionary<int, int>(), [actor], TimeSpan.Zero).ToArray();
        Assert.Contains((byte)200, baseline);
        Assert.Equal(baseline, renderer.Render(new Dictionary<int, int>(), [actor with { GroundShadowRadius601To608 = 0 }], TimeSpan.Zero));
        Assert.NotEqual(baseline, renderer.Render(new Dictionary<int, int>(), [actor with { GroundShadowRadius601To608 = 0, PolygonColor = new Vector3(64, 128, 192) }], TimeSpan.Zero));
        Assert.Throws<NotSupportedException>(() => renderer.Render(new Dictionary<int, int>(), [actor with { GroundShadowRadius601To608 = 1 }], TimeSpan.Zero));
    }

    [Fact]
    public void NativeSubtractiveFanUsesBodyCenterGradientAndHiddenEligibility()
    {
        PixelImage ground = new(1, 1, [new Rgba32(200, 180, 160, 255)]);
        FieldModel emptyModel = new("shadow-only", [], [], [], [], []);
        BackgroundTile[] tiles = Enumerable.Range(0, 300).Select(index => new BackgroundTile((index % 20) * 16 - 160, (index / 20) * 16 - 120, 1000, 255, 0, 0, MapBlendMode.None, false, true, ground)).ToArray();
        FieldAssets field = new(100, "shadow-fan-fixture", [], [], tiles, new FieldCamera(Vector3.UnitX, Vector3.UnitY, Vector3.UnitZ, new Vector3(0, 0, 256), Vector2.Zero, 256), new FieldWalkmesh([]), [emptyModel]) { NativeExecutableHash = NativeFieldRuntimeProfile.FF8EnExecutableHash };
        FieldRenderer renderer = new(field);
        ActorVisual actor = new(0, 0, 500, 700, 0, 0, 0, 0, true, GroundShadowRadius601To608: 4, GroundShadowLevel609: 96, GroundShadowCentre: Vector3.Zero);
        byte[] baseline = renderer.Render(new Dictionary<int, int>(), [], TimeSpan.Zero).ToArray();
        byte[] shadow = renderer.Render(new Dictionary<int, int>(), [actor], TimeSpan.Zero).ToArray();
        int center = (120 * 320 + 160) * 4;
        int halfway = (120 * 320 + 176) * 4;
        int outside = (120 * 320 + 200) * 4;
        Assert.InRange(shadow[center], (byte)104, (byte)108);
        Assert.True(shadow[halfway] > shadow[center] && shadow[halfway] < baseline[halfway]);
        Assert.Equal(baseline[outside], shadow[outside]);
        Assert.Equal(Enumerable.Range(0, baseline.Length / 4).Where(pixel => baseline[pixel * 4] != shadow[pixel * 4]).ToArray(), Enumerable.Range(0, baseline.Length / 4).Where(pixel => baseline[pixel * 4 + 1] != shadow[pixel * 4 + 1]).ToArray());
        Assert.All(Enumerable.Range(0, shadow.Length / 4), pixel => Assert.True(baseline[pixel * 4] - shadow[pixel * 4] <= 96));
        Assert.Equal(baseline, renderer.Render(new Dictionary<int, int>(), [actor with { Visible = false }], TimeSpan.Zero));
        Assert.Equal(baseline, renderer.Render(new Dictionary<int, int>(), [actor with { ModelId = -1 }], TimeSpan.Zero));
        Assert.Equal(baseline, renderer.Render(new Dictionary<int, int>(), [actor with { GroundShadowLevel609 = 0 }], TimeSpan.Zero));
        Assert.Equal(shadow[center], renderer.Render(new Dictionary<int, int>(), [actor], TimeSpan.Zero, actorScrollOffset: new Vector2(10, 5))[((125 * 320) + 170) * 4]);
        Assert.Throws<NotSupportedException>(() => renderer.Render(new Dictionary<int, int>(), [actor with { GroundShadowLevel609 = null }], TimeSpan.Zero));
        FieldAssets foreground = field with { Background = tiles.Select(tile => tile with { Z = 1 }).ToArray() };
        Assert.Equal(baseline, new FieldRenderer(foreground).Render(new Dictionary<int, int>(), [actor], TimeSpan.Zero));
    }
}
