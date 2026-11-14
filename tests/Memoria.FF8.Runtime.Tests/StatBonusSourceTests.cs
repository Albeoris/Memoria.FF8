using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed partial class NativePlayerJunctionStatTests
{
    [Theory]
    [InlineData(40)]
    [InlineData(100)]
    public void HpBonusParticipatesBeforeHpPercentageAndFinalCap(int level)
    {
        BattleNativePlayerStatRules rules = Rules(1) with { Level = (byte)level, LevelBasedStatBonuses = true, Abilities = 65 | 39u << 8 };
        rules = rules with { SavedDefinitions = rules.SavedDefinitions with { CharacterPassives = rules.SavedDefinitions.CharacterPassives.SetItem(0, new(0, 0, 80)) } };
        BattleNativeCharacterGrowth growth = new(0, [0, 0, 0, 0, 0, 0]);
        int baseline = (rules with { Abilities = 0 }).Project([], growth).MaximumHp;
        Assert.Equal(Math.Min(9999, (baseline + 30 * (level - 1)) * 180 / 100), rules.Project([], growth).MaximumHp);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(40)]
    [InlineData(100)]
    public void EquippedLevelBonusesReplaceHistoricalGrowthAndRespectDuplicateAbilities(int level)
    {
        NativeStatBonusSources sources = new([
            new(NativeStatBonusSources.LevelUp, 600, [20, 21, 22, 23, 0, 0]),
            new(NativeStatBonusSources.Items, 10, [1, 2, 3, 4, 5, 6]),
            new(NativeStatBonusSources.Devour, 10, [1, 1, 1, 1, 1, 0]),
            new("extension", 10, [1, 1, 1, 1, 1, 1])
        ]);
        BattleNativeCharacterGrowth growth = sources.Total() with { BonusSources = sources };
        BattleNativePlayerStatRules rules = Rules(1) with { Level = (byte)level, LevelBasedStatBonuses = true, Abilities = 65 | 66u << 8 | 66u << 16 | 68u << 24 };
        BattleNativePlayerStatistics actual = rules.Project([], growth);
        BattleNativeCharacterGrowth expected = new((ushort)(30 + 30 * (level - 1)), [(byte)(3 + level - 1), 4, (byte)(5 + level - 1), 6, 7, 7]);
        Assert.Equal(Values((rules with { LevelBasedStatBonuses = false }).Project([], expected)), Values(actual));
        Assert.Equal(Values((rules with { LevelBasedStatBonuses = false }).Project([], sources.Total())), Values((rules with { LevelBasedStatBonuses = false }).Project([], growth)));
        BattleNativePlayerStatistics removed = (rules with { Abilities = 0 }).Project([], growth);
        Assert.Equal(level - 1, actual.Strength - removed.Strength);
        Assert.Equal(30 * (level - 1), actual.MaximumHp - removed.MaximumHp);
        BattleNativeCharacterGrowth restored = JsonSerializer.Deserialize<BattleNativeCharacterGrowth>(JsonSerializer.Serialize(growth))!;
        restored.Validate();
        Assert.Equal(Values(actual), Values(rules.Project([], restored)));
    }
}

public sealed class StatBonusSourceTests
{
    [Fact]
    public void ModeSwitchUpdatesHpPolicyWithoutRewritingGrowthAndWaitsForBattleCompletion()
    {
        GameSession session = GameSession.CreateColdProcess();
        session.StartNewGame();
        NativeCharacterVitals vitals = MagicSystemFixture.Create();
        vitals.Records[0] = vitals.Records[0] with { Experience = 20, HitPointBonus = 30, StatBonuses = [20, 0, 0, 0, 0, 0], Junction = vitals.Records[0].Junction with { Abilities = 65 | 66u << 8 } };
        session.SetCharacterVitals(vitals);
        string before = JsonSerializer.Serialize(session.CharacterVitals!.Records[0].Growth.Sources);
        GameplayTuning enabled = new() { LevelBasedStatBonuses = true };
        session.ConfigureGameplayTuning(new(), enabled);
        Assert.True(session.CharacterVitals.LevelBasedStatBonuses);
        Assert.Equal(before, JsonSerializer.Serialize(session.CharacterVitals.Records[0].Growth.Sources));
        Assert.Equal(10, session.CharacterVitals.Records[0].HitPoints.Minimum);
        Assert.Equal(60, session.CharacterVitals.EffectiveGrowth(session.CharacterVitals.Records[0]).HitPointBonus);
        BattleSession battle = new(new BattleDefinition { Tuning = enabled, Combatants = [new() { Id = "party", Name = "Party", IsPlayer = true }, new() { Id = "enemy", Name = "Enemy" }] }, BattleAtbMode.TurnBased);
        session.SetBattle(battle.Capture());
        session.ConfigureGameplayTuning(new(), new());
        Assert.True(session.CharacterVitals.LevelBasedStatBonuses);
        Assert.False(session.State.PendingGameplayPreferences!.Tuning.LevelBasedStatBonuses);
        session.SetBattle(null);
        Assert.False(session.CharacterVitals.LevelBasedStatBonuses);
        Assert.Equal(30, session.CharacterVitals.EffectiveGrowth(session.CharacterVitals.Records[0]).HitPointBonus);
        Assert.Equal(before, JsonSerializer.Serialize(session.CharacterVitals.Records[0].Growth.Sources));
        NativeCharacterVitals restored = JsonSerializer.Deserialize<NativeCharacterVitals>(JsonSerializer.Serialize(session.CharacterVitals))!;
        restored.Validate();
        Assert.Equal(before, JsonSerializer.Serialize(restored.Records[0].Growth.Sources));
    }

