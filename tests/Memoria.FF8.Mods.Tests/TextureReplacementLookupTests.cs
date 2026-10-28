using System.Runtime.CompilerServices;
using Albeoris.Games.FF8.Imaging;
using Memoria.FF8.Resources;
using Memoria.FF8.Resources.Localization;

namespace Memoria.FF8.Mods.Tests;

public sealed class TextureReplacementLookupTests
{
    [Fact]
    public void Negative_lookup_does_not_decode_register_or_change_the_default_eager_path()
    {
        using InMemoryResourceSource source = Source();
        ResourceService service = new(source);
        int decodes = 0;
        PixelImage original = new(2, 2);
        TextureBuildContext context = Context(() =>
        {
            decodes++;
            return original;
        });
        Assert.Null(service.GetTextureReplacement(context, retain: true));
        Assert.Equal(0, decodes);
        Assert.Equal(0, service.ResidentTextureCount);
        service.Prepare(null).Commit();
        Assert.Equal(0, decodes);
        AssetHandle<TextureValue> native = service.GetTexture(context);
        Assert.Equal(1, decodes);
        Assert.Same(original, native.Value.Image);
    }

    [Fact]
    public void Negative_lookup_does_not_keep_the_factory_or_its_captured_source_alive()
    {
        using InMemoryResourceSource source = Source();
        ResourceService service = new(source);
        WeakReference<TextureBuildContext> context = QueryMiss(service);
        GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, true, true);
        GC.WaitForPendingFinalizers();
        GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, true, true);
        Assert.False(context.TryGetTarget(out _));
        Assert.Equal(0, service.ResidentTextureCount);
        GC.KeepAlive(service);
    }

    [Fact]
    public void Positive_lookup_uses_the_existing_stable_handle_without_a_second_resolver_call()
    {
        using InMemoryResourceSource source = Source();
        ResourceService service = new(source);
        int resolves = 0;
        PixelImage replacement = new(4, 4);
        service.Prepare(new Resolver(_ =>
        {
            resolves++;
            return new(replacement, "replacement", 2, 2);
        })).Commit();
        TextureBuildContext context = Context(() => throw new InvalidOperationException("Unused original"));
        AssetHandle<TextureValue> handle = Assert.IsType<AssetHandle<TextureValue>>(service.GetTextureReplacement(context));
        Assert.Same(handle, service.GetTextureReplacement(context));
        Assert.Same(handle, service.GetTexture(context));
        Assert.True(handle.Value.IsReplacement);
        Assert.Same(replacement, handle.Value.Image);
        Assert.Equal(1, resolves);
        Assert.Equal(1, service.ResidentTextureCount);
    }

    [Fact]
    public void Existing_native_handle_is_not_returned_or_retained_as_a_replacement()
    {
        using InMemoryResourceSource source = Source();
        ResourceService service = new(source);
        TextureBuildContext context = Context(() => new(2, 2));
        service.GetTexture(context);
        Assert.Null(service.GetTextureReplacement(context, retain: true));
        service.Release([context.Key]);
        Assert.Equal(0, service.ResidentTextureCount);
    }

    [Fact]
    public void A_catalog_removed_replacement_is_not_returned_or_retained_under_a_matching_revision()
    {
        using InMemoryResourceSource source = Source();
        ResourceService service = new(source);
        service.Prepare(new Resolver(_ => new(new(4, 4), "replacement", 2, 2))).Commit();
        TextureBuildContext context = Context(() => new(2, 2));
        AssetHandle<TextureValue> handle = Assert.IsType<AssetHandle<TextureValue>>(service.GetTextureReplacement(context));
        service.Prepare(null).Commit();
        Assert.False(handle.Value.IsReplacement);
        Assert.Null(service.GetTextureReplacement(context, retain: true));
        service.Release([context.Key]);
        Assert.Equal(0, service.ResidentTextureCount);
    }

    [Fact]
    public void An_unbound_miss_is_resolved_again_after_catalog_reload()
    {
        using InMemoryResourceSource source = Source();
        ResourceService service = new(source);
        TextureBuildContext context = Context(() => throw new InvalidOperationException("Unused original"));
        Assert.Null(service.GetTextureReplacement(context));
        PixelImage replacement = new(4, 4);
        service.Prepare(new Resolver(_ => new(replacement, "new catalog", 2, 2))).Commit();
        AssetHandle<TextureValue> handle = Assert.IsType<AssetHandle<TextureValue>>(service.GetTextureReplacement(context));
        Assert.Same(replacement, handle.Value.Image);
        Assert.Equal(1, service.ResidentTextureCount);
    }

    [Fact]
    public void Resolver_original_access_and_source_priority_are_preserved()
    {
        PixelImage original = new(2, 2);
        int decodes = 0;
        using ReplacementSource source = new();
        ResourceService service = new(source);
        service.Prepare(new Resolver(context =>
        {
            Assert.Same(original, context.Original);
            return null;
        })).Commit();
        TextureBuildContext context = Context(() =>
        {
            decodes++;
            return original;
        });
        TextureValue value = Assert.IsType<AssetHandle<TextureValue>>(service.GetTextureReplacement(context)).Value;
        Assert.True(value.IsReplacement);
        Assert.Same(source.Image, value.Image);
        Assert.Equal(1, source.ResolveCalls);
        Assert.Equal(1, decodes);
    }

    [Fact]
    public void Localization_still_precedes_both_replacement_resolvers()
    {
        using ReplacementSource source = new();
        ResourceService service = new(source);
        TextureBuildContext context = Context(() => throw new InvalidOperationException("Unused original"));
        LocalizationArtworkValue artwork = new(context.Key, 2, 2, 2, 2, [new(1, 2, 3), new(1, 2, 3), new(1, 2, 3), new(1, 2, 3)], new string('a', 64), "localized", "fixture");
        LocalizationCatalog localization = new([], artwork: new Dictionary<TextureKey, LocalizationArtworkValue> { [context.Key] = artwork });
        service.Prepare(new Resolver(_ => throw new InvalidOperationException("Localization must win")), localization: localization).Commit();
        TextureValue value = Assert.IsType<AssetHandle<TextureValue>>(service.GetTextureReplacement(context)).Value;
        Assert.Equal("localized/fixture", value.Provenance);
        Assert.Equal(0, source.ResolveCalls);
    }

    [Fact]
    public void Replacement_failure_is_reported_without_decoding_or_registering_the_unused_fallback()
    {
        using InMemoryResourceSource source = Source();
        ResourceService service = new(source);
        int failures = 0;
        service.Prepare(new Resolver(_ => throw new InvalidDataException("Invalid replacement fixture"))).Commit();
        service.ReplacementFailed += (_, _) => failures++;
        Assert.Null(service.GetTextureReplacement(Context(() => throw new InvalidOperationException("Unused original"))));
        Assert.Equal(1, failures);
        Assert.Equal(0, service.ResidentTextureCount);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference<TextureBuildContext> QueryMiss(ResourceService service)
    {
        byte[] captured = new byte[1024 * 1024];
        TextureBuildContext context = Context(() => new(2, 2, Enumerable.Repeat(new Rgba32(captured[0], 0, 0), 4).ToArray()));
        Assert.Null(service.GetTextureReplacement(context));
        return new(context);
    }

    private static TextureBuildContext Context(Func<PixelImage> original) => new(new(new("fixture/original"), "crop"), new("fixture", GameDataEdition.Classic, []), 1, "fixture", 2, 2, original);
    private static InMemoryResourceSource Source() => new(new("fixture", GameDataEdition.Classic, []));

    private sealed class Resolver(Func<TextureBuildContext, TextureReplacement?> resolve) : ITextureReplacementResolver
    {
        public TextureReplacement? Resolve(TextureBuildContext context) => resolve(context);
    }

    private sealed class ReplacementSource : IResourceSource, ITextureReplacementResolver
    {
        public ResourceSourceDescriptor Descriptor { get; } = new("fixture", GameDataEdition.Classic, []);
        public PixelImage Image { get; } = new(4, 4);
        public int ResolveCalls { get; private set; }
        public ResourceContent? Find(ResourceKey key) => null;
        public TextureReplacement? Resolve(TextureBuildContext context)
        {
            ResolveCalls++;
            return new(Image, "source-replacement", 2, 2);
        }
        public void Dispose()
        {
        }
    }
}
