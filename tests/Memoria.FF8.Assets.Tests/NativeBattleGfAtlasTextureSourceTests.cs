using System.Collections.Immutable;
using Albeoris.Games.FF8.Imaging;
using Memoria.FF8.Resources;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeBattleGfAtlasTextureSourceTests
{
    [Fact]
    public void PrimitiveVariantsReachThePrimaryAdapterAndKeepDistinctResourceContexts()
    {
        using AtlasSource source = new();
        ResourceService resources = new(source);
        RecordingResolver resolver = new();
        resources.Prepare(resolver).Commit();
        NativeBattleEffectTextureState state = NativeBattleEffectTextureState.FromColdProcess() with { TextureRegistrations = [] };
        using NativeBattleEffectTextureSource textures = new(resources);
        TexturePrimitiveSample upper = new(0.25f, 0.25f);
        TexturePrimitiveSample lower = new(0.25f, 0.75f);

        textures.Apply(new(16, 16), state, 29, 0, 0, 0, 16, 16, null, null, null, upper);
        textures.Apply(new(16, 16), state, 29, 0, 0, 0, 16, 16, null, null, null, lower);

        Assert.Equal(new TexturePrimitiveSample?[] { upper, lower }, source.Samples);
        Assert.Equal(2, resolver.Contexts.Count);
        Assert.NotEqual(resolver.Contexts[0].Key, resolver.Contexts[1].Key);
        Assert.Equal(textures.GetPrimitiveSampleKey(upper), textures.GetPrimitiveSampleKey(new(0.9f, 0.4f)));
        Assert.NotEqual(textures.GetPrimitiveSampleKey(upper), textures.GetPrimitiveSampleKey(lower));
    }

    [Fact]
    public void PrimarySourceAtlasResolvesWholeWindowBeforeRawAndRegionFallbacks()
    {
        using AtlasSource source = new();
        ResourceService resources = new(source);
        RecordingResolver resolver = new();
        resources.Prepare(resolver).Commit();
        NativeBattleEffectTextureState state = NativeBattleEffectTextureState.FromColdProcess() with { TextureRegistrations = [] };
        using NativeBattleEffectTextureSource textures = new(resources);

        PixelImage result = textures.Apply(new(16, 16), state, 29, 0, 32, 128, 16, 16);

        TextureBuildContext context = Assert.Single(resolver.Contexts);
        Assert.Equal("native-battle-gf-atlas", context.Layout);
        Assert.Equal(new TextureSourceAtlasAddress("mag005", 0, 2, 256, 256, 32, 128, 16, 16), context.NativeAtlas);
        Assert.Null(context.NativeUpload);
        Assert.Same(resolver.Replacement.Image, result);
        textures.Dispose();
        Assert.Equal(0, resources.ResidentTextureCount);
    }

    [Fact]
    public void MissingAtlasReplacementContinuesThroughOriginalUploadContract()
    {
        using AtlasSource source = new();
        ResourceService resources = new(source);
        RecordingResolver resolver = new() { ReplaceAtlas = false };
        resources.Prepare(resolver).Commit();
        NativeBattleEffectTextureState state = NativeBattleEffectTextureState.FromColdProcess() with { TextureRegistrations = [] };
        using NativeBattleEffectTextureSource textures = new(resources);
        PixelImage original = new(16, 16);

        Assert.Same(original, textures.Apply(original, state, 29, 0, 0, 0, 16, 16));

        Assert.Equal(new[] { "native-battle-gf-atlas", "native-pc-texture-upload" }, resolver.Contexts.Select(context => context.Layout));
        Assert.NotNull(resolver.Contexts[1].NativeUpload);
    }

    private sealed class AtlasSource : IResourceSource, INativeBattleGfAtlasSource
    {
        public ResourceSourceDescriptor Descriptor { get; } = new("fixture", GameDataEdition.Remastered, ["native-battle-gf-atlas", "demaster-content-upload"]);
        public List<TexturePrimitiveSample?> Samples { get; } = [];

        public ResourceContent? Find(ResourceKey key) => null;

        public TextureSourceAtlasAddress? TryGetGfAtlasAddress(ImmutableArray<NativeBattleTextureRegistration> registrations, ushort page, ushort clut, int x, int y, int width, int height, TexturePrimitiveSample? sample = null)
        {
            Assert.Empty(registrations);
            Samples.Add(sample);
            return new("mag005", 0, 2, 256, 256, x, y, width, height);
        }

        public void Dispose()
        {
        }
    }

    private sealed class RecordingResolver : ITextureReplacementResolver
    {
        public List<TextureBuildContext> Contexts { get; } = [];
        public TextureReplacement Replacement { get; } = new(new(64, 64), "fixture", 16, 16);
        public bool ReplaceAtlas { get; init; } = true;

        public TextureReplacement? Resolve(TextureBuildContext context)
        {
            Contexts.Add(context);
            return ReplaceAtlas && context.NativeAtlas is not null ? Replacement : null;
        }
    }
}
