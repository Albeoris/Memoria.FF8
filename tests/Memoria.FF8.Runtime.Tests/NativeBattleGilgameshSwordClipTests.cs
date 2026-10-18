using System.Collections.Immutable;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleGilgameshSwordClipTests
{
    [Theory]
    [InlineData(1, true, "0,3,2")]
    [InlineData(1, false, "3,1,2")]
    [InlineData(2, true, "0,1,4")]
    [InlineData(2, false, "0,4,2")]
    [InlineData(3, true, "3,1,4")]
    [InlineData(3, false, "0,3,2,4")]
    [InlineData(4, true, "5,1,2")]
    [InlineData(4, false, "0,1,5")]
    [InlineData(5, true, "0,3,5")]
    [InlineData(5, false, "5,3,2,1")]
    [InlineData(6, true, "5,4,2")]
    [InlineData(6, false, "0,1,5,4")]
    public void Dispatch_uses_exact_native_row_and_signed_distance_branch(int mask, bool positive, string expected)
    {
        ImmutableArray<NativeBattleGilgameshSwordClipVertex> points = Enumerable.Range(0, 6).Select(index => new NativeBattleGilgameshSwordClipVertex(new((short)index, 0, 0), (ushort)index)).ToImmutableArray();
        ImmutableArray<int> distances = Enumerable.Repeat(positive ? 1 : -1, 3).ToImmutableArray();
        Assert.Equal(expected.Split(',').Select(int.Parse).ToArray(), NativeBattleGilgameshSwordClip.Select595B40(mask, points, distances).Select(point => (int)point.Uv).ToArray());
        Assert.Empty(NativeBattleGilgameshSwordClip.Select595B40(7, points, distances));
    }

    [Fact]
    public void Zero_edge_asymmetry_keeps_native_degenerate_triple_and_zero_or_negative_mask0_emits_nothing()
    {
        NativeBattleGilgameshSwordClipVertex first = new(new(0, 0, 0), 0x1001);
        NativeBattleGilgameshSwordClipVertex second = new(new(10, 1, 0), 0x2002);
        NativeBattleGilgameshSwordClipVertex third = new(new(0, 0, 10), 0x3003);
        Assert.Equal(new ushort[] { 0x1001, 0x3003, 0x3003 }, NativeBattleGilgameshSwordClip.Clip595B40(first, second, third).Select(point => point.Uv).ToArray());
        Assert.Empty(NativeBattleGilgameshSwordClip.Clip595B40(first, second with { Position = new(10, 0, 0) }, third));
        Assert.Empty(NativeBattleGilgameshSwordClip.Clip595B40(first with { Position = new(0, 1, 0) }, second, third with { Position = new(0, 1, 10) }));
        Assert.Equal(short.MaxValue, NativeBattleGilgameshSwordClip.Distance(new(new(0, short.MinValue, 0), 0)));
    }

    [Fact]
    public void Intersection_preserves_low32_products_shifted_V_and_U_carry_into_high_byte()
    {
        NativeBattleGilgameshSwordClipVertex result = NativeBattleGilgameshSwordClip.Intersection58F650(new(new(32767, 30000, -32768), 0x0101), new(new(-32768, 30001, 32767), 0xFEFE), new(0, -4096, 0), 0);
        Assert.Equal(new NativeBattleVector(2767, 0, -2768), result.Position);
        Assert.Equal((ushort)0xBF91, result.Uv);
    }

    [Fact]
    public void Parallel_denominator_substitutes_one_but_signed_IDIV_overflow_stays_strict()
    {
        NativeBattleGilgameshSwordClipVertex result = NativeBattleGilgameshSwordClip.Intersection58F650(new(new(0, 1, 0), 0), new(new(10, 1, 0), 0xFFFF), new(0, -4096, 0), 0);
        Assert.Equal(new NativeBattleVector(10, 1, 0), result.Position);
        Assert.Equal(ushort.MaxValue, result.Uv);
        Assert.Throws<OverflowException>(() => NativeBattleGilgameshSwordClip.Intersection58F650(new(new(short.MinValue, short.MinValue, 0), 0), new(new(short.MinValue, short.MinValue, 1), 0), new(short.MinValue, short.MinValue, -4096), 0));
    }
}
