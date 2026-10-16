using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativePlayerStatReprojectionTests
{
    [Fact]
    public void Status_resistance_reuses_the_original_junction_formula_after_stock_changes()
    {
        using GameAssets game = new(TestResources.GameDirectory);
        using BattleAssets assets = new(game.GameDirectory);
        NativeCharacterVitals original = game.ReadNewGameCharacterVitals();
        NativeCharacterVital character = original.Records[0];
        BattleNativePlayerStatRules originalRules = assets.ReadPlayerNativeStatRules(original, 0);
        byte spell = (byte)Enumerable.Range(1, 56).First(index => (originalRules.MagicRecords[index][40] & 4) != 0 && originalRules.MagicRecords[index][37] != 0);
        byte[] junctions = character.Junction.MagicJunctions.ToArray();
        junctions[15] = spell;
        NativeMagicStock[] stocks = Enumerable.Repeat(new NativeMagicStock(0, 0), 32).ToArray();
        stocks[0] = new(spell, 100);
        NativeCharacterVital modified = character with { Junction = character.Junction with { MagicJunctions = junctions, Abilities = 0 }, MagicInventory = stocks };
        NativeCharacterVitals vitals = original with { Records = [modified, .. original.Records.Skip(1)] };
        BattleNativePlayerStatRules rules = assets.ReadPlayerNativeStatRules(vitals, 0);
        ImmutableArray<byte> full = rules.ProjectStatusResistances(stocks);

        Assert.Equal(assets.ReadPlayerNativeStatusData(vitals, 0).Resistances, full);
        Assert.Equal(Math.Min(200, 100 + rules.MagicRecords[spell][37]), full[2]);
        Assert.Equal(full[2], full[20]);
        stocks[0] = new(spell, 0);
        ImmutableArray<byte> depleted = rules.ProjectStatusResistances(stocks);
        Assert.Equal((byte)100, depleted[2]);
        Assert.Equal(depleted[2], depleted[20]);
        Assert.Throws<NotSupportedException>(() => (rules with { NativeStatusPassiveFlags = null }).ProjectStatusResistances(stocks));
    }

    [Fact]
    public void Current_magic_stock_and_saved_growth_recompute_original_stats_without_incrementing_projected_values()
    {
        using GameAssets game = new(TestResources.GameDirectory);
        using BattleAssets assets = new(game.GameDirectory);
        NativeCharacterVitals original = game.ReadNewGameCharacterVitals();
        NativeCharacterVital character = original.Records[0];
        byte[] junctions = character.Junction.MagicJunctions.ToArray();
        junctions[0] = 1;
        junctions[1] = 1;
        NativeMagicStock[] stocks = character.MagicInventory.Select(stock => stock.SpellId == 1 ? new NativeMagicStock(0, 0) : stock).ToArray();
        stocks[0] = new NativeMagicStock(1, 100);
        NativeCharacterVital modified = character with { Junction = character.Junction with { MagicJunctions = junctions }, MagicInventory = stocks };
        NativeCharacterVitals vitals = original with { Records = [modified, .. original.Records.Skip(1)] };
        BattleNativePlayerStatRules rules = assets.ReadPlayerNativeStatRules(vitals, 0);
        BattleNativeCharacterGrowth growth = new(modified.HitPointBonus, modified.StatBonuses.ToImmutableArray());
        BattleNativePlayerStatistics fullStock = rules.Project(stocks, growth);

        NativeMagicStock[] depleted = stocks.ToArray();
        depleted[0] = new NativeMagicStock(1, 0);
        BattleNativePlayerStatistics emptyStock = rules.Project(depleted, growth);
        Assert.True(emptyStock.Strength < fullStock.Strength);
        Assert.True(emptyStock.MaximumHp < fullStock.MaximumHp);
        byte[] bonuses = growth.StatBonuses.ToArray();
        bonuses[0]++;
        BattleNativePlayerStatistics grown = rules.Project(depleted, growth with { StatBonuses = bonuses.ToImmutableArray() });
        Assert.Equal(emptyStock.Strength + 1, grown.Strength);
        Assert.Equal(emptyStock.MaximumHp, grown.MaximumHp);
        Assert.Equal(vitals.Definitions.CharacterMaximum(modified), fullStock.MaximumHp);
    }

    [Fact]
    public void Immutable_projector_and_attack_inputs_survive_an_independent_round_trip()
    {
        using GameAssets game = new(TestResources.GameDirectory);
        using BattleAssets assets = new(game.GameDirectory);
        NativeCharacterVitals vitals = game.ReadNewGameCharacterVitals();
        NativeCharacterVital character = vitals.Records[0];
        BattleNativePlayerStatRules rules = assets.ReadPlayerNativeStatRules(vitals, 0);
        BattleNativeCharacterGrowth growth = new(character.HitPointBonus, character.StatBonuses.ToImmutableArray());
        BattleNativePlayerStatistics expected = rules.Project(character.MagicInventory, growth);

        BattleNativePlayerStatRules restored = JsonSerializer.Deserialize<BattleNativePlayerStatRules>(JsonSerializer.Serialize(rules))!;

        Assert.Equal(JsonSerializer.Serialize(expected), JsonSerializer.Serialize(restored.Project(character.MagicInventory, growth)));
        BattleNativeAction attack = assets.ReadPlayerNativeAttackData(vitals, 0);
        Assert.Equal(12, attack.RawRecord.Length);
        Assert.Equal(60, attack.SupplementalRecord.Length);
        Assert.Equal((byte)1, attack.Command);
        Assert.Equal(attack.Power, attack.RawRecord[6]);
        Assert.Equal(attack.Critical, attack.RawRecord[10]);
        Assert.Equal(attack, restored.ProjectAttack(attack, character.MagicInventory, expected));
    }
}
