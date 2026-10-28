using Albeoris.Games.FF8.Imaging;
using Memoria.FF8.Resources;

namespace Memoria.FF8.Mods.Tests;

public sealed class ResourceServiceTests
{
    [Fact]
    public void CatalogGenerationTracksPreparedCatalogsWithoutFollowingNativeContextsOrResidency()
    {
        using InMemoryResourceSource source = Source();
        ResourceService service = new(source);
        service.Prepare(null).Commit();
        long catalog = service.CatalogGeneration;
        long generation = service.Generation;
        TextureBuildContext first = Context("first", 1, () => new(2, 2));
        service.GetTexture(first);
        service.GetTexture(Context("second", 1, () => new(2, 2)));
        service.GetTexture(Context("first", 2, () => new(2, 2)));

        Assert.True(service.Generation > generation);
        Assert.Equal(catalog, service.CatalogGeneration);
        service.Release([first.Key, Context("second", 1, () => new(2, 2)).Key]);
        Assert.Equal(0, service.ResidentTextureCount);
        Assert.Equal(catalog, service.CatalogGeneration);

        service.Prepare(new Resolver(_ => null)).Commit();
        Assert.Equal(++catalog, service.CatalogGeneration);
        EncodedResource encoded = new(new("test/unconsumed.dat"), "raw", 1, "fixture", [1, 2]);
        service.Prepare(null, encodedResources: [encoded]).Commit();
        Assert.Equal(++catalog, service.CatalogGeneration);
        service.PrepareLocalization(Memoria.FF8.Resources.Localization.LocalizationCatalog.Empty).Commit();
        Assert.Equal(++catalog, service.CatalogGeneration);
    }

    [Fact]
    public void PathlessSourceOwnsEncodedBytesAndRetainsIndependentRevisions()
    {
        using InMemoryResourceSource source = new(new("memory", GameDataEdition.Remastered, ["native-hd"]));
        ResourceKey key = new("world/texl.obj");
        byte[] original = [1, 2, 3];
        source.Set(key, original, "tim");
        original[0] = 99;
        ResourceContent first = Assert.IsType<ResourceContent>(source.Find(key));
        source.Set(key, [4, 5], "tim");
        ResourceContent second = Assert.IsType<ResourceContent>(source.Find(key));
        ResourceService service = new(source);
        Assert.Equal(new byte[] { 4, 5 }, service.Read(key));
        Assert.True(second.Revision > first.Revision);
        using Stream retained = first.OpenRead();
        Assert.Equal(1, retained.ReadByte());
        Assert.Equal(GameDataEdition.Remastered, service.Source.Edition);
        Assert.Throws<FileNotFoundException>(() => service.Read(new("unknown/missing.bin")));
    }

    [Theory]
    [InlineData("../world/texl.obj")]
    [InlineData("world/../texl.obj")]
    [InlineData("world//texl.obj")]
    [InlineData("/world/texl.obj")]
    [InlineData("C:/game/texl.obj")]
    [InlineData("world/./texl.obj")]
    [InlineData("world/texl.obj/")]
    [InlineData("")]
    public void MalformedLogicalIdentityFailsBeforeProviderAccess(string path)
    {
        Assert.Throws<ArgumentException>(() => new ResourceKey(path));
    }

    [Fact]
    public void LogicalIdentityNormalizesCaseAndSeparators()
    {
        Assert.Equal(new ResourceKey("world/texl.obj"), new ResourceKey("WORLD\\TEXL.OBJ"));
    }

    [Fact]
    public void NoModPreservesExactOriginalImageAndStableHandle()
    {
        using InMemoryResourceSource source = Source();
        ResourceService service = new(source);
        PixelImage original = new(2, 2);
        int decodes = 0;
        TextureBuildContext context = Context("first", 1, () =>
        {
            decodes++;
            return original;
        });
        AssetHandle<TextureValue> first = service.GetTexture(context);
        Assert.Same(original, first.Value.Image);
        Assert.Same(first, service.GetTexture(context));
        service.Prepare(null).Commit();
        Assert.Same(original, first.Value.Image);
        Assert.Equal(1, decodes);
    }

    [Fact]
    public void GroupPreparationFailureRetainsEveryPublishedBindingAndGeneration()
    {
        using InMemoryResourceSource source = Source();
        ResourceService service = new(source);
        PixelImage firstOriginal = new(2, 2);
        PixelImage secondOriginal = new(2, 2);
        AssetHandle<TextureValue> first = service.GetTexture(Context("first", 1, () => firstOriginal));
        AssetHandle<TextureValue> second = service.GetTexture(Context("second", 1, () => secondOriginal));
        int attempts = 0;
        Resolver broken = new(context =>
        {
            attempts++;
            if (attempts == 2)
            {
                throw new InvalidDataException("Torn replacement image.");
            }
            return new TextureReplacement(new(4, 4), "candidate", 2, 2);
        });
        Assert.Throws<InvalidDataException>(() => service.Prepare(broken));
        Assert.Same(firstOriginal, first.Value.Image);
        Assert.Same(secondOriginal, second.Value.Image);
        Assert.Equal(0, service.Generation);
    }

