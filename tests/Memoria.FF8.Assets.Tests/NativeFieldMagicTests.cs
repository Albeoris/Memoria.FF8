using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeFieldMagicTests
{
    private static string GameDirectory => TestResources.GameDirectory;

    private static NativeCharacterVitals Prepare(GameAssets assets, int spell, int quantity = 2, int targetHp = 1, ushort targetStatus = 0)
    {
        NativeCharacterVitals state = assets.ReadNewGameCharacterVitals();
        NativeCharacterVital[] records = state.Records.ToArray();
        NativeMagicStock[] stocks = Enumerable.Repeat(new NativeMagicStock(0, 0), 32).ToArray();
        stocks[7] = new((byte)spell, (byte)quantity);
        records[0] = records[0] with { MagicInventory = stocks };
        records[1] = records[1] with { HitPoints = new((ushort)targetHp, (ushort)targetHp), Status = targetStatus };
        return state with { Records = records };
    }

    [Theory]
    [InlineData(21)]
    [InlineData(22)]
    [InlineData(23)]
    public void OriginalCureFamilyUsesFieldFormulaAndOnlyConsumesOnChange(int spell)
    {
        using GameAssets assets = new(GameDirectory);
        using BattleAssets battle = new(GameDirectory);
        NativeCharacterVitals state = Prepare(assets, spell);
        BattleNativePlayerStatRules rules = battle.ReadPlayerNativeStatRules(state, 0);
        int magic = rules.Project(state.Records[0].MagicInventory, new(state.Records[0].HitPointBonus, state.Records[0].StatBonuses.ToImmutableArray())).Magic;
        int power = rules.MagicRecords[spell][8];
        int expected = Math.Min(state.Definitions.CharacterMaximum(state.Records[1]), 1 + (magic + power) / 2 * power);
        string before = JsonSerializer.Serialize(state);
        NativeCharacterVitals after = state.CastFieldMagic(0, spell, 1, rules, out bool used);
        Assert.True(used);
        Assert.Equal(expected, after.Records[1].HitPoints.Minimum);
        Assert.Equal(new NativeMagicStock((byte)spell, 1), after.Records[0].MagicInventory[7]);
        Assert.Equal(before, JsonSerializer.Serialize(state));
        NativeCharacterVital[] records = state.Records.ToArray();
        ushort maximum = state.Definitions.CharacterMaximum(records[1]);
        records[1] = records[1] with { HitPoints = new(maximum, maximum), Status = 0 };
        state = state with { Records = records };
        Assert.Same(state, state.CastFieldMagic(0, spell, 1, rules, out used));
        Assert.False(used);
    }

    [Theory]
    [InlineData(24, false)]
    [InlineData(25, true)]
    public void OriginalRevivalSpellsRequireKoAndRestoreNativeFraction(int spell, bool full)
    {
        using GameAssets assets = new(GameDirectory);
        using BattleAssets battle = new(GameDirectory);
        NativeCharacterVitals state = Prepare(assets, spell, targetHp: 0, targetStatus: 1);
        BattleNativePlayerStatRules rules = battle.ReadPlayerNativeStatRules(state, 0);
        NativeCharacterVitals after = state.CastFieldMagic(0, spell, 1, rules, out bool used);
        Assert.True(used);
        int maximum = state.Definitions.CharacterMaximum(state.Records[1]);
        Assert.Equal(full ? maximum : maximum / 8, after.Records[1].HitPoints.Minimum);
        Assert.Equal(0, after.Records[1].Status & 1);
        Assert.Same(after, after.CastFieldMagic(0, spell, 1, battle.ReadPlayerNativeStatRules(after, 0), out used));
        Assert.False(used);
    }

    [Fact]
    public void EsunaCuresSavedStatusesWhileCureCannotHealZombieOrKo()
    {
        using GameAssets assets = new(GameDirectory);
        using BattleAssets battle = new(GameDirectory);
        NativeCharacterVitals state = Prepare(assets, 27, targetHp: 100, targetStatus: 0x7E);
        NativeCharacterVitals after = state.CastFieldMagic(0, 27, 1, battle.ReadPlayerNativeStatRules(state, 0), out bool used);
        Assert.True(used);
        Assert.Equal(0, after.Records[1].Status & 0x7E);
        Assert.Equal(100, after.Records[1].HitPoints.Minimum);
        foreach ((int hp, ushort status) in new[] { (0, (ushort)1), (1, (ushort)0x340) })
        {
            state = Prepare(assets, 21, targetHp: hp, targetStatus: status);
            Assert.Same(state, state.CastFieldMagic(0, 21, 1, battle.ReadPlayerNativeStatRules(state, 0), out used));
            Assert.False(used);
        }
    }

    [Fact]
    public void CasterRulesMustMatchCurrentIdentityLevelAbilitiesAndJunctions()
    {
        using GameAssets assets = new(GameDirectory);
        using BattleAssets battle = new(GameDirectory);
        NativeCharacterVitals state = Prepare(assets, 21);
        BattleNativePlayerStatRules rules = battle.ReadPlayerNativeStatRules(state, 0);
        foreach (BattleNativePlayerStatRules stale in new[] { rules with { CharacterId = 1 }, rules with { Level = (byte)(rules.Level + 1) }, rules with { Abilities = rules.Abilities ^ 1 }, rules with { MagicJunctions = rules.MagicJunctions.SetItem(1, 21) } })
        {
            Assert.Throws<InvalidDataException>(() => state.CastFieldMagic(0, 21, 1, stale, out _));
        }
    }

    [Fact]
    public void ExhaustedCastNormalizesAllJunctionBytesAndClampsCasterHp()
    {
        using GameAssets assets = new(GameDirectory);
        using BattleAssets battle = new(GameDirectory);
        NativeCharacterVitals state = Prepare(assets, 21, 1);
        NativeCharacterVital[] records = state.Records.ToArray();
        byte[] junctions = records[0].Junction.MagicJunctions.ToArray();
        junctions[0] = 21;
        junctions[19] = 21;
        records[0] = records[0] with { Junction = records[0].Junction with { MagicJunctions = junctions } };
        ushort beforeMaximum = state.Definitions.CharacterMaximum(records[0]);
        records[0] = records[0] with { HitPoints = new(beforeMaximum, beforeMaximum) };
        state = state with { Records = records };
        NativeCharacterVitals after = state.CastFieldMagic(0, 21, 1, battle.ReadPlayerNativeStatRules(state, 0), out bool used);
        Assert.True(used);
        Assert.Equal(new NativeMagicStock(0, 0), after.Records[0].MagicInventory[7]);
        Assert.Equal(0, after.Records[0].Junction.MagicJunctions[0]);
        Assert.Equal(0, after.Records[0].Junction.MagicJunctions[19]);
        Assert.Equal(after.Definitions.CharacterMaximum(after.Records[0]), after.Records[0].HitPoints.Minimum);
        Assert.True(after.Records[0].HitPoints.Minimum < beforeMaximum);
    }

    [Fact]
    public void TransfersRespectBothCapacitiesAndCleanOnlyDepletedNativeJunctionBytes()
    {
        using GameAssets assets = new(GameDirectory);
        NativeCharacterVitals state = Prepare(assets, 21, 8);
        NativeCharacterVital[] records = state.Records.ToArray();
        NativeMagicStock[] target = records[1].MagicInventory.ToArray();
        target[9] = new(21, 98);
        byte[] junctions = new byte[20];
        junctions[1] = 21;
        junctions[19] = 21;
        records[0] = records[0] with { Junction = records[0].Junction with { MagicJunctions = junctions } };
        records[1] = records[1] with { MagicInventory = target };
        state = state with { Records = records };
        NativeCharacterVitals after = state.TransferMagic(0, 21, 1, 8, out int moved);
        Assert.Equal(2, moved);
        Assert.Equal(new NativeMagicStock(21, 6), after.Records[0].MagicInventory[7]);
        Assert.Equal(new NativeMagicStock(21, 100), after.Records[1].MagicInventory[9]);
        Assert.Equal(21, after.Records[0].Junction.MagicJunctions[1]);
        after = after.TransferMagic(0, 21, 2, 100, out moved);
        Assert.Equal(6, moved);
        Assert.Equal(new NativeMagicStock(0, 0), after.Records[0].MagicInventory[7]);
        Assert.Equal(0, after.Records[0].Junction.MagicJunctions[1]);
        Assert.Equal(21, after.Records[0].Junction.MagicJunctions[19]);
        records = state.Records.ToArray();
        records[1] = records[1] with { MagicInventory = Enumerable.Range(1, 32).Select(id => new NativeMagicStock((byte)(id + 24), 1)).ToArray() };
        state = state with { Records = records };
        Assert.Same(state, state.TransferMagic(0, 21, 1, 8, out moved));
        Assert.Equal(0, moved);
    }
}
