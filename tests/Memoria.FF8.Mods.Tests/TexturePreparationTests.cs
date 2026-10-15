using Albeoris.Games.FF8.Imaging;
using Memoria.FF8.Resources;

namespace Memoria.FF8.Mods.Tests;

public sealed class TexturePreparationTests
{
    [Fact]
    public void DerivedPreparationReceivesReplacedSourcesAndPublishesImageAndLayoutTogether()
    {
        using InMemoryResourceSource source = Source();
        ResourceService service = new(source);
        TextureBuildContext firstContext = Context("first", 1, new(10, 20, 30));
        TextureBuildContext secondContext = Context("second", 1, new(40, 50, 60));
        AssetHandle<TextureValue> first = service.GetTexture(firstContext);
        AssetHandle<TextureValue> second = service.GetTexture(secondContext);
        TextureTransformContext transform = new(Key("atlas"), [firstContext.Key, secondContext.Key], Pack);
        AssetHandle<TextureValue> atlas = service.GetTransformedTexture(transform);
        TextureValue originalFirst = first.Value;
        TextureValue originalAtlas = atlas.Value;
        List<TextureKey> resolved = [];
        ResourceService.PreparedResources prepared = service.Prepare(new Resolver(context =>
        {
            resolved.Add(context.Key);
            return new(Solid(2, 2, context.Key == firstContext.Key ? new(0, 255, 0) : new(255, 255, 0)), "modded", 1, 1);
        }));
        Assert.Same(originalFirst, first.Value);
        Assert.Same(originalAtlas, atlas.Value);
        Assert.Equal(0, service.Generation);
        TextureValue? observedFirst = null;
        TextureValue? observedAtlas = null;
        using IDisposable subscription = service.Subscribe(_ =>
        {
            observedFirst = first.Value;
            observedAtlas = atlas.Value;
        });
        prepared.Commit();
        Assert.Equal(new[] { firstContext.Key, secondContext.Key }, resolved);
        Assert.DoesNotContain(transform.Key, resolved);
        Assert.Equal(new Rgba32(0, 255, 0), first.Value.Image[0, 0]);
        Assert.Equal(new Rgba32(255, 255, 0), second.Value.Image[0, 0]);
        Assert.Equal((4, 2, 2, 1), (atlas.Value.Image.Width, atlas.Value.Image.Height, atlas.Value.LogicalWidth, atlas.Value.LogicalHeight));
        Assert.Equal(new Rgba32(0, 255, 0), atlas.Value.Image[0, 0]);
        Assert.Equal(new Rgba32(255, 255, 0), atlas.Value.Image[2, 0]);
        Assert.Same(first.Value, observedFirst);
        Assert.Same(atlas.Value, observedAtlas);
        Assert.Equal(first.Generation, atlas.Generation);
        Assert.Same(atlas, service.GetTransformedTexture(transform));
    }

    [Fact]
    public void PackingFailureRetainsEverySourceAndDerivedValueAndGeneration()
    {
        using InMemoryResourceSource source = Source();
        ResourceService service = new(source);
        TextureBuildContext context = Context("source", 1, new(1, 2, 3));
        AssetHandle<TextureValue> original = service.GetTexture(context);
        bool reject = false;
        AssetHandle<TextureValue> atlas = service.GetTransformedTexture(new(Key("atlas"), [context.Key], values => reject ? throw new InvalidDataException("The native packing failed.") : Pack(values)));
        TextureValue oldSource = original.Value;
        TextureValue oldAtlas = atlas.Value;
        reject = true;
        Assert.Throws<InvalidDataException>(() => service.Prepare(new Resolver(_ => new(Solid(2, 2, new(0, 255, 0)), "replacement", 1, 1))));
        Assert.Same(oldSource, original.Value);
        Assert.Same(oldAtlas, atlas.Value);
        Assert.Equal(0, service.Generation);
        Assert.Equal(original.Generation, atlas.Generation);
    }

