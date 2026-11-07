using System.Collections.Immutable;
using System.Numerics;
using System.Reflection;
using System.Runtime.CompilerServices;
using Albeoris.Games.FF8.Imaging;
using Memoria.FF8.Assets;
using Memoria.FF8.Rendering;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeBattleSceneReplayMemoTests
{
    private static readonly NativeBattleEffectTextureState OuterTextures = NativeBattleEffectTextureState.FromColdProcess();

    [Fact]
    public void Interpolated_renders_replay_the_same_completed_source_once_and_match_cold_pixels()
    {
        Fixture fixture = Create();
        CountingSource source = new(fixture.SourceTextures);
        Capture capture = new();
        BattleRenderer renderer = Renderer(capture);
        NativeBattlePreparedPresentation earlier = Presentation(fixture.Frame, 1);
        NativeBattlePreparedPresentation later = Presentation(fixture.Frame, 2);
        foreach (float alpha in new[] { 0.25f, 0.5f, 0.75f })
        {
            Assert.True(renderer.CanSampleNativePilotInterval(earlier, later));
            renderer.RenderNativePilotPrimitiveFrame(earlier, later, alpha, capabilities: NativeBattlePrimitiveCapabilities.PrimaryRgba8(), effectTextureSource: source);
            PixelImage warm = capture.Image!;
            Capture coldCapture = new();
            Renderer(coldCapture).RenderNativePilotPrimitiveFrame(earlier, later, alpha, capabilities: NativeBattlePrimitiveCapabilities.PrimaryRgba8(), effectTextureSource: new CountingSource(fixture.SourceTextures));
            Assert.Equal(coldCapture.Image!.Pixels, warm.Pixels);
            Assert.Equal(new Rgba32(255, 0, 0, 255), warm[100, 100]);
        }
        Assert.Equal(1, source.Replays);
    }

    [Fact]
    public void Pool_identity_source_replacement_capabilities_and_texture_epochs_invalidate_words()
    {
        Fixture fixture = Create();
        Capture capture = new();
        BattleRenderer renderer = Renderer(capture);
        CountingSource source = new(fixture.SourceTextures);
        Render(renderer, fixture.Frame, source);
        Render(renderer, fixture.Frame, source);
        Assert.Equal(1, source.Replays);
        var pool = fixture.Frame.SceneReplaySources!;
        NativeBattlePreparedPrimitiveFrame copiedPool = fixture.Frame with { SceneReplaySources = ImmutableDictionary<long, NativeBattleSceneReplaySource>.Empty.Add(1, pool[1]) };
        Render(renderer, copiedPool, source);
        Assert.Equal(2, source.Replays);
        NativeBattleSceneReplaySource green = pool[1] with { Frame = SolidFrame(0x2800FF00) };
        Render(renderer, fixture.Frame with { SceneReplaySources = pool.SetItem(1, green) }, source);
        Assert.Equal(new Rgba32(0, 255, 0, 255), capture.Image![100, 100]);
        Render(renderer, fixture.Frame, source);
        Assert.Equal(3, source.Replays);
        Render(renderer, fixture.Frame, source, NativeBattlePrimitiveCapabilities.PrimaryRgba8() with { GouraudAlpha = false });
        Assert.Equal(4, source.Replays);
        source.Generation++;
        source.Tint = new(0, 0, 255, 255);
        Render(renderer, fixture.Frame, source);
        Assert.Equal(5, source.Replays);
        Assert.Equal(new Rgba32(0, 0, 255, 255), capture.Image![100, 100]);
        CountingSource replacement = new(fixture.SourceTextures);
        Render(renderer, fixture.Frame, replacement);
        Assert.Equal(1, replacement.Replays);
    }

    [Fact]
    public void Populated_UI_and_nonempty_replacements_bypass_persistent_words()
    {
        Fixture fixture = Create();
        var pool = fixture.Frame.SceneReplaySources!;
        NativeBattlePcUiQuad quad = new(0, 0, 0xFFFFFFFF, [new(0, 0, 0, 0, 0), new(0, 216, 0, 0, 1), new(320, 0, 0, 1, 0), new(320, 216, 0, 1, 1)], new(0, 0, 320, 216), 0);
        NativeBattlePcUiFrame ui = new([], [new(quad.Viewport, 0, [quad])], [], 320, 216);
        NativeBattlePreparedPrimitiveFrame frame = fixture.Frame with { SceneReplaySources = pool.SetItem(1, pool[1] with { UiFrame = ui }) };
        Capture capture = new();
        BattleRenderer renderer = Renderer(capture);
        CountingSource source = new(fixture.SourceTextures);
        PixelImage icon = new(1, 1, [new(0, 255, 0, 255)]);
        Func<int, int, bool, PixelImage> resolve = (_, _, _) => icon;
        renderer.RenderNativePrimitiveFrame(frame, OuterTextures, capabilities: NativeBattlePrimitiveCapabilities.PrimaryRgba8(), uiTexture: resolve, effectTextureSource: source);
        Rgba32 before = capture.Image![100, 100];
        Assert.Equal(new Rgba32(0, 255, 0, 255), before);
        icon = new(1, 1, [new(0, 0, 255, 255)]);
        renderer.RenderNativePrimitiveFrame(frame, OuterTextures, capabilities: NativeBattlePrimitiveCapabilities.PrimaryRgba8(), uiTexture: resolve, effectTextureSource: source);
        Assert.Equal(new Rgba32(0, 0, 255, 255), capture.Image![100, 100]);
        Assert.Equal(2, source.Replays);
        NativeBattleTextureReplacement replacement = new(0, 0, 0, 0, 1, 1, icon, 1, [0, 0], []);
        renderer.RenderNativePrimitiveFrame(fixture.Frame, OuterTextures, capabilities: NativeBattlePrimitiveCapabilities.PrimaryRgba8(), replacements: [replacement], effectTextureSource: source);
        renderer.RenderNativePrimitiveFrame(fixture.Frame, OuterTextures, capabilities: NativeBattlePrimitiveCapabilities.PrimaryRgba8(), replacements: [replacement], effectTextureSource: source);
        Assert.Equal(4, source.Replays);
    }

    [Theory]
    [InlineData(64)]
    [InlineData(66)]
    public void Bounded_pools_cache_every_root_and_larger_pools_bypass_persistent_words(int count)
    {
        Fixture fixture = Create();
        var pool = Enumerable.Range(1, count).ToImmutableDictionary(id => (long)id, _ => fixture.Frame.SceneReplaySources![1]);
        Capture capture = new();
        BattleRenderer renderer = Renderer(capture);
        CountingSource source = new(fixture.SourceTextures);
        for (int id = 1; id <= count; id++)
        {
            Render(renderer, Consumer(pool, id), source);
        }
        Assert.Equal(count, source.Replays);
        object? memo = typeof(BattleRenderer).GetField("sceneReplayMemo", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(renderer);
        if (count > 64)
        {
            Assert.Null(memo);
            Render(renderer, Consumer(pool, 1), source);
            Assert.Equal(count + 1, source.Replays);
            return;
        }
        Assert.NotNull(memo);
        object roots = memo.GetType().GetField("Roots", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(memo)!;
        Assert.Equal(64, (int)roots.GetType().GetProperty("Count")!.GetValue(roots)!);
        long retainedBytes = 0;
        foreach (object entry in ((System.Collections.IDictionary)roots).Values)
        {
            ushort[] words = (ushort[])entry.GetType().GetField("Item1")!.GetValue(entry)!;
            retainedBytes += words.LongLength * sizeof(ushort);
        }
        Assert.Equal(64L * 320 * 216 * sizeof(ushort), retainedBytes);
        Render(renderer, Consumer(pool, 1), source);
        Assert.Equal(count, source.Replays);
    }

    [Fact]
    public void Three_roots_with_mixed_warm_and_cold_sources_match_independent_replays()
    {
        Fixture fixture = Create();
        NativeBattleSceneReplaySource first = fixture.Frame.SceneReplaySources![1];
        var pool = ImmutableDictionary<long, NativeBattleSceneReplaySource>.Empty.Add(1, first).Add(2, WithColor(first, 0x03E0)).Add(3, WithColor(first, 0x7C00));
        Capture capture = new();
        BattleRenderer renderer = Renderer(capture);
        CountingSource source = new(fixture.SourceTextures);
        Render(renderer, Consumer(pool, 1), source);
        Assert.Equal(1, source.Replays);
        foreach (long[] order in new long[][] { [1, 2, 3], [3, 1, 2] })
        {
            NativeBattlePreparedPrimitiveFrame frame = Combine(order.Select(id => Consumer(pool, id)).ToArray());
            Render(renderer, frame, source);
            Capture cold = new();
            Render(Renderer(cold), frame, new(fixture.SourceTextures));
            Assert.Equal(3, capture.Images.Count);
            Assert.Equal(cold.Images.Count, capture.Images.Count);
            for (int index = 0; index < capture.Images.Count; index++)
            {
                Assert.Equal(cold.Images[index].Pixels, capture.Images[index].Pixels);
                Rgba32 expected = order[index] == 1 ? new(255, 0, 0, 255) : order[index] == 2 ? new(0, 255, 0, 255) : new(0, 0, 255, 255);
                Assert.Equal(expected, capture.Images[index][100, 100]);
            }
            Assert.Equal(3, source.Replays);
        }
    }

    [Fact]
    public void Warm_roots_cannot_mask_missing_sources_cycles_or_excessive_dependency_depth()
    {
        Fixture fixture = Create();
        var pool = fixture.Frame.SceneReplaySources!.Add(2, new(Consumer(ImmutableDictionary<long, NativeBattleSceneReplaySource>.Empty, 2), fixture.SourceTextures, BattleSceneVisibleArea.Native));
        BattleRenderer renderer = Renderer(new());
        Render(renderer, Consumer(pool, 1), null);
        InvalidDataException missing = Assert.Throws<InvalidDataException>(() => Render(renderer, Consumer(pool, 3), null));
        Assert.Contains("missing retained source", missing.Message);
        Assert.Throws<InvalidDataException>(() => Render(renderer, Consumer(pool, 2), null));
        var deep = Enumerable.Range(1, 65).ToImmutableDictionary(id => (long)id, id => new NativeBattleSceneReplaySource(id == 65 ? SolidFrame(0x280000FF) : Consumer(ImmutableDictionary<long, NativeBattleSceneReplaySource>.Empty, id + 1), fixture.SourceTextures, BattleSceneVisibleArea.Native));
        Assert.Throws<InvalidDataException>(() => Render(renderer, Consumer(deep, 1), null));
    }

    private static NativeBattlePreparedPrimitiveFrame Combine(NativeBattlePreparedPrimitiveFrame[] frames)
    {
        return frames[0] with
        {
            LastCommands = frames.Select((frame, index) => frame.LastCommands[0] with { DrawMode = frame.LastCommands[0].DrawMode with { TexturePixelSnapshot = index } }).ToImmutableArray(),
            LastTextureSnapshots = frames.Select(frame => frame.LastTextureSnapshots!.Value[0]).ToImmutableArray()
        };
    }

    private static NativeBattleSceneReplaySource WithColor(NativeBattleSceneReplaySource source, ushort word)
    {
        NativeBattlePcTextureWindowPixels pixels = source.Frame.LastTextureSnapshots!.Value[0];
        byte[] bytes = pixels.Data.ToArray();
        for (int offset = 0; offset < bytes.Length; offset += 2)
        {
            bytes[offset] = (byte)word;
            bytes[offset + 1] = (byte)(word >> 8);
        }
        return source with { Frame = source.Frame with { LastTextureSnapshots = [pixels with { Data = bytes.ToImmutableArray() }] } };
    }

    [Fact]
    public void Large_pools_preserve_the_current_render_dependency_union_limit()
    {
        Fixture fixture = Create();
        var builder = ImmutableDictionary.CreateBuilder<long, NativeBattleSceneReplaySource>();
        for (int branch = 0; branch < 2; branch++)
        {
            long start = 1 + branch * 33;
            for (long id = start; id < start + 33; id++)
            {
                NativeBattlePreparedPrimitiveFrame frame = id == start + 32 ? SolidFrame(0x280000FF) : Consumer(ImmutableDictionary<long, NativeBattleSceneReplaySource>.Empty, id + 1);
                builder.Add(id, new(frame, fixture.SourceTextures, BattleSceneVisibleArea.Native));
            }
        }
        var pool = builder.ToImmutable();
        Capture capture = new();
        BattleRenderer renderer = Renderer(capture);
        Render(renderer, Consumer(pool, 1), null);
        Render(renderer, Consumer(pool, 34), null);
        NativeBattlePreparedPrimitiveFrame first = Consumer(pool, 1);
        NativeBattlePreparedPrimitiveFrame second = Consumer(pool, 34);
        NativeBattleLogicalResolvedCommand secondCommand = second.LastCommands[0] with { DrawMode = second.LastCommands[0].DrawMode with { TexturePixelSnapshot = 1 } };
        NativeBattlePreparedPrimitiveFrame both = first with { LastCommands = [first.LastCommands[0], secondCommand], LastTextureSnapshots = [first.LastTextureSnapshots!.Value[0], second.LastTextureSnapshots!.Value[0]] };
        Assert.Throws<InvalidDataException>(() => Render(renderer, both, null));
        Assert.Throws<InvalidDataException>(() => Render(Renderer(new()), both, null));
    }

    [Fact]
    public void The_memo_does_not_keep_its_source_pool_or_frames_alive()
    {
        BattleRenderer renderer = Renderer(new());
        WeakReference pool = PopulateWeak(renderer);
        for (int attempt = 0; attempt < 3 && pool.IsAlive; attempt++)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
        }
        Assert.False(pool.IsAlive);
        GC.KeepAlive(renderer);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference PopulateWeak(BattleRenderer renderer)
    {
        Fixture fixture = Create();
        Render(renderer, fixture.Frame, null);
        return new(fixture.Frame.SceneReplaySources!);
    }

    private sealed record Fixture(NativeBattlePreparedPrimitiveFrame Frame, NativeBattleEffectTextureState SourceTextures);

    private static Fixture Create()
    {
        NativeBattleEffectTextureState textures = NativeBattleEffectTextureState.FromColdProcess();
        NativeBattlePreparedPrimitiveFrame source = Consumer(ImmutableDictionary<long, NativeBattleSceneReplaySource>.Empty, 1);
        NativeBattlePcTextureWindowPixels pixels = source.LastTextureSnapshots!.Value[0] with { SceneCopies = null };
        byte[] data = pixels.Data.ToArray();
        for (int offset = 0; offset < data.Length; offset += 2)
        {
            data[offset] = 31;
            data[offset + 1] = 0;
        }
        source = source with { LastTextureSnapshots = [pixels with { Data = data.ToImmutableArray() }], LastCommands = [source.LastCommands[0] with { Command = source.LastCommands[0].Command with { PilotGeometry = null } }] };
        var pool = ImmutableDictionary<long, NativeBattleSceneReplaySource>.Empty.Add(1, new(source, textures, BattleSceneVisibleArea.Native));
        return new(Consumer(pool, 1), textures);
    }

    private static NativeBattlePreparedPrimitiveFrame Consumer(ImmutableDictionary<long, NativeBattleSceneReplaySource> pool, long id)
    {
        NativeBattleSceneCapture copy = new(BattleSceneVisibleArea.Native, NativeBattleSceneCaptureKind.Copy) { X = 384, Y = 256, Height = 224, PaddingBottom = 8, ReplaySourceId = id };
        NativeBattlePcTextureWindowPixels pixels = new(384, 256, 256, 256, ImmutableArray.CreateRange(new byte[256 * 256 * 2])) { SceneCopies = [new(copy, 384, 256, 128, 224, 0, 0)] };
        NativeBattleLogicalCommand command = new(new("consumer", 0, 0), 1, 0x2C, [0x2C808080, 0, 0, 320, 0x011600FF, 216u << 16, 0x0000FF00, 320u | 216u << 16, 0x0000FFFF], 1, [0, 0, 0, 0], null)
        {
            OrderingBucket = 17,
            PilotGeometry = new(new(2, 1, 0, 1, 0), new(0, 0, 100), new(2560, 0, 100), new(0, 1728, 100), new(2560, 1728, 100), 4) { ScreenSpace = true }
        };
        NativeBattlePrimitiveDrawMode mode = new(0x116) { TextureWindowBits = 0, TextureSelection = new(86, 0, 0, 0, 256, 256, 1f / 256, 1f / 256, false, false), TexturePixelSnapshot = 0, SceneCopyPalette = [] };
        return Frame(new(command, mode, 1)) with { LastTextureSnapshots = [pixels], SceneReplaySources = pool };
    }

    private static NativeBattlePreparedPrimitiveFrame SolidFrame(uint color)
    {
        NativeBattleLogicalCommand command = new(new("solid", 0, 0), 1, 0x28, [color, 0, 320, 216u << 16, 320u | 216u << 16], 1, [0, 0, 0, 0], null) { OrderingBucket = 17, ScreenCoverage = NativeBattleScreenCoverage.Full };
        return Frame(new(command, new(0), 1));
    }

    private static NativeBattlePreparedPrimitiveFrame Frame(NativeBattleLogicalResolvedCommand command) => new([command], []) { LastDescriptorPerspective = false, LastDepthState = NativeBattlePrimitiveDepthState.FromColdProcess(), LastColorState = NativeBattlePrimitiveColorState.FromColdProcess() };

    private static NativeBattlePreparedPresentation Presentation(NativeBattlePreparedPrimitiveFrame frame, long callback) => new(callback, callback, callback, false, frame, OuterTextures, null, null, null, [], null, null, [], null)
    {
        PilotEffect = 2,
        PilotGeneration = 1,
        PilotCamera = new(new(0, 0, 0), new(0, 0, 100), 100, 0),
        PilotProjection = new(NativeBattleCuragaMatrices.Identity8DD770(), 100, 160, 108)
    };

    private static BattleRenderer Renderer(Capture capture) => new(320, 216, new PixelBounds(0, 0, 320, 216)) { TriangleRenderer = capture };

    private static void Render(BattleRenderer renderer, NativeBattlePreparedPrimitiveFrame frame, CountingSource? source, NativeBattlePrimitiveCapabilities? capabilities = null) => renderer.RenderNativePrimitiveFrame(frame, OuterTextures, capabilities: capabilities ?? NativeBattlePrimitiveCapabilities.PrimaryRgba8(), effectTextureSource: source);

    private sealed class CountingSource(NativeBattleEffectTextureState watched) : INativeBattleEffectTextureSource
    {
        public long Generation { get; set; }
        internal int Replays { get; private set; }
        internal Rgba32? Tint { get; set; }

        public PixelImage Apply(PixelImage original, NativeBattleEffectTextureState state, ushort page, ushort clut, int u, int v, int width, int height, NativeBattlePcTextureWindowPixels? retainedPixels = null, byte? alternatePaletteShift = null, NativeBattlePcDirectTextureFormat? directTextureFormat = null)
        {
            if (ReferenceEquals(watched, state))
            {
                Replays++;
                if (Tint is { } tint)
                {
                    return new(original.Width, original.Height, Enumerable.Repeat(tint, original.Pixels.Length).ToArray());
                }
            }
            return original;
        }
    }

    private sealed class Capture : ITriangleRenderer
    {
        internal PixelImage? Image;
        internal List<PixelImage> Images { get; } = [];

        public void Begin(int width, int height, Vector4 clearColor, float clearDepth = 1)
        {
            Image = null;
            Images.Clear();
        }

        public void Draw(in TriangleVertex a, in TriangleVertex b, in TriangleVertex c, in TriangleMaterial material)
        {
            Image ??= material.Texture;
            if (material.Texture is { } texture && (Images.Count == 0 || !ReferenceEquals(Images[^1], texture)))
            {
                Images.Add(texture);
            }
        }

        public void End()
        {
        }
    }
}
