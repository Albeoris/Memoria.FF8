using System.Collections.Immutable;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativePlayerJunctionStatTests
{
    private static BattleNativePlayerStatRules Rules(int slot, byte quantitySpell = 1)
    {
        byte[] character = new byte[36];
        for (int offset = 12; offset < 36; offset += 4)
        {
            character[offset] = 1;
            character[offset + 1] = 2;
            character[offset + 2] = 40;
            character[offset + 3] = 10;
        }
        byte[] weapon = new byte[12];
        weapon[7] = 100;
        weapon[8] = 8;
        byte[] magic = new byte[60];
        magic[23] = 8;
        for (int index = 1; index < 9; index++)
        {
            magic[23 + index] = (byte)(10 + index);
        }
        ImmutableArray<ImmutableArray<byte>> spells = Enumerable.Range(0, 57).Select(index => (index == 1 ? magic : new byte[60]).ToImmutableArray()).ToImmutableArray();
        ImmutableArray<ImmutableArray<byte>> passives = Enumerable.Range(0, 19).Select(_ => new byte[8].ToImmutableArray()).ToImmutableArray();
        NativeStatCurve curve = new(100, 0, 50, 255, 100);
        NativeStatModifier modifier = new(0, 9, 0);
        NativeStatDefinitions definitions = new(Enumerable.Repeat(curve, 11).ToImmutableArray(), spells.Select(spell => spell[23]).ToImmutableArray(), Enumerable.Repeat(modifier, 19).ToImmutableArray(), Enumerable.Repeat(curve, 16).ToImmutableArray(), Enumerable.Repeat(modifier, 9).ToImmutableArray());
        byte[] junctions = new byte[20];
        junctions[slot] = quantitySpell;
        return new BattleNativePlayerStatRules(0, 10, character.ToImmutableArray(), weapon.ToImmutableArray(), spells, passives, junctions.ToImmutableArray(), 0, definitions);
    }

    private static int[] Values(BattleNativePlayerStatistics stats)
    {
        return [stats.MaximumHp, stats.Strength, stats.Vitality, stats.Magic, stats.Spirit, stats.Speed, stats.Evasion, stats.Hit, stats.Luck];
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(6)]
    [InlineData(7)]
    [InlineData(8)]
    public void Each_stat_uses_its_kernel_coefficient_and_first_matching_stock(int slot)
    {
        BattleNativePlayerStatRules rules = Rules(slot);
        BattleNativeCharacterGrowth growth = new(0, new byte[6].ToImmutableArray());
        BattleNativePlayerStatistics baseline = rules.Project([], growth);
        BattleNativePlayerStatistics quarter = rules.Project([new NativeMagicStock(1, 25), new NativeMagicStock(1, 100)], growth);
        BattleNativePlayerStatistics full = rules.Project([new NativeMagicStock(1, 100)], growth);
        int coefficient = slot == 0 ? 8 : 10 + slot;
        Assert.Equal(Values(baseline)[slot] + coefficient * 25 / (slot == 0 ? 1 : 100), Values(quarter)[slot]);
        Assert.Equal(Values(baseline)[slot] + coefficient * (slot == 0 ? 100 : 1), Values(full)[slot]);
        Assert.Equal(Values(baseline), Values(rules.Project([new NativeMagicStock(1, 0)], growth)));
        Assert.Equal(Values(baseline), Values((rules with { MagicJunctions = new byte[20].ToImmutableArray() }).Project([new NativeMagicStock(1, 100)], growth)));
        if (slot == 5)
        {
            Assert.Equal(full.Speed / 4, full.Evasion);
        }
    }

    [Fact]
    public void Passive_selector_and_byte_overflow_apply_after_the_base_stat_cap()
    {
        BattleNativePlayerStatRules rules = Rules(1);
        byte[] strength = new byte[8];
        strength[5] = 1;
        strength[6] = 80;
        rules = rules with { PassiveRecords = rules.PassiveRecords.SetItem(0, strength.ToImmutableArray()).SetItem(1, strength.ToImmutableArray()), Abilities = 39 | 40u << 8 };
        BattleNativeCharacterGrowth growth = new(9999, Enumerable.Repeat((byte)255, 6).ToImmutableArray());
        BattleNativePlayerStatistics stats = rules.Project([new NativeMagicStock(1, 100)], growth);
        Assert.Equal(10, stats.Strength);
        Assert.Equal(255, stats.Vitality);
        Assert.Equal(9999, stats.MaximumHp);
        Assert.Equal(63, stats.Evasion);
    }
}
