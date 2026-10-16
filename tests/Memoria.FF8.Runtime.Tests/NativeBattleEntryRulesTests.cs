using System.Collections.Immutable;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleEntryRulesTests
{
    [Theory]
    [InlineData(0x800000u, 0)]
    [InlineData(0x200000u, 1)]
    [InlineData(0x400000u, 2)]
    [InlineData(0xE00000u, 0)]
    public void ForcedFormationFlagsSkipResourceAndRandomReads(uint flags, byte expected)
    {
        NativeBattleRandomState random = Random(0);
        NativeBattleRandomState before = random;
        Assert.Equal(expected, NativeBattleEntryRules.Resolve(flags, 0, null, ref random));
        Assert.Equal(before, random);
    }

    [Theory]
    [InlineData(0, 0, 0, 0, 1)]
    [InlineData(1, 0, 0, 4, 2)]
    [InlineData(2, 0, 0, 1, 2)]
    [InlineData(2, 1, 0, 0, 1)]
    [InlineData(1, 0, 1, 0, 1)]
    [InlineData(2, 0, 2, 0, 1)]
    [InlineData(1, 0, 4, 0, 1)]
    public void NativeThresholdsAndEnemyDatFlagsConsumeOnlyTheirReachedDraws(byte index, byte abilities, byte enemyFlags, byte expected, int draws)
    {
        NativeBattleRandomState random = Random(index);
        NativeBattleEntryEnemy[] enemies = Enumerable.Repeat(new NativeBattleEntryEnemy(0, enemyFlags), 4).ToArray();
        Assert.Equal(expected, NativeBattleEntryRules.Resolve(0, abilities, enemies, ref random));
        Assert.Equal((byte)(index + draws), random.Indexes[0]);
    }

    [Fact]
    public void KnockedOutSlotsSkipTheDatPointerButNonKoUnknownSlotsFail()
    {
        NativeBattleRandomState random = Random(0);
        NativeBattleEntryEnemy[] enemies = [new(0, 0), new(1, null), new(1, null), new(1, null)];
        Assert.Equal((byte)0, NativeBattleEntryRules.Resolve(0, 0, enemies, ref random));
        enemies[1] = new(0, null);
        Assert.Throws<NotSupportedException>(() => NativeBattleEntryRules.Resolve(0, 0, enemies, ref random));
    }

    private static NativeBattleRandomState Random(byte index)
    {
        return new(Enumerable.Repeat(index, 8).ToImmutableArray(), 0);
    }
}
