using System.Numerics;
using Memoria.FF8.Assets;
using Memoria.FF8.Rendering;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativePolygonColorRenderTests
{
    [Fact]
    public void ActualMagazineUsesOnePerInstancePrimitiveRgbAndRequiresKnownBytesBeforePixels()
    {
        string directory = TestResources.GameDirectory;
        using GameAssets assets = new(directory);
        FieldAssets original = assets.LoadField(895);
        FieldModel magazine = original.Models[6];
        //Declared component camera/placement isolate the shader; geometry/textures remain the actual reached MCH.
        FieldAssets viewer = new(895, "native-magazine-rgb-component", [], [], [], new FieldCamera(Vector3.UnitX, Vector3.UnitY, Vector3.UnitZ, new Vector3(0, 0, 2048), Vector2.Zero, 1024), new FieldWalkmesh([]), [magazine]);
        FieldRenderer renderer = new(viewer, modelTextureSampling: ModelTextureSampling.Nearest);
        ActorVisual neutral = new(0, 0, 0, 0, 0, 192, 0, 0, true, PolygonColor: new Vector3(128));
        byte[] pixels = renderer.Render(new Dictionary<int, int>(), [neutral], TimeSpan.Zero, backgroundVisible: false);
        byte[] baseline = pixels.ToArray();
        Assert.Contains(Enumerable.Range(0, baseline.Length / 4), pixel => baseline[pixel * 4] != 0 || baseline[pixel * 4 + 1] != 0 || baseline[pixel * 4 + 2] != 0);
        byte[] tinted = renderer.Render(new Dictionary<int, int>(), [neutral with { PolygonColor = new Vector3(128, 150, 128) }], TimeSpan.Zero, backgroundVisible: false).ToArray();
        Assert.False(baseline.SequenceEqual(tinted));
        for (int pixel = 0; pixel < baseline.Length; pixel += 4)
        {
            Assert.Equal(baseline[pixel], tinted[pixel]);
            Assert.Equal((byte)Math.Min(255, baseline[pixel + 1] * 150 / 128), tinted[pixel + 1]);
            Assert.Equal(baseline[pixel + 2], tinted[pixel + 2]);
        }
        renderer.Render(new Dictionary<int, int>(), [neutral with { PolygonColor = new Vector3(64, 32, 16) }], TimeSpan.Zero, backgroundVisible: false);
        Assert.Equal(baseline, renderer.Render(new Dictionary<int, int>(), [neutral], TimeSpan.Zero, backgroundVisible: false));
        byte[] retained = pixels.ToArray();
        Assert.Throws<NotSupportedException>(() => renderer.Render(new Dictionary<int, int>(), [neutral with { PolygonColor = null }], TimeSpan.Zero));
        Assert.Equal(retained, pixels);
        Assert.Throws<NotSupportedException>(() => renderer.Render(new Dictionary<int, int>(), [neutral with { PolygonColor = new Vector3(128, 150.5f, 128) }], TimeSpan.Zero));
        Assert.Equal(retained, pixels);
    }
}
