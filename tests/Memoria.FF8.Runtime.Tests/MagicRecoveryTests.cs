using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Runtime.Tests;

public sealed class MagicRecoveryTests
{
    [Fact]
    public void RevivalPrecedesStatusAndHealingAndPrefersFullLife()
    {
        BattleSession battle = Create([Spell(1, BattleActionEffect.Heal, 20), Spell(2, BattleActionEffect.Revive, 0), Spell(3, BattleActionEffect.Revive, 1)], [Ally("fallen", 0)]);
        BattleMagicRecoveryChoice choice = Assert.IsType<BattleMagicRecoveryChoice>(battle.PreviewMagicRecovery("caster"));
        Assert.Equal(3, choice.ActionId);
        Assert.Equal("fallen", choice.TargetId);
    }

    [Fact]
    public void StatusRecoveryCountsStatusesOnOneAllyAndPrecedesHpRecovery()
    {
        BattleActionDefinition esuna = Spell(2, BattleActionEffect.RemoveStatus, 0) with { NativeAction = Native(2, 4, 0) with { Statuses0 = 0x1A } };
        BattleActionDefinition eyeDrops = Spell(3, BattleActionEffect.RemoveStatus, 0) with { NativeAction = Native(3, 4, 0) with { Statuses0 = 8 } };
        BattleSession battle = Create([Spell(1, BattleActionEffect.Heal, 20), esuna, eyeDrops], [Ally("one", 1000) with { NativeStatus0 = 8 }, Ally("two", 1000) with { NativeStatus0 = 0x12 }]);
        BattleMagicRecoveryChoice choice = Assert.IsType<BattleMagicRecoveryChoice>(battle.PreviewMagicRecovery("caster"));
        Assert.Equal(2, choice.ActionId);
        Assert.Equal("two", choice.TargetId);
    }

    [Fact]
    public void ChoosesWeakestReliableHealForLargestAbsoluteHpDeficit()
    {
        BattleSession battle = Create([Spell(1, BattleActionEffect.Heal, 20), Spell(2, BattleActionEffect.Heal, 40), Spell(3, BattleActionEffect.Heal, 80)], [Ally("small", 50) with { MaximumHp = 100 }, Ally("large", 100)], casterHp: 1000);
        BattleMagicRecoveryChoice choice = Assert.IsType<BattleMagicRecoveryChoice>(battle.PreviewMagicRecovery("caster"));
        Assert.Equal(2, choice.ActionId);
        Assert.Equal("large", choice.TargetId);
        Assert.True(choice.FullRecoveryProbability >= 0.8);
    }

    [Fact]
    public void FallsBackToStrongestHealWhenNoCastCanRestoreAllMissingHp()
    {
        BattleSession battle = Create([Spell(1, BattleActionEffect.Heal, 1), Spell(2, BattleActionEffect.Heal, 2)], [Ally("ally", 1)], casterHp: 1000);
        BattleMagicRecoveryChoice choice = Assert.IsType<BattleMagicRecoveryChoice>(battle.PreviewMagicRecovery("caster"));
        Assert.Equal(2, choice.ActionId);
        Assert.Equal(0, choice.FullRecoveryProbability);
    }

    [Fact]
    public void ReliabilityBoundaryUsesAtLeastEightyPercentRatherThanExpectedHealing()
    {
        BattleDamageForecast forecast = BattleActionForecaster.FromNativePhases(draw => NativeBattleDamage.Cure(20, 20, draw()), 1);
        int threshold = forecast.Distribution.Keys.Where(amount => forecast.Distribution.Where(sample => sample.Key >= amount).Sum(sample => sample.Value) >= 0.8).Max();
        BattleActionDefinition[] spells = [Spell(1, BattleActionEffect.Heal, 20), Spell(2, BattleActionEffect.Heal, 40)];
        BattleSession reliable = Create(spells, [Ally("ally", 1000 - threshold)], casterHp: 1000);
        BattleSession insufficient = Create(spells, [Ally("ally", 999 - threshold)], casterHp: 1000);
        Assert.Equal(1, reliable.PreviewMagicRecovery("caster")!.ActionId);
        Assert.Equal(2, insufficient.PreviewMagicRecovery("caster")!.ActionId);
    }

    [Theory]
    [InlineData(0x40, 0)]
    [InlineData(4, 0)]
    [InlineData(0, 0x80)]
    public void NativeHealingDoesNotChooseZombiePetrifiedOrReflectingAlly(int saved, uint statuses)
    {
        BattleActionDefinition cure = Spell(1, BattleActionEffect.Heal, 20) with { NativeAction = Native(1, 3, 20) with { AttackFlags = 0x10 } };
        BattleSession battle = Create([cure], [Ally("ally", 1) with { NativeStatus0 = (ushort)saved, NativeStatus1 = statuses }], casterHp: 1000);
        Assert.Null(battle.PreviewMagicRecovery("caster"));
    }

