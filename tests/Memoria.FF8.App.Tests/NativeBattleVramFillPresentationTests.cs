using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Rendering;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeBattleVramFillPresentationTests
{
    [Theory]
    [InlineData(0, 0)]
    [InlineData(576, 256)]
    [InlineData(640, 240)]
    public void Already_executed_absolute_VRAM_fill_does_not_clear_or_rasterize_over_viewport(int x, int y)
    {
        NativeBattleLogicalCommand quad = new(new("off_B6D070", 0, 0), 1, 0x28, [0x280000FF, Point(10, 10), Point(30, 10), Point(10, 30), Point(30, 30)], 1, [], null) { OrderingBucket = 50 };
        NativeBattleLogicalCommand fill = new(new("20DFAB8", 0, 0), 1, 0x02, [0x02FF0000, Point(x, y), Point(320, 216)], 0, [0, 0, 0, 0], null) { OrderingBucket = 4385 };
        NativeBattleLogicalResolvedCommand drawn = new(quad, new(0), 1);
        NativeBattlePrimitiveFrame baseline = new(NativeBattleLogicalOrdering.Clear(), [drawn], []) { LastDescriptorPerspective = false, LastDepthState = NativeBattlePrimitiveDepthState.FromColdProcess(), LastColorState = NativeBattlePrimitiveColorState.FromColdProcess() };
        NativeBattlePrimitiveFrame frame = baseline with { LastCommands = [drawn, new(fill, new(0), 1)] };
        byte[] expected = new BattleRenderer(320, 216).RenderNativePrimitiveFrame(baseline, null).ToArray();
        Assert.Equal(new byte[] { 255, 0, 0, 255 }, expected.AsSpan(4 * (20 * 320 + 20), 4).ToArray());
        Assert.Equal(expected, new BattleRenderer(320, 216).RenderNativePrimitiveFrame(frame, null));
        NativeBattlePrimitiveFrame restored = JsonSerializer.Deserialize<NativeBattlePrimitiveFrame>(JsonSerializer.Serialize(frame))!;
        Assert.Equal(expected, new BattleRenderer(320, 216).RenderNativePrimitiveFrame(restored, null));
    }

    [Theory]
    [InlineData(2, 3)]
    [InlineData(1, 2)]
    public void Unexecuted_pass_or_incomplete_VRAM_command_is_rejected(byte pass, int length)
    {
        uint[] words = [0x02000000, 0x1000240, 0xD80140];
        NativeBattleLogicalCommand fill = new(new("20DFAB8", 0, 0), 1, 0x02, words.Take(length).ToImmutableArray(), 0, [], null);
        NativeBattlePrimitiveFrame frame = new(NativeBattleLogicalOrdering.Clear(), [new(fill, new(0), pass)], []);
        Assert.Throws<InvalidDataException>(() => new BattleRenderer(320, 216).RenderNativePrimitiveFrame(frame, null));
    }

    private static uint Point(int x, int y) => unchecked((ushort)x | (uint)(ushort)y << 16);
}
