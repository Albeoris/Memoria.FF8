using Albeoris.Games.FF8.Imaging;
using Memoria.FF8.Resources;

namespace Memoria.FF8.Mods.Tests;

public sealed class ResourceRequestedVariantTests
{
    [Fact]
    public void SourceAndModVariantsShareStableSelectionFallbackAndReload()
    {
        using VariantSource source = new();
        ResourceService resources = new(source);
        TextureBuildContext Context(string? variant) => new(new(new("field/model"), "texture/0"), source.Descriptor, 1, "synthetic", 128, 128, () => source.Normal) { RequestedVariant = variant };
        AssetHandle<TextureValue> handle = resources.GetTexture(Context(null));
        Assert.Same(source.Normal, handle.Value.Image);
        Assert.Same(handle, resources.GetTexture(Context("eyes-closed")));
        Assert.Same(source.Closed, handle.Value.Image);
        Assert.Same(source.Closed, handle.Value.Variants!["eyes-closed"]);
        VariantResolver mod = new();
        resources.Prepare(mod).Commit();
        Assert.Same(mod.Closed, handle.Value.Image);
        Assert.Equal("mod", handle.Value.Provenance);
        Assert.Same(handle, resources.GetTexture(Context(null)));
        Assert.Same(mod.Normal, handle.Value.Image);
        resources.GetTexture(Context("eyes-closed"));
        VariantResolver reloaded = new();
        resources.Prepare(reloaded).Commit();
        Assert.Same(reloaded.Closed, handle.Value.Image);
        resources.Prepare(new VariantResolver(false)).Commit();
        Assert.NotSame(source.Closed, handle.Value.Image);
        Assert.Null(handle.Value.Variants);
        Assert.Equal("mod", handle.Value.Provenance);
        resources.Prepare(null).Commit();
        Assert.Same(source.Closed, handle.Value.Image);
        Assert.Same(handle, resources.GetTexture(Context("missing")));
        Assert.Same(source.Normal, handle.Value.Image);
        Assert.Equal((128, 128), (handle.Value.LogicalWidth, handle.Value.LogicalHeight));
    }

    [Fact]
    public void VariantSelectionLeavesNormalMipAndAnimationMetadataIntactAndDropsThemForAnotherImage()
    {
        using VariantSource source = new();
        ResourceService resources = new(source);
        TextureBuildContext Context(string? variant) => new(new(new("field/model"), "texture/0"), source.Descriptor, 1, "synthetic", 128, 128, () => source.Normal) { RequestedVariant = variant };
        AssetHandle<TextureValue> handle = resources.GetTexture(Context(null));
        Assert.NotNull(handle.Value.MipLevels);
        Assert.NotNull(handle.Value.Animation);
        resources.GetTexture(Context("eyes-closed"));
        Assert.Null(handle.Value.MipLevels);
        Assert.Null(handle.Value.Animation);
        resources.GetTexture(Context(null));
        Assert.NotNull(handle.Value.MipLevels);
        Assert.NotNull(handle.Value.Animation);
    }

    private sealed class VariantResolver(bool hasVariant = true) : ITextureReplacementResolver
    {
        public PixelImage Normal { get; } = new(256, 256);
        public PixelImage Closed { get; } = new(256, 256);
        public TextureReplacement Resolve(TextureBuildContext context) => new(Normal, "mod", 128, 128, Variants: hasVariant ? new Dictionary<string, PixelImage> { ["eyes-closed"] = Closed } : null);
    }

    private sealed class VariantSource : IResourceSource, ITextureReplacementResolver
    {
        public ResourceSourceDescriptor Descriptor { get; } = new("source", GameDataEdition.Remastered, []);
        public PixelImage Normal { get; } = new(256, 256);
        public PixelImage Closed { get; } = new(256, 256);
        public ResourceContent? Find(ResourceKey key) => null;
        public TextureReplacement Resolve(TextureBuildContext context) => new(Normal, "source", 128, 128, [Normal], new Dictionary<string, PixelImage> { ["eyes-closed"] = Closed }) { Animation = new([Normal], 1) };
        public void Dispose()
        {
        }
    }
}
