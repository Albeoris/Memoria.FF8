using System.Collections.Immutable;
using Albeoris.Games.FF8.Imaging;
using Memoria.FF8.Resources;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeBattleGfAtlasTextureSourceTests
{
    [Fact]
    public void Sample_neutral_default_retains_a_legacy_decoded_only_adapter()
    {
        LegacyDecodedSource legacy = new();
        INativeBattleEffectTextureSource textures = legacy;
        PixelImage fallback = new(16, 16);
        PixelImage decoded = new(16, 16);
        PixelImage result = textures.ApplyDecoded(fallback, decoded, NativeBattleEffectTextureState.FromColdProcess(), 0, 0, 0, 0, 16, 16, null, null, null, new(0.25f, 0.75f));
        Assert.Same(decoded, result);
        Assert.Same(decoded, legacy.Decoded);
        Assert.Equal(1, legacy.Calls);
    }

    [Fact]
    public void Variant_default_forwards_the_sample_to_a_sample_only_adapter()
    {
        SampleOnlySource sampled = new();
        INativeBattleEffectTextureSource textures = sampled;
        TexturePrimitiveSample sample = new(0.25f, 0.75f);
        PixelImage result = textures.ApplyDecoded(new(16, 16), new(16, 16), NativeBattleEffectTextureState.FromColdProcess(), 0, 0, 0, 0, 16, 16, null, null, null, sample);
        Assert.Same(sampled.Replacement, result);
        Assert.Equal(sample, sampled.Sample);
        Assert.Equal(1, sampled.Calls);
    }

    [Fact]
    public void Decoded_sample_calls_keep_upper_and_lower_atlas_replacements_and_contexts_distinct()
    {
        using AtlasSource source = new() { VariantAddress = true };
        ResourceService resources = new(source);
        RecordingResolver resolver = new() { LowerReplacement = new(new(16, 16), "fixture:lower", 16, 16) };
        resources.Prepare(resolver).Commit();
        NativeBattleEffectTextureState state = NativeBattleEffectTextureState.FromColdProcess() with { TextureRegistrations = [] };
        using NativeBattleEffectTextureSource textures = new(resources);
        PixelImage fallback = new(16, 16);
        PixelImage decoded = new(16, 16);
        TexturePrimitiveSample upper = new(0.25f, 0.25f);
        TexturePrimitiveSample lower = new(0.25f, 0.75f);

        Assert.Same(resolver.Replacement.Image, textures.ApplyDecoded(fallback, decoded, state, 29, 0, 0, 0, 16, 16, null, null, null, upper));
        Assert.Same(resolver.LowerReplacement!.Image, textures.ApplyDecoded(fallback, decoded, state, 29, 0, 0, 0, 16, 16, null, null, null, lower));
        Assert.Equal(new TexturePrimitiveSample?[] { upper, lower }, source.Samples);
        Assert.Equal(2, resolver.Contexts.Count);
        Assert.NotEqual(resolver.Contexts[0].Key, resolver.Contexts[1].Key);
        Assert.Equal(0, resolver.Contexts[0].NativeAtlas!.SampleXOffset);
        Assert.Equal(128, resolver.Contexts[1].NativeAtlas!.SampleXOffset);
        NativeBattleEffectTextureState alias = state.OpenTextureFile(new("next.tim", NativeBattleTextureDomain.Magic));
        Assert.Same(state.VramPublicationIdentity, alias.VramPublicationIdentity);
        Assert.Same(resolver.Replacement.Image, textures.ApplyDecoded(fallback, decoded, alias, 29, 0, 0, 0, 16, 16, null, null, null, upper));
        Assert.Same(resolver.LowerReplacement!.Image, textures.ApplyDecoded(fallback, decoded, alias, 29, 0, 0, 0, 16, 16, null, null, null, lower));
        Assert.Equal(2, resolver.Contexts.Count);
    }

    [Fact]
    public void Sample_aware_prefix_proof_cannot_bypass_a_real_atlas_replacement()
    {
        using AtlasSource source = new() { VariantAddress = true };
        ResourceService resources = new(source);
        RecordingResolver resolver = new();
        resources.Prepare(resolver).Commit();
        NativeBattleEffectTextureState state = NativeBattleEffectTextureState.FromColdProcess() with { TextureRegistrations = [] };
        using NativeBattleEffectTextureSource textures = new(resources);
        Assert.False(textures.TryProveNoReplacement(state, 29, 0, 0, 0, 256, 256));
        Assert.Empty(resolver.Contexts);
        Assert.False(textures.TryProveNoReplacement(state, 29, 0, 0, 0, 256, 256, sample: new(0.25f, 0.25f)));
        Assert.False(textures.TryProveNoReplacement(state, 29, 0, 0, 0, 256, 256, sample: new(0.25f, 0.75f)));
        Assert.Equal(2, resolver.Contexts.Count);
        Assert.NotEqual(resolver.Contexts[0].Key, resolver.Contexts[1].Key);
    }

    [Theory]
    [InlineData(0, true)]
    [InlineData(256, false)]
    public void Classic_content_capability_without_a_replacement_keeps_prefix_proof_and_does_not_read_pixels(ushort page, bool hasUpload)
    {
        using InMemoryResourceSource source = new(new("fixture", GameDataEdition.Classic, ["demaster-content-upload"]));
        ResourceService resources = new(source);
        RecordingResolver resolver = new() { ReplaceAtlas = false };
        resources.Prepare(resolver).Commit();
        NativeBattleEffectTextureState state = NativeBattleEffectTextureState.FromColdProcess();
        using NativeBattleEffectTextureSource textures = new(resources);
        TrafficCounter traffic = new();
        using (NativeBattleTelemetry.BeginScope(traffic))
        {
            Assert.True(textures.TryProveNoReplacement(state, page, 0, 0, 0, 256, 256, sample: new(0.25f, 0.25f)));
        }
        Assert.Equal(0, traffic.Bytes);
        Assert.Equal(0, resources.ResidentTextureCount);
        if (hasUpload)
        {
            TextureBuildContext context = Assert.Single(resolver.Contexts);
            Assert.Equal("native-pc-texture-upload", context.Layout);
            Assert.NotNull(context.NativeUpload);
        }
        else
        {
            Assert.Empty(resolver.Contexts);
        }
    }

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
        public bool VariantAddress { get; init; }

        public ResourceContent? Find(ResourceKey key) => null;

        public TextureSourceAtlasAddress? TryGetGfAtlasAddress(ImmutableArray<NativeBattleTextureRegistration> registrations, ushort page, ushort clut, int x, int y, int width, int height, TexturePrimitiveSample? sample = null)
        {
            Assert.Empty(registrations);
            Samples.Add(sample);
            return new("mag005", 0, 2, 256, 256, x, y, width, height) { SampleXOffset = VariantAddress && sample is { FirstV: >= 0.5f } ? 128 : 0 };
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
        public TextureReplacement? LowerReplacement { get; init; }

        public TextureReplacement? Resolve(TextureBuildContext context)
        {
            Contexts.Add(context);
            return ReplaceAtlas && context.NativeAtlas is { } atlas ? atlas.SampleXOffset == 0 ? Replacement : LowerReplacement ?? Replacement : null;
        }
    }

    private sealed class LegacyDecodedSource : INativeBattleEffectTextureSource
    {
        public long Generation => 0;
        public PixelImage? Decoded { get; private set; }
        public int Calls { get; private set; }

        public PixelImage Apply(PixelImage original, NativeBattleEffectTextureState state, ushort page, ushort clut, int u, int v, int width, int height, NativeBattlePcTextureWindowPixels? retainedPixels = null, byte? alternatePaletteShift = null, NativeBattlePcDirectTextureFormat? directTextureFormat = null)
        {
            throw new InvalidOperationException("The legacy decoded adapter must retain its decoded call.");
        }

        public PixelImage ApplyDecoded(PixelImage original, PixelImage? decodedNative, NativeBattleEffectTextureState state, ushort page, ushort clut, int u, int v, int width, int height, NativeBattlePcTextureWindowPixels? retainedPixels = null, byte? alternatePaletteShift = null, NativeBattlePcDirectTextureFormat? directTextureFormat = null)
        {
            Calls++;
            Decoded = decodedNative;
            return decodedNative ?? original;
        }
    }

    private sealed class SampleOnlySource : INativeBattleEffectTextureSource
    {
        public long Generation => 0;
        public PixelImage Replacement { get; } = new(16, 16);
        public TexturePrimitiveSample? Sample { get; private set; }
        public int Calls { get; private set; }
        public int GetPrimitiveSampleKey(TexturePrimitiveSample sample) => sample.FirstV < 0.5f ? 1 : 2;

        public PixelImage Apply(PixelImage original, NativeBattleEffectTextureState state, ushort page, ushort clut, int u, int v, int width, int height, NativeBattlePcTextureWindowPixels? retainedPixels = null, byte? alternatePaletteShift = null, NativeBattlePcDirectTextureFormat? directTextureFormat = null)
        {
            throw new InvalidOperationException("The sample adapter must receive its declared variant.");
        }

        public PixelImage Apply(PixelImage original, NativeBattleEffectTextureState state, ushort page, ushort clut, int u, int v, int width, int height, NativeBattlePcTextureWindowPixels? retainedPixels, byte? alternatePaletteShift, NativeBattlePcDirectTextureFormat? directTextureFormat, TexturePrimitiveSample sample)
        {
            Calls++;
            Sample = sample;
            return Replacement;
        }
    }

    private sealed class TrafficCounter : INativeBattleTelemetrySink
    {
        public long Bytes { get; private set; }

        public void Record(int siteId, NativeBattleTrafficKind kind, long payloadBytes)
        {
            Bytes += payloadBytes;
        }
    }
}
