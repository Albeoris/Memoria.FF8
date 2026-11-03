using Albeoris.Games.FF8.Imaging;
using Memoria.FF8.Resources;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeTripleTriadArtworkLoadingTests
{
    [Fact]
    public void HdFrontsResolveOnlyWhenRequestedAndAreCachedPerCard()
    {
        string directory = TestResources.GameDirectory;
        using IResourceSource source = GameDataSourceFactory.Open(directory);
        ResourceService resources = new(source);
        RecordingResolver resolver = new();
        resources.Prepare(resolver).Commit();
        NativeTripleTriadArtwork artwork = NativeTripleTriadArtwork.Load(directory, resources);
        Assert.Empty(resolver.Fronts);
        PixelImage first = artwork.GetFront(7);
        Assert.Equal(new[] { "front/7" }, resolver.Fronts);
        Assert.Equal(128, first.Width);
        Assert.Same(first, artwork.GetFront(7));
        Assert.Equal(new[] { "front/7" }, resolver.Fronts);
        _ = artwork.GetFront(8);
        Assert.Equal(new[] { "front/7", "front/8" }, resolver.Fronts);
        Assert.Throws<ArgumentOutOfRangeException>(() => artwork.GetFront(-1));
        Assert.Throws<ArgumentOutOfRangeException>(() => artwork.GetFront(CardArtwork.CardCount));
    }

    private sealed class RecordingResolver : ITextureReplacementResolver
    {
        public List<string> Fronts { get; } = [];

        public TextureReplacement? Resolve(TextureBuildContext context)
        {
            if (!context.Key.Subresource.StartsWith("front/", StringComparison.Ordinal))
            {
                return null;
            }
            Fronts.Add(context.Key.Subresource);
            return new TextureReplacement(new PixelImage(128, 128), "fixture:card", context.LogicalWidth, context.LogicalHeight);
        }
    }
}