    [Fact]
    public void LegacyGrowthSplitsOnlyExistingBonusAbilitiesAndKeepsRemainderAsItems()
    {
        BattleNativeCharacterGrowth legacy = JsonSerializer.Deserialize<BattleNativeCharacterGrowth>("{\"HitPointBonus\":3100,\"StatBonuses\":[120,90,100,255,20,30]}")!;
        legacy.Validate();
        NativeStatBonusGroup levels = Assert.Single(legacy.Sources.Groups, group => group.Source == NativeStatBonusSources.LevelUp);
        Assert.Equal(2970, levels.HitPoints);
        Assert.Equal(new[] { 99, 90, 99, 99, 0, 0 }, levels.Stats);
        Assert.Equal(new byte[] { 21, 0, 1, 156, 20, 30 }, legacy.Effective(true, 1, 0).StatBonuses);
        Assert.Equal((ushort)130, legacy.Effective(true, 1, 0).HitPointBonus);
        Assert.Equal(legacy.HitPointBonus, legacy.Effective(false, 1, 0).HitPointBonus);
        Assert.Equal(legacy.StatBonuses, legacy.Effective(false, 1, 0).StatBonuses);
    }

    [Fact]
    public void HiddenGrowthCannotBlockItemsOrDevourAndDisableRestoresCappedTotal()
    {
        NativeStatBonusSources sources = new([new(NativeStatBonusSources.LevelUp, 2970, [99, 0, 0, 0, 0, 0]), new(NativeStatBonusSources.Items, 7000, [155, 0, 0, 0, 0, 0])]);
        BattleNativeCharacterGrowth growth = sources.Total() with { BonusSources = sources };
        BattleNativeCharacterGrowth ordinary = growth.Add(NativeStatBonusSources.Items, 100, [10, 0, 0, 0, 0, 0], false);
        Assert.Equal(156, Assert.Single(ordinary.Sources.Groups, group => group.Source == NativeStatBonusSources.Items).Stats[0]);
        BattleNativeCharacterGrowth enabled = growth.Add(NativeStatBonusSources.Items, 100, [10, 0, 0, 0, 0, 0], true).Add(NativeStatBonusSources.Devour, 10, [1, 0, 0, 0, 0, 0], true).Add(NativeStatBonusSources.LevelUp, 30, [1, 0, 0, 0, 0, 0], true);
        Assert.Equal(166, enabled.Effective(true, 1, 0).StatBonuses[0]);
        Assert.Equal(7110, enabled.Effective(true, 1, 0).HitPointBonus);
        Assert.Equal(255, enabled.Effective(false, 1, 0).StatBonuses[0]);
        Assert.Equal(9999, enabled.Effective(false, 1, 0).HitPointBonus);
        Assert.Equal(100, Assert.Single(enabled.Sources.Groups, group => group.Source == NativeStatBonusSources.LevelUp).Stats[0]);
        enabled.Validate();
    }

    [Fact]
    public void SourceCorruptionIsRejectedAndExtensionGroupsRoundtrip()
    {
        NativeStatBonusSources sources = new([new("future-source", 10, [1, 2, 3, 4, 5, 6])]);
        NativeStatBonusSources restored = JsonSerializer.Deserialize<NativeStatBonusSources>(JsonSerializer.Serialize(sources))!;
        Assert.True(sources.EquivalentTo(restored));
        Assert.Throws<InvalidDataException>(() => (new BattleNativeCharacterGrowth(0, [0, 0, 0, 0, 0, 0]) { BonusSources = sources }).Validate());
        Assert.Throws<InvalidDataException>(() => new NativeStatBonusSources([new("bad", 0, [1])]).Validate());
        Assert.Throws<InvalidDataException>(() => new NativeStatBonusSources([sources.Groups[0], sources.Groups[0]]).Validate());
    }
}

public sealed partial class NativeBattleSpecialActionTests
{
    [Fact]
    public void DevourKeepsItsSourceAcrossJsonContinuationEvenWhenNativeTotalIsSaturated()
    {
        BattleSnapshot snapshot = Create(19).Capture();
        snapshot = snapshot with { Definition = snapshot.Definition with { Tuning = snapshot.Definition.Tuning with { LevelBasedStatBonuses = true } } };
        BattleSession battle = BattleSession.Restore(snapshot);
        BattleNativeCharacterGrowth before = battle.Combatants[0].NativeGrowth!;
        battle.QueueNativePlayerCommand("party-0", "enemy", 7, 9);
        battle.Tick();
        BattleSnapshot midpoint = JsonSerializer.Deserialize<BattleSnapshot>(JsonSerializer.Serialize(battle.Capture()))!;
        BattleSession restored = BattleSession.Restore(midpoint);
        Complete(battle);
        Complete(restored);
        BattleNativeCharacterGrowth after = restored.Combatants[0].NativeGrowth!;
        Assert.Equal(1, Assert.Single(after.Sources.Groups, group => group.Source == NativeStatBonusSources.Devour).Stats[0]);
        Assert.Equal(before.Sources.Total(true).StatBonuses[0] + 1, after.Sources.Total(true).StatBonuses[0]);
        Assert.Equal(JsonSerializer.Serialize(battle.Capture()), JsonSerializer.Serialize(restored.Capture()));
    }
}
