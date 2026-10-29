using System.Collections.Immutable;
using Albeoris.Games.FF8.Imaging;
using Memoria.FF8.Resources;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeBattleEffectTextureSourceTests
{
    [Fact]
    public void Native_only_proof_uses_the_full_query_and_rechecks_catalog_replacements()
    {
        NativeBattleEffectTextureState state = PixelState(new("mag012_b.00", 2, NativeBattleTextureRole.Pixels, 4, 1, 1));
        using InMemoryResourceSource source = Source();
        ResourceService resources = new(source);
        RecordingResolver missing = new(_ => null);
        resources.Prepare(missing).Commit();
        using NativeBattleEffectTextureSource textures = new(resources);
        Assert.True(textures.TryProveNoReplacement(state, 0, 0, 0, 0, 256, 256));
        TextureBuildContext full = Assert.Single(missing.Contexts);
        Assert.Contains("/window/0/0/256/256/", full.Key.ToString());
        Assert.Equal(0, resources.ResidentTextureCount);
        resources.Prepare(new RecordingResolver(context => Solid(context, new(90, 0, 0, 127)))).Commit();
        Assert.False(textures.TryProveNoReplacement(state, 0, 0, 0, 0, 256, 256));
        PixelImage original = new(256, 256);
        Assert.Equal((byte)90, textures.Apply(original, state, 0, 0, 0, 0, 256, 256)[0, 0].R);
        resources.Prepare(null).Commit();
        Assert.True(textures.TryProveNoReplacement(state, 0, 0, 0, 0, 256, 256));
        Assert.Equal(0, resources.ResidentTextureCount);
    }

    [Fact]
    public void A_resolver_reading_original_pixels_cannot_prove_the_prefix_path_even_when_it_returns_no_replacement()
    {
        NativeBattleEffectTextureState state = PixelState(new("mag012_b.00", 2, NativeBattleTextureRole.Pixels, 4, 1, 1));
        using InMemoryResourceSource source = Source();
        ResourceService resources = new(source);
        RecordingResolver resolver = new(context =>
        {
            Assert.Equal(4, context.Original.Width);
            Assert.Equal(1, context.Original.Height);
            return null;
        });
        resources.Prepare(resolver).Commit();
        using NativeBattleEffectTextureSource textures = new(resources);
        Assert.False(textures.TryProveNoReplacement(state, 0, 0, 0, 0, 256, 256));
        Assert.Single(resolver.Contexts);
        Assert.Equal(0, resources.ResidentTextureCount);
    }

    [Fact]
    public void Native_only_probe_keeps_the_existing_replacement_failure_reporting()
    {
        NativeBattleEffectTextureState state = PixelState(new("mag012_b.00", 2, NativeBattleTextureRole.Pixels, 4, 1, 1));
        using InMemoryResourceSource source = Source();
        ResourceService resources = new(source);
        int failures = 0;
        resources.ReplacementFailed += (_, error) =>
        {
            Assert.Equal("fixture replacement failure", error.Message);
            failures++;
        };
        resources.Prepare(new RecordingResolver(_ => throw new InvalidDataException("fixture replacement failure"))).Commit();
        using NativeBattleEffectTextureSource textures = new(resources);
        Assert.True(textures.TryProveNoReplacement(state, 0, 0, 0, 0, 256, 256));
        Assert.Equal(1, failures);
        Assert.Equal(0, resources.ResidentTextureCount);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void MissingFragmentReplacementDoesNotDecodeAnUnusedOriginalCrop(bool alternate)
    {
        NativeBattleEffectTextureState state = PixelState(new("mag012_b.00", 2, NativeBattleTextureRole.Pixels, 4, 1, 1), alternate ? (short)0 : (short)1);
        PixelImage decoded = new(alternate ? 4 : 12, 1);
        using InMemoryResourceSource source = Source();
        ResourceService resources = new(source);
        RecordingResolver resolver = new(_ => null);
        resources.Prepare(resolver).Commit();
        using NativeBattleEffectTextureSource textures = new(resources);

        Assert.Same(decoded, textures.ApplyDecoded(decoded, decoded, state, 0, 0, 0, 0, decoded.Width, 1, alternatePaletteShift: alternate ? (byte)1 : null));
        TextureBuildContext context = Assert.Single(resolver.Contexts);
        Lazy<PixelImage> original = (Lazy<PixelImage>)typeof(TextureBuildContext).GetField("original", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.GetValue(context)!;
        Assert.False(original.IsValueCreated);
        Assert.Equal(0, resources.ResidentTextureCount);

        resources.Prepare(new RecordingResolver(candidate => Solid(candidate, new(90, 20, 30, 127)))).Commit();
        Assert.False(original.IsValueCreated);
        Assert.NotSame(decoded, textures.ApplyDecoded(decoded, decoded, state, 0, 0, 0, 0, decoded.Width, 1, alternatePaletteShift: alternate ? (byte)1 : null));
        Assert.Equal(1, resources.ResidentTextureCount);
        resources.Prepare(null).Commit();
        Assert.Same(decoded, textures.ApplyDecoded(decoded, decoded, state, 0, 0, 0, 0, decoded.Width, 1, alternatePaletteShift: alternate ? (byte)1 : null));
        Assert.Equal(0, resources.ResidentTextureCount);
        Assert.NotSame(decoded, context.Original);
        Assert.Equal(4, context.Original.Width);
        Assert.True(original.IsValueCreated);
    }

    [Fact]
    public void FullDecodedNativeImageIsTheResolverOriginalWhileModelFallbackAndReloadRemainAuthoritative()
    {
        NativeBattleEffectTextureState state = PixelState(new("mag012_b.00", 2, NativeBattleTextureRole.Pixels, 4, 1, 1));
        PixelImage decoded = NativeBattleEffectTextureReader.DecodePcRegion(state, 0, 0, 0, 0, 4, 1);
        PixelImage modelFallback = new(8, 2);
        using InMemoryResourceSource source = Source();
        ResourceService resources = new(source);
        RecordingResolver first = new(context =>
        {
            if (context.Layout == "native-effect-texture")
            {
                Assert.Same(decoded, context.Original);
            }
            return null;
        });
        resources.Prepare(first).Commit();
        using NativeBattleEffectTextureSource textures = new(resources);
        long catalog = textures.Generation;

        Assert.Same(modelFallback, textures.ApplyDecoded(modelFallback, decoded, state, 0, 0, 0, 0, 4, 1));
        NativeBattleEffectTextureState changed = state.PublishVramSpan(0, new byte[] { 1, 0 });
        PixelImage nextDecoded = NativeBattleEffectTextureReader.DecodePcRegion(changed, 0, 0, 0, 0, 4, 1);
        resources.GetTexture(new(new(new("fixture/unrelated"), "native"), resources.Source, 1, "native", 1, 1, () => new(1, 1)));
        resources.GetTexture(new(new(new("fixture/unrelated"), "native"), resources.Source, 2, "native", 1, 1, () => new(1, 1)));
        Assert.Equal(catalog, textures.Generation);

        RecordingResolver second = new(context => Solid(context, new(90, 0, 0, 127)));
        resources.Prepare(second).Commit();
        Assert.True(textures.Generation > catalog);
        Assert.Equal((byte)90, textures.ApplyDecoded(modelFallback, nextDecoded, changed, 0, 0, 0, 0, 4, 1)[0, 0].R);
        TextureBuildContext current = Assert.Single(second.Contexts, context => context.Layout == "native-effect-texture" && ReferenceEquals(context.Original, nextDecoded));
        Assert.Same(nextDecoded, current.Original);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void FragmentedOrAlternatePaletteQueriesKeepTheirNativeDecodeFallback(bool alternate)
    {
        NativeBattleEffectTextureState state = PixelState(new("mag012_b.00", 2, NativeBattleTextureRole.Pixels, 4, 1, 1), alternate ? (short)0 : (short)1);
        PixelImage decoded = new(alternate ? 4 : 12, 1);
        using InMemoryResourceSource source = Source();
        ResourceService resources = new(source);
        RecordingResolver resolver = new(_ => null);
        resources.Prepare(resolver).Commit();
        using NativeBattleEffectTextureSource textures = new(resources);

        textures.ApplyDecoded(decoded, decoded, state, 0, 0, 0, 0, decoded.Width, 1, alternatePaletteShift: alternate ? (byte)1 : null);

        TextureBuildContext context = Assert.Single(resolver.Contexts);
        Assert.NotSame(decoded, context.Original);
        Assert.Equal(4, context.Original.Width);
    }

    [Theory]
    [InlineData(NativeBattleTextureDomain.Magic, "magic")]
    [InlineData(NativeBattleTextureDomain.Battle, "battle")]
    public void TypedSourcePublishesItsOriginalCropCanvasAndNamespace(NativeBattleTextureDomain domain, string externalDomain)
    {
        NativeBattleTextureOrigin origin = new("mag012.tim", 3, NativeBattleTextureRole.Pixels, 8, 8, 4) { Domain = domain, Kind = NativeBattleTextureSourceKind.Tim };
        NativeBattleEffectTextureState state = NativeBattleEffectTextureState.FromColdProcess() with
        {
            LiveTextureSources = [new(origin, 66, 258, 2, 2, 2, 1)],
            TextureRegistrations = [new(origin, 64, 257, 8, 4, [])]
        };
        using InMemoryResourceSource source = Source();
        ResourceService resources = new(source);
        RecordingResolver resolver = new(context => Solid(context, new(20, 30, 40, 127), 3));
        resources.Prepare(resolver).Commit();
        using NativeBattleEffectTextureSource textures = new(resources);

        PixelImage result = textures.Apply(new(2, 1), state, 0x91, 0, 5, 3, 2, 1);

        TextureBuildContext context = Assert.Single(resolver.Contexts);
        Assert.Equal("native-effect-texture", context.Layout);
        Assert.Equal(new ResourceKey("battle/mag012.tim"), context.Key.Owner);
        Assert.Contains($"/{domain}/Tim/3/", context.Key.Subresource);
        Assert.Equal(new TextureSourceAddress(new($"{externalDomain}/mag012.tim"), "texture/3", 0, 5, 2, 2, 1, 16, 4), context.NativeAlias);
        Assert.Equal(2, context.LogicalWidth);
        Assert.Equal(1, context.LogicalHeight);
        Assert.Equal(8, context.Region.BitsPerPixel);
        Assert.Equal(5, context.NativePage!.X);
        Assert.Equal(3, context.NativePage.Y);
        Assert.Equal(new TexturePageMember("effect", "mag012.tim", 3, 64, 257, 8, 4, 8, -1), Assert.Single(context.NativePage.Members));
        Assert.Equal(6, result.Width);
        Assert.Equal(3, result.Height);
        Assert.Equal(new Rgba32(20, 30, 40, 127), result[0, 0]);
    }

    [Theory]
    [InlineData(0, 33, 1)]
    [InlineData(16, 33, 0)]
    [InlineData(0, 31, 0)]
    [InlineData(0, 34, 0)]
    public void PhysicalClutAnchorSelectsOnlyItsNativeXAndBoundedRows(int x, int y, int expectedRow)
    {
        NativeBattleEffectTextureState state = AnchoredState();
        using InMemoryResourceSource source = Source();
        ResourceService resources = new(source);
        RecordingResolver resolver = new(context => Solid(context, new((byte)(context.Key.Palette + 10), 0, 0, 127)));
        resources.Prepare(resolver).Commit();
        using NativeBattleEffectTextureSource textures = new(resources);

        textures.Apply(new(8, 1), state, 17, Clut(x, y), 0, 0, 8, 1);

        TextureBuildContext context = Assert.Single(resolver.Contexts);
        Assert.Equal(expectedRow, context.Key.Palette);
        Assert.Equal(expectedRow, context.NativeAlias!.Palette);
        Assert.Equal(2, context.PaletteCount);
        Assert.Equal(x, context.Region.PaletteX);
        Assert.Equal(y, context.Region.PaletteY);
    }

    [Fact]
    public void ARelocatedPaletteCopyDoesNotInventAnOriginalRowMapping()
    {
        NativeBattleEffectTextureState state = AnchoredState().EnqueueCopy505EB0(0, 33, 16, 1, 0, 100).Drain505D20().State;
        using InMemoryResourceSource source = Source();
        ResourceService resources = new(source);
        RecordingResolver resolver = new(context => Solid(context, new(10, 20, 30, 127)));
        resources.Prepare(resolver).Commit();
        using NativeBattleEffectTextureSource textures = new(resources);

        textures.Apply(new(8, 1), state, 17, Clut(0, 100), 0, 0, 8, 1);

        Assert.Equal(0, Assert.Single(resolver.Contexts).NativeAlias!.Palette);
    }

    [Fact]
    public void ReplacingPaletteWordsDoesNotChangeThePixelSourcesPhysicalPaletteIndex()
    {
        NativeBattleEffectTextureState state = AnchoredState().Upload45BD30(0, 33, 16, 1, new byte[32]);
        using InMemoryResourceSource source = Source();
        ResourceService resources = new(source);
        RecordingResolver resolver = new(context => Solid(context, new(10, 20, 30, 127)));
        resources.Prepare(resolver).Commit();
        using NativeBattleEffectTextureSource textures = new(resources);

        textures.Apply(new(8, 1), state, 17, Clut(0, 33), 0, 0, 8, 1);

        Assert.Equal(1, Assert.Single(resolver.Contexts).NativeAlias!.Palette);
    }

    [Fact]
    public void AnUnknownPixelOverwriteStopsApplyingItsOldResource()
    {
        NativeBattleEffectTextureState originalState = PixelState(new("mag012_b.00", 2, NativeBattleTextureRole.Pixels, 4, 1, 1));
        using InMemoryResourceSource source = Source();
        ResourceService resources = new(source);
        RecordingResolver resolver = new(context => Solid(context, new(90, 20, 30, 127)));
        resources.Prepare(resolver).Commit();
        using NativeBattleEffectTextureSource textures = new(resources);
        PixelImage original = new(4, 1);
        Assert.Equal((byte)90, textures.Apply(original, originalState, 0, 0, 0, 0, 4, 1)[0, 0].R);

        NativeBattleEffectTextureState overwritten = originalState.Upload45BD30(0, 0, 1, 1, [0, 0]);

        Assert.Same(original, textures.Apply(original, overwritten, 0, 0, 0, 0, 4, 1));
        Assert.Single(resolver.Contexts);
    }

    [Fact]
    public void RetainedPixelWordsKeepTheirOriginWhileTheCurrentPaletteStaysLive()
    {
        NativeBattleEffectTextureState initial = AnchoredState();
        NativeBattlePcTextureWindowPixels retained = NativeBattlePcTextureWindowPixels.Capture(17, new(0, 0, 8, 1), initial);
        byte[] currentPalette = new byte[32];
        currentPalette[0] = 31;
        NativeBattleEffectTextureState current = initial.Upload45BD30(64, 256, 2, 1, [0x11, 0x11, 0x11, 0x11]).Upload45BD30(0, 33, 16, 1, currentPalette);
        using InMemoryResourceSource source = Source();
        ResourceService resources = new(source);
        RecordingResolver resolver = new(context => Solid(context, new((byte)(40 + context.Key.Palette), 20, 30, 127)));
        resources.Prepare(resolver).Commit();
        using NativeBattleEffectTextureSource textures = new(resources);
        PixelImage original = new(8, 1);

        Assert.Same(original, textures.Apply(original, current, 17, Clut(0, 33), 0, 0, 8, 1));
        PixelImage result = textures.Apply(original, current, 17, Clut(0, 33), 0, 0, 8, 1, retained);

        Assert.Equal((byte)41, result[0, 0].R);
        Assert.Equal(1, Assert.Single(resolver.Contexts).NativeAlias!.Palette);
        Assert.Equal("magic/mag012_b.00", resolver.Contexts[0].NativeAlias!.Owner.Path);
        Assert.Equal(new Rgba32(255, 0, 0, 127), resolver.Contexts[0].Original[0, 0]);
    }

    [Fact]
    public void CopiedAndChainedSourceReplacementsSurviveSourceOverwriteWithoutDestinationMods()
    {
        NativeBattleEffectTextureState state = PixelState(new("mag012_b.00", 2, NativeBattleTextureRole.Pixels, 4, 2, 1));
        state = state.EnqueueCopy505EB0(0, 0, 2, 1, 16, 0).Drain505D20().State;
        state = state.EnqueueCopy505EB0(16, 0, 2, 1, 32, 0).Drain505D20().State;
        state = state.Upload45BD30(0, 0, 2, 1, new byte[4]);
        using InMemoryResourceSource source = Source();
        ResourceService resources = new(source);
        RecordingResolver resolver = new(context => context.NativeAlias!.Owner.Path == "magic/mag012_b.00" ? Solid(context, new(90, 20, 30, 127), 2) : null);
        resources.Prepare(resolver).Commit();
        using NativeBattleEffectTextureSource textures = new(resources);
        PixelImage original = new(8, 1);

        Assert.Same(original, textures.Apply(original, state, 0, 0, 0, 0, 8, 1));
        PixelImage copied = textures.Apply(original, state, 0, 0, 64, 0, 8, 1);
        PixelImage chained = textures.Apply(original, state, 0, 0, 128, 0, 8, 1);

        Assert.Equal(16, copied.Width);
        Assert.Equal(copied[0, 0], chained[0, 0]);
        Assert.Equal(new Rgba32(90, 20, 30, 127), chained[15, 1]);
        Assert.Equal(2, resolver.Contexts.Count);
        Assert.All(resolver.Contexts, context =>
        {
            Assert.Equal("magic/mag012_b.00", context.NativeAlias!.Owner.Path);
            Assert.Equal(0, context.NativeAlias.X);
            Assert.Empty(context.NativePage!.Members);
        });
    }

    [Fact]
    public void DistinctRetainedFrameHistoriesDoNotShareAResourceContextAgainstTheSameLiveState()
    {
        NativeBattleEffectTextureState state = PixelState(new("mag012_b.00", 2, NativeBattleTextureRole.Pixels, 4, 2, 1));
        var firstHistory = NativeBattleTextureCopyHistory.Copy(state.TextureRegistrations, new(0, 0, 1, 1, 0, 0));
        var secondHistory = NativeBattleTextureCopyHistory.Copy(firstHistory, new(1, 0, 1, 1, 0, 0));
        NativeBattlePcTextureWindowPixels words = NativeBattlePcTextureWindowPixels.Capture(0, new(0, 0, 8, 1), state);
        NativeBattlePcTextureWindowPixels first = words with { TextureRegistrations = firstHistory };
        NativeBattlePcTextureWindowPixels second = words with { TextureRegistrations = secondHistory };
        using InMemoryResourceSource source = Source();
        ResourceService resources = new(source);
        RecordingResolver resolver = new(context => Solid(context, new((byte)(20 + 60 * Assert.Single(context.NativePage!.Members).Frame), 0, 0, 127)));
        resources.Prepare(resolver).Commit();
        using NativeBattleEffectTextureSource textures = new(resources);

        PixelImage earlier = textures.Apply(new(8, 1), state, 0, 0, 0, 0, 8, 1, first);
        PixelImage later = textures.Apply(new(8, 1), state, 0, 0, 0, 0, 8, 1, second);
        PixelImage repeated = textures.Apply(new(8, 1), state, 0, 0, 0, 0, 8, 1, first);

        Assert.Equal((byte)20, earlier[0, 0].R);
        Assert.Equal((byte)80, later[0, 0].R);
        Assert.Equal(earlier[0, 0], repeated[0, 0]);
        Assert.Equal(2, resolver.Contexts.Count);
        Assert.NotEqual(resolver.Contexts[0].Key, resolver.Contexts[1].Key);
        Assert.Equal(2, resources.ResidentTextureCount);
    }

    [Fact]
    public void PageMembersFollowTheEntireSelectedWindowWithoutIncludingOtherOwnersElsewhereOnThePage()
    {
        NativeBattleTextureOrigin first = new("mag012_b.00", 2, NativeBattleTextureRole.Pixels, 4, 2, 1);
        NativeBattleTextureOrigin second = new("mag013_b.00", 3, NativeBattleTextureRole.Pixels, 4, 2, 1);
        NativeBattleEffectTextureState state = PixelState(first)
            .EnqueueRectangle505DF0(new(16, 0, 2, 1), new("fixture:second", new byte[4].ToImmutableArray()), origin: second)
            .Drain505D20().State;
        using InMemoryResourceSource source = Source();
        ResourceService resources = new(source);
        RecordingResolver resolver = new(context => Solid(context, new((byte)(10 * context.NativePage!.Members.Count), 0, 0, 127)));
        resources.Prepare(resolver).Commit();
        using NativeBattleEffectTextureSource textures = new(resources);

        PixelImage small = textures.Apply(new(8, 1), state, 0, 0, 0, 0, 8, 1);
        PixelImage spanning = textures.Apply(new(72, 1), state, 0, 0, 0, 0, 72, 1);

        Assert.Equal((byte)10, small[0, 0].R);
        Assert.Equal("mag012_b.00", Assert.Single(resolver.Contexts[0].NativePage!.Members).Name);
        Assert.Equal((byte)20, spanning[0, 0].R);
        Assert.Equal((byte)20, spanning[64, 0].R);
        Assert.Equal(3, resolver.Contexts.Count);
        Assert.All(resolver.Contexts.Skip(1), context => Assert.Equal(new[] { "mag012_b.00", "mag013_b.00" }, context.NativePage!.Members.Select(member => member.Name)));
        Assert.NotEqual(resolver.Contexts[0].Key, resolver.Contexts[1].Key);
    }

    [Fact]
    public void AnAbsentReplacementPreservesTheExactOriginalImage()
    {
        NativeBattleEffectTextureState state = PixelState(new("mag012_b.00", 2, NativeBattleTextureRole.Pixels, 4, 1, 1));
        using InMemoryResourceSource source = Source();
        ResourceService resources = new(source);
        RecordingResolver resolver = new(_ => null);
        resources.Prepare(resolver).Commit();
        using NativeBattleEffectTextureSource textures = new(resources);
        PixelImage original = new(4, 1, [new(1, 2, 3), new(4, 5, 6), new(7, 8, 9), new(10, 11, 12)]);

        Assert.Same(original, textures.Apply(original, state, 0, 0, 0, 0, 4, 1));
        Assert.Single(resolver.Contexts);
        Assert.Same(original, textures.Apply(original, NativeBattleEffectTextureState.FromColdProcess(), 0, 0, 0, 0, 4, 1));
        Assert.Single(resolver.Contexts);
    }

    [Fact]
    public void HigherResolutionFragmentsPreserveUnreplacedNativeAreas()
    {
        NativeBattleEffectTextureState state = PixelState(new("mag012_b.00", 2, NativeBattleTextureRole.Pixels, 4, 1, 1), 1, 0);
        using InMemoryResourceSource source = Source();
        ResourceService resources = new(source);
        RecordingResolver resolver = new(context => Solid(context, new(90, 20, 30, 127), 2));
        resources.Prepare(resolver).Commit();
        using NativeBattleEffectTextureSource textures = new(resources);
        PixelImage original = new(12, 2, Enumerable.Range(0, 24).Select(index => new Rgba32((byte)index, 0, 0, 127)).ToArray());

        PixelImage result = textures.Apply(original, state, 0, 0, 0, 0, 12, 2);

        Assert.Equal(24, result.Width);
        Assert.Equal(4, result.Height);
        Assert.Equal(original[3, 0], result[7, 0]);
        Assert.Equal(new Rgba32(90, 20, 30, 127), result[8, 0]);
        Assert.Equal(new Rgba32(90, 20, 30, 127), result[15, 1]);
        Assert.Equal(original[8, 0], result[16, 0]);
        Assert.Equal(original[4, 1], result[8, 2]);
    }

    [Theory]
    [InlineData(4, 0, 8)]
    [InlineData(8, 0x80, 4)]
    [InlineData(16, 0x100, 2)]
    public void SourceQueriesUseTheCurrentPageDepth(int bits, ushort page, int canvasWidth)
    {
        NativeBattleEffectTextureState state = PixelState(new("mag012_b.00", 2, NativeBattleTextureRole.Pixels, bits, 2, 1));
        using InMemoryResourceSource source = Source();
        ResourceService resources = new(source);
        RecordingResolver resolver = new(context => Solid(context, new(90, 20, 30, 127)));
        resources.Prepare(resolver).Commit();
        using NativeBattleEffectTextureSource textures = new(resources);
        PixelImage original = new(2, 1);

        textures.Apply(original, state, page, 0, 0, 0, 2, 1);
        TextureBuildContext context = Assert.Single(resolver.Contexts);
        Assert.Equal(bits, context.Region.BitsPerPixel);
        Assert.Equal(canvasWidth, context.NativeAlias!.CanvasWidth);
        Assert.Equal(2, context.LogicalWidth);
        ushort mismatched = page == 0 ? (ushort)0x80 : (ushort)0;
        Assert.Same(original, textures.Apply(original, state, mismatched, 0, 0, 0, 2, 1));
        Assert.Single(resolver.Contexts);
    }

    [Fact]
    public void ResourceRefreshUpdatesStableHandlesWithoutGrowingResidentContexts()
    {
        NativeBattleEffectTextureState state = PixelState(new("mag012_b.00", 2, NativeBattleTextureRole.Pixels, 4, 1, 1));
        using InMemoryResourceSource source = Source();
        ResourceService resources = new(source);
        RecordingResolver first = new(context => Solid(context, new(20, 0, 0, 127)));
        resources.Prepare(first).Commit();
        using NativeBattleEffectTextureSource textures = new(resources);
        PixelImage original = new(4, 1);
        textures.Apply(original, state, 0, 0, 0, 0, 4, 1);
        TextureBuildContext context = Assert.Single(first.Contexts);
        AssetHandle<TextureValue> handle = resources.GetTexture(context);
        textures.Apply(original, state, 0, 0, 0, 0, 4, 1);
        Assert.Single(first.Contexts);
        long generation = textures.Generation;
        RecordingResolver second = new(candidate => Solid(candidate, new(90, 0, 0, 127)));

        resources.Prepare(second).Commit();

        Assert.True(textures.Generation > generation);
        Assert.Same(handle, resources.GetTexture(context));
        Assert.Equal((byte)90, handle.Value.Image[0, 0].R);
        Assert.Equal((byte)90, textures.Apply(original, state, 0, 0, 0, 0, 4, 1)[0, 0].R);
        Assert.Single(second.Contexts);
        Assert.Equal(1, resources.ResidentTextureCount);
        textures.Dispose();
        Assert.Equal(0, resources.ResidentTextureCount);
        Assert.Equal((byte)90, handle.Value.Image[0, 0].R);
    }

    [Fact]
    public void RecordCopiesWithTheSameTexturePublicationReuseTheResourceRevisionAndReloadNormally()
    {
        NativeBattleEffectTextureState state = PixelState(new("mag012_b.00", 2, NativeBattleTextureRole.Pixels, 4, 1, 1));
        using InMemoryResourceSource source = Source();
        ResourceService resources = new(source);
        RecordingResolver first = new(context => Solid(context, new(20, 0, 0, 127)));
        resources.Prepare(first).Commit();
        using NativeBattleEffectTextureSource textures = new(resources);
        PixelImage original = new(4, 1);
        textures.Apply(original, state, 0, 0, 0, 0, 4, 1);
        long generation = resources.Generation;

        for (int frame = 0; frame < 32; frame++)
        {
            NativeBattleEffectTextureState alias = state.OpenTextureFile(new($"mag{frame:000}.tim", NativeBattleTextureDomain.Magic));
            Assert.Same(state.VramPublicationIdentity, alias.VramPublicationIdentity);
            Assert.Equal((byte)20, textures.Apply(original, alias, 0, 0, 0, 0, 4, 1)[0, 0].R);
            state = alias;
        }

        Assert.Single(first.Contexts);
        Assert.Equal(generation, resources.Generation);
        Assert.Equal(1, resources.ResidentTextureCount);
        RecordingResolver second = new(context => Solid(context, new(90, 0, 0, 127)));
        resources.Prepare(second).Commit();
        NativeBattleEffectTextureState reloadedAlias = state with { Count = 1 };
        Assert.Equal((byte)90, textures.Apply(original, reloadedAlias, 0, 0, 0, 0, 4, 1)[0, 0].R);
        Assert.Single(second.Contexts);
        Assert.Equal(1, resources.ResidentTextureCount);
    }

    [Theory]
    [InlineData("pixels")]
    [InlineData("palette")]
    [InlineData("source")]
    [InlineData("registration")]
    [InlineData("tail")]
    public void EveryTexturePublicationInputInvalidatesTheResourceRevision(string change)
    {
        NativeBattleEffectTextureState state = AnchoredState().PublishVramSpan(2 * (33 * 1024 + 1), new byte[] { 31, 0 });
        using InMemoryResourceSource source = Source();
        ResourceService resources = new(source);
        RecordingResolver resolver = new(context => Solid(context, context.Original[0, 0]));
        resources.Prepare(resolver).Commit();
        using NativeBattleEffectTextureSource textures = new(resources);
        PixelImage original = new(8, 1);
        textures.Apply(original, state, 17, Clut(0, 33), 0, 0, 8, 1);
        long generation = resources.Generation;
        NativeBattleEffectTextureState changed;
        switch (change)
        {
            case "pixels":
                changed = state.PublishVramSpan(2 * (256 * 1024 + 64), new byte[] { 0x11, 0x11 });
                break;
            case "palette":
                changed = state.PublishVramSpan(2 * (33 * 1024), new byte[] { 0, 124 });
                break;
            case "source":
                ImmutableArray<NativeBattleTextureSourceRegion> sources = state.LiveTextureSources!.Value;
                NativeBattleTextureSourceRegion pixels = sources[0];
                changed = state with { LiveTextureSources = sources.SetItem(0, pixels with { Origin = pixels.Origin with { WidthWords = pixels.Origin.WidthWords + 1 } }) };
                break;
            case "registration":
                ImmutableArray<NativeBattleTextureRegistration> registrations = state.TextureRegistrations!.Value;
                NativeBattleTextureRegistration registration = registrations[0];
                changed = state with { TextureRegistrations = registrations.SetItem(0, registration with { CopyFrames = [new(64, 256, 1, 1)], CurrentFrame = 0, CopyHistoryKnown = true, CopyOrder = [0], PaletteOwnedSpans = [] }) };
                break;
            default:
                changed = state with { VramReadTail = state.VramReadTail!.Value.SetItem(0, 1) };
                break;
        }

        textures.Apply(original, changed, 17, Clut(0, 33), 0, 0, 8, 1);
        textures.Apply(original, changed.OpenTextureFile(new("next.tim", NativeBattleTextureDomain.Magic)), 17, Clut(0, 33), 0, 0, 8, 1);

        Assert.Equal(2, resolver.Contexts.Count);
        Assert.NotEqual(resolver.Contexts[0].Revision, resolver.Contexts[1].Revision);
        Assert.True(resources.Generation > generation);
        Assert.Equal(1, resources.ResidentTextureCount);
        if (change is "pixels" or "palette")
        {
            Assert.NotEqual(resolver.Contexts[0].Original[0, 0], resolver.Contexts[1].Original[0, 0]);
        }
        else
        {
            Assert.Same(state.VramPublicationIdentity, changed.VramPublicationIdentity);
        }
        if (change == "source")
        {
            Assert.Equal(12, resolver.Contexts[1].NativeAlias!.CanvasWidth);
        }
        if (change == "registration")
        {
            Assert.Equal(0, Assert.Single(resolver.Contexts[1].NativePage!.Members).Frame);
        }
    }

    [Fact]
    public void ALongSequenceOfPaletteQueriesKeepsOnlyBoundedResidentContexts()
    {
        NativeBattleEffectTextureState state = PixelState(new("mag012_b.00", 2, NativeBattleTextureRole.Pixels, 4, 1, 1));
        using InMemoryResourceSource source = Source();
        ResourceService resources = new(source);
        RecordingResolver resolver = new(context => Solid(context, new(20, 30, 40, 127)));
        resources.Prepare(resolver).Commit();
        using NativeBattleEffectTextureSource textures = new(resources);
        PixelImage original = new(4, 1);

        for (ushort clut = 0; clut < 300; clut++)
        {
            textures.Apply(original, state, 0, clut, 0, 0, 4, 1);
            Assert.InRange(resources.ResidentTextureCount, 1, 256);
        }

        Assert.Equal(300, resolver.Contexts.Count);
        textures.Dispose();
        Assert.Equal(0, resources.ResidentTextureCount);
    }

    [Theory]
    [InlineData(0, 62, 186)]
    [InlineData(1, 31, 93)]
    public void AlternatePcPaletteUsesNativeBlackRgbAndBrightnessAlpha(byte shift, byte redAlpha, byte whiteAlpha)
    {
        NativeBattleEffectTextureState state = PixelState(new("mag012_b.00", 2, NativeBattleTextureRole.Pixels, 4, 1, 1));
        using InMemoryResourceSource source = Source();
        ResourceService resources = new(source);
        PixelImage replacement = new(4, 1, [new(255, 0, 0, 127), new(255, 255, 255, 127), new(255, 255, 255, 0), new(0, 0, 0, 127)]);
        resources.Prepare(new RecordingResolver(context => new(replacement, "fixture:alternate", 4, 1))).Commit();
        using NativeBattleEffectTextureSource textures = new(resources);

        PixelImage result = textures.Apply(new(4, 1), state, 0, 0, 0, 0, 4, 1, alternatePaletteShift: shift);

        Assert.Equal(new Rgba32(0, 0, 0, redAlpha), result[0, 0]);
        Assert.Equal(new Rgba32(0, 0, 0, whiteAlpha), result[1, 0]);
        Assert.Equal(new Rgba32(0, 0, 0, 0), result[2, 0]);
        Assert.Equal(new Rgba32(0, 0, 0, 0), result[3, 0]);
        Assert.Equal(new Rgba32(255, 0, 0, 127), replacement[0, 0]);
    }

    private static NativeBattleEffectTextureState AnchoredState()
    {
        NativeBattleTextureOrigin pixels = new("mag012_b.00", 2, NativeBattleTextureRole.Pixels, 4, 2, 1);
        NativeBattleTextureOrigin palette = new("mag012_b.01", 5, NativeBattleTextureRole.Clut, 4, 16, 2);
        return NativeBattleEffectTextureState.FromColdProcess()
            .EnqueueRectangle505DF0(new(64, 256, 2, 1), new("fixture:pixels", new byte[4].ToImmutableArray()), origin: pixels)
            .EnqueueRectangle505DF0(new(0, 32, 16, 2), new("fixture:palette", new byte[64].ToImmutableArray()), origin: palette)
            .Drain505D20().State;
    }

    private static NativeBattleEffectTextureState PixelState(NativeBattleTextureOrigin origin, short x = 0, short y = 0) => NativeBattleEffectTextureState.FromColdProcess()
        .EnqueueRectangle505DF0(new(x, y, (short)origin.WidthWords, (short)origin.Height), new("fixture:pixels", new byte[2 * origin.WidthWords * origin.Height].ToImmutableArray()), origin: origin)
        .Drain505D20().State;

    private static ushort Clut(int x, int y) => (ushort)(y * 64 + x / 16);

    private static TextureReplacement Solid(TextureBuildContext context, Rgba32 color, int scale = 1) => new(new(context.LogicalWidth * scale, context.LogicalHeight * scale, Enumerable.Repeat(color, context.LogicalWidth * context.LogicalHeight * scale * scale).ToArray()), "fixture:effect", context.LogicalWidth, context.LogicalHeight);

    private static InMemoryResourceSource Source() => new(new("fixture", GameDataEdition.Classic, ["classic-ffnx"]));

    private sealed class RecordingResolver(Func<TextureBuildContext, TextureReplacement?> resolve) : ITextureReplacementResolver
    {
        public List<TextureBuildContext> Contexts { get; } = [];

        public TextureReplacement? Resolve(TextureBuildContext context)
        {
            Contexts.Add(context);
            return resolve(context);
        }
    }
}
