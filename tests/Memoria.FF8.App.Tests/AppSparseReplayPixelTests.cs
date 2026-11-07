using System.Collections.Immutable;
using System.Numerics;
using System.Reflection;
using Albeoris.Games.FF8.Imaging;
using Memoria.FF8.Rendering;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class AppSparseReplayPixelTests
{
    private static readonly NativeBattlePrimitiveCapabilities Capabilities = NativeBattlePrimitiveCapabilities.PrimaryRgba8();
    private static readonly NativeBattleEffectTextureState Textures = NativeBattleEffectTextureState.FromColdProcess();

    [Theory]
    [InlineData(320, 216, false)]
    [InlineData(1920, 1080, false)]
    [InlineData(853, 479, true)]
    [InlineData(159, 103, true)]
    public void Actual_replay_words_exactly_match_full_raster_for_all_selected_pixel_centres(int width, int height, bool expanded)
    {
        BattleSceneVisibleArea area = expanded ? new(-173, -91, 2731, 1847) : BattleSceneVisibleArea.Native;
        NativeBattlePreparedPrimitiveFrame frame = PatternFrame();
        NativeBattlePcUiFrame ui = Ui();
        NativeBattleSceneReplaySource source = new(frame, Textures, area) { UiFrame = ui };
        var pool = ImmutableDictionary<long, NativeBattleSceneReplaySource>.Empty.Add(1, source);
        ushort[] expected = FullWords(width, height, source, pool);
        ushort[] actual = ReplayWords(width, height, pool, 1);
        Assert.Equal(expected, actual);
        Assert.True(actual.Distinct().Count() > 32);
        Assert.Contains(actual, word => word != 0);
    }

    [Fact]
    public void Framebuffer_tail_disables_sparse_raster_and_preserves_actual_replay_words()
    {
        NativeBattlePreparedPrimitiveFrame frame = PatternFrame() with { LastPcRenderTail = new(1, new(0, 0, 320, 216)) };
        NativeBattleSceneReplaySource source = new(frame, Textures, BattleSceneVisibleArea.Native);
        var pool = ImmutableDictionary<long, NativeBattleSceneReplaySource>.Empty.Add(1, source);
        Assert.Equal(FullWords(853, 479, source, pool), ReplayWords(853, 479, pool, 1));
        BattleRenderer probe = new(853, 479);
        typeof(BattleRenderer).GetField("isSceneReplay", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(probe, true);
        typeof(BattleRenderer).GetMethod("ConfigureSceneReplaySampling", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(probe, [frame]);
        Assert.Null(typeof(BattleRenderer).GetField("sceneReplaySampleColumns", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(probe));
        Assert.Null(typeof(BattleRenderer).GetField("sceneReplaySampleRows", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(probe));
    }

    [Fact]
    public void Nested_replay_matches_an_independently_materialized_full_child_capture()
    {
        const int width = 853;
        const int height = 479;
        NativeBattleSceneReplaySource child = new(PatternFrame(), Textures, BattleSceneVisibleArea.Native);
        var childPool = ImmutableDictionary<long, NativeBattleSceneReplaySource>.Empty.Add(1, child);
        ushort[] childWords = FullWords(width, height, child, childPool);
        NativeBattlePreparedPrimitiveFrame parent = CopyFrame(1);
        NativeBattlePcTextureWindowPixels retained = parent.LastTextureSnapshots!.Value[0];
        byte[] materialized = retained.Data.ToArray();
        for (int y = 0; y < 216; y++)
        {
            for (int x = 0; x < 128; x++)
            {
                ushort word = childWords[y * 320 + x];
                int offset = 2 * (y * retained.Width + x);
                materialized[offset] = (byte)word;
                materialized[offset + 1] = (byte)(word >> 8);
            }
        }
        NativeBattlePreparedPrimitiveFrame reference = parent with { LastTextureSnapshots = [retained with { Data = materialized.ToImmutableArray(), SceneCopies = null }] };
        NativeBattleSceneReplaySource expectedParent = new(reference, Textures, BattleSceneVisibleArea.Native);
        var pool = childPool.Add(2, new(parent, Textures, BattleSceneVisibleArea.Native));
        Assert.Equal(FullWords(width, height, expectedParent, pool), ReplayWords(width, height, pool, 2));
    }

    [Fact]
    public void Missing_blend_contract_outside_the_sample_grid_still_fails_before_rasterization()
    {
        NativeBattleLogicalCommand tiny = new(new("missing-mode", 0, 0), 1, 0x28, [0x280000FF, 0, 1, 1u << 16, 1u | 1u << 16], 0, [], null) { OrderingBucket = 1 };
        NativeBattlePreparedPrimitiveFrame frame = Frame([new(tiny, new(null), 1)]);
        NativeBattleSceneReplaySource source = new(frame, Textures, BattleSceneVisibleArea.Native);
        var pool = ImmutableDictionary<long, NativeBattleSceneReplaySource>.Empty.Add(1, source);
        NotSupportedException full = Assert.Throws<NotSupportedException>(() => FullWords(1920, 1080, source, pool));
        TargetInvocationException sparse = Assert.Throws<TargetInvocationException>(() => ReplayWords(1920, 1080, pool, 1));
        Assert.IsType<NotSupportedException>(sparse.InnerException);
        Assert.Equal(full.Message, sparse.InnerException!.Message);
        Assert.Contains("executed ABR", full.Message);
    }

    [Fact]
    public void State_commands_do_not_disable_masks_and_unsampled_covered_pixels_remain_clear()
    {
        const int width = 960;
        const int height = 648;
        NativeBattlePreparedPrimitiveFrame frame = Frame([State(0xE1), State(0xE3), Quad(0x280000FF, 0, 0, 320, 216, 17)]);
        BattleRenderer sparse = new(width, height);
        typeof(BattleRenderer).GetField("isSceneReplay", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(sparse, true);
        typeof(BattleRenderer).GetMethod("ConfigureSceneReplaySampling", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(sparse, [frame]);
        Assert.NotNull(typeof(BattleRenderer).GetField("sceneReplaySampleColumns", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(sparse));
        Assert.NotNull(typeof(BattleRenderer).GetField("sceneReplaySampleRows", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(sparse));
        byte[] actual = sparse.RenderNativePrimitiveFrame(frame, Textures, capabilities: Capabilities);
        byte[] full = new BattleRenderer(width, height).RenderNativePrimitiveFrame(frame, Textures, capabilities: Capabilities);
        int selected = (1 * width + 1) * 4;
        Assert.Equal((byte)255, actual[selected]);
        Assert.Equal(full.AsSpan(selected, 4).ToArray(), actual.AsSpan(selected, 4).ToArray());
        Assert.Equal((byte)255, full[0]);
        Assert.Equal((byte)0, actual[0]);
        Assert.Equal((byte)255, actual[3]);
    }

    private static ushort[] FullWords(int width, int height, NativeBattleSceneReplaySource source, ImmutableDictionary<long, NativeBattleSceneReplaySource> pool)
    {
        BattleSceneVisibleArea area = source.VisibleArea;
        int projectionWidth = Math.Clamp((int)Math.Round(width * 2560 / (area.Right - area.Left)), 1, width);
        int projectionHeight = Math.Clamp((int)Math.Round(height * 1728 / (area.Bottom - area.Top)), 1, height);
        int projectionX = Math.Clamp((int)Math.Round(-area.Left * projectionWidth / 2560), 0, width - projectionWidth);
        int projectionY = Math.Clamp((int)Math.Round(-area.Top * projectionHeight / 1728), 0, height - projectionHeight);
        BattleRenderer renderer = new(width, height, new PixelBounds(projectionX, projectionY, projectionWidth, projectionHeight));
        byte[] rgba = renderer.RenderNativePrimitiveFrame(source.Frame with { SceneReplaySources = pool }, source.Textures, capabilities: Capabilities, uiFrame: source.UiFrame, uiTexture: ResolveUi);
        ushort[] words = new ushort[320 * 216];
        for (int y = 0; y < 216; y++)
        {
            int py = (int)(((2L * y + 1) * height) / 432);
            for (int x = 0; x < 320; x++)
            {
                int px = (int)(((2L * x + 1) * width) / 640);
                int offset = (py * width + px) * 4;
                words[y * 320 + x] = (ushort)((rgba[offset] >> 3) | (rgba[offset + 1] >> 3) << 5 | (rgba[offset + 2] >> 3) << 10);
            }
        }
        return words;
    }

    private static ushort[] ReplayWords(int width, int height, ImmutableDictionary<long, NativeBattleSceneReplaySource> pool, long id)
    {
        BattleRenderer renderer = new(width, height);
        typeof(BattleRenderer).GetField("sceneCopyUiTexture", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(renderer, (Func<int, int, bool, PixelImage>)ResolveUi);
        NativeBattlePreparedPrimitiveFrame frame = Frame([]) with { SceneReplaySources = pool };
        return (ushort[])typeof(BattleRenderer).GetMethod("ReplaySceneWords", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(renderer, [frame, id, Capabilities])!;
    }

    private static NativeBattlePreparedPrimitiveFrame PatternFrame()
    {
        List<NativeBattleLogicalResolvedCommand> commands = [];
        commands.Add(State(0xE1));
        commands.Add(State(0xE3));
        commands.Add(Quad(0x28102030, 16, 8, 304, 208, 100));
        commands.Add(new(new(new("gouraud", 0, 0), 1, 0x30, [0x300000FF, Point(25, 20), 0x0000FF00, Point(285, 35), 0x00FF0000, Point(60, 195)], 1, [], null) { OrderingBucket = 50 }, new(0), 1));
        for (int blend = 0; blend < 4; blend++)
        {
            NativeBattleLogicalResolvedCommand command = Quad(0x2A204060, 40 + blend * 20, 35 + blend * 20, 220 + blend * 20, 150 + blend * 10, 7);
            commands.Add(command with { Pass = 2, DrawMode = new((ushort)(blend << 5)), Command = command.Command with { PresentationActorId = blend == 1 ? "faded" : null } });
        }
        NativeBattleLogicalCommand textured = new(new("texture", 0, 0), 1, 0x2C, [0x2C808080, Point(100, 30), 0, Point(300, 25), 0x010000FF, Point(110, 200), 0x0000FF00, Point(310, 190), 0x0000FFFF], 1, [724, 900, 1100, 1400], null) { OrderingBucket = 11, PresentationActorId = "faded" };
        commands.Add(new(textured, TextureMode(0x100, 64), 1));
        commands.Add(Quad(0x28FFFFFF, 75, 45, 240, 180, 200));
        NativeBattleLogicalCommand line = new(new("line", 0, 0), 1, 0x50, [0x5000FFFF, Point(0, 215), 0x00FF00FF, Point(319, 0)], 1, [], null) { OrderingBucket = 0 };
        commands.Add(new(line, new(0), 1));
        byte[] pixels = new byte[256 * 256 * 2];
        for (int y = 0; y < 256; y++)
        {
            for (int x = 0; x < 256; x++)
            {
                ushort word = (ushort)((x & 31) | (y & 31) << 5 | ((x + y) & 31) << 10);
                int offset = 2 * (y * 256 + x);
                pixels[offset] = (byte)word;
                pixels[offset + 1] = (byte)(word >> 8);
            }
        }
        return Frame(commands.ToImmutableArray()) with { LastDescriptorPerspective = true, LastTextureSnapshots = [new(0, 0, 256, 256, pixels.ToImmutableArray())], ActorOpacities = ImmutableDictionary<string, float>.Empty.Add("faded", 0.55f) };
    }

    private static NativeBattleLogicalResolvedCommand Quad(uint color, int left, int top, int right, int bottom, int bucket)
    {
        NativeBattleLogicalCommand command = new(new("quad", checked((byte)bucket), 0), 1, (byte)(color >> 24), [color, Point(left, top), Point(right, top), Point(left, bottom), Point(right, bottom)], 1, [], null) { OrderingBucket = bucket };
        return new(command, new(0), 1);
    }

    private static NativeBattlePreparedPrimitiveFrame CopyFrame(long id)
    {
        NativeBattleSceneCapture copy = new(BattleSceneVisibleArea.Native, NativeBattleSceneCaptureKind.Copy) { X = 384, Y = 256, Height = 224, PaddingBottom = 8, ReplaySourceId = id };
        NativeBattlePcTextureWindowPixels pixels = new(384, 256, 256, 256, ImmutableArray.CreateRange(new byte[256 * 256 * 2])) { SceneCopies = [new(copy, 384, 256, 128, 224, 0, 0)] };
        NativeBattleLogicalCommand command = new(new("copy", 0, 0), 1, 0x2C, [0x2C808080, 0, 0, 320, 0x011600FF, 216u << 16, 0x0000FF00, 320u | 216u << 16, 0x0000FFFF], 1, [], null) { OrderingBucket = 17 };
        return Frame([new(command, TextureMode(0x116, 86), 1)]) with { LastTextureSnapshots = [pixels] };
    }

    private static NativeBattlePrimitiveDrawMode TextureMode(ushort page, int row) => new(page) { TextureWindowBits = 0, TextureSelection = new(row, 0, 0, 0, 256, 256, 1f / 256, 1f / 256, false, false), TexturePixelSnapshot = 0, SceneCopyPalette = [] };

    private static NativeBattleLogicalResolvedCommand State(byte opcode) => new(new(new("state", 0, 0), 1, opcode, [(uint)opcode << 24], 1, [], null), new(null), 1);

    private static NativeBattlePreparedPrimitiveFrame Frame(ImmutableArray<NativeBattleLogicalResolvedCommand> commands) => new(commands, []) { LastDescriptorPerspective = false, LastDepthState = NativeBattlePrimitiveDepthState.FromColdProcess(), LastColorState = NativeBattlePrimitiveColorState.FromColdProcess() };

    private static NativeBattlePcUiFrame Ui()
    {
        NativeBattlePcUiQuad opaque = UiQuad(0, 0, 40, 30);
        NativeBattlePcUiQuad semi = UiQuad(0, 170, 80, 216);
        return new([opaque], [new(semi.Viewport, 0, [semi])], [], 320, 216);
    }

    private static NativeBattlePcUiQuad UiQuad(float left, float top, float right, float bottom) => new(0, 0, 0xFFFFFFFF, [new(left, top, 0, 0, 0), new(left, bottom, 0, 0, 1), new(right, top, 0, 1, 0), new(right, bottom, 0, 1, 1)], new(0, 0, 320, 216), 0);

    private static PixelImage ResolveUi(int page, int palette, bool semi) => new(1, 1, [semi ? new(0, 255, 0, 127) : new Rgba32(255, 0, 255, 255)]);

    private static uint Point(int x, int y) => unchecked((ushort)x | (uint)(ushort)y << 16);
}
