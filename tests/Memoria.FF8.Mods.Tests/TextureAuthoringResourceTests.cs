using System.Text;
using Albeoris.Games.FF8.Imaging;
using Memoria.FF8.Resources;

namespace Memoria.FF8.Mods.Tests;

public sealed class TextureAuthoringResourceTests
{
    private static readonly ResourceSourceDescriptor Source = new("synthetic", GameDataEdition.Classic, ["image"]);

    [Fact]
    public void PresentedDerivedImagesExpandToAuthoredSourcesWithoutIncludingOtherResidentTextures()
    {
        using InMemoryResourceSource source = new(Source);
        ResourceService resources = new(source);
        TextureBuildContext first = Context("first", Image(10));
        TextureBuildContext second = Context("second", Image(20));
        TextureBuildContext unrelated = Context("unrelated", Image(30));
        _ = resources.GetTexture(first);
        _ = resources.GetTexture(second);
        _ = resources.GetTexture(unrelated);
        TextureKey derived = new(new("synthetic/atlas"), "packed");
        PixelImage packed = Image(40);
        AssetHandle<TextureValue> handle = resources.GetTransformedTexture(new(derived, [first.Key, second.Key], values => new(packed, 1, 1, "derived")));
        Assert.Equal(derived, handle.TextureKey);
        IReadOnlyList<TextureSourceSnapshot> snapshots = resources.CaptureTextureSourcesForImages([packed]);
        Assert.Equal(new[] { first.Key, second.Key }, snapshots.Select(snapshot => snapshot.Context.Key));
        Assert.All(snapshots, snapshot => Assert.Equal(resources.Generation, snapshot.Generation));
        Assert.Same(first.Original, snapshots[0].Original);
        Assert.DoesNotContain(snapshots, snapshot => snapshot.Context.Key == unrelated.Key);
        Assert.Equal(3, resources.CaptureTextureSources([derived, unrelated.Key]).Count);
    }

    [Fact]
    public void PresentedMipVariantAndAnimationImagesRetainTheirRealAuthoredSource()
    {
        using InMemoryResourceSource source = new(Source);
        ResourceService resources = new(source);
        TextureBuildContext context = Context("animated", Image(10));
        _ = resources.GetTexture(context);
        PixelImage image = Image(20);
        PixelImage mip = Image(30);
        PixelImage variant = Image(40);
        PixelImage frame = Image(50);
        TextureReplacement replacement = new(image, "authored", 1, 1, [image, mip], new Dictionary<string, PixelImage> { ["eyes-closed"] = variant }) { Animation = new([image, frame], 50) };
        resources.Prepare(new Resolver(context.Key, replacement)).Commit();
        foreach (PixelImage presented in new[] { image, mip, variant, frame })
        {
            TextureSourceSnapshot snapshot = Assert.Single(resources.CaptureTextureSourcesForImages([presented]));
            Assert.Equal(context.Key, snapshot.Context.Key);
            Assert.Same(image, snapshot.Resolved.Image);
        }
    }

    [Fact]
    public void PreparedGenerationRebuildsOnlyChangedSourceDependencyClosureAndKeepsStableHandles()
    {
        using InMemoryResourceSource source = new(Source);
        ResourceService resources = new(source);
        TextureBuildContext first = Context("first", Image(10));
        TextureBuildContext second = Context("second", Image(20));
        AssetHandle<TextureValue> firstHandle = resources.GetTexture(first);
        _ = resources.GetTexture(second);
        TextureKey firstDerived = new(new("synthetic/atlas"), "first");
        TextureKey secondDerived = new(new("synthetic/atlas"), "second");
        TextureKey nested = new(new("synthetic/atlas"), "nested");
        int firstBuilds = 0;
        int secondBuilds = 0;
        int nestedBuilds = 0;
        AssetHandle<TextureValue> firstAtlas = resources.GetTransformedTexture(new(firstDerived, [first.Key], values =>
        {
            firstBuilds++;
            return values[0] with { Provenance = "first atlas" };
        }));
        _ = resources.GetTransformedTexture(new(secondDerived, [second.Key], values =>
        {
            secondBuilds++;
            return values[0] with { Provenance = "second atlas" };
        }));
        _ = resources.GetTransformedTexture(new(nested, [firstDerived], values =>
        {
            nestedBuilds++;
            return values[0] with { Provenance = "nested atlas" };
        }));
        PixelImage changed = Image(99);
        Resolver resolver = new(first.Key, new(changed, "authored", 1, 1));
        ResourceService.PreparedResources prepared = resources.Prepare(resolver);
        Assert.Equal(new HashSet<TextureKey> { first.Key, firstDerived, nested }, prepared.TextureValues.Keys.ToHashSet());
        Assert.Equal((2, 1, 2), (firstBuilds, secondBuilds, nestedBuilds));
        Assert.NotSame(changed, firstHandle.Value.Image);
        prepared.Commit();
        Assert.Same(changed, firstHandle.Value.Image);
        Assert.Same(changed, firstAtlas.Value.Image);
        ResourceService.PreparedResources unchanged = resources.Prepare(resolver);
        Assert.Empty(unchanged.TextureValues);
        Assert.Equal((2, 1, 2), (firstBuilds, secondBuilds, nestedBuilds));
    }

