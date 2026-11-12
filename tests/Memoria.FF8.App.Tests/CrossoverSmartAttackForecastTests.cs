using Memoria.FF7.Runtime;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class CrossoverSmartAttackForecastTests
{
    [Theory]
    [InlineData(Ff7BattleDamageAffinity.Absorb)]
    [InlineData(Ff7BattleDamageAffinity.Nullify)]
    public void SelectorRejectsHigherPowerSpellWhenHostAffinityMakesItUseless(Ff7BattleDamageAffinity affinity)
    {
        Ff7BattleGuestPhysicalHit strong = Spell(90);
        Ff7BattleGuestPhysicalHit useful = Spell(36);
        BattleDamageForecast rejected = Forecast(strong, Context(strong) with { Attack = strong.Attack with { Affinity = affinity } });
        BattleDamageForecast usefulForecast = Forecast(useful, Context(useful));
        BattleSmartAttackPreview physical = PhysicalPreview(1);

        Assert.True(Forecast(strong, Context(strong)).ExpectedDamage > usefulForecast.ExpectedDamage);
        Assert.True(rejected.ExpectedDamage <= 0);
        if (affinity == Ff7BattleDamageAffinity.Absorb)
        {
            Assert.True(rejected.ExpectedDamage < 0);
        }
        Assert.Equal(0, rejected.KillProbability);
        BattleSmartAttackPreview selected = BattleSmartAttackSelector.Select(physical, [Candidate(1, 90, rejected), Candidate(2, 36, usefulForecast)]);
        Assert.Equal(BattleAbility.Magic, selected.Ability);
        Assert.Equal(2, selected.ActionId);
        Assert.Equal(usefulForecast.ExpectedDamage, selected.ExpectedDamage);
        Assert.Equal(physical, BattleSmartAttackSelector.Select(physical, [Candidate(1, 90, rejected)]));
    }

    [Fact]
    public void DefenseAndMagicBarrierCanChangeTheSharedSelectorsChoice()
    {
        Ff7BattleGuestPhysicalHit hit = Spell(36);
        Ff7BattleGuestContactContext context = Context(hit);
        BattleDamageForecast normal = Forecast(hit, context);
        BattleDamageForecast defended = Forecast(hit, context with { Target = context.Target with { MagicDefense = 400 } });
        BattleDamageForecast barrier = Forecast(hit, context with { TargetVitals = context.TargetVitals with { Statuses = 0x20000 } });

        Assert.InRange(defended.ExpectedDamage, 0, normal.ExpectedDamage - 1);
        Assert.InRange(barrier.ExpectedDamage, 0, normal.ExpectedDamage - 1);
        BattleSmartAttackPreview physical = PhysicalPreview((normal.ExpectedDamage + barrier.ExpectedDamage) / 2);
        Assert.Equal(BattleAbility.Magic, BattleSmartAttackSelector.Select(physical, [Candidate(1, 36, normal)]).Ability);
        Assert.Equal(physical, BattleSmartAttackSelector.Select(physical, [Candidate(1, 36, barrier)]));
    }

    [Theory]
    [InlineData(0x4000u)]
    [InlineData(0x01000000u)]
    public void HostInvulnerabilityStatusesPreventFalseDamageAndKills(uint statuses)
    {
        Ff7BattleGuestPhysicalHit hit = Spell(90);
        Ff7BattleGuestContactContext context = Context(hit) with { TargetVitals = new(1, 10000, 0, 0, statuses) };

        Assert.True(Forecast(hit, context with { TargetVitals = context.TargetVitals with { Statuses = 0 } }).KillProbability > 0.9);
        BattleDamageForecast forecast = Forecast(hit, context);
        Assert.Equal(0, forecast.ExpectedDamage);
        Assert.Equal(0, forecast.KillProbability);
        Assert.Equal(PhysicalPreview(1), BattleSmartAttackSelector.Select(PhysicalPreview(1), [Candidate(1, 90, forecast)]));
    }

    [Fact]
    public void RepeatedForecastsLeaveContextAndSharedKernelUnchanged()
    {
        Ff7BattleGuestPhysicalHit hit = Spell(36);
        Ff7BattleGuestContactContext context = Context(hit);
        byte[] originalKernel = context.RandomKernel.ToArray();
        Ff7BattleGuestContactContext original = context;
        BattleDamageForecast first = Forecast(hit, context);

        for (int repetition = 0; repetition < 3; repetition++)
        {
            BattleDamageForecast repeated = Forecast(hit, context);
            Assert.Equal(first.ExpectedDamage, repeated.ExpectedDamage);
            Assert.Equal(first.KillProbability, repeated.KillProbability);
            Assert.Equal(first.Distribution.OrderBy(pair => pair.Key), repeated.Distribution.OrderBy(pair => pair.Key));
        }

        Assert.Equal(original, context);
        Assert.Equal(originalKernel, context.RandomKernel.ToArray());
        Assert.True(first.ExpectedDamage > 0);
        Assert.Equal(0, first.KillProbability);
        Assert.Equal(1, first.Distribution.Values.Sum(), 10);
    }

    private static BattleDamageForecast Forecast(Ff7BattleGuestPhysicalHit hit, Ff7BattleGuestContactContext context)
    {
        return CrossoverGuestHostActions.ForecastSmartContact(hit, context);
    }

    private static Ff7BattleGuestPhysicalHit Spell(byte power)
    {
        BattlePlayerStats stats = new(17, 8, 6, 8, 20, 16, 100, 0, new int[8], 0, 0);
        BattleNativeAction action = NativeAction(1, power);
        CrossoverFf8BattleKit kit = new(0, 100, 100, 7, 0, stats, new(0, 0, []), action, new(0, 5, 0, 10, []), 0, [2], []);
        return CrossoverBattleAbilityRules.Describe(kit, action);
    }

    private static Ff7BattleGuestContactContext Context(Ff7BattleGuestPhysicalHit hit)
    {
        byte[] kernel = new byte[3988];
        for (int index = 0; index < 256; index++)
        {
            kernel[0xE1C + index] = (byte)index;
        }
        return new(hit.Source, new(100, 100, 100, 100, 0), new(10, 10, 10, 10, 10, 0, 0, 20, 20, IsEnemy: true), new(10000, 10000, 0, 0, 0), hit.Attack, kernel);
    }

    private static BattleSmartAttackCandidate Candidate(int id, byte power, BattleDamageForecast forecast)
    {
        BattleNativeAction native = NativeAction((ushort)id, power);
        BattleActionDefinition action = new(BattleAbility.Magic, id, $"Spell {id}", BattleActionEffect.Native, 0, false) { NativeAction = native };
        return new(action, 10, 10, 1, forecast);
    }

    private static BattleSmartAttackPreview PhysicalPreview(double damage)
    {
        return new("guest", "target", BattleAbility.Attack, 0, "Attack", 1, false, "test", 4, damage, 0);
    }

    private static BattleNativeAction NativeAction(ushort id, byte power)
    {
        return new(2, id, 1, 1)
        {
            MagicId = 1, AttackType = 2, Power = power, HitCount = 1, TargetFlags = 0x10, AttackFlags = 1, DamagePresentation = 0, Element = 1, ElementPercent = null, StatusAttack = 0, Statuses0 = 0, Statuses1 = 0, Hit = 255, Critical = 0, PowerModifier = null, LevelModifier = null, RawRecord = [], SupplementalRecord = []
        };
    }
}
