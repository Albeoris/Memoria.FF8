using Albeoris.Games.FF8.Imaging;
using Memoria.FF8.Resources;

namespace Memoria.FF8.Mods.Tests;

public sealed class TextureSourceProviderAuthoringTests
{
    [Theory]
    [InlineData(GameDataEdition.Classic)]
    [InlineData(GameDataEdition.Remastered)]
    public void OriginalExportUsesSelectedProviderArtworkBeforeUserOverrides(GameDataEdition edition)
    {
        PixelImage native = new(1, 1, [new(255, 0, 0, 128)]);
        PixelImage installed = new(4, 4, Enumerable.Repeat(new Rgba32(0, 255, 0), 16).ToArray());
        PixelImage authored = new(8, 8, Enumerable.Repeat(new Rgba32(0, 0, 255), 64).ToArray());
        using Source source = new(edition, installed);
        ResourceService service = new(source);
        TextureBuildContext context = new(new(new("battle/example.dat"), "texture/0"), source.Descriptor, 1, "classic-battle", 1, 1, () => native);
        TextureValue original = service.ResolveOriginalForExport(context);
        Assert.Same(installed, original.Image);
        Assert.Equal((1, 1), (original.LogicalWidth, original.LogicalHeight));
        Assert.Equal(0, service.ResidentTextureCount);
        AssetHandle<TextureValue> handle = service.GetTexture(context);
        service.Prepare(new Resolver(authored)).Commit();
        TextureSourceSnapshot capture = Assert.Single(service.CaptureTextureSources([context.Key]));
        Assert.Same(authored, capture.Resolved.Image);
        Assert.Same(installed, capture.Original);
        Assert.Same(native, context.Original);
        Assert.Same(authored, handle.Value.Image);
    }

    [Fact]
    public void CoverageEditsAreSemanticChangesEvenWhenImagePixelsAreUnchanged()
    {
        PixelImage image = new(1, 1, [new(255, 0, 0)]);
        TextureTarget target = new("ff8", "field/example/example.mim", "tile/0");
        TextureTargetContract contract = new("classic-field-tile", 1, 1);
        string opaque = TexturePixelExchange.SemanticFingerprint(image, target, contract, Convert.ToBase64String([255]), 1, 1);
        string semitransparent = TexturePixelExchange.SemanticFingerprint(image, target, contract, Convert.ToBase64String([128]), 1, 1);
        Assert.NotEqual(opaque, semitransparent);
        Assert.Equal(opaque, TexturePixelExchange.SemanticFingerprint(new PixelImage(1, 1, image.Pixels.ToArray()), target, contract, Convert.ToBase64String([255]), 1, 1));
    }

    private sealed class Source(GameDataEdition edition, PixelImage image) : IResourceSource, ITextureReplacementResolver
    {
        public ResourceSourceDescriptor Descriptor { get; } = new("selected-provider", edition, []);
        public ResourceContent? Find(ResourceKey key) => null;
        public TextureReplacement? Resolve(TextureBuildContext context) => new(image, "installed-provider", 1, 1);
        public void Dispose()
        {
        }
    }

    private sealed class Resolver(PixelImage image) : ITextureReplacementResolver
    {
        public TextureReplacement? Resolve(TextureBuildContext context) => new(image, "authored-mod", 1, 1);
    }
}