    [Fact]
    public void SuccessfulGroupPublishesBeforeNotificationsAndStableHandleFallsBack()
    {
        using InMemoryResourceSource source = Source();
        ResourceService service = new(source);
        PixelImage original = new(2, 2);
        PixelImage replacement = new(4, 4);
        TextureBuildContext firstContext = Context("first", 1, () => original);
        TextureBuildContext secondContext = Context("second", 1, () => original);
        AssetHandle<TextureValue> first = service.GetTexture(firstContext);
        AssetHandle<TextureValue> second = service.GetTexture(secondContext);
        bool observedWholeGroup = false;
        using IDisposable subscription = service.Subscribe(change => observedWholeGroup = ReferenceEquals(replacement, first.Value.Image) && ReferenceEquals(replacement, second.Value.Image) && service.Generation == change.NewGeneration && change.Textures.Length == 2);
        service.Prepare(new Resolver(_ => new(replacement, "mod", 2, 2))).Commit();
        Assert.True(observedWholeGroup);
        Assert.Same(first, service.GetTexture(firstContext));
        service.Prepare(null).Commit();
        Assert.Same(original, first.Value.Image);
        Assert.Same(original, second.Value.Image);
    }

    [Fact]
    public void SubscribersAreIsolatedAndDisposedSubscriptionStopsDelivery()
    {
        using InMemoryResourceSource source = Source();
        ResourceService service = new(source);
        service.GetTexture(Context("first", 1, () => new(2, 2)));
        int callbacks = 0;
        using IDisposable broken = service.Subscribe(_ => throw new InvalidOperationException("Consumer failed."));
        IDisposable healthy = service.Subscribe(_ => callbacks++);
        service.Prepare(null).Commit();
        Assert.Equal(1, callbacks);
        healthy.Dispose();
        healthy.Dispose();
        service.Prepare(null).Commit();
        Assert.Equal(1, callbacks);
        Assert.Equal(2, service.Generation);
    }

    [Fact]
    public void SourceRevisionChangeRejectsPreviouslyPreparedGroup()
    {
        using InMemoryResourceSource source = Source();
        ResourceService service = new(source);
        AssetHandle<TextureValue> handle = service.GetTexture(Context("first", 1, () => new(2, 2)));
        ResourceService.PreparedResources stale = service.Prepare(new Resolver(_ => new(new(4, 4), "old catalog", 2, 2)));
        PixelImage newerSource = new(2, 2);
        Assert.Same(handle, service.GetTexture(Context("first", 2, () => newerSource)));
        Assert.Throws<InvalidOperationException>(() => stale.Commit());
        Assert.Same(newerSource, handle.Value.Image);
    }

    [Fact]
    public void ResidentSetChangesRejectPreviouslyPreparedGroup()
    {
        using InMemoryResourceSource source = Source();
        ResourceService service = new(source);
        service.GetTexture(Context("first", 1, () => new(2, 2)));
        ResourceService.PreparedResources stale = service.Prepare(null);
        service.GetTexture(Context("second", 1, () => new(2, 2)));
        Assert.Throws<InvalidOperationException>(() => stale.Commit());
    }

    [Fact]
    public void ReleasedHandleRetainsLastValueWhenIdentityIsRegisteredAgain()
    {
        using InMemoryResourceSource source = Source();
        ResourceService service = new(source);
        PixelImage original = new(2, 2);
        TextureBuildContext context = Context("first", 1, () => original);
        AssetHandle<TextureValue> retired = service.GetTexture(context);
        service.Release([context.Key]);
        Assert.Equal(0, service.ResidentTextureCount);
        PixelImage next = new(2, 2);
        AssetHandle<TextureValue> current = service.GetTexture(Context("first", 2, () => next));
        Assert.NotSame(retired, current);
        Assert.Same(original, retired.Value.Image);
        Assert.Same(next, current.Value.Image);
    }

    [Fact]
    public void OneOwnerReleaseDoesNotUnloadAnotherOwnersBinding()
    {
        using InMemoryResourceSource source = Source();
        ResourceService service = new(source);
        TextureBuildContext context = Context("shared", 1, () => new(2, 2));
        AssetHandle<TextureValue> first = service.GetTexture(context, retain: true);
        Assert.Same(first, service.GetTexture(context, retain: true));
        service.Release([context.Key]);
        Assert.Equal(1, service.ResidentTextureCount);
        PixelImage replacement = new(4, 4);
        service.Prepare(new Resolver(_ => new(replacement, "remaining owner", 2, 2))).Commit();
        Assert.Same(replacement, first.Value.Image);
        service.Release([context.Key]);
        Assert.Equal(0, service.ResidentTextureCount);
        service.Prepare(null).Commit();
        Assert.Same(replacement, first.Value.Image);
    }

    [Fact]
    public void SourceMutationDuringPreparationCannotPublishTheStaleBinding()
    {
        using InMemoryResourceSource source = Source();
        ResourceService service = new(source);
        AssetHandle<TextureValue> handle = service.GetTexture(Context("first", 1, () => new(2, 2)));
        PixelImage newerSource = new(2, 2);
        Resolver changing = new(_ =>
        {
            service.GetTexture(Context("first", 2, () => newerSource));
            return new(new(4, 4), "stale output", 2, 2);
        });
        Assert.Throws<InvalidOperationException>(() => service.Prepare(changing).Commit());
        Assert.Same(newerSource, handle.Value.Image);
    }

    private static InMemoryResourceSource Source() => new(new("memory", GameDataEdition.Classic, ["test-layout"]));

    private static TextureBuildContext Context(string name, long revision, Func<PixelImage> original) => new(new(new("test/source.tim"), name), new("memory", GameDataEdition.Classic, ["test-layout"]), revision, "test-layout", 2, 2, original);

    private sealed class Resolver(Func<TextureBuildContext, TextureReplacement?> resolve) : ITextureReplacementResolver
    {
        public TextureReplacement? Resolve(TextureBuildContext context) => resolve(context);
    }
}