    [Theory]
    [InlineData("CON", "a/b", "")]
    [InlineData("owner.tex", "a%2Fb", "A:variant")]
    [InlineData("owner.tex", "material/æ—¥æœ¬èªž", "../variant")]
    public void CanonicalIdentityRoundTripsReservedAndUnicodeComponentsWithoutFilesystemCollisions(string owner, string member, string variant)
    {
        TextureTarget target = new("ff8", "synthetic/" + owner.ToLowerInvariant(), member, 7, variant);
        string path = target.CanonicalPath();
        Assert.DoesNotContain(':', path);
        Assert.DoesNotContain("../", path);
        Assert.True(TextureTarget.TryParsePath(path, out TextureTarget? parsed));
        Assert.Equal(target, parsed);
        Assert.False(TextureTarget.TryParsePath(path.ToUpperInvariant(), out _));
        Assert.NotEqual(path, (target with { Subresource = member + "/" }).CanonicalPath());
    }

    [Theory]
    [InlineData("textures/ff8/../bad.png")]
    [InlineData("C:/textures/bad.png")]
    [InlineData("/textures/ff8/bad.png")]
    [InlineData("textures\\ff8\\bad.png")]
    public void UnsafeCanonicalClaimsAreRejected(string path)
    {
        Assert.False(TextureTarget.TryParsePath(path, out _));
    }

    [Theory]
    [InlineData("{\"schemaVersion\":1,\"schemaVersion\":1}")]
    [InlineData("{\"schemaVersion\":2,\"target\":{\"game\":\"ff8\",\"owner\":\"synthetic/item\",\"subresource\":\"image\"},\"contract\":{\"layout\":\"image\",\"logicalWidth\":1,\"logicalHeight\":1}}")]
    [InlineData("{\"schemaVersion\":1,\"target\":{\"game\":\"ff8\",\"owner\":\"Synthetic/item\",\"subresource\":\"image\"},\"contract\":{\"layout\":\"image\",\"logicalWidth\":1,\"logicalHeight\":1}}")]
    public void MalformedVersionDuplicateAndNoncanonicalMetadataCannotSilentlyFallBack(string json)
    {
        Assert.Throws<InvalidDataException>(() => TextureExchangeMetadata.Parse(Encoding.UTF8.GetBytes(json)));
    }

    [Fact]
    public void EditorAlphaRestoresScaledNativeStpWhileRetainingNewOpacityAndTransparency()
    {
        PixelImage original = new(2, 1, [new(1, 2, 3, 128), new(4, 5, 6, 0)]);
        PixelImage editable = TexturePixelExchange.ToEditable(original);
        Assert.Equal(255, editable[0, 0].A);
        PixelImage edited = new(4, 2, [new(1, 2, 3, 255), new(1, 2, 3, 64), new(4, 5, 6, 255), new(4, 5, 6, 0), new(1, 2, 3, 255), new(1, 2, 3, 255), new(4, 5, 6, 255), new(4, 5, 6, 0)]);
        PixelImage native = TexturePixelExchange.RestoreNativeFlags(edited, 2, 1, TexturePixelExchange.CaptureNativeFlags(original));
        Assert.Equal(new byte[] { 128, 64, 255, 0, 128, 128, 255, 0 }, native.Pixels.Select(pixel => pixel.A));
        Assert.Equal(TexturePixelExchange.Fingerprint(editable), TexturePixelExchange.Fingerprint(TexturePixelExchange.ToEditable(original)));
        Assert.NotEqual(TexturePixelExchange.Fingerprint(original), TexturePixelExchange.Fingerprint(editable));
        TextureBuildContext desktop = new(Context("desktop", original).Key, Source with { Edition = GameDataEdition.Remastered }, 1, "classic-battle", 2, 1, () => original);
        Assert.False(TexturePixelExchange.UsesNativeFlags(desktop));
    }

