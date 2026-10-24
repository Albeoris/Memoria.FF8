using System.Numerics;
using Albeoris.Games.FF8.Imaging;
using Memoria.FF8.Assets;
using Memoria.FF8.Rendering;
using Color = Microsoft.Xna.Framework.Color;

namespace Memoria.FF8.App.Tests;

[Collection("GPU rendering")]
public sealed class FieldModelTexelCenterTests
{
    [Theory]
    [InlineData(false, 1, 1, 200, 80, 40)]
    [InlineData(true, 1, 1, 200, 80, 40)]
    [InlineData(false, 1.5f, 1, 150, 120, 60)]
    [InlineData(true, 1.5f, 1, 150, 120, 60)]
    [InlineData(false, 1, 1.5f, 140, 100, 120)]
    [InlineData(true, 1, 1.5f, 140, 100, 120)]
    [InlineData(false, 1.5f, 1.5f, 150, 140, 110)]
    [InlineData(true, 1.5f, 1.5f, 150, 140, 110)]
    public void SmoothRgbUsesIntegerSourceTexelCentersAndBlendsTheNextTexels(bool hardware, float u, float v, int red, int green, int blue)
    {
        PixelImage texture = Texture();
        FieldAssets field = Field(texture, new Vector2(u, v));
        Color pixel = Render(field, ModelTextureSampling.Bilinear, hardware);
        AssertRgb(pixel, red, green, blue);
        AssertRgb(Render(field, ModelTextureSampling.Nearest, hardware), 200, 80, 40);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void SmoothRgbExcludesTransparentNeighborsAndRetainsPointCutoutCoverage(bool hardware)
    {
        PixelImage texture = Texture();
        texture.Pixels[6] = new Rgba32(255, 0, 255, 0);
        FieldAssets visible = Field(texture, new Vector2(1.5f, 1));
        AssertRgb(Render(visible, ModelTextureSampling.Bilinear, hardware), 200, 80, 40);
        AssertRgb(Render(visible, ModelTextureSampling.Nearest, hardware), 200, 80, 40);

        FieldAssets cutout = Field(texture, new Vector2(2, 1));
        Color expected = Render(cutout with { Models = [] }, ModelTextureSampling.Nearest, hardware, false);
        Assert.Equal(expected, Render(cutout, ModelTextureSampling.Nearest, hardware));
        Assert.Equal(expected, Render(cutout, ModelTextureSampling.Bilinear, hardware));
    }

    [Theory]
    [InlineData(false, 0)]
    [InlineData(true, 0)]
    [InlineData(true, 2)]
    [InlineData(true, 4)]
    [InlineData(true, 8)]
    public void MirroredFacesKeepTheInternalAtlasSeamFreeOfThePrecedingDarkTexel(bool hardware, int samples)
    {
        FieldAssets field = Field(Texture(), Vector2.One);
        FieldModel model = field.Models[0];
        ModelVertex[] vertices =
        [
            new(new Vector3(0, -32, -32), 0), new(new Vector3(0, 0, -32), 0), new(new Vector3(0, 32, -32), 0),
            new(new Vector3(0, -32, 32), 0), new(new Vector3(0, 0, 32), 0), new(new Vector3(0, 32, 32), 0)
        ];
        ModelFace[] faces =
        [
            new([0, 3, 1, 4], [new(2, 1), new(2, 1), new(1, 1), new(1, 1)], 0, true),
            new([1, 4, 2, 5], [new(1, 1), new(1, 1), new(2, 1), new(2, 1)], 0, true)
        ];
        field = field with { Models = [model with { Vertices = vertices, Faces = faces }] };
        Color pixel = Render(field, ModelTextureSampling.Bilinear, hardware, samples: samples);
        // The probe is within one source texel of the mirrored seam. The preceding atlas column is black.
        Assert.InRange(pixel.R, (byte)170, (byte)200);
        Assert.InRange(pixel.G, (byte)80, (byte)105);
        Assert.InRange(pixel.B, (byte)40, (byte)55);
    }

    private static PixelImage Texture()
    {
        return new PixelImage(4, 4,
        [
            new(0, 0, 0, 255), new(0, 0, 0, 255), new(0, 0, 0, 255), new(0, 0, 0, 255),
            new(0, 0, 0, 255), new(200, 80, 40, 255), new(100, 160, 80, 255), new(0, 0, 0, 255),
            new(0, 0, 0, 255), new(80, 120, 200, 255), new(220, 200, 120, 255), new(0, 0, 0, 255),
            new(0, 0, 0, 255), new(0, 0, 0, 255), new(0, 0, 0, 255), new(0, 0, 0, 255)
        ]);
    }

    private static FieldAssets Field(PixelImage texture, Vector2 uv)
    {
        ModelVertex[] vertices = [new(new Vector3(0, -32, -32), 0), new(new Vector3(0, 32, -32), 0), new(new Vector3(0, -32, 32), 0), new(new Vector3(0, 32, 32), 0)];
        ModelFace face = new([0, 2, 1, 3], [uv, uv, uv, uv], 0, true);
        FieldModel model = new("texel-center-component", [new(-1, 0)], vertices, [face], [texture], [new([new(Vector3.Zero, [Vector3.Zero])])]);
        return new FieldAssets(100, "texel-center-component", [], [], [], new FieldCamera(Vector3.UnitX, Vector3.UnitY, Vector3.UnitZ, new Vector3(0, 0, 256), Vector2.Zero, 256), new FieldWalkmesh([]), [model]);
    }

    private static Color Render(FieldAssets field, ModelTextureSampling sampling, bool hardware, bool showActor = true, int samples = 0)
    {
        ActorVisual[] actors = showActor ? [new(0, 0, 0, 0, 0, 192, 0, 0, true, PolygonColor: new Vector3(128))] : [];
        FieldRenderer renderer = new(field, modelTextureSampling: sampling);
        const int x = 160;
        const int y = 112;
        if (hardware)
        {
            using GpuTestDevice device = new(samples);
            using GpuTriangleRenderer gpu = new(device.Device);
            renderer.TriangleRenderer = gpu;
            renderer.Render(new Dictionary<int, int>(), actors, TimeSpan.Zero);
            return GpuTestDevice.Read(gpu.Target)[y * gpu.Target.Width + x];
        }
        ReadOnlySpan<byte> pixels = renderer.Render(new Dictionary<int, int>(), actors, TimeSpan.Zero);
        int index = (y * renderer.OutputWidth + x) * 4;
        return new Color(pixels[index], pixels[index + 1], pixels[index + 2], pixels[index + 3]);
    }

    private static void AssertRgb(Color pixel, int red, int green, int blue)
    {
        Assert.InRange(Math.Abs(pixel.R - red), 0, 1);
        Assert.InRange(Math.Abs(pixel.G - green), 0, 1);
        Assert.InRange(Math.Abs(pixel.B - blue), 0, 1);
    }
}