    [Fact]
    public void NativeRevivalRespectsFieldProhibition()
    {
        BattleActionDefinition life = Spell(1, BattleActionEffect.Revive, 0) with { NativeAction = Native(1, 5, 0) };
        BattleSession battle = Create([life], [Ally("fallen", 0)], fieldRules: 0x40);
        Assert.Null(battle.PreviewMagicRecovery("caster"));
    }

    [Theory]
    [InlineData(1, false)]
    [InlineData(255, true)]
    public void NativePercentageRecoveryUsesHitCheckAndIgnoresShell(int hit, bool useful)
    {
        BattleActionDefinition cure = Spell(1, BattleActionEffect.FullHeal, 16) with { NativeAction = Native(1, 32, 16) with { Hit = (byte)hit } };
        BattleSession battle = Create([cure], [Ally("ally", 1) with { NativeStatus1 = 0x40 }], casterHp: 1000);
        BattleMagicRecoveryChoice? choice = battle.PreviewMagicRecovery("caster");
        if (useful)
        {
            Assert.NotNull(choice);
            Assert.Equal(1000, choice.ExpectedHealing);
            Assert.Equal(1, choice.FullRecoveryProbability);
        }
        else
        {
            Assert.Null(choice);
        }
    }

    [Fact]
    public void ForecastUsesNativePhasesAndShellSignedRoundingWithoutMutatingSession()
    {
        BattleActionDefinition cure = Spell(1, BattleActionEffect.Heal, 20) with { NativeAction = Native(1, 3, 20) };
        BattleSession battle = Create([cure], [Ally("ally", 400) with { NativeStatus1 = 0x40 }], casterHp: 1000);
        string before = JsonSerializer.Serialize(battle.Capture());
        BattleMagicRecoveryChoice choice = Assert.IsType<BattleMagicRecoveryChoice>(battle.PreviewMagicRecovery("caster"));
        BattleDamageForecast expected = BattleActionForecaster.FromNativePhases(draw => -(-NativeBattleDamage.Cure(20, 20, draw()) >> 1), 600);
        Assert.Equal(expected.ExpectedDamage, choice.ExpectedHealing);
        Assert.Equal(expected.KillProbability, choice.FullRecoveryProbability);
        Assert.Equal(choice, battle.PreviewMagicRecovery("caster"));
        Assert.Equal(before, JsonSerializer.Serialize(battle.Capture()));
    }

    [Fact]
    public void UnavailableAndUnstockedMagicAreNotCandidates()
    {
        BattleActionDefinition[] actions = [Spell(1, BattleActionEffect.Heal, 20) with { Available = false }, Spell(2, BattleActionEffect.Heal, 40)];
        BattleSession battle = Create(actions, [Ally("ally", 1)], casterHp: 1000, stocks: [new(1, 5)]);
        Assert.Null(battle.PreviewMagicRecovery("caster"));
    }

    private static BattleActionDefinition Spell(int id, BattleActionEffect effect, int power)
    {
        return new(BattleAbility.Magic, id, $"Spell {id}", effect, power, true);
    }

    private static BattleCombatantDefinition Ally(string id, int hp)
    {
        return new() { Id = id, Name = id, IsPlayer = true, MaximumHp = 1000, CurrentHp = hp, NativeStatus0 = hp == 0 ? (ushort)1 : (ushort)0 };
    }

    private static BattleSession Create(BattleActionDefinition[] actions, BattleCombatantDefinition[] allies, int casterHp = 500, byte fieldRules = 0, BattleStock[]? stocks = null)
    {
        BattleCombatantDefinition caster = Ally("caster", casterHp) with { InitialAtb = BattleSession.AtbMaximum, Abilities = [BattleAbility.Magic], Magic = 20, MagicStocks = stocks ?? actions.Select(action => new BattleStock(action.Id, 5)).ToArray() };
        return new(new BattleDefinition
        {
            Name = "Magic recovery",
            NativeFieldBattleRules = fieldRules,
            Combatants = [caster, .. allies, new() { Id = "enemy", Name = "Enemy", MaximumHp = 1000, CurrentHp = 1000 }],
            ActionDefinitions = actions
        }, BattleAtbMode.TurnBased);
    }

    private static BattleNativeAction Native(int id, byte type, byte power)
    {
        return new(2, (ushort)id, 1, id)
        {
            MagicId = (ushort)id,
            AttackType = type,
            Power = power,
            HitCount = 1,
            TargetFlags = 0x20,
            AttackFlags = 0,
            DamagePresentation = 0,
            Element = 0,
            ElementPercent = 0,
            StatusAttack = 255,
            Statuses0 = 0,
            Statuses1 = 0,
            Hit = 255,
            Critical = 0,
            PowerModifier = 0,
            LevelModifier = 0,
            RawRecord = ImmutableArray<byte>.Empty,
            SupplementalRecord = ImmutableArray<byte>.Empty
        };
    }
}
