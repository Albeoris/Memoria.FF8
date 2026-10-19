using System.Collections.Immutable;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleCinematicMonsterVerticesTests
{
    [Fact]
    public void WobbleUsesHeightWordBitsAndSeparateCosineBlockWithWrappedCoordinates()
    {
        ImmutableArray<short> table = NativeBattleCinematicMonsterVertices.WobbleTable(300, 0, 1024);
        Assert.Equal((short)0, table[0]);
        Assert.Equal((short)300, table[1]);
        Assert.Equal((short)300, table[512]);
        Assert.Equal((short)0, table[513]);
        Assert.Equal(new NativeBattleVector(unchecked((short)33000), 4, 32700), NativeBattleCinematicMonsterVertices.Wobble(new(32700, 4, 32700), table));
        Assert.Equal(NativeBattleCinematicMonsterVertices.Wobble(new(7, 1020, 20), table) with { Y = -4 }, NativeBattleCinematicMonsterVertices.Wobble(new(7, -4, 20), table));
    }

    [Fact]
    public void GlowDistancePreservesTheTwoTruncatingSquareRoots()
    {
        Assert.Equal((ushort)1, NativeBattleCinematicMonsterVertices.Distance(new(1, 1, 1, 0), new(0, 0, 0)));
        Assert.Equal((ushort)13, NativeBattleCinematicMonsterVertices.Distance(new(3, 4, 12, 0), new(0, 0, 0)));
    }

    [Fact]
    public void GlowRetainsSignedDistanceAndPhaseAndOnlyUpperClampsItsResult()
    {
        ImmutableArray<uint> ramp = Enumerable.Repeat(0x00FFFFFFu, 256).ToImmutableArray().SetItem(255, 0x00000080);
        Assert.Equal(0u, NativeBattleCinematicMonsterVertices.Glow(10, 10, 256, 0, 0x00808080, new(0, 0, 0), ramp));
        Assert.Equal(0x00FFFFFFu, NativeBattleCinematicMonsterVertices.Glow(0, 10, 256, 0, 0x00808080, new(0, 0, 0), ramp));
        Assert.Equal(0x00808000u, NativeBattleCinematicMonsterVertices.Glow(0, 10, -256, -1, 0x00808080, new(0, 0, 0), ramp));
        Assert.Equal(0x00808080u, NativeBattleCinematicMonsterVertices.Glow(5, 10, 0, 0, 0x00808080, new(0, 0, 0), ramp));
        Assert.Throws<InvalidDataException>(() => NativeBattleCinematicMonsterVertices.Glow(ushort.MaxValue, 0, 1, 0, 0, new(0, 0, 0), ramp));
    }

    [Fact]
    public void GlowClutExpansionDiscardsTheSourceHighBitAndRetainsFiveBitChannels()
    {
        ushort[] source = new ushort[256];
        source[1] = 0xFFFF;
        source[2] = 0x801F;
        ImmutableArray<uint> ramp = NativeBattleCinematicMonsterVertices.Palette(source);
        Assert.Equal(0x00F8F8F8u, ramp[1]);
        Assert.Equal(0x000000F8u, ramp[2]);
    }
}
