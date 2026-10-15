using System.Numerics;
using Albeoris.Games.FF8.Imaging;
using Albeoris.Games.FF8.Imaging.Map;
using Memoria.FF8.Assets;
using Memoria.FF8.Rendering;

namespace Memoria.FF8.App.Tests;

public sealed class NativeActorTransparencyTests
{
    [Theory]
    [InlineData(128)]
    [InlineData(255)]
    public void ActorTransparencyAddsEveryCoveredTexelAndPreservesCutouts(byte alpha)
    {
        PixelImage texture = new(2, 1, [new Rgba32(100, 140, 200, alpha), new Rgba32(255, 255, 255, 0)]);
        FieldModel model = new("triangle", [new ModelBone(-1, 0)], [new ModelVertex(new Vector3(0, -16, -16), 0), new ModelVertex(new Vector3(0, 16, -16), 0), new ModelVertex(new Vector3(0, 0, 16), 0)], [new ModelFace([0, 2, 1], [Vector2.Zero, new Vector2(1, 0), new Vector2(2, 0)], 0, false)], [texture], [new ModelAnimation([new AnimationFrame(Vector3.Zero, [Vector3.Zero])])]);
        PixelImage ground = new(16, 16, Enumerable.Repeat(new Rgba32(80, 90, 100, 255), 256).ToArray());
        BackgroundTile[] tiles = Enumerable.Range(0, 300).Select(index => new BackgroundTile((index % 20) * 16 - 160, (index / 20) * 16 - 120, 1000, 255, 0, 0, MapBlendMode.None, false, true, ground)).ToArray();
        FieldAssets field = new(100, "transparency", [], [], tiles, new FieldCamera(Vector3.UnitX, Vector3.UnitY, Vector3.UnitZ, new Vector3(0, 0, 256), Vector2.Zero, 256), new FieldWalkmesh([]), [model]);
        FieldRenderer renderer = new(field, modelTextureSampling: ModelTextureSampling.Nearest);
        ActorVisual actor = new(0, 0, 0, 0, 0, 0, 0, 0, true, PolygonColor: new Vector3(128));
        byte[] background = renderer.Render(new Dictionary<int, int>(), [], TimeSpan.Zero).ToArray();
        byte[] opaque = renderer.Render(new Dictionary<int, int>(), [actor], TimeSpan.Zero).ToArray();
        byte[] transparent = renderer.Render(new Dictionary<int, int>(), [actor with { PrimitiveTransparencyEnabled = true }], TimeSpan.Zero).ToArray();
        int covered = 0;
        for (int index = 0; index < opaque.Length; index += 4)
        {
            bool coverage = opaque[index] != background[index];
            covered += coverage ? 1 : 0;
            for (int channel = 0; channel < 3; channel++)
            {
                Assert.Equal(coverage ? (byte)Math.Min(255, background[index + channel] + opaque[index + channel]) : background[index + channel], transparent[index + channel]);
            }
        }
        Assert.True(covered > 0);
        Assert.Equal(opaque, renderer.Render(new Dictionary<int, int>(), [actor], TimeSpan.Zero));
        ActorVisual behind = actor with { ObjectId = 1, Z = 16, PolygonColor = new Vector3(64) };
        Assert.Equal(renderer.Render(new Dictionary<int, int>(), [behind, actor with { PrimitiveTransparencyEnabled = true }], TimeSpan.Zero).ToArray(), renderer.Render(new Dictionary<int, int>(), [actor with { PrimitiveTransparencyEnabled = true }, behind], TimeSpan.Zero));
    }
}
