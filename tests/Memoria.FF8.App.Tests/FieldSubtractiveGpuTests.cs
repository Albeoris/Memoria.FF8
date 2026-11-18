using System.Numerics;
using Albeoris.Games.FF8.Imaging;
using Albeoris.Games.FF8.Imaging.Map;
using Memoria.FF8.Assets;
using Memoria.FF8.Rendering;
using Memoria.FF8.Resources;
using Color = Microsoft.Xna.Framework.Color;

namespace Memoria.FF8.App.Tests;

[Collection("GPU rendering")]
public sealed class FieldSubtractiveGpuTests
{
    [Fact]
    public void AlphaOnlyBackgroundKeepsCapturedPagePaletteAcrossSoftwareAndGpuFrames()
    {
        ushort[] words = new ushort[4096];
        words[8 * 256 + 15] = 0xCA52;
        words[10 * 256 + 15] = 0xFFFF;
        PixelImage background = Solid(new(160, 100, 50));
        PixelImage gray = Solid(new(148, 148, 148, 128));
        PixelImage white = Solid(new(255, 255, 255, 128));
        byte[] indices = Enumerable.Repeat((byte)15, 256).ToArray();
        FieldAssets field = Field([
            new(0, 0, 40, 255, 0, 0, MapBlendMode.None, false, true, background),
            new(0, 0, 10, 1, 0, 0, MapBlendMode.Subtract, true, true, gray) { PackedPalette = 0x3E00, IndexedSource = new(24, indices, words) },
            new(0, 0, 10, 2, 0, 0, MapBlendMode.Subtract, true, true, white) { PackedPalette = 0x3E80, IndexedSource = new(24, indices, words) }
        ]);
        FieldRenderer renderer = new(field);
        renderer.Render(new Dictionary<int, int> { [2] = 0 }, [], TimeSpan.Zero);
        Dictionary<int, int> visible = new() { [1] = 0 };
        Dictionary<int, Vector3> shade = new() { [1] = Vector3.Zero };
        byte[] software = renderer.Render(visible, [], TimeSpan.Zero, backgroundModuleShades: shade).ToArray();
        int sample = 121 * 320 + 161;
        Assert.Equal(new byte[] { 43, 27, 13, 255 }, software.AsSpan(sample * 4, 4).ToArray());
        using GpuTestDevice device = new();
        using GpuTriangleRenderer gpu = new(device.Device);
        renderer.TriangleRenderer = gpu;
        renderer.Render(visible, [], TimeSpan.Zero, backgroundModuleShades: shade);
        Color[] actual = GpuTestDevice.Read(gpu.Target);
        AssertPixels(software, actual);
        Assert.Equal(1, gpu.FrameTextureUploads);
        renderer.Render(visible, [], TimeSpan.Zero, backgroundModuleShades: shade);
        Assert.Equal(0, gpu.FrameTextureUploads);
        AssertPixels(software, GpuTestDevice.Read(gpu.Target));
    }

    [Fact]
    public void ReplacedMaskRefreshesItsAlphaTextureAndResizedAtlasWithoutChangingCoverage()
    {
        using InMemoryResourceSource source = new(new ResourceSourceDescriptor("field-alpha", GameDataEdition.Classic, []));
        ResourceService resources = new(source);
        PixelImage mask = Solid(new(148, 148, 148, 128));
        AssetHandle<TextureValue> handle = resources.GetTexture(new TextureBuildContext(new(new ResourceKey("field/alpha"), "mask"), source.Descriptor, 1, "rgba", 16, 16, () => mask));
        FieldAssets field = Field([
            new(0, 0, 40, 255, 0, 0, MapBlendMode.None, false, true, Solid(new(160, 100, 50))),
            new(0, 0, 10, 255, 0, 0, MapBlendMode.Subtract, true, true, mask) { Texture = handle }
        ]);
        FieldRenderer renderer = new(field);
        using GpuTestDevice device = new();
        using GpuTriangleRenderer gpu = new(device.Device);
        renderer.TriangleRenderer = gpu;
        renderer.Render(new Dictionary<int, int>(), [], TimeSpan.Zero);
        foreach (int size in new[] { 16, 32 })
        {
            PixelImage replacement = new(size, size, Enumerable.Repeat(new Rgba32(255, 255, 255, 128), size * size).ToArray());
            resources.Prepare(new Resolver(replacement)).Commit();
            renderer.TriangleRenderer = null;
            byte[] software = renderer.Render(new Dictionary<int, int>(), [], TimeSpan.Zero).ToArray();
            Assert.Equal(new byte[] { 43, 27, 13, 255 }, software.AsSpan((121 * 320 + 161) * 4, 4).ToArray());
            renderer.TriangleRenderer = gpu;
            renderer.Render(new Dictionary<int, int>(), [], TimeSpan.Zero);
            AssertPixels(software, GpuTestDevice.Read(gpu.Target));
            Assert.Equal(1, gpu.FrameTextureUploads);
            renderer.Render(new Dictionary<int, int>(), [], TimeSpan.Zero);
            Assert.Equal(0, gpu.FrameTextureUploads);
        }
    }

    private static FieldAssets Field(IReadOnlyList<BackgroundTile> tiles)
    {
        return new(0, "alpha-palette", [], [], tiles, new(Vector3.UnitX, Vector3.UnitY, Vector3.UnitZ, Vector3.Zero, Vector2.Zero, 100), new([]), []);
    }

    private static PixelImage Solid(Rgba32 color)
    {
        return new(16, 16, Enumerable.Repeat(color, 256).ToArray());
    }

    private static void AssertPixels(byte[] expected, Color[] actual)
    {
        Assert.Equal(expected.Length / 4, actual.Length);
        for (int pixel = 0; pixel < actual.Length; pixel++)
        {
            Assert.InRange(Math.Abs(expected[pixel * 4] - actual[pixel].R), 0, 1);
            Assert.InRange(Math.Abs(expected[pixel * 4 + 1] - actual[pixel].G), 0, 1);
            Assert.InRange(Math.Abs(expected[pixel * 4 + 2] - actual[pixel].B), 0, 1);
            Assert.Equal(expected[pixel * 4 + 3], actual[pixel].A);
        }
    }

    private sealed class Resolver(PixelImage image) : ITextureReplacementResolver
    {
        public TextureReplacement? Resolve(TextureBuildContext context)
        {
            return new(image, "field-alpha", 16, 16);
        }
    }
}
