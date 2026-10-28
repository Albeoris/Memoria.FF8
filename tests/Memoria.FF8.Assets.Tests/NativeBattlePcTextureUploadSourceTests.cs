using Albeoris.Games.FF8.Imaging;
using Memoria.FF8.Resources;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeBattlePcTextureUploadSourceTests
{
    [Theory]
    [InlineData(0, 0x3210, 0)]
    [InlineData(1, 0x0100, 0x0302)]
    public void FullNativeWindowPublishesOriginalBgraWithoutSourceProvenanceOrRenderedPixels(int depth, ushort first, ushort second)
    {
        NativeBattleEffectTextureState state = State(first, second);
        using InMemoryResourceSource source = Source();
        ResourceService resources = new(source);
        RecordingResolver resolver = new();
        resources.Prepare(resolver).Commit();
        using NativeBattleEffectTextureSource textures = new(resources);
        PixelImage original = new(4, 1, Enumerable.Repeat(new Rgba32(40, 50, 60, 70), 4).ToArray());

        Assert.Same(original, textures.Apply(original, state, (ushort)(17 | depth << 7), 32 * 64, 0, 0, 4, 1));

        TextureBuildContext context = Assert.Single(resolver.Contexts);
        Assert.Equal("native-pc-texture-upload", context.Layout);
        TextureSourceUpload upload = Assert.IsType<TextureSourceUpload>(context.NativeUpload);
        Assert.Equal((4, 1, 4, 7, 0, 0, 4, 1), (upload.Width, upload.Height, upload.BytesPerPixel, upload.NativeMode, upload.X, upload.Y, upload.RegionWidth, upload.RegionHeight));
        Assert.Equal(new byte[] { 1, 0, 0, 0, 0, 0, 255, 127, 8, 0, 0, 127, 0, 255, 0, 127 }, upload.Pixels.ToArray());
    }

    [Fact]
    public void RetainedUploadUsesRetainedIndicesAndTheSelectedLiveClut()
    {
        NativeBattleEffectTextureState initial = State(0x1111, 0);
        NativeBattlePcTextureWindowPixels retained = NativeBattlePcTextureWindowPixels.Capture(17, new(0, 0, 4, 1), initial);
        NativeBattleEffectTextureState current = initial.Upload45BD30(64, 256, 1, 1, [0x33, 0x33]).Upload45BD30(0, 33, 256, 1, Palette(0x7C00));
        using InMemoryResourceSource source = Source();
        ResourceService resources = new(source);
        RecordingResolver resolver = new();
        resources.Prepare(resolver).Commit();
        using NativeBattleEffectTextureSource textures = new(resources);

        textures.Apply(new(4, 1), current, 17, 33 * 64, 0, 0, 4, 1, retained);

        TextureSourceUpload upload = Assert.Single(resolver.Contexts).NativeUpload!;
        Assert.Equal(Enumerable.Repeat(new byte[] { 255, 0, 0, 127 }, 4).SelectMany(value => value).ToArray(), upload.Pixels.ToArray());
    }

    [Fact]
    public void UnsupportedDirectTextureDoesNotInventUploadMetadata()
    {
        using InMemoryResourceSource source = Source();
        ResourceService resources = new(source);
        RecordingResolver resolver = new();
        resources.Prepare(resolver).Commit();
        using NativeBattleEffectTextureSource textures = new(resources);
        PixelImage original = new(4, 1);

        Assert.Same(original, textures.Apply(original, State(0, 0), 0x111, 0, 0, 0, 4, 1));
        Assert.Empty(resolver.Contexts);
    }

    [Theory]
    [InlineData(int.MaxValue, 1)]
    [InlineData(1, int.MaxValue)]
    [InlineData(255, 2)]
    public void UnprovedWrappingOrOverflowingWindowsDoNotInventUploadMetadata(int u, int width)
    {
        using InMemoryResourceSource source = Source();
        ResourceService resources = new(source);
        RecordingResolver resolver = new();
        resources.Prepare(resolver).Commit();
        using NativeBattleEffectTextureSource textures = new(resources);
        PixelImage original = new(4, 1);

        Assert.Same(original, textures.Apply(original, State(0, 0), 17, 0, u, 0, width, 1));
        Assert.Empty(resolver.Contexts);
    }

    [Fact]
    public void WholeWindowReplacementWorksWithoutSemanticRegionsAndKeepsAlternatePass()
    {
        using InMemoryResourceSource source = Source();
        ResourceService resources = new(source);
        RecordingResolver resolver = new() { Replacement = new(new(8, 2, Enumerable.Repeat(new Rgba32(255, 0, 0, 127), 16).ToArray()), "fixture:upload", 4, 1) };
        resources.Prepare(resolver).Commit();
        using NativeBattleEffectTextureSource textures = new(resources);

        PixelImage image = textures.Apply(new(4, 1), State(0, 0), 17, 32 * 64, 0, 0, 4, 1, alternatePaletteShift: 0);

        Assert.Equal((8, 2), (image.Width, image.Height));
        Assert.Equal(new Rgba32(0, 0, 0, 62), image[0, 0]);
        Assert.Single(resolver.Contexts);
        Assert.Equal(1, resources.ResidentTextureCount);
        textures.Dispose();
        Assert.Equal(0, resources.ResidentTextureCount);
    }

    private static NativeBattleEffectTextureState State(ushort first, ushort second) => NativeBattleEffectTextureState.FromColdProcess()
        .Upload45BD30(64, 256, 2, 1, [(byte)first, (byte)(first >> 8), (byte)second, (byte)(second >> 8)])
        .Upload45BD30(0, 32, 256, 1, Palette(31));

    private static byte[] Palette(ushort firstColor)
    {
        byte[] palette = new byte[512];
        palette[2] = (byte)firstColor;
        palette[3] = (byte)(firstColor >> 8);
        palette[5] = 0x80;
        palette[6] = 0xE0;
        palette[7] = 3;
        return palette;
    }

    private static InMemoryResourceSource Source() => new(new("fixture", GameDataEdition.Classic, ["demaster-content-upload"]));

    private sealed class RecordingResolver : ITextureReplacementResolver
    {
        public List<TextureBuildContext> Contexts { get; } = [];
        public TextureReplacement? Replacement { get; init; }

        public TextureReplacement? Resolve(TextureBuildContext context)
        {
            Contexts.Add(context);
            return Replacement;
        }
    }
}
