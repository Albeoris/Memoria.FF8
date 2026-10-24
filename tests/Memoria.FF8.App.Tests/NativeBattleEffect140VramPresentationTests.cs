using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Rendering;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeBattleEffect140VramPresentationTests
{
    [Fact]
    public void Executed_scanline_copy_keeps_viewport_geometry_and_survives_json()
    {
        NativeBattleLogicalCommand quad = new(new("off_B6D070", 0, 0), 1, 0x28, [0x280000FF, Point(10, 10), Point(30, 10), Point(10, 30), Point(30, 30)], 1, [], null) { OrderingBucket = 50 };
        NativeBattleLogicalCommand copy = new(new("20DFAB8", 0, 0), 1, 0x80, [0x80000000, Point(0, 8), Point(320, 251), Point(320, 1)], 0, [], null) { OrderingBucket = 4385 };
        NativeBattleLogicalResolvedCommand drawn = new(quad, new(0), 1);
        NativeBattlePrimitiveFrame baseline = new(NativeBattleLogicalOrdering.Clear(), [drawn], []) { LastDescriptorPerspective = false, LastDepthState = NativeBattlePrimitiveDepthState.FromColdProcess(), LastColorState = NativeBattlePrimitiveColorState.FromColdProcess() };
        NativeBattlePrimitiveFrame frame = baseline with { LastCommands = [drawn, new(copy, new(0), 1)] };
        byte[] expected = new BattleRenderer(320, 216).RenderNativePrimitiveFrame(baseline, null).ToArray();
        Assert.Equal(new byte[] { 255, 0, 0, 255 }, expected.AsSpan(4 * (20 * 320 + 20), 4).ToArray());
        Assert.Equal(expected, new BattleRenderer(320, 216).RenderNativePrimitiveFrame(frame, null));
        NativeBattlePrimitiveFrame restored = JsonSerializer.Deserialize<NativeBattlePrimitiveFrame>(JsonSerializer.Serialize(frame))!;
        Assert.Equal(expected, new BattleRenderer(320, 216).RenderNativePrimitiveFrame(restored, null));
    }

    [Theory]
    [InlineData(2, 4)]
    [InlineData(1, 3)]
    public void Copy_requires_executed_first_pass_and_complete_payload(byte pass, int length)
    {
        uint[] words = [0x80000000, Point(0, 8), Point(320, 251), Point(320, 1)];
        NativeBattleLogicalCommand copy = new(new("20DFAB8", 0, 0), 1, 0x80, words.Take(length).ToImmutableArray(), 0, [], null);
        NativeBattlePrimitiveFrame frame = new(NativeBattleLogicalOrdering.Clear(), [new(copy, new(0), pass)], []);
        Assert.Throws<InvalidDataException>(() => new BattleRenderer(320, 216).RenderNativePrimitiveFrame(frame, null));
    }

    private static uint Point(int x, int y) => unchecked((ushort)x | (uint)(ushort)y << 16);
}
