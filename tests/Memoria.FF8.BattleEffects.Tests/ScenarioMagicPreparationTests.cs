using Memoria.FF8.Runtime;

namespace Memoria.FF8.BattleEffects.Tests;

public sealed class ScenarioMagicPreparationTests
{
    [Theory]
    [InlineData(3, 0, false)]
    [InlineData(3, 0, true)]
    [InlineData(2, 40, false)]
    [InlineData(2, 40, true)]
    [InlineData(3, 20, false)]
    [InlineData(32, 16, false)]
    public void NonhealingMagicRetainsTheResourceDerivedMaximumInsteadOfAnInheritedOneHpTemplate(byte type, byte power, bool partyRecipient)
    {
        Assert.Equal(486, ScenarioDiscovery.ResolveMagicPreparationHitPoints(Action(type, power), partyRecipient, 486));
        Assert.Equal(2321, ScenarioDiscovery.ResolveMagicPreparationHitPoints(Action(type, power), partyRecipient, 2321));
    }

    [Theory]
    [InlineData(3)]
    [InlineData(32)]
    public void PositivePowerHealingOfThePartyHasAGenuineLowHpRecipient(byte type)
    {
        Assert.Equal(1, ScenarioDiscovery.ResolveMagicPreparationHitPoints(Action(type, 20), true, 486));
    }

    private static BattleNativeAction Action(byte type, byte power) => new(2, 28, 1, 28)
    {
        MagicId = 109,
        AttackType = type,
        Power = power,
        HitCount = 1,
        TargetFlags = 84,
        AttackFlags = 97,
        DamagePresentation = 0,
        Element = 0,
        ElementPercent = null,
        StatusAttack = 254,
        Statuses0 = 0,
        Statuses1 = 0,
        Hit = null,
        Critical = null,
        PowerModifier = null,
        LevelModifier = null,
        RawRecord = [],
        SupplementalRecord = []
    };
}