    [Fact]
    public void DerivedRegistrationAfterPreparationRejectsTheStaleGroup()
    {
        using InMemoryResourceSource source = Source();
        ResourceService service = new(source);
        TextureBuildContext context = Context("source", 1, new(1, 2, 3));
        AssetHandle<TextureValue> original = service.GetTexture(context);
        AssetHandle<TextureValue> atlas = service.GetTransformedTexture(new(Key("atlas"), [context.Key], Pack));
        TextureValue oldAtlas = atlas.Value;
        ResourceService.PreparedResources stale = service.Prepare(new Resolver(_ => new(Solid(2, 2, new(0, 255, 0)), "replacement", 1, 1)));
        service.GetTransformedTexture(new(Key("later-atlas"), [context.Key], Pack));
        Assert.Throws<InvalidOperationException>(() => stale.Commit());
        Assert.Same(oldAtlas, atlas.Value);
        Assert.Equal(new Rgba32(1, 2, 3), original.Value.Image[0, 0]);
        Assert.Equal(0, service.Generation);
    }

    [Fact]
    public void SourceRevisionUpdateRebuildsDerivedValuesBeforePublishing()
    {
        using InMemoryResourceSource source = Source();
        ResourceService service = new(source);
        TextureBuildContext context = Context("source", 1, new(1, 2, 3));
        AssetHandle<TextureValue> original = service.GetTexture(context);
        AssetHandle<TextureValue> atlas = service.GetTransformedTexture(new(Key("atlas"), [context.Key], Pack));
        TextureValue oldAtlas = atlas.Value;
        TextureBuildContext update = Context("source", 2, new(9, 8, 7));
        Assert.Same(original, service.GetTexture(update));
        Assert.Equal(new Rgba32(9, 8, 7), original.Value.Image[0, 0]);
        Assert.Equal(new Rgba32(9, 8, 7), atlas.Value.Image[0, 0]);
        Assert.NotSame(oldAtlas, atlas.Value);
        Assert.Equal(original.Generation, atlas.Generation);
        Assert.Equal(1, service.Generation);
    }

    [Fact]
    public void FailedPackingDuringSourceUpdatePreservesTheOldSourceContextAndPublication()
    {
        using InMemoryResourceSource source = Source();
        ResourceService service = new(source);
        TextureBuildContext context = Context("source", 1, new(1, 2, 3));
        AssetHandle<TextureValue> original = service.GetTexture(context);
        bool reject = false;
        AssetHandle<TextureValue> atlas = service.GetTransformedTexture(new(Key("atlas"), [context.Key], values => reject ? throw new InvalidDataException("Native packing rejected the new source.") : Pack(values)));
        TextureValue oldSource = original.Value;
        TextureValue oldAtlas = atlas.Value;
        reject = true;
        Assert.Throws<InvalidDataException>(() => service.GetTexture(Context("source", 2, new(9, 8, 7))));
        Assert.Same(oldSource, original.Value);
        Assert.Same(oldAtlas, atlas.Value);
        Assert.Equal(0, service.Generation);
        reject = false;
        service.Prepare(null).Commit();
        Assert.Equal(new Rgba32(1, 2, 3), original.Value.Image[0, 0]);
        Assert.Equal(new Rgba32(1, 2, 3), atlas.Value.Image[0, 0]);
    }

    [Fact]
    public void SharedSourceSurvivesUntilEveryDerivedOwnerHasReleasedIt()
    {
        using InMemoryResourceSource source = Source();
        ResourceService service = new(source);
        TextureBuildContext context = Context("source", 1, new(1, 2, 3));
        AssetHandle<TextureValue> original = service.GetTexture(context);
        TextureKey firstKey = Key("first-atlas");
        TextureKey secondKey = Key("second-atlas");
        service.GetTransformedTexture(new(firstKey, [context.Key], Pack));
        AssetHandle<TextureValue> second = service.GetTransformedTexture(new(secondKey, [context.Key], Pack));
        service.Release([context.Key]);
        Assert.Equal(3, service.ResidentTextureCount);
        service.Release([firstKey]);
        Assert.Equal(2, service.ResidentTextureCount);
        service.Prepare(new Resolver(_ => new(Solid(2, 2, new(0, 255, 0)), "replacement", 1, 1))).Commit();
        Assert.Equal(new Rgba32(0, 255, 0), second.Value.Image[0, 0]);
        service.Release([secondKey]);
        Assert.Equal(0, service.ResidentTextureCount);
        Assert.Equal(new Rgba32(0, 255, 0), original.Value.Image[0, 0]);
        Assert.Equal(new Rgba32(0, 255, 0), second.Value.Image[0, 0]);
        service.Prepare(null).Commit();
        Assert.Equal(new Rgba32(0, 255, 0), second.Value.Image[0, 0]);
    }

