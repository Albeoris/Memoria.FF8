using Albeoris.Games.FF8.Imaging;
using Memoria.FF8.Resources;

namespace Memoria.FF8.Mods.Tests;

public sealed class ResourceRejectionPublicationTests
{
    [Fact]
    public void RejectedWinnerUsesOriginalWhileOtherTexturesPublishWithoutTryingLowerPriority()
    {
        using InMemoryResourceSource source = Source();
        ResourceService service = new(source);
        PixelImage original = new(2, 2);
        PixelImage valid = new(4, 4);
        TextureBuildContext rejectedContext = Context("rejected", () => original);
        AssetHandle<TextureValue> rejected = service.GetTexture(rejectedContext);
        AssetHandle<TextureValue> accepted = service.GetTexture(Context("accepted", () => new(2, 2)));
        int lowerPriorityRejectedReads = 0;
        int reports = 0;
        service.ReplacementFailed += (_, _) => throw new InvalidOperationException("Broken diagnostic subscriber.");
        service.ReplacementFailed += (key, error) =>
        {
            Assert.Equal(rejectedContext.Key, key);
            Assert.IsType<ResourceReplacementRejectedException>(error);
            Assert.Same(original, rejected.Value.Image);
            Assert.Same(valid, accepted.Value.Image);
            reports++;
        };
        OrderedTextureResolver resolver = new(new Resolver(context => context.Key == rejectedContext.Key ? throw new ResourceReplacementRejectedException("Rejected selected texture.") : null), new Resolver(context =>
        {
            if (context.Key == rejectedContext.Key)
            {
                lowerPriorityRejectedReads++;
            }
            return Replacement(valid);
        }));

        ResourceService.PreparedResources prepared = service.Prepare(resolver);
        Assert.Equal(0, reports);
        prepared.Commit();

        Assert.Equal(1, reports);
        Assert.Equal(0, lowerPriorityRejectedReads);
        Assert.False(rejected.Value.IsReplacement);
        Assert.True(accepted.Value.IsReplacement);
        Assert.Equal(1, service.Generation);
    }

    [Fact]
    public void RejectedThenFixedThenDisabledSelectionKeepsStableResidentHandle()
    {
        using InMemoryResourceSource source = Source();
        ResourceService service = new(source);
        PixelImage original = new(2, 2);
        PixelImage fixedImage = new(4, 4);
        TextureBuildContext context = Context("texture", () => original);
        AssetHandle<TextureValue> handle = service.GetTexture(context);
        int reports = 0;
        service.ReplacementFailed += (_, _) => reports++;

        service.Prepare(Rejected()).Commit();
        Assert.Same(original, handle.Value.Image);
        Assert.Equal(1, reports);
        service.Prepare(new Resolver(_ => Replacement(fixedImage))).Commit();
        Assert.Same(fixedImage, handle.Value.Image);
        Assert.True(handle.Value.IsReplacement);
        service.Prepare(null).Commit();

        Assert.Same(handle, service.GetTexture(context));
        Assert.Same(original, handle.Value.Image);
        Assert.False(handle.Value.IsReplacement);
        Assert.Equal(1, reports);
        Assert.Equal(3, service.Generation);
    }

    [Fact]
    public void DelayedNotificationReportsCommittedRejectionsOnce()
    {
        using InMemoryResourceSource source = Source();
        ResourceService service = new(source);
        AssetHandle<TextureValue> handle = service.GetTexture(Context("texture", () => new(2, 2)));
        int reports = 0;
        service.ReplacementFailed += (_, _) => reports++;
        ResourceService.PreparedResources prepared = service.Prepare(Rejected());

        prepared.Notify();
        Assert.Equal(0, reports);
        prepared.Commit(notify: false);
        Assert.Equal(0, reports);
        Assert.False(handle.Value.IsReplacement);
        Assert.Equal(1, service.Generation);
        prepared.Notify();
        prepared.Notify();

        Assert.Equal(1, reports);
    }