    [Fact]
    public void IncrementalPreparationYieldsBetweenSourcesAndTransformsAndPublishesOnlyAtCommit()
    {
        using InMemoryResourceSource source = new(Source);
        ResourceService resources = new(source);
        TextureBuildContext first = Context("first", Image(10));
        AssetHandle<TextureValue> handle = resources.GetTexture(first);
        _ = resources.GetTexture(Context("second", Image(20)));
        int builds = 0;
        AssetHandle<TextureValue> atlas = resources.GetTransformedTexture(new(new(new("synthetic/atlas"), "image"), [first.Key], values =>
        {
            builds++;
            return values[0];
        }));
        long generation = resources.Generation;
        PixelImage changed = Image(99);
        using ResourceService.PreparationJob job = resources.BeginPrepare(new Resolver(first.Key, new(changed, "edited", 1, 1)));
        Assert.False(job.Advance(TimeSpan.Zero, cancellationToken: TestContext.Current.CancellationToken));
        Assert.Equal(1, job.CompletedUnits);
        Assert.Null(job.Prepared);
        Assert.Equal(1, builds);
        Assert.Equal(generation, resources.Generation);
        Assert.NotSame(changed, handle.Value.Image);
        Assert.False(job.Advance(TimeSpan.Zero, cancellationToken: TestContext.Current.CancellationToken));
        Assert.Equal(1, builds);
        Assert.False(job.Advance(TimeSpan.Zero, cancellationToken: TestContext.Current.CancellationToken));
        Assert.Equal(2, builds);
        Assert.NotSame(changed, atlas.Value.Image);
        Assert.True(job.Advance(TimeSpan.Zero, cancellationToken: TestContext.Current.CancellationToken));
        Assert.Equal(3, job.CompletedUnits);
        Assert.Equal(generation, resources.Generation);
        job.Prepared!.Commit();
        Assert.Same(changed, handle.Value.Image);
        Assert.Same(changed, atlas.Value.Image);
        Assert.Equal(generation + 1, resources.Generation);
    }

    [Fact]
    public void IncrementalCancellationAndChangedRegistrationCannotPublishPartialCandidates()
    {
        using InMemoryResourceSource source = new(Source);
        ResourceService resources = new(source);
        TextureBuildContext first = Context("first", Image(10));
        AssetHandle<TextureValue> handle = resources.GetTexture(first);
        PixelImage original = handle.Value.Image;
        long generation = resources.Generation;
        Resolver resolver = new(first.Key, new(Image(99), "edited", 1, 1));
        using ResourceService.PreparationJob cancelled = resources.BeginPrepare(resolver);
        Assert.False(cancelled.Advance(TimeSpan.Zero, cancellationToken: TestContext.Current.CancellationToken));
        using CancellationTokenSource cancellation = new();
        cancellation.Cancel();
        Assert.Throws<OperationCanceledException>(() => cancelled.Advance(TimeSpan.Zero, cancellationToken: cancellation.Token));
        Assert.Null(cancelled.Prepared);
        Assert.Same(original, handle.Value.Image);
        Assert.Equal(generation, resources.Generation);
        using ResourceService.PreparationJob stale = resources.BeginPrepare(resolver);
        Assert.False(stale.Advance(TimeSpan.Zero, cancellationToken: TestContext.Current.CancellationToken));
        _ = resources.GetTexture(Context("later", Image(20)));
        Assert.True(stale.IsStale);
        Assert.Throws<InvalidOperationException>(() => stale.Advance(TimeSpan.Zero, cancellationToken: TestContext.Current.CancellationToken));
        Assert.Null(stale.Prepared);
        Assert.Same(original, handle.Value.Image);
        Assert.Equal(generation, resources.Generation);
    }

    [Fact]
    public void IncrementalTransformFailureLeavesTheWholePublishedGraphUnchanged()
    {
        using InMemoryResourceSource source = new(Source);
        ResourceService resources = new(source);
        TextureBuildContext first = Context("first", Image(10));
        AssetHandle<TextureValue> handle = resources.GetTexture(first);
        bool fail = false;
        AssetHandle<TextureValue> atlas = resources.GetTransformedTexture(new(new(new("synthetic/atlas"), "image"), [first.Key], values => fail ? throw new InvalidDataException("invalid transform") : values[0]));
        PixelImage original = handle.Value.Image;
        long generation = resources.Generation;
        fail = true;
        using ResourceService.PreparationJob job = resources.BeginPrepare(new Resolver(first.Key, new(Image(99), "edited", 1, 1)));
        Assert.False(job.Advance(TimeSpan.Zero, cancellationToken: TestContext.Current.CancellationToken));
        Assert.Throws<InvalidDataException>(() => job.Advance(TimeSpan.Zero, cancellationToken: TestContext.Current.CancellationToken));
        Assert.False(job.IsStale);
        Assert.Null(job.Prepared);
        Assert.Same(original, handle.Value.Image);
        Assert.Same(original, atlas.Value.Image);
        Assert.Equal(generation, resources.Generation);
    }

    private static TextureBuildContext Context(string name, PixelImage image) => new(new(new("synthetic/" + name), "image"), Source, 1, "image", image.Width, image.Height, () => image);
    private static PixelImage Image(byte red) => new(1, 1, [new(red, 0, 0, 255)]);
    private sealed class Resolver(TextureKey target, TextureReplacement replacement) : ITextureReplacementResolver
    {
        public TextureReplacement? Resolve(TextureBuildContext context) => context.Key == target ? replacement : null;
    }
}