    [Fact]
    public void RetainedNestedTransformReleasesItsDependencyGraphOnlyOnFinalRelease()
    {
        using InMemoryResourceSource source = Source();
        ResourceService service = new(source);
        TextureBuildContext context = Context("source", 1, new(1, 2, 3));
        service.GetTexture(context);
        TextureTransformContext middle = new(Key("middle"), [context.Key], Pack);
        TextureTransformContext outer = new(Key("outer"), [middle.Key], Pack);
        service.GetTransformedTexture(middle);
        service.GetTransformedTexture(outer);
        service.GetTransformedTexture(outer, retain: true);
        service.Release([context.Key, middle.Key, outer.Key]);
        Assert.Equal(3, service.ResidentTextureCount);
        service.Prepare(null).Commit();
        service.Release([outer.Key]);
        Assert.Equal(0, service.ResidentTextureCount);
    }

    [Fact]
    public void SubscriberCannotReleaseThePublishedDependencyGroupBeforeLaterSubscribersObserveIt()
    {
        using InMemoryResourceSource source = Source();
        ResourceService service = new(source);
        TextureBuildContext context = Context("source", 1, new(1, 2, 3));
        service.GetTexture(context);
        AssetHandle<TextureValue> atlas = service.GetTransformedTexture(new(Key("atlas"), [context.Key], Pack));
        using IDisposable first = service.Subscribe(_ => service.Release([context.Key, Key("atlas")]));
        int observedCount = 0;
        using IDisposable second = service.Subscribe(_ => observedCount = service.ResidentTextureCount);
        service.Prepare(new Resolver(_ => new(Solid(2, 2, new(0, 255, 0)), "replacement", 1, 1))).Commit();
        Assert.Equal(2, observedCount);
        Assert.Equal(2, service.ResidentTextureCount);
        Assert.Equal(new Rgba32(0, 255, 0), atlas.Value.Image[0, 0]);
    }

    [Fact]
    public void SourceUpdateCannotOverwriteNewerGraphChangesMadeDuringPacking()
    {
        using InMemoryResourceSource source = Source();
        ResourceService service = new(source);
        TextureBuildContext context = Context("source", 1, new(1, 2, 3));
        AssetHandle<TextureValue> original = service.GetTexture(context);
        bool mutate = false;
        AssetHandle<TextureValue> atlas = service.GetTransformedTexture(new(Key("atlas"), [context.Key], values =>
        {
            if (mutate)
            {
                mutate = false;
                service.GetTexture(Context("extra", 1, new(4, 5, 6)));
            }
            return Pack(values);
        }));
        TextureValue oldAtlas = atlas.Value;
        mutate = true;
        Assert.Throws<InvalidOperationException>(() => service.GetTexture(Context("source", 2, new(255, 0, 0))));
        Assert.Equal(new Rgba32(1, 2, 3), original.Value.Image[0, 0]);
        Assert.Same(oldAtlas, atlas.Value);
        Assert.Equal(3, service.ResidentTextureCount);
    }

    private static InMemoryResourceSource Source()
    {
        return new(new("memory", GameDataEdition.Classic, ["test-source"]));
    }

    private static TextureKey Key(string name)
    {
        return new(new("test/" + name), string.Empty);
    }

    private static TextureBuildContext Context(string name, long revision, Rgba32 color)
    {
        return new(Key(name), new("memory", GameDataEdition.Classic, ["test-source"]), revision, "test-source", 1, 1, () => Solid(1, 1, color));
    }

    private static PixelImage Solid(int width, int height, Rgba32 color)
    {
        return new(width, height, Enumerable.Repeat(color, width * height).ToArray());
    }

    private static TextureValue Pack(IReadOnlyList<TextureValue> inputs)
    {
        PixelImage image = new(inputs.Sum(input => input.Image.Width), inputs.Max(input => input.Image.Height));
        int offset = 0;
        foreach (TextureValue input in inputs)
        {
            for (int y = 0; y < input.Image.Height; y++)
            {
                for (int x = 0; x < input.Image.Width; x++)
                {
                    image[offset + x, y] = input.Image[x, y];
                }
            }
            offset += input.Image.Width;
        }
        return new(image, inputs.Sum(input => input.LogicalWidth), inputs.Max(input => input.LogicalHeight), "native preparation");
    }

    private sealed class Resolver(Func<TextureBuildContext, TextureReplacement?> resolve) : ITextureReplacementResolver
    {
        public TextureReplacement? Resolve(TextureBuildContext context)
        {
            return resolve(context);
        }
    }
}