    [Fact]
    public void StalePreparationNeverReportsItsRejectedResources()
    {
        using InMemoryResourceSource source = Source();
        ResourceService service = new(source);
        service.GetTexture(Context("texture", () => new(2, 2)));
        int reports = 0;
        service.ReplacementFailed += (_, _) => reports++;
        ResourceService.PreparedResources stale = service.Prepare(Rejected());
        service.Prepare(null).Commit();

        Assert.Throws<InvalidOperationException>(() => stale.Commit());
        stale.Notify();

        Assert.Equal(0, reports);
        Assert.Equal(1, service.Generation);
    }

    [Fact]
    public void UnmarkedReplacementFailureAbortsWholePublicationWithoutReportingEarlierRejection()
    {
        using InMemoryResourceSource source = Source();
        ResourceService service = new(source);
        PixelImage firstOriginal = new(2, 2);
        PixelImage secondOriginal = new(2, 2);
        TextureBuildContext firstContext = Context("first", () => firstOriginal);
        AssetHandle<TextureValue> first = service.GetTexture(firstContext);
        AssetHandle<TextureValue> second = service.GetTexture(Context("second", () => secondOriginal));
        int reports = 0;
        service.ReplacementFailed += (_, _) => reports++;
        Resolver resolver = new(context => context.Key == firstContext.Key ? throw new ResourceReplacementRejectedException("Local rejection.") : throw new InvalidDataException("Unexpected preparation failure."));

        Assert.Throws<InvalidDataException>(() => service.Prepare(resolver));

        Assert.Equal(0, reports);
        Assert.Equal(0, service.Generation);
        Assert.Same(firstOriginal, first.Value.Image);
        Assert.Same(secondOriginal, second.Value.Image);
    }

    [Fact]
    public void OriginalSourceFailureAfterRejectionAbortsAndPreservesPreviouslyValidReplacement()
    {
        using InMemoryResourceSource source = Source();
        ResourceService service = new(source);
        PixelImage previous = new(4, 4);
        service.Prepare(new Resolver(_ => Replacement(previous))).Commit();
        AssetHandle<TextureValue> handle = service.GetTexture(Context("texture", () => throw new IOException("Original source unavailable.")));
        long generation = service.Generation;
        int reports = 0;
        service.ReplacementFailed += (_, _) => reports++;

        Assert.Throws<IOException>(() => service.Prepare(Rejected()));

        Assert.Same(previous, handle.Value.Image);
        Assert.Equal(generation, service.Generation);
        Assert.Equal(0, reports);
    }

    [Fact]
    public void TransformFailureRemainsAtomicEvenWhenItUsesTheRejectionExceptionType()
    {
        using InMemoryResourceSource source = Source();
        ResourceService service = new(source);
        PixelImage original = new(2, 2);
        TextureBuildContext context = Context("texture", () => original);
        service.GetTexture(context);
        bool broken = false;
        AssetHandle<TextureValue> transformed = service.GetTransformedTexture(new(new(new("test/source.tim"), "derived"), [context.Key], dependencies => broken ? throw new ResourceReplacementRejectedException("Transform cannot be discarded independently.") : dependencies[0]));
        int reports = 0;
        service.ReplacementFailed += (_, _) => reports++;
        broken = true;

        Assert.Throws<ResourceReplacementRejectedException>(() => service.Prepare(Rejected()));

        Assert.Equal(0, reports);
        Assert.Equal(0, service.Generation);
        Assert.Same(original, transformed.Value.Image);
    }

    private static InMemoryResourceSource Source() => new(new("memory", GameDataEdition.Classic, ["test-layout"]));

    private static TextureBuildContext Context(string name, Func<PixelImage> original) => new(new(new("test/source.tim"), name), new("memory", GameDataEdition.Classic, ["test-layout"]), 1, "test-layout", 2, 2, original);

    private static TextureReplacement Replacement(PixelImage image) => new(image, "selected", 2, 2);

    private static Resolver Rejected() => new(_ => throw new ResourceReplacementRejectedException("Selected replacement was rejected."));

    private sealed class Resolver(Func<TextureBuildContext, TextureReplacement?> resolve) : ITextureReplacementResolver
    {
        public TextureReplacement? Resolve(TextureBuildContext context) => resolve(context);
    }
}
