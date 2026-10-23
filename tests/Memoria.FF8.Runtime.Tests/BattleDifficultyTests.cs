using System.Text.Json;
using System.Text.Json.Nodes;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Runtime.Tests;

public sealed class BattleDifficultyTests
{
    [Fact]
    public void Difficulty_defaults_and_invalid_values_are_explicit()
    {
        Assert.Equal(BattleDifficulty.Normal, new GameplayPreferences().Difficulty);
        Assert.Equal(BattleDifficulty.Normal, JsonSerializer.Deserialize<GameplayPreferences>("{}")!.Difficulty);
        Assert.Throws<InvalidDataException>(() => (new GameplayPreferences { Difficulty = (BattleDifficulty)99 }).Validate());
        BattleSession battle = Create(true, false, false);
        Assert.Equal(BattleDifficulty.Normal, battle.Difficulty);
        Assert.Throws<ArgumentOutOfRangeException>(() => battle.SetDifficulty((BattleDifficulty)99));
        Assert.Throws<InvalidDataException>(() => BattleSession.Restore(battle.Capture() with { Difficulty = (BattleDifficulty)99 }));
    }

    [Fact]
    public void Fresh_and_live_json_preserve_hard_and_missing_legacy_choice_defaults_to_normal()
    {
        BattleSession battle = Create(true, false, true);
        battle.SetDifficulty(BattleDifficulty.Hard);
        battle = RoundTrip(battle);
        Assert.Equal(BattleDifficulty.Hard, battle.Difficulty);
        battle.QueueAction("caster", "target", BattleAbility.Magic, 1);
        battle.Tick();
        battle = RoundTrip(battle);
        Assert.Equal(BattleDifficulty.Hard, battle.Difficulty);
        Complete(battle);
        Assert.Contains(battle.Events, item => item.Kind == "Damage" && item.ActorId == "caster");
        JsonObject legacy = JsonNode.Parse(JsonSerializer.Serialize(battle.Capture()))!.AsObject();
        legacy.Remove(nameof(BattleSnapshot.Difficulty));
        Assert.Equal(BattleDifficulty.Normal, BattleSession.Restore(legacy.Deserialize<BattleSnapshot>()!).Difficulty);
    }

    [Theory]
    [InlineData(false, true, false)]
    [InlineData(false, false, false)]
    [InlineData(true, false, false)]
    [InlineData(true, true, false)]
    [InlineData(false, true, true)]
    [InlineData(false, false, true)]
    [InlineData(true, false, true)]
    [InlineData(true, true, true)]
    public void Normal_halves_enemy_caster_magic_for_either_side_and_hard_removes_only_that_half(bool casterPlayer, bool targetPlayer, bool native)
    {
        BattleSession normal = Create(casterPlayer, targetPlayer, native);
        BattleSession hard = Create(casterPlayer, targetPlayer, native);
        hard.SetDifficulty(BattleDifficulty.Hard);
        normal.QueueAction("caster", "target", BattleAbility.Magic, 1);
        hard.QueueAction("caster", "target", BattleAbility.Magic, 1);
        Complete(normal);
        Complete(hard);
        int normalDamage = Assert.Single(normal.Events, item => item.Kind == "Damage" && item.ActorId == "caster" && item.TargetId == "target").Amount;
        int hardDamage = Assert.Single(hard.Events, item => item.Kind == "Damage" && item.ActorId == "caster" && item.TargetId == "target").Amount;
        Assert.True(hardDamage > 0);
        Assert.Equal(casterPlayer ? hardDamage : hardDamage >> 1, normalDamage);
        Assert.Equal(JsonSerializer.Serialize(normal.Capture().Random), JsonSerializer.Serialize(hard.Capture().Random));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Hardcore_keeps_hard_enemy_magic_damage_and_cannot_be_downgraded(bool native)
    {
        BattleSession hard = Create(false, true, native);
        BattleSession hardcore = Create(false, true, native);
        hard.SetDifficulty(BattleDifficulty.Hard);
        hardcore.SetDifficulty(BattleDifficulty.Hardcore);
        hard.QueueAction("caster", "target", BattleAbility.Magic, 1);
        hardcore.QueueAction("caster", "target", BattleAbility.Magic, 1);
        Complete(hard);
        Complete(hardcore);
        Assert.Equal(Assert.Single(hard.Events, item => item.Kind == "Damage").Amount, Assert.Single(hardcore.Events, item => item.Kind == "Damage").Amount);
        BattleSession restored = RoundTrip(hardcore);
        Assert.Equal(BattleDifficulty.Hardcore, restored.Difficulty);
        Assert.Throws<InvalidOperationException>(() => restored.SetDifficulty(BattleDifficulty.Hard));
    }

    private static BattleSession Create(bool casterPlayer, bool targetPlayer, bool native)
    {
        BattleNativeAction magic = new(2, 1, 1, 1)
        {
            MagicId = 0,
            AttackType = 2,
            Power = 18,
            HitCount = 1,
            TargetFlags = 16,
            AttackFlags = 1,
            DamagePresentation = 0,
            Element = 0,
            ElementPercent = null,
            Hit = null,
            Critical = null,
            PowerModifier = null,
            LevelModifier = null,
            StatusAttack = 0,
            Statuses0 = 0,
            Statuses1 = 0,
            RawRecord = [],
            SupplementalRecord = []
        };
        List<BattleCombatantDefinition> actors =
        [
            new() { Id = "caster", Name = "Caster", IsPlayer = casterPlayer, MaximumHp = 9000, CurrentHp = 9000, Speed = 1, Magic = 40, InitialAtb = BattleSession.AtbMaximum, Abilities = [BattleAbility.Magic], MagicStocks = [new(1, 10)] },
            new() { Id = "target", Name = "Target", IsPlayer = targetPlayer, MaximumHp = 9000, CurrentHp = 9000, Speed = 1, Spirit = 5 }
        ];
        if (casterPlayer == targetPlayer)
        {
            actors.Add(new() { Id = "opponent", Name = "Opponent", IsPlayer = !casterPlayer, Speed = 1, MaximumHp = 9000, CurrentHp = 9000 });
        }
        return new(new()
        {
            NativeCommandSequences = NativeBattleCommandSequenceFixture.Values,
            Combatants = actors.ToArray(),
            ActionDefinitions = [new(BattleAbility.Magic, 1, "Magic", BattleActionEffect.Damage, 18, casterPlayer == targetPlayer) { NativeAction = native ? magic : null }]
        }, BattleAtbMode.Normal);
    }

    private static BattleSession RoundTrip(BattleSession battle)
    {
        return BattleSession.Restore(JsonSerializer.Deserialize<BattleSnapshot>(JsonSerializer.Serialize(battle.Capture()))!);
    }

    private static void Complete(BattleSession battle)
    {
        for (int tick = 0; tick < 100 && (battle.QueuedActions.Count > 0 || battle.ActiveAction is not null); tick++)
        {
            battle.Tick();
        }
        Assert.Empty(battle.QueuedActions);
        Assert.Null(battle.ActiveAction);
    }
}
