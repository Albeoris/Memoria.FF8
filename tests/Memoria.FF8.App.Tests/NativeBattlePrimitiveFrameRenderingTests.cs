using System.Collections.Immutable;
using System.Buffers.Binary;
using Albeoris.Games.FF8.Imaging;
using Memoria.FF8.Assets;
using Memoria.FF8.Rendering;
using Memoria.FF8.Resources;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeBattlePrimitiveFrameRenderingTests
{
    [Fact]
    public void Raw_pixels_survive_a_held_final_frame_while_metadata_owner_and_catalog_reapply_replacements()
    {
        NativeBattleEffectTextureState state = Texture(31);
        NativeBattlePcTextureWindowPixels held = NativeBattlePcTextureWindowPixels.Capture(0, new(0, 0, 8, 8), state);
        NativeBattlePrimitiveFrame frame = Frame([RawCacheQuad()]) with { LastTextureSnapshots = [held] };
        TextureCapture capture = new();
        BattleRenderer renderer = new(320, 216) { TriangleRenderer = capture, PilotDiagnosticsEnabled = true };
        RawCacheTextureSource source = new();
        renderer.RenderNativePrimitiveFrame(frame, state, effectTextureSource: source);
        PixelImage native = Assert.Single(source.NativeImages);
        PixelImage firstFinal = capture.Texture!;
        Assert.NotSame(native, firstFinal);
        Assert.Equal(1, renderer.TextureDecoding!.Value.DecodeCount);
        Assert.Equal(64, renderer.TextureDecoding!.Value.DecodedPixels);
        Assert.Equal(1, renderer.TextureDecoding!.Value.RawNoCandidate);

        renderer.RenderNativePrimitiveFrame(frame, state, effectTextureSource: source);
        Assert.Single(source.NativeImages);
        Assert.Same(firstFinal, capture.Texture);
        Assert.Equal(0, renderer.TextureDecoding!.Value.DecodeCount);
        Assert.Equal(1, renderer.TextureDecoding!.Value.FullFinalPreviousHits);
        Assert.Equal(1, renderer.TextureDecoding!.Value.RawPromotions);

        NativeBattlePcTextureWindowPixels metadata = held with { Data = held.Data.ToArray().ToImmutableArray(), LiveTextureSources = [] };
        frame = frame with { LastTextureSnapshots = [metadata] };
        renderer.RenderNativePrimitiveFrame(frame, state, effectTextureSource: source);
        Assert.Equal(2, source.NativeImages.Count);
        Assert.Same(native, source.NativeImages[1]);
        Assert.Equal(1, renderer.TextureDecoding!.Value.RawPreviousHits);
        Assert.Equal(0, renderer.TextureDecoding!.Value.DecodeCount);
        Assert.NotSame(firstFinal, capture.Texture);
        Assert.NotEqual(firstFinal[0, 0], capture.Texture![0, 0]);

        frame = frame with { LastCommands = [RawCacheQuad(owner: "another-owner")] };
        renderer.RenderNativePrimitiveFrame(frame, state, effectTextureSource: source);
        Assert.Equal(3, source.NativeImages.Count);
        Assert.Same(native, source.NativeImages[2]);
        source.Generation++;
        renderer.RenderNativePrimitiveFrame(frame, state, effectTextureSource: source);
        Assert.Equal(4, source.NativeImages.Count);
        Assert.All(source.NativeImages, image => Assert.Same(native, image));
    }

    [Fact]
    public void Changed_retained_words_and_live_palette_do_not_reuse_native_RGBA()
    {
        NativeBattleEffectTextureState state = Texture(31).Upload45BD30(2, 256, 1, 1, [0xE0, 3]);
        NativeBattlePcTextureWindowPixels first = NativeBattlePcTextureWindowPixels.Capture(0, new(0, 0, 8, 8), state);
        byte[] words = first.Data.ToArray();
        words[0] = 0x22;
        NativeBattlePcTextureWindowPixels changed = first with { Data = words.ToImmutableArray() };
        NativeBattlePrimitiveFrame frame = Frame([RawCacheQuad()]) with { LastTextureSnapshots = [first] };
        BattleRenderer renderer = new(320, 216) { TriangleRenderer = new TextureCapture(), PilotDiagnosticsEnabled = true };
        RawCacheTextureSource source = new();
        renderer.RenderNativePrimitiveFrame(frame, state, effectTextureSource: source);
        renderer.RenderNativePrimitiveFrame(frame with { LastTextureSnapshots = [changed] }, state, effectTextureSource: source);
        Assert.NotSame(source.NativeImages[0], source.NativeImages[1]);
        Assert.NotEqual(source.NativeImages[0][0, 0], source.NativeImages[1][0, 0]);
        Assert.Equal(1, renderer.TextureDecoding!.Value.RawWordMismatches);
        Assert.Equal(0, renderer.TextureDecoding!.Value.RawPaletteMismatches);
        renderer.RenderNativePrimitiveFrame(frame, state, effectTextureSource: source);

        NativeBattleEffectTextureState palette = state.PublishVramSpan(2 * (256 * 1024 + 1), new byte[] { 0, 0x7C });
        renderer.RenderNativePrimitiveFrame(frame, palette, effectTextureSource: source);
        Assert.NotSame(source.NativeImages[0], source.NativeImages[3]);
        Assert.NotSame(source.NativeImages[1], source.NativeImages[3]);
        Assert.NotEqual(source.NativeImages[0][0, 0], source.NativeImages[3][0, 0]);
        Assert.Equal(1, renderer.TextureDecoding!.Value.RawPaletteMismatches);
        Assert.Equal(0, renderer.TextureDecoding!.Value.RawWordMismatches);
        Assert.Throws<InvalidDataException>(() => renderer.RenderNativePrimitiveFrame(frame with { LastTextureSnapshots = [first with { Data = [] }] }, palette, effectTextureSource: source));
    }

    [Fact]
    public void Same_frame_A_B_A_word_epochs_keep_their_exact_final_materials()
    {
        NativeBattleEffectTextureState state = Texture(31).Upload45BD30(2, 256, 1, 1, [0xE0, 3]);
        NativeBattlePcTextureWindowPixels first = NativeBattlePcTextureWindowPixels.Capture(0, new(0, 0, 8, 8), state);
        byte[] words = first.Data.ToArray();
        words[0] = 0x22;
        NativeBattlePcTextureWindowPixels second = first with { Data = words.ToImmutableArray() };
        NativeBattlePrimitiveFrame frame = Frame([RawCacheQuad(), RawCacheQuad(snapshot: 1), RawCacheQuad()]) with { LastTextureSnapshots = [first, second] };
        TextureCapture capture = new();
        BattleRenderer renderer = new(320, 216) { TriangleRenderer = capture, PilotDiagnosticsEnabled = true };
        RawCacheTextureSource source = new();
        renderer.RenderNativePrimitiveFrame(frame, state, effectTextureSource: source);
        Assert.Equal(2, source.NativeImages.Count);
        Assert.NotSame(source.NativeImages[0], source.NativeImages[1]);
        Assert.Same(source.FinalImages[0], capture.Texture);
        Assert.Equal(1, renderer.TextureDecoding!.Value.FullFinalCurrentHits);
    }

    [Fact]
    public void Crop_alternate_palette_and_retained_to_live_changes_are_raw_cache_misses()
    {
        NativeBattleEffectTextureState state = Texture(31);
        NativeBattlePcTextureWindowPixels small = NativeBattlePcTextureWindowPixels.Capture(0, new(0, 0, 8, 8), state);
        NativeBattlePcTextureWindowPixels large = NativeBattlePcTextureWindowPixels.Capture(0, new(0, 0, 16, 16), state);
        BattleRenderer renderer = new(320, 216) { TriangleRenderer = new TextureCapture() };
        RawCacheTextureSource source = new();
        renderer.RenderNativePrimitiveFrame(Frame([RawCacheQuad()]) with { LastTextureSnapshots = [small] }, state, effectTextureSource: source);
        renderer.RenderNativePrimitiveFrame(Frame([RawCacheQuad(size: 16)]) with { LastTextureSnapshots = [large] }, state, effectTextureSource: source);
        Assert.Equal(16, source.NativeImages[1].Width);
        Assert.NotSame(source.NativeImages[0], source.NativeImages[1]);

        NativeBattlePrimitiveFrame alternate = Frame([RawCacheQuad(page: 0x40)]) with { LastTextureSnapshots = [small] };
        renderer.RenderNativePrimitiveFrame(alternate, state, effectTextureSource: source);
        renderer.RenderNativePrimitiveFrame(alternate with { LastColorState = NativeBattlePrimitiveColorState.FromColdProcess() with { AlternatePaletteShift = 1 } }, state, effectTextureSource: source);
        Assert.NotSame(source.NativeImages[2], source.NativeImages[3]);
        renderer.RenderNativePrimitiveFrame(Frame([RawCacheQuad()]) with { LastTextureSnapshots = [small] }, state, effectTextureSource: source);
        renderer.RenderNativePrimitiveFrame(Frame([RawCacheQuad(snapshot: null)]), state, effectTextureSource: source);
        Assert.NotSame(source.NativeImages[4], source.NativeImages[5]);
    }

    [Fact]
    public void Direct_format_changes_clear_both_cache_generations_and_required_owners_still_fail()
    {
        NativeBattleEffectTextureState state = Texture(31);
        NativeBattlePrimitiveFrame frame = Frame([RawCacheQuad(page: 0x100, snapshot: null)]);
        BattleRenderer renderer = new(320, 216) { TriangleRenderer = new TextureCapture() };
        RawCacheTextureSource source = new();
        renderer.RenderNativePrimitiveFrame(frame, state, capabilities: NativeBattlePrimitiveCapabilities.PrimaryRgba8(), effectTextureSource: source);
        Assert.Throws<NotSupportedException>(() => renderer.RenderNativePrimitiveFrame(frame, null, capabilities: NativeBattlePrimitiveCapabilities.PrimaryRgba8(), effectTextureSource: source));
        renderer.RenderNativePrimitiveFrame(frame, state, capabilities: NativeBattlePrimitiveCapabilities.PrimaryRgba8() with { DirectTextureFormat = NativeBattlePcDirectTextureFormat.Rgb565 }, effectTextureSource: source);
        Assert.NotSame(source.NativeImages[0], source.NativeImages[1]);
        Assert.Equal(0, RawCacheCount(renderer, "previousNativeRawTexturePages"));
        Assert.Equal(0, RawCacheCount(renderer, "previousNativeTexturePages"));
        Assert.Throws<NotSupportedException>(() => renderer.RenderNativePrimitiveFrame(frame, state, effectTextureSource: source));
    }

    [Theory]
    [InlineData(8, 257, 256)]
    [InlineData(256, 256, 227)]
    public void Raw_candidates_have_entry_and_byte_bounds_and_clear_after_two_empty_frames(int size, int materials, int expectedCandidates)
    {
        NativeBattleEffectTextureState state = NativeBattleEffectTextureState.FromColdProcess();
        NativeBattlePcTextureWindowPixels held = NativeBattlePcTextureWindowPixels.Capture(0, new(0, 0, size, size), state);
        NativeBattleLogicalResolvedCommand[] commands = Enumerable.Range(0, materials).Select(index => RawCacheQuad(clut: checked((ushort)index), size: size)).ToArray();
        BattleRenderer renderer = new(320, 216) { TriangleRenderer = new TextureCapture(), PilotDiagnosticsEnabled = true };
        renderer.RenderNativePrimitiveFrame(Frame(commands.ToImmutableArray()) with { LastTextureSnapshots = [held] }, state);
        Assert.Equal(expectedCandidates, RawCacheCount(renderer, "nativeRawTexturePages"));
        Assert.Equal(materials - expectedCandidates, renderer.TextureDecoding!.Value.RawBudgetSkips);
        Assert.Equal(materials, renderer.TextureDecoding!.Value.DecodeCount);
        Assert.Equal((long)materials * size * size, renderer.TextureDecoding!.Value.DecodedPixels);
        int bytes = (int)typeof(BattleRenderer).GetField("nativeRawTextureBytes", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.GetValue(renderer)!;
        Assert.Equal(expectedCandidates * (size * size * 4 + held.Data.Length), bytes);
        Assert.InRange(bytes, 1, 64 * 1024 * 1024);
        renderer.RenderNativePrimitiveFrame(Frame([]), state);
        Assert.Equal(0, RawCacheCount(renderer, "nativeRawTexturePages"));
        Assert.Equal(expectedCandidates, RawCacheCount(renderer, "previousNativeRawTexturePages"));
        renderer.RenderNativePrimitiveFrame(Frame([]), state);
        Assert.Equal(0, RawCacheCount(renderer, "nativeRawTexturePages"));
        Assert.Equal(0, RawCacheCount(renderer, "previousNativeRawTexturePages"));
    }

    [Fact]
    public void Texture_decode_observation_is_opt_in_and_counts_same_frame_raw_reuse()
    {
        NativeBattleEffectTextureState state = Texture(31);
        NativeBattlePcTextureWindowPixels held = NativeBattlePcTextureWindowPixels.Capture(0, new(0, 0, 8, 8), state);
        NativeBattlePcTextureWindowPixels metadata = held with { LiveTextureSources = [] };
        NativeBattlePrimitiveFrame frame = Frame([RawCacheQuad(), RawCacheQuad(snapshot: 1)]) with { LastTextureSnapshots = [held, metadata] };
        BattleRenderer renderer = new(320, 216) { TriangleRenderer = new TextureCapture() };
        Assert.Null(renderer.TextureDecoding);
        renderer.RenderNativePrimitiveFrame(Frame([]), state);
        Assert.Null(renderer.TextureDecoding);
        renderer.PilotDiagnosticsEnabled = true;
        renderer.RenderNativePrimitiveFrame(frame, state);
        NativeBattleTextureDecodingObservation observation = renderer.TextureDecoding!.Value;
        Assert.Equal(1, observation.DecodeCount);
        Assert.Equal(1, observation.RawCurrentHits);
        Assert.Equal(1, observation.RawNoCandidate);
        Assert.Equal(1, observation.CurrentCandidates);
        renderer.RenderNativePrimitiveFrame(Frame([]), state);
        NativeBattleTextureDecodingObservation empty = renderer.TextureDecoding!.Value;
        Assert.Equal(observation.RenderOrdinal + 1, empty.RenderOrdinal);
        Assert.Equal(0, empty.DecodeCount);
        Assert.Equal(0, empty.RawCurrentHits);
        Assert.Equal(0, empty.CurrentCandidates);
        renderer.PilotDiagnosticsEnabled = false;
        Assert.Null(renderer.TextureDecoding);
    }

    private static int RawCacheCount(BattleRenderer renderer, string field)
    {
        return ((System.Collections.IDictionary)typeof(BattleRenderer).GetField(field, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.GetValue(renderer)!).Count;
    }

    private static NativeBattleLogicalResolvedCommand RawCacheQuad(ushort page = 0, ushort clut = 0x4000, int size = 8, int? snapshot = 0, string? owner = null)
    {
        uint window = size == 256 ? 0 : NativeBattleQuezacotlGeometryPackets.TextureWindowCommand(new(0, 0, checked((short)size), checked((short)size))) & 0xFFFFFF;
        NativeBattleLogicalCommand command = new(new("off_B6D070", 0, 0), 1, 0x2C, [0x2C808080, Point(10, 10), (uint)clut << 16, Point(30, 10), (uint)page << 16 | 7, Point(10, 30), 7u << 8, Point(30, 30), 7u << 8 | 7], 1, [], null) { OrderingBucket = 50, TextureSourceOwner = owner };
        return new(command, TexturedMode(page, window, clut) with { TexturePixelSnapshot = snapshot }, 1);
    }

    private sealed class RawCacheTextureSource : INativeBattleEffectTextureSource
    {
        public long Generation { get; set; }
        public List<PixelImage> NativeImages { get; } = [];
        public List<PixelImage> FinalImages { get; } = [];

        public PixelImage Apply(PixelImage original, NativeBattleEffectTextureState state, ushort page, ushort clut, int u, int v, int width, int height, NativeBattlePcTextureWindowPixels? retainedPixels = null, byte? alternatePaletteShift = null, NativeBattlePcDirectTextureFormat? directTextureFormat = null)
        {
            throw new InvalidOperationException("Expected the native image separately from the final material.");
        }

        public PixelImage ApplyDecoded(PixelImage original, PixelImage? decodedNative, NativeBattleEffectTextureState state, ushort page, ushort clut, int u, int v, int width, int height, NativeBattlePcTextureWindowPixels? retainedPixels = null, byte? alternatePaletteShift = null, NativeBattlePcDirectTextureFormat? directTextureFormat = null)
        {
            NativeImages.Add(decodedNative ?? original);
            PixelImage final = new(width, height, Enumerable.Repeat(new Rgba32((byte)(NativeImages.Count % 200 + 1), 0, 0, 127), width * height).ToArray());
            FinalImages.Add(final);
            return final;
        }
    }

    [Fact]
    public void HeldWindowReusesItsFrozenProvenanceAcrossUnrelatedLiveMetadataButChecksLivePaletteAndHeldFrames()
    {
        const ushort page = 0;
        const ushort clut = 0x4000;
        uint bits = NativeBattleQuezacotlGeometryPackets.TextureWindowCommand(new(0, 0, 8, 8)) & 0xFFFFFF;
        NativeBattleTextureOrigin origin = new("mag001_b.00", 0, NativeBattleTextureRole.Pixels, 4, 2, 8);
        NativeBattleEffectTextureState state = NativeBattleEffectTextureState.FromColdProcess().EnqueueRectangle505DF0(new(0, 0, 2, 8), new("fixture:pixels", new byte[32].ToImmutableArray()), origin: origin).Drain505D20().State;
        NativeBattlePcTextureWindowPixels held = NativeBattlePcTextureWindowPixels.Capture(0, new(0, 0, 8, 8), state);
        NativeBattleLogicalCommand command = new(new("off_B6D070", 0, 0), 1, 0x2C, [0x2C808080, Point(10, 10), (uint)clut << 16, Point(30, 10), 7, Point(10, 30), 7u << 8, Point(30, 30), 7u << 8 | 7], 1, [], null) { OrderingBucket = 50 };
        NativeBattlePrimitiveDrawMode mode = TexturedMode(page, bits, clut) with { TexturePixelSnapshot = 0 };
        NativeBattlePrimitiveFrame frame = Frame([new(command, mode, 1)]) with { LastTextureSnapshots = [held] };
        using InMemoryResourceSource resources = new(new("fixture", GameDataEdition.Classic, ["classic-ffnx"]));
        ResourceService service = new(resources);
        CountingHeldTextureResolver resolver = new();
        service.Prepare(resolver).Commit();
        using NativeBattleEffectTextureSource effectTextures = new(service);
        TextureCapture capture = new();
        BattleRenderer renderer = new(320, 216) { TriangleRenderer = capture };
        renderer.RenderNativePrimitiveFrame(frame, state, effectTextureSource: effectTextures);
        PixelImage initial = capture.Texture!;
        NativeBattleEffectTextureState unrelated = state with { LiveTextureSources = [], TextureRegistrations = [] };

        renderer.RenderNativePrimitiveFrame(frame, unrelated, effectTextureSource: effectTextures);

        Assert.Same(initial, capture.Texture);
        Assert.Equal(1, resolver.Calls);
        NativeBattleEffectTextureState palette = unrelated.PublishVramSpan(2 * (256 * 1024), new byte[] { 31, 0 });
        renderer.RenderNativePrimitiveFrame(frame, palette, effectTextureSource: effectTextures);
        Assert.Equal(2, resolver.Calls);
        Assert.NotSame(initial, capture.Texture);
        ImmutableArray<NativeBattleTextureRegistration> registrations = held.TextureRegistrations!.Value;
        NativeBattleTextureRegistration registration = registrations[0];
        NativeBattlePcTextureWindowPixels later = held with { TextureRegistrations = registrations.SetItem(0, registration with { CopyFrames = [new(0, 0, 2, 8)], CurrentFrame = 0, CopyHistoryKnown = true, PaletteOwnedSpans = [] }) };
        renderer.RenderNativePrimitiveFrame(frame with { LastTextureSnapshots = [later] }, palette, effectTextureSource: effectTextures);
        Assert.Equal(3, resolver.Calls);
        Assert.Equal(0, resolver.LastFrame);
    }

    private sealed class CountingHeldTextureResolver : ITextureReplacementResolver
    {
        public int Calls { get; private set; }
        public int LastFrame { get; private set; }

        public TextureReplacement? Resolve(TextureBuildContext context)
        {
            Calls++;
            LastFrame = Assert.Single(context.NativePage!.Members).Frame;
            return new(new(context.LogicalWidth, context.LogicalHeight, Enumerable.Repeat(new Rgba32((byte)(Calls * 20), 0, 0, 127), context.LogicalWidth * context.LogicalHeight).ToArray()), "fixture:held", context.LogicalWidth, context.LogicalHeight);
        }
    }

    [Fact]
    public void EffectSourceReceivesNativePixelsBeforeModelReplacementWithoutChangingReplacementPriority()
    {
        const ushort page = 0x80;
        NativeBattleLogicalCommand command = new(new("off_B6D070", 0, 0), 1, 0x2C, [0x2C808080, Point(10, 10), 0, Point(30, 10), (uint)page << 16 | 7, Point(10, 30), 7u << 8, Point(30, 30), 7u << 8 | 7], 1, [], null) { OrderingBucket = 50 };
        NativeBattlePrimitiveFrame frame = Frame([new(command, TexturedMode(page, 0), 1)]);
        NativeBattleEffectTextureState state = NativeBattleEffectTextureState.FromColdProcess();
        PixelImage modelImage = new(16, 16, Enumerable.Repeat(new Rgba32(90, 20, 30, 127), 256).ToArray());
        NativeBattleTextureReplacement replacement = new(page, 0, 0, 0, 8, 8, modelImage, 4, new byte[64].ToImmutableArray(), new byte[512].ToImmutableArray());
        DecodedNativeTextureSource source = new();
        BattleRenderer renderer = new(320, 216) { TriangleRenderer = new TextureCapture() };

        renderer.RenderNativePrimitiveFrame(frame, state, replacements: [replacement], effectTextureSource: source);

        Assert.NotNull(source.DecodedNative);
        Assert.NotNull(source.ModelFallback);
        Assert.NotSame(source.DecodedNative, source.ModelFallback);
        Assert.Equal((byte)0, source.DecodedNative[0, 0].R);
        Assert.Equal((byte)90, source.ModelFallback[0, 0].R);
        Assert.True(source.ModelFallback.Width > source.DecodedNative.Width);
        PixelImage initialNative = source.DecodedNative!;
        PixelImage nextModel = new(16, 16, Enumerable.Repeat(new Rgba32(10, 40, 50, 127), 256).ToArray());
        renderer.RenderNativePrimitiveFrame(frame, state, replacements: [replacement with { Image = nextModel }], effectTextureSource: source);
        Assert.Same(initialNative, source.DecodedNative);
        Assert.Equal((byte)10, source.ModelFallback![0, 0].R);
    }

    private sealed class DecodedNativeTextureSource : INativeBattleEffectTextureSource
    {
        public long Generation => 0;
        public PixelImage? DecodedNative { get; private set; }
        public PixelImage? ModelFallback { get; private set; }

        public PixelImage Apply(PixelImage original, NativeBattleEffectTextureState state, ushort page, ushort clut, int u, int v, int width, int height, NativeBattlePcTextureWindowPixels? retainedPixels = null, byte? alternatePaletteShift = null, NativeBattlePcDirectTextureFormat? directTextureFormat = null)
        {
            throw new InvalidOperationException("The renderer must preserve its native decode separately.");
        }

        public PixelImage ApplyDecoded(PixelImage original, PixelImage? decodedNative, NativeBattleEffectTextureState state, ushort page, ushort clut, int u, int v, int width, int height, NativeBattlePcTextureWindowPixels? retainedPixels = null, byte? alternatePaletteShift = null, NativeBattlePcDirectTextureFormat? directTextureFormat = null)
        {
            DecodedNative = decodedNative;
            ModelFallback = original;
            return original;
        }
    }

    [Fact]
    public void CopyFrameRegistrationChangesInvalidateTextureReuseWithoutChangingVramWords()
    {
        const ushort page = 0x80;
        NativeBattleLogicalCommand command = new(new("off_B6D070", 0, 0), 1, 0x2C, [0x2C808080, Point(10, 10), 0, Point(30, 10), (uint)page << 16, Point(10, 30), 0, Point(30, 30), 0], 1, [], null) { OrderingBucket = 50 };
        NativeBattlePrimitiveFrame frame = Frame([new(command, TexturedMode(page, 0), 1)]);
        NativeBattleTextureOrigin origin = new("mag001_b.00", 0, NativeBattleTextureRole.Pixels, 8, 64, 256);
        NativeBattleTextureRegistration registration = new(origin, 0, 0, 64, 256, [new(0, 0, 64, 256), new(64, 0, 64, 256)], 0);
        NativeBattleEffectTextureState first = NativeBattleEffectTextureState.FromColdProcess() with { LiveTextureSources = [], TextureRegistrations = [registration] };
        NativeBattleEffectTextureState second = first with { TextureRegistrations = [registration with { CurrentFrame = 1 }] };
        TextureCapture capture = new();
        BattleRenderer renderer = new(320, 216) { TriangleRenderer = capture };
        CopyFrameTextureSource source = new();
        renderer.RenderNativePrimitiveFrame(frame, first, effectTextureSource: source);
        PixelImage initial = capture.Texture!;
        int calls = source.Calls;
        renderer.RenderNativePrimitiveFrame(frame, first, effectTextureSource: source);
        Assert.Equal(calls, source.Calls);
        Assert.Same(initial, capture.Texture);
        renderer.RenderNativePrimitiveFrame(frame, second, effectTextureSource: source);
        Assert.True(source.Calls > calls);
        Assert.NotSame(initial, capture.Texture);
        Assert.NotEqual(initial[0, 0], capture.Texture![0, 0]);
    }

    private sealed class CopyFrameTextureSource : INativeBattleEffectTextureSource
    {
        public long Generation => 0;
        public int Calls { get; private set; }

        public PixelImage Apply(PixelImage original, NativeBattleEffectTextureState state, ushort page, ushort clut, int u, int v, int width, int height, NativeBattlePcTextureWindowPixels? retainedPixels = null, byte? alternatePaletteShift = null, NativeBattlePcDirectTextureFormat? directTextureFormat = null)
        {
            Calls++;
            byte color = (byte)(state.TextureRegistrations!.Value[0].CurrentFrame + 1);
            return new(width, height, Enumerable.Repeat(new Rgba32(color, 0, 0, 127), width * height).ToArray());
        }
    }

    [Fact]
    public void Prepared_commands_keep_their_texture_and_palette_epoch_after_later_publication()
    {
        const ushort page = 0x80;
        const ushort clut = 0x4000;
        NativeBattleLogicalCommand command = new(new("off_B6D070", 0, 0), 1, 0x2C, [0x2C808080, Point(10, 10), (uint)clut << 16, Point(30, 10), (uint)page << 16, Point(10, 30), 0, Point(30, 30), 0], 1, [], null) { OrderingBucket = 50 };
        NativeBattlePrimitiveFrame source = Frame([new(command, TexturedMode(page, 0), 1)]);
        NativeBattlePreparedPrimitiveFrame prepared = NativeBattlePreparedPrimitiveFrame.From(source);
        NativeBattleEffectTextureState first = NativeBattleEffectTextureState.FromColdProcess().Upload45BD30(0, 0, 1, 1, [1, 0]).Upload45BD30(0, 256, 2, 1, [0, 0, 0x1F, 0]);
        NativeBattleEffectTextureState next = first.Upload45BD30(1, 256, 1, 1, [0xE0, 3]);
        TextureCapture capture = new();
        BattleRenderer renderer = new(320, 216) { TriangleRenderer = capture };
        renderer.RenderNativePrimitiveFrame(prepared, first);
        Rgba32 originalPixel = capture.Texture![0, 0];
        renderer.RenderNativePrimitiveFrame(prepared, next);
        Assert.NotEqual(originalPixel, capture.Texture![0, 0]);
        renderer.RenderNativePrimitiveFrame(prepared, first);
        Assert.Equal(originalPixel, capture.Texture![0, 0]);
        Assert.Null(typeof(NativeBattlePreparedPrimitiveFrame).GetProperty("Pending"));
        Assert.Equal(source.LastCommands, prepared.LastCommands);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void FractionalIndependentImageAxesKeepNativePageAndWindowGeometry(bool windowed)
    {
        const ushort page = 0x80;
        uint window = windowed ? NativeBattleQuezacotlGeometryPackets.TextureWindowCommand(new(0, 0, 8, 8)) & 0xFFFFFF : 0;
        const uint uv = 2 | 3 << 8;
        NativeBattleLogicalCommand command = new(new("off_B6D070", 0, 0), 1, 0x2C, [0x2C808080, Point(10, 10), uv, Point(30, 10), (uint)page << 16 | uv, Point(10, 30), uv, Point(30, 30), uv], 1, [], null) { OrderingBucket = 50 };
        NativeBattlePrimitiveFrame frame = Frame([new(command, TexturedMode(page, window), 1)]);
        NativeBattleEffectTextureState state = NativeBattleEffectTextureState.FromColdProcess();
        TextureCapture capture = new();
        BattleRenderer renderer = new(320, 216) { TriangleRenderer = capture };
        renderer.RenderNativePrimitiveFrame(frame, state);
        System.Numerics.Vector2 originalUv = capture.Uv;
        PixelImage image = new(7, 11, Enumerable.Repeat(new Rgba32(37, 81, 127, 127), 77).ToArray());
        NativeBattleTextureReplacement replacement = new(page, 0, 0, 0, 8, 8, image, 4, new byte[64].ToImmutableArray(), new byte[512].ToImmutableArray());
        renderer.RenderNativePrimitiveFrame(frame, state, replacements: [replacement]);
        Assert.Equal(windowed ? 8 : 256, capture.Texture!.Width);
        Assert.Equal(windowed ? 11 : 352, capture.Texture.Height);
        Assert.Equal(image[0, 0], capture.Texture[0, 0]);
        Assert.Equal(originalUv, capture.Uv);
        renderer.RenderNativePrimitiveFrame(frame, state);
        Assert.Equal(windowed ? 8 : 256, capture.Texture!.Height);
        Assert.Equal(originalUv, capture.Uv);
    }

    [Theory]
    [InlineData(false, 1)]
    [InlineData(true, 1)]
    [InlineData(false, 2)]
    [InlineData(true, 2)]
    [InlineData(false, 3)]
    [InlineData(true, 3)]
    [InlineData(false, 4)]
    [InlineData(true, 6)]
    public void Native_hd_materials_preserve_normalized_window_UVs_and_source_checked_cache_reuse(bool windowed, int scale)
    {
        const ushort page = 0x80;
        int origin = windowed ? 8 : 0;
        uint window = windowed ? NativeBattleQuezacotlGeometryPackets.TextureWindowCommand(new(8, 0, 8, 8)) & 0xFFFFFF : 0;
        const uint uv = 2 | 3 << 8;
        NativeBattleLogicalCommand command = new(new("off_B6D070", 0, 0), 1, 0x2C, [0x2C808080, Point(10, 10), uv, Point(30, 10), (uint)page << 16 | uv, Point(10, 30), uv, Point(30, 30), uv], 1, [], null) { OrderingBucket = 50 };
        NativeBattlePrimitiveFrame frame = Frame([new(command, TexturedMode(page, window), 1)]);
        NativeBattleEffectTextureState state = NativeBattleEffectTextureState.FromColdProcess();
        TextureCapture capture = new();
        BattleRenderer renderer = new(320, 216) { TriangleRenderer = capture };
        renderer.RenderNativePrimitiveFrame(frame, state);
        System.Numerics.Vector2 originalUv = capture.Uv;
        PixelImage hd = new(8 * scale, 8 * scale, Enumerable.Repeat(new Rgba32(10, 20, 30, 255), 64 * scale * scale).ToArray());
        NativeBattleTextureReplacement replacement = new(page, 0, origin, 0, 8, 8, hd, 4, new byte[64].ToImmutableArray(), new byte[512].ToImmutableArray());
        renderer.RenderNativePrimitiveFrame(frame, state, replacements: [replacement]);
        PixelImage first = capture.Texture!;
        Assert.Equal((windowed ? 8 : 256) * scale, first.Width);
        Assert.Equal(originalUv, capture.Uv);
        Assert.Equal(hd[0, 0], first[0, 0]);
        renderer.RenderNativePrimitiveFrame(frame, state, replacements: [replacement]);
        Assert.Same(first, capture.Texture);
        NativeBattleEffectTextureState unrelated = CloneForTextureBranch(state).Upload45BD30(400, 400, 1, 1, [1, 2]);
        renderer.RenderNativePrimitiveFrame(frame, unrelated, replacements: [replacement]);
        Assert.Same(first, capture.Texture);
        NativeBattleEffectTextureState changedPalette = CloneForTextureBranch(state).Upload45BD30(0, 0, 1, 1, [1, 0]);
        renderer.RenderNativePrimitiveFrame(frame, changedPalette, replacements: [replacement]);
        Assert.NotSame(first, capture.Texture);
        Assert.Equal(originalUv, capture.Uv);
        renderer.RenderNativePrimitiveFrame(frame, state);
        Assert.Equal(windowed ? 8 : 256, capture.Texture!.Width);
        Assert.NotEqual(hd[0, 0], capture.Texture[0, 0]);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Native_hd_materials_preserve_rendered_checker_placement_in_full_pages_and_windows(bool windowed)
    {
        const ushort page = 0x80;
        const ushort clut = 0x4000;
        short origin = (short)(windowed ? 8 : 0);
        byte[] words = Enumerable.Repeat(new byte[] { 1, 1, 1, 1, 2, 2, 2, 2 }, 8).SelectMany(row => row).ToArray();
        byte[] palette = new byte[512];
        BinaryPrimitives.WriteUInt16LittleEndian(palette.AsSpan(2), 31);
        BinaryPrimitives.WriteUInt16LittleEndian(palette.AsSpan(4), 0x3E0);
        NativeBattleEffectTextureState state = NativeBattleEffectTextureState.FromColdProcess().Upload45BD30((short)(origin / 2), 0, 4, 8, words).Upload45BD30(0, 256, 256, 1, palette);
        PixelImage original = NativeBattleEffectTextureReader.DecodePcRegion(state, page, clut, origin, 0, 8, 8);
        PixelImage hd = new(24, 24);
        for (int row = 0; row < hd.Height; row++)
        {
            for (int column = 0; column < hd.Width; column++)
            {
                hd[column, row] = original[column / 3, row / 3];
            }
        }
        NativeBattleTextureReplacement replacement = new(page, clut, origin, 0, 8, 8, hd, 4, words.ToImmutableArray(), palette.ToImmutableArray());
        uint window = windowed ? NativeBattleQuezacotlGeometryPackets.TextureWindowCommand(new(8, 0, 8, 8)) & 0xFFFFFF : 0;
        uint u = windowed ? 0u : (uint)origin;
        NativeBattleLogicalCommand command = new(new("off_B6D070", 0, 0), 1, 0x2C, [0x2C808080, Point(10, 10), (uint)clut << 16 | u, Point(30, 10), (uint)page << 16 | u + 7, Point(10, 30), u | 7 << 8, Point(30, 30), u + 7 | 7 << 8], 1, [], null) { OrderingBucket = 50 };
        NativeBattlePrimitiveFrame frame = Frame([new(command, TexturedMode(page, window, clut), 1)]);
        byte[] baseline = new BattleRenderer(320, 216).RenderNativePrimitiveFrame(frame, state).ToArray();
        byte[] selected = new BattleRenderer(320, 216).RenderNativePrimitiveFrame(frame, state, replacements: [replacement]);
        Assert.NotEqual(baseline.AsSpan(4 * (20 * 320 + 12), 4).ToArray(), baseline.AsSpan(4 * (20 * 320 + 28), 4).ToArray());
        Assert.Equal(baseline, selected);
    }

    [Fact]
    public void Unrelated_VRAM_upload_reuses_decoded_material_but_palette_and_page_changes_replace_it()
    {
        TextureCapture capture = new();
        BattleRenderer renderer = new(320, 216) { TriangleRenderer = capture };
        NativeBattlePrimitiveFrame frame = Frame([TexturedQuad(1)]);
        NativeBattleEffectTextureState initial = Texture(31);
        renderer.RenderNativePrimitiveFrame(frame, initial);
        object first = capture.Texture!;
        NativeBattleEffectTextureState unrelated = CloneForTextureBranch(initial).Upload45BD30(400, 400, 1, 1, [9, 10]);
        renderer.RenderNativePrimitiveFrame(frame, unrelated);
        Assert.Same(first, capture.Texture);
        renderer.RenderNativePrimitiveFrame(frame, Texture(0x3E0));
        Assert.NotSame(first, capture.Texture);
        object changedPalette = capture.Texture!;
        NativeBattleEffectTextureState changedPage = CloneForTextureBranch(Texture(0x3E0)).Upload45BD30(0, 0, 1, 1, [0x22, 0x22]);
        renderer.RenderNativePrimitiveFrame(frame, changedPage);
        Assert.NotSame(changedPalette, capture.Texture);
        NativeBattleEffectTextureState invalid = changedPage with { Slots = [] };
        Assert.Throws<InvalidDataException>(() => renderer.RenderNativePrimitiveFrame(frame, invalid));
    }

    [Fact]
    public void Retained_pixel_words_keep_their_material_across_live_page_changes_but_changed_retained_words_replace_it()
    {
        TextureCapture capture = new();
        BattleRenderer renderer = new(320, 216) { TriangleRenderer = capture };
        NativeBattleEffectTextureState initial = Texture(31);
        NativeBattleLogicalResolvedCommand command = TexturedQuad(1);
        NativeBattlePcTextureWindowSelection selection = command.DrawMode.TextureSelection! with { UsesRetainedPixelWords = true };
        NativeBattlePrimitiveFrame frame = Frame([command with { DrawMode = command.DrawMode with { TextureSelection = selection } }]);
        NativeBattlePcTextureWindowCache cache = NativeBattlePcTextureWindowCache.Reset4647A0(null).Preprocess462F90(0x20, 0x4000).Prepare464BD0().Cache;
        NativeBattlePrimitiveTextureWindow rectangle = new(0, 0, 256, 256);
        NativeBattlePcTextureWindowRow row = cache.Rows[selection.Row];
        NativeBattlePcTextureWindowRegion region = row.Regions[0] with { CurrentPixels = false, RetainedPixels = NativeBattlePcTextureWindowPixels.Capture(selection.Row, rectangle, initial) };
        cache = cache with { Rows = cache.Rows.SetItem(selection.Row, row with { Regions = [region] }) };
        renderer.RenderNativePrimitiveFrame(frame, initial, textureWindowCache: cache);
        object first = capture.Texture!;
        NativeBattleEffectTextureState changedPage = CloneForTextureBranch(initial).Upload45BD30(0, 0, 1, 1, [0x22, 0x22]);
        renderer.RenderNativePrimitiveFrame(frame, changedPage, textureWindowCache: cache);
        Assert.Same(first, capture.Texture);
        NativeBattlePcTextureWindowRegion changed = region with { RetainedPixels = NativeBattlePcTextureWindowPixels.Capture(selection.Row, rectangle, changedPage) };
        cache = cache with { Rows = cache.Rows.SetItem(selection.Row, row with { Regions = [changed] }) };
        renderer.RenderNativePrimitiveFrame(frame, changedPage, textureWindowCache: cache);
        Assert.NotSame(first, capture.Texture);
        object retained = capture.Texture!;
        renderer.RenderNativePrimitiveFrame(frame, Texture(0x3E0), textureWindowCache: cache);
        Assert.NotSame(retained, capture.Texture);
    }

    private sealed class TextureCapture : ITriangleRenderer
    {
        public Albeoris.Games.FF8.Imaging.PixelImage? Texture { get; private set; }
        public System.Numerics.Vector2 Uv { get; private set; }

        public void Begin(int width, int height, System.Numerics.Vector4 clearColor, float clearDepth = 1)
        {
            Texture = null;
        }

        public void Draw(in TriangleVertex a, in TriangleVertex b, in TriangleVertex c, in TriangleMaterial material)
        {
            Texture = material.Texture;
            Uv = a.Uv;
        }

        public void End()
        {
        }
    }

    [Fact]
    public void Cached_materials_follow_VRAM_and_device_format_changes_on_the_same_renderer()
    {
        BattleRenderer renderer = new(960, 720);
        NativeBattlePrimitiveFrame indexed = Frame([TexturedQuad(1)]);
        NativeBattleEffectTextureState first = Texture(31);
        NativeBattleEffectTextureState second = Texture(0x3E0);
        foreach (NativeBattleEffectTextureState texture in new[] { first, first, second, second, first })
        {
            byte[] expected = new BattleRenderer(960, 720).RenderNativePrimitiveFrame(indexed, texture).ToArray();
            Assert.Equal(expected, renderer.RenderNativePrimitiveFrame(indexed, texture));
        }
        NativeBattleLogicalCommand command = new(new("off_B6D070", 0, 0), 1, 0x2C, [0x2C808080, Point(10, 10), 0, Point(30, 10), 0x01000000, Point(10, 30), 0, Point(30, 30), 0], 1, [], null) { OrderingBucket = 50 };
        NativeBattlePrimitiveFrame direct = Frame([new(command, TexturedMode(0x100), 1)]);
        NativeBattleEffectTextureState directTexture = NativeBattleEffectTextureState.FromColdProcess() with { VramRegions = [new(0, [0xE0, 3])] };
        foreach (NativeBattlePcDirectTextureFormat format in new[] { NativeBattlePcDirectTextureFormat.Rgb555, NativeBattlePcDirectTextureFormat.Rgb565, NativeBattlePcDirectTextureFormat.Rgb555 })
        {
            NativeBattlePrimitiveCapabilities capabilities = NativeBattlePrimitiveCapabilities.PrimaryRgba8() with { DirectTextureFormat = format };
            byte[] expected = new BattleRenderer(960, 720).RenderNativePrimitiveFrame(direct, directTexture, capabilities: capabilities).ToArray();
            Assert.Equal(expected, renderer.RenderNativePrimitiveFrame(direct, directTexture, capabilities: capabilities));
        }
    }

    [Fact]
    public void Parallel_stripes_preserve_opaque_depth_and_semi_submission_order_across_every_row()
    {
        NativeBattleLogicalResolvedCommand Cover(byte opcode, uint color, int bucket, byte pass)
        {
            NativeBattleLogicalResolvedCommand item = Quad(opcode, color, bucket, pass);
            return item with { Command = item.Command with { Words = [(uint)opcode << 24 | color, Point(0, 0), Point(320, 0), Point(0, 216), Point(320, 216)] } };
        }
        NativeBattlePrimitiveFrame frame = Frame([Cover(0x28, 0x000064, 100, 1), Cover(0x28, 0x640000, 200, 1), Cover(0x2A, 0xFFFFFF, 50, 2), Cover(0x2A, 0xFFFFFF, 50, 2)]);
        byte[] pixels = new BattleRenderer(960, 721).RenderNativePrimitiveFrame(frame, null);
        for (int index = 0; index < pixels.Length; index += 4)
        {
            Assert.Equal(new byte[] { 215, 190, 190, 255 }, pixels.AsSpan(index, 4).ToArray());
        }
    }

    [Fact]
    public void Empty_original_draw_environment_is_black_and_does_not_require_a_fabricated_camera()
    {
        BattleRenderer renderer = new(320, 216);
        byte[] pixels = renderer.RenderNativePrimitiveFrame(NativeBattlePrimitiveFrame.Clear(), null);
        Assert.All(Enumerable.Range(0, 320 * 216), index => Assert.Equal(new byte[] { 0, 0, 0, 255 }, pixels.AsSpan(index * 4, 4).ToArray()));
        Assert.False(renderer.TryProject(default, out _));
    }

    [Fact]
    public void Battle_entry_depth_blend_keeps_the_sky_bucket_behind_overlapping_nearer_geometry()
    {
        NativeBattleLogicalResolvedCommand mountain = Quad(0x28, 0x000064, 4122, 1);
        mountain = mountain with { Command = mountain.Command with { VertexPayload4 = [30000, 30000, 30000, 30000] } };
        NativeBattleLogicalResolvedCommand sky = Quad(0x28, 0x640000, 4382, 1);
        sky = sky with { Command = sky.Command with { VertexPayload4 = [29500, 29500, 29500, 29500] } };
        NativeBattlePrimitiveFrame frame = Frame([mountain, sky]) with { LastDescriptorPerspective = true };
        BattleRenderer renderer = new(320, 216);
        int sample = 4 * (20 * 320 + 20);
        Assert.Equal(new byte[] { 0, 0, 100, 255 }, renderer.RenderNativePrimitiveFrame(frame, null).AsSpan(sample, 4).ToArray());

        frame = frame with { LastDepthState = frame.LastDepthState!.SetBucketMix460B60(0.8f) };

        Assert.Equal(new byte[] { 100, 0, 0, 255 }, renderer.RenderNativePrimitiveFrame(frame, null).AsSpan(sample, 4).ToArray());
        Assert.Equal(new byte[] { 100, 0, 0, 255 }, renderer.RenderNativePrimitiveFrame(frame with { LastCommands = [sky, mountain] }, null).AsSpan(sample, 4).ToArray());
    }

    [Fact]
    public void Opaque_original_packets_use_less_equal_depth_independently_of_their_submission_order()
    {
        BattleRenderer renderer = new(320, 216);
        NativeBattleLogicalResolvedCommand near = Quad(0x28, 0x000064, 100, 1);
        NativeBattleLogicalResolvedCommand far = Quad(0x28, 0x640000, 200, 1);
        byte[] forward = renderer.RenderNativePrimitiveFrame(Frame([far, near]), null).ToArray();
        byte[] reverse = renderer.RenderNativePrimitiveFrame(Frame([near, far]), null).ToArray();
        Assert.Equal(forward, reverse);
        Assert.Equal(new byte[] { 100, 0, 0, 255 }, forward.AsSpan(4 * (20 * 320 + 20), 4).ToArray());
    }

    [Fact]
    public void Semi_quad_shared_diagonal_is_blended_once_and_does_not_write_depth()
    {
        BattleRenderer renderer = new(320, 216);
        NativeBattleLogicalResolvedCommand first = Quad(0x2A, 0xFFFFFF, 50, 2);
        NativeBattleLogicalResolvedCommand behind = Quad(0x2A, 0xFFFFFF, 100, 2);
        byte[] once = renderer.RenderNativePrimitiveFrame(Frame([first]), null).ToArray();
        byte[] twice = renderer.RenderNativePrimitiveFrame(Frame([first, behind]), null).ToArray();
        int diagonal = 4 * (20 * 320 + 20);
        Assert.Equal(127, once[diagonal]);
        Assert.InRange(twice[diagonal], (byte)190, (byte)191);
        Assert.Equal(once[diagonal], once[diagonal + 4]);
    }

    [Fact]
    public void Whole_pixel_and_eighth_pixel_proxy_coordinates_produce_the_same_original_polygon()
    {
        BattleRenderer renderer = new(320, 216);
        NativeBattleLogicalResolvedCommand whole = Quad(0x28, 0x00FF00, 100, 1);
        NativeBattleLogicalResolvedCommand eighth = whole with { Command = whole.Command with { Flags20 = 0, Words = [0x2800FF00, Point(80, 80), Point(240, 80), Point(80, 240), Point(240, 240)] } };
        Assert.Equal(renderer.RenderNativePrimitiveFrame(Frame([whole]), null).ToArray(), renderer.RenderNativePrimitiveFrame(Frame([eighth]), null));
    }

    [Fact]
    public void Old_snapshot_unknown_descriptor_or_depth_is_rejected_at_the_actual_polygon_read()
    {
        BattleRenderer renderer = new(320, 216);
        NativeBattleLogicalResolvedCommand quad = Quad(0x28, 0xFFFFFF, 100, 1);
        NativeBattlePrimitiveFrame frame = Frame([quad with { Command = quad.Command with { VertexPayload4 = [1000, 1000, 1000, 1000] } }]);
        Assert.Throws<NotSupportedException>(() => renderer.RenderNativePrimitiveFrame(frame with { LastDescriptorPerspective = null }, null));
        Assert.Throws<NotSupportedException>(() => renderer.RenderNativePrimitiveFrame(frame with { LastDepthState = null }, null));
        Assert.Throws<NotSupportedException>(() => renderer.RenderNativePrimitiveFrame(frame with { LastColorState = null }, null));
    }

    [Fact]
    public void Native_PC_semi_textures_are_admitted_by_the_command_pass_and_do_not_split_by_STP()
    {
        BattleRenderer renderer = new(320, 216);
        NativeBattleLogicalResolvedCommand background = Quad(0x28, 10, 100, 1);
        NativeBattleLogicalCommand textured = new(new("off_B6D070", 0, 20), 1, 0x2E, [0x2E808080, Point(10, 10), 0x40000000, Point(30, 10), 0x00200000, Point(10, 30), 0, Point(30, 30), 0], 1, [], null) { OrderingBucket = 50 };
        NativeBattleLogicalResolvedCommand effect = new(textured, TexturedMode(0x20, clut: 0x4000), 2);
        byte[] first = renderer.RenderNativePrimitiveFrame(Frame([background, effect]), Texture(3)).ToArray();
        byte[] second = renderer.RenderNativePrimitiveFrame(Frame([background, effect]), Texture(0x8003)).ToArray();
        Assert.Equal(first, second);
        Assert.Equal(34, first[4 * (20 * 320 + 20)]);
    }

    [Theory]
    [InlineData(0, 75)]
    [InlineData(1, 87)]
    public void Native_PC_ABR2_uses_its_alternate_palette_alpha_instead_of_subtracting_the_texel(byte shift, byte expectedRed)
    {
        BattleRenderer renderer = new(320, 216);
        NativeBattleLogicalResolvedCommand background = Quad(0x28, 100, 100, 1);
        NativeBattleLogicalCommand textured = new(new("off_B6D070", 0, 20), 1, 0x2E, [0x2E808080, Point(10, 10), 0x40000000, Point(30, 10), 0x00400000, Point(10, 30), 0, Point(30, 30), 0], 1, [], null) { OrderingBucket = 50 };
        NativeBattlePrimitiveFrame frame = Frame([background, new(textured, TexturedMode(0x40, clut: 0x4000), 2)]);
        frame = frame with { LastColorState = frame.LastColorState! with { AlternatePaletteShift = shift } };
        byte[] pixels = renderer.RenderNativePrimitiveFrame(frame, Texture(31));
        Assert.Equal(expectedRed, pixels[4 * (20 * 320 + 20)]);
        Assert.Throws<NotSupportedException>(() => renderer.RenderNativePrimitiveFrame(frame with { LastColorState = frame.LastColorState with { AlternatePaletteShift = null } }, Texture(31)));
    }

    [Theory]
    [InlineData(0, 62)]
    [InlineData(1, 124)]
    [InlineData(2, 97)]
    [InlineData(3, 102)]
    public void All_four_PC_material_modes_use_original_diffuse_palette_and_primary_blend_factors(int mode, byte expectedRed)
    {
        //Mode3 contributes24*64*127/(255*255), which is just below3 before the final integer framebuffer write.
        BattleRenderer renderer = new(320, 216);
        byte[] pixels = renderer.RenderNativePrimitiveFrame(Frame([Quad(0x28, 100, 100, 1), TexturedQuad(mode)]), Texture(3));
        Assert.Equal(expectedRed, pixels[4 * (20 * 320 + 20)]);
    }

    [Fact]
    public void Variable_TILE_uses_original_XY_WH_and_covers_each_pixel_of_its_rectangle_once()
    {
        BattleRenderer renderer = new(320, 216);
        NativeBattleLogicalCommand tile = new(new("off_B6D070", 0, 0), 1, 0x62, [0x62000064, Point(10, 20), Point(20, 30)], 1, [], null) { OrderingBucket = 17 };
        byte[] pixels = renderer.RenderNativePrimitiveFrame(Frame([new(tile, new(0), 2)]), null);
        Assert.Equal(49, pixels[4 * (20 * 320 + 10)]);
        Assert.Equal(49, pixels[4 * (49 * 320 + 29)]);
        Assert.Equal(0, pixels[4 * (19 * 320 + 10)]);
        Assert.Equal(0, pixels[4 * (20 * 320 + 30)]);
        Assert.Equal(600, Enumerable.Range(0, 320 * 216).Count(index => pixels[index * 4] != 0));
    }

    [Fact]
    public void Gouraud_triangle_interpolates_the_original_per_corner_PC_diffuse_colors()
    {
        BattleRenderer renderer = new(320, 216);
        NativeBattleLogicalCommand triangle = new(new("off_B6D070", 0, 0), 1, 0x30, [0x300000FF, Point(10, 10), 0x0000FF00, Point(30, 10), 0x00FF0000, Point(10, 30)], 1, [], null) { OrderingBucket = 100 };
        byte[] pixels = renderer.RenderNativePrimitiveFrame(Frame([new(triangle, new(0), 1)]), null);
        Assert.Equal(new byte[] { 114, 70, 70, 255 }, pixels.AsSpan(4 * (15 * 320 + 15), 4).ToArray());
    }

    [Fact]
    public void Material_sampling_rejects_unobserved_VRAM_or_inherited_ABR_at_the_actual_read()
    {
        BattleRenderer renderer = new(320, 216);
        Assert.Throws<NotSupportedException>(() => renderer.RenderNativePrimitiveFrame(Frame([TexturedQuad(1)]), null));
        NativeBattleLogicalResolvedCommand flat = Quad(0x2A, 0xFFFFFF, 100, 2);
        Assert.Throws<NotSupportedException>(() => renderer.RenderNativePrimitiveFrame(Frame([flat with { DrawMode = new(null) }]), null));
    }

    [Theory]
    [InlineData(0x03F8, 100)]
    [InlineData(0x03F9, 0)]
    public void Opaque_alternate_palette_respects_original_GREATEREQUAL112_alpha_test(ushort color, byte expectedRed)
    {
        BattleRenderer renderer = new(320, 216);
        NativeBattleLogicalResolvedCommand texture = TexturedQuad(2);
        texture = texture with { Pass = 1, Command = texture.Command with { Opcode = 0x2C, Words = texture.Command.Words.SetItem(0, 0x2C808080) } };
        byte[] pixels = renderer.RenderNativePrimitiveFrame(Frame([Quad(0x28, 100, 100, 1), texture]), Texture(color));
        Assert.Equal(expectedRed, pixels[4 * (20 * 320 + 20)]);
    }

    [Theory]
    [InlineData(NativeBattlePcDirectTextureFormat.Rgb555, 255)]
    [InlineData(NativeBattlePcDirectTextureFormat.Rgb565, 250)]
    public void Direct_texture_rasterization_uses_the_explicit_format_without_an_indexed_palette(NativeBattlePcDirectTextureFormat format, byte maximumGreen)
    {
        NativeBattleLogicalCommand command = new(new("off_B6D070", 0, 0), 1, 0x2C, [0x2C808080, Point(10, 10), 0, Point(30, 10), 0x01000000, Point(10, 30), 0, Point(30, 30), 0], 1, [], null) { OrderingBucket = 50 };
        NativeBattlePrimitiveFrame frame = Frame([new(command, TexturedMode(0x100), 1)]);
        NativeBattleEffectTextureState texture = NativeBattleEffectTextureState.FromColdProcess() with { VramRegions = [new(0, ImmutableArray.Create((byte)0xE0, (byte)0x03))] };
        BattleRenderer renderer = new(320, 216);
        Assert.Throws<NotSupportedException>(() => renderer.RenderNativePrimitiveFrame(frame, texture));
        byte[] pixels = renderer.RenderNativePrimitiveFrame(frame, texture, capabilities: NativeBattlePrimitiveCapabilities.PrimaryRgba8() with { DirectTextureFormat = format });
        Assert.Equal(new byte[] { 0, maximumGreen, 0, 255 }, pixels.AsSpan(4 * (20 * 320 + 20), 4).ToArray());
    }

    private static NativeBattlePrimitiveFrame Frame(ImmutableArray<NativeBattleLogicalResolvedCommand> commands) => new(NativeBattleLogicalOrdering.Clear(), commands, []) { LastDescriptorPerspective = false, LastDepthState = NativeBattlePrimitiveDepthState.FromColdProcess(), LastColorState = NativeBattlePrimitiveColorState.FromColdProcess() };

    [Theory]
    [InlineData(0x41310u, 255, 1, 159, 65)]
    [InlineData(0x400u, 255, 0, 15, 0)]
    [InlineData(0x400u, 248, 0, 8, 0)]
    public void E2_crops_then_wraps_raw_UV_without_subtracting_the_window_origin(uint window, byte u, byte v, int texelX, int texelY)
    {
        uint uv = (uint)(u | v << 8);
        NativeBattleLogicalCommand command = new(new("off_B6D070", 0, 0), 1, 0x2C, [0x2C808080, Point(10, 10), uv, Point(30, 10), 0x01000000 | uv, Point(10, 30), uv, Point(30, 30), uv], 1, [], null) { OrderingBucket = 50 };
        NativeBattlePrimitiveDrawMode mode = TexturedMode(0x100, window);
        NativeBattlePrimitiveFrame frame = Frame([new(command, mode, 1)]);
        NativeBattleEffectTextureState texture = NativeBattleEffectTextureState.FromColdProcess() with { VramRegions = [new(2 * (texelY * 1024 + texelX), ImmutableArray.Create((byte)31, (byte)0))] };
        BattleRenderer renderer = new(320, 216);
        byte[] pixels = renderer.RenderNativePrimitiveFrame(frame, texture, capabilities: NativeBattlePrimitiveCapabilities.PrimaryRgba8());
        Assert.Equal(new byte[] { 255, 0, 0, 255 }, pixels.AsSpan(4 * (20 * 320 + 20), 4).ToArray());
        Assert.Throws<NotSupportedException>(() => renderer.RenderNativePrimitiveFrame(Frame([new(command, mode with { TextureWindowBits = null }, 1)]), texture, capabilities: NativeBattlePrimitiveCapabilities.PrimaryRgba8()));
        Assert.Throws<NotSupportedException>(() => renderer.RenderNativePrimitiveFrame(Frame([new(command, mode with { TextureSelection = null }, 1)]), texture, capabilities: NativeBattlePrimitiveCapabilities.PrimaryRgba8()));
    }

    [Fact]
    public void Dirty_cached_crop_renders_its_retained_words_after_JSON_while_the_base_texture_refreshes()
    {
        const ushort page = 0x100;
        uint bits = NativeBattleQuezacotlGeometryPackets.TextureWindowCommand(new(8, 0, 8, 8)) & 0xFFFFFF;
        NativeBattleEffectTextureState original = NativeBattleEffectTextureState.FromColdProcess() with { VramRegions = [new(16, [31, 0])] };
        NativeBattleEffectTextureState current = original with { VramRegions = [new(16, [0xE0, 3])] };
        NativeBattlePcTextureWindowCache cache = NativeBattlePcTextureWindowCache.Reset4647A0(null).Preprocess462F90(page, 0).Prepare464BD0().Cache;
        cache = cache.Resolve465CE0(page, 0, bits).Cache.Flush465930().Invalidate464850(0, 0, 63, 255, original).Preprocess462F90(page, 0).Prepare464BD0().Cache;
        NativeBattlePcTextureWindowResolve resolved = cache.Resolve465CE0(page, 0, bits);
        cache = System.Text.Json.JsonSerializer.Deserialize<NativeBattlePcTextureWindowCache>(System.Text.Json.JsonSerializer.Serialize(resolved.Cache))!;
        NativeBattleLogicalCommand command = new(new("off_B6D070", 0, 0), 1, 0x2C, [0x2C808080, Point(10, 10), 0, Point(30, 10), 0x01000000, Point(10, 30), 0, Point(30, 30), 0], 1, [], null) { OrderingBucket = 50 };
        NativeBattlePrimitiveDrawMode mode = new(page) { TextureWindowBits = bits, TextureSelection = resolved.Selection };
        BattleRenderer renderer = new(320, 216);
        NativeBattlePrimitiveFrame frame = Frame([new(command, mode, 1)]);
        Assert.Throws<NotSupportedException>(() => renderer.RenderNativePrimitiveFrame(frame, current, capabilities: NativeBattlePrimitiveCapabilities.PrimaryRgba8()));
        byte[] pixels = renderer.RenderNativePrimitiveFrame(frame, current, capabilities: NativeBattlePrimitiveCapabilities.PrimaryRgba8(), textureWindowCache: cache);
        Assert.Equal(new byte[] { 255, 0, 0, 255 }, pixels.AsSpan(4 * (20 * 320 + 20), 4).ToArray());
    }

    private static NativeBattlePrimitiveDrawMode TexturedMode(ushort page, uint window = 0, ushort clut = 0)
    {
        NativeBattlePcTextureWindowCache cache = NativeBattlePcTextureWindowCache.Reset4647A0(null).Preprocess462F90(page, clut).Prepare464BD0().Cache;
        return new(page) { TextureWindowBits = window, TextureSelection = cache.Resolve465CE0(page, clut, window).Selection };
    }

    [Fact]
    public void Exhausted_window_cache_renders_the_full_page_using_the_retained_crop_scale()
    {
        const ushort page = 0x100;
        NativeBattlePcTextureWindowCache cache = NativeBattlePcTextureWindowCache.Reset4647A0(null).Preprocess462F90(page, 0).Prepare464BD0().Cache;
        for (int index = 1; index <= 7; index++)
        {
            uint bits = NativeBattleQuezacotlGeometryPackets.TextureWindowCommand(new((short)(8 * index), 0, 64, 32)) & 0xFFFFFF;
            cache = cache.Resolve465CE0(page, 0, bits).Cache;
        }
        uint missing = NativeBattleQuezacotlGeometryPackets.TextureWindowCommand(new(80, 0, 64, 32)) & 0xFFFFFF;
        NativeBattlePcTextureWindowSelection selection = cache.Resolve465CE0(page, 0, missing).Selection;
        Assert.True(selection.Fallback);
        NativeBattlePrimitiveDrawMode mode = new(page) { TextureWindowBits = missing, TextureSelection = selection };
        NativeBattleLogicalCommand command = new(new("off_B6D070", 0, 0), 1, 0x2C, [0x2C808080, Point(10, 10), 16, Point(30, 10), 0x01000010, Point(10, 30), 16, Point(30, 30), 16], 1, [], null) { OrderingBucket = 50 };
        NativeBattleEffectTextureState texture = NativeBattleEffectTextureState.FromColdProcess() with { VramRegions = [new(2 * 64, ImmutableArray.Create((byte)31, (byte)0))] };
        byte[] pixels = new BattleRenderer(320, 216).RenderNativePrimitiveFrame(Frame([new(command, mode, 1)]), texture, capabilities: NativeBattlePrimitiveCapabilities.PrimaryRgba8());
        Assert.Equal(new byte[] { 255, 0, 0, 255 }, pixels.AsSpan(4 * (20 * 320 + 20), 4).ToArray());
    }

    private static NativeBattleLogicalResolvedCommand Quad(byte opcode, uint color, int bucket, byte pass)
    {
        NativeBattleLogicalCommand command = new(new("off_B6D070", 0, 0), 1, opcode, [(uint)opcode << 24 | color, Point(10, 10), Point(30, 10), Point(10, 30), Point(30, 30)], 1, [], null) { OrderingBucket = bucket };
        return new(command, new(0), pass);
    }

    private static uint Point(int x, int y) => (ushort)x | unchecked((uint)y << 16);

    private static NativeBattleLogicalResolvedCommand TexturedQuad(int mode)
    {
        ushort page = checked((ushort)(mode << 5));
        NativeBattleLogicalCommand command = new(new("off_B6D070", 0, 20), 1, 0x2E, [0x2E808080, Point(10, 10), 0x40000000, Point(30, 10), (uint)page << 16, Point(10, 30), 0, Point(30, 30), 0], 1, [], null) { OrderingBucket = 50 };
        return new(command, TexturedMode(page, clut: 0x4000), 2);
    }

    private static NativeBattleEffectTextureState Texture(ushort color)
    {
        byte[] palette = new byte[4];
        BinaryPrimitives.WriteUInt16LittleEndian(palette.AsSpan(2), color);
        return NativeBattleEffectTextureState.FromColdProcess() with { VramRegions = [new(0, ImmutableArray.Create((byte)0x11, (byte)0x11)), new(2 * 256 * 1024, palette.ToImmutableArray())] };
    }
    // Divergent fixture histories own independent VRAM; production render inputs stay bounded.
    private static NativeBattleEffectTextureState CloneForTextureBranch(NativeBattleEffectTextureState texture) => texture with { VramRegions = [new(0, texture.ReadVram().ToImmutableArray())] };
}
