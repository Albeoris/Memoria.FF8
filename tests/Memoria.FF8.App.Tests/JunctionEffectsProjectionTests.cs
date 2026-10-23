using System.Collections.Immutable;
using Memoria.FF8.GameUi;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class JunctionEffectsProjectionTests
{
    [Theory]
    [InlineData(0, 0, 0)]
    [InlineData(25, 20, 72)]
    [InlineData(100, 80, 200)]
    public void ElementProjectionUsesStockScalingMasksAndAbsorptionCap(byte quantity, int attack, int defense)
    {
        BattleNativePlayerStatRules rules = Rules();
        MenuJunctionEffects effects = Project(rules, quantity);
        Assert.Equal(8, effects.Elements.Count);
        Assert.Equal(new MenuJunctionPercentage("Fire", attack, defense), effects.Elements[0]);
        Assert.Equal(new MenuJunctionPercentage("Holy", attack, defense), effects.Elements[7]);
        Assert.All(effects.Elements.Skip(1).Take(6), value =>
        {
            Assert.Equal(0, value.Attack);
            Assert.Equal(0, value.Defense);
        });
    }

    [Fact]
    public void StatusProjectionMapsAllThirteenJunctionBitsToTheirActualResistances()
    {
        BattleNativePlayerStatRules rules = Rules();
        for (int bit = 0; bit < 13; bit++)
        {
            byte[] spell = rules.MagicRecords[1].ToArray();
            spell[38] = spell[40] = (byte)(1 << bit);
            spell[39] = spell[41] = (byte)((1 << bit) >> 8);
            MenuJunctionEffects effects = Project(rules with { MagicRecords = rules.MagicRecords.SetItem(1, spell.ToImmutableArray()) }, 25);
            Assert.Equal(13, effects.Statuses.Count);
            Assert.Equal(15, effects.Statuses[bit].Attack);
            Assert.Equal(40, effects.Statuses[bit].Defense);
            Assert.All(effects.Statuses.Where((_, index) => index != bit), value =>
            {
                Assert.Equal(0, value.Attack);
                Assert.Equal(0, value.Defense);
            });
        }
        Assert.Equal(new[] { "Curse", "Confuse", "Drain" }, Project(rules, 100).Statuses.Skip(10).Select(value => value.Name));
    }

    [Fact]
    public void StatusProjectionPreservesDefenseCapsAndEquippedPassiveImmunities()
    {
        MenuJunctionEffects effects = Project(Rules(), 100);
        Assert.All(effects.Statuses, value => Assert.Equal(100, value.Defense));
        effects = Project(Rules() with { NativeStatusPassiveFlags = 0x8000 }, 0);
        Assert.Equal(100, effects.Statuses.Single(value => value.Name == "Slow").Defense);
        Assert.Equal(100, effects.Statuses.Single(value => value.Name == "Stop").Defense);
        Assert.Equal(0, effects.Statuses.Single(value => value.Name == "Sleep").Defense);
    }

    private static MenuJunctionEffects Project(BattleNativePlayerStatRules rules, byte quantity)
    {
        NativeMagicStock[] stock = [new(1, quantity)];
        NativeCharacterVital character = new(0, new(10, 10), 0, new(0, 0, 0, rules.MagicJunctions.ToArray()), 0, 0, 0, new byte[6], stock, 1, 0);
        return LiveMenuData.JunctionEffects(rules, character, stock);
    }

    private static BattleNativePlayerStatRules Rules()
    {
        byte[] character = new byte[36];
        for (int offset = 12; offset < 36; offset += 4)
        {
            character[offset + 1] = 1;
            character[offset + 2] = 100;
            character[offset + 3] = 1;
        }
        byte[] spell = new byte[60];
        spell[32] = spell[34] = 0x81;
        spell[33] = 80;
        spell[35] = 75;
        spell[36] = 60;
        spell[37] = 40;
        spell[38] = spell[40] = 0xFF;
        spell[39] = spell[41] = 0x1F;
        ImmutableArray<ImmutableArray<byte>> spells = Enumerable.Range(0, 57).Select(index => (index == 1 ? spell : new byte[60]).ToImmutableArray()).ToImmutableArray();
        ImmutableArray<ImmutableArray<byte>> passives = Enumerable.Range(0, 19).Select(_ => new byte[8].ToImmutableArray()).ToImmutableArray();
        NativeStatCurve curve = new(100, 0, 50, 255, 100);
        NativeStatModifier modifier = new(0, 9, 0);
        NativeStatDefinitions definitions = new(Enumerable.Repeat(curve, 11).ToImmutableArray(), new byte[57].ToImmutableArray(), Enumerable.Repeat(modifier, 19).ToImmutableArray(), Enumerable.Repeat(curve, 16).ToImmutableArray(), Enumerable.Repeat(modifier, 9).ToImmutableArray());
        byte[] junctions = new byte[20];
        Array.Fill(junctions, (byte)1, 9, 10);
        return new BattleNativePlayerStatRules(0, 10, character.ToImmutableArray(), new byte[12].ToImmutableArray(), spells, passives, junctions.ToImmutableArray(), 0, definitions) { NativeStatusPassiveFlags = 0 };
    }
}
