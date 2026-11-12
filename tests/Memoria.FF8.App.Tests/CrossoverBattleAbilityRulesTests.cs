using Memoria.FF7.Runtime;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class CrossoverBattleAbilityRulesTests
{
    [Theory]
    [InlineData(0x54, 2)]
    [InlineData(0x14, 0)]
    [InlineData(0x58, 0x12)]
    [InlineData(0x18, 0x10)]
    public void TargetSideUsesTheOriginalEnemyBitAndSideSelectionMode(byte native, byte host)
    {
        Assert.Equal(host, CrossoverBattleAbilityRules.HostTargetFlags(native));
    }

    [Theory]
    [InlineData(1, 0x11, true, false, false, 28)]
    [InlineData(2, 0x22, false, false, false, 16)]
    [InlineData(3, 0x25, false, true, false, 10)]
    [InlineData(8, 0x23, false, false, false, 72)]
    [InlineData(9, 0x11, true, false, false, 28)]
    [InlineData(21, 0x24, false, true, false, 72)]
    [InlineData(22, 0x22, false, false, true, 16)]
    public void AdmittedFamiliesUseSharedFormulaAndPowerProjection(byte type, byte formula, bool physical, bool healing, bool ignoreDefense, byte power)
    {
        BattleNativeAction action = Action(type);
        Assert.Null(CrossoverBattleAbilityRules.UnsupportedReason(action));
        Ff7BattleGuestPhysicalHit hit = CrossoverBattleAbilityRules.Describe(Kit(), action);

        Assert.Equal(formula, hit.Attack.Formula);
        Assert.Equal(power, hit.Attack.Power);
        Assert.Equal(physical, hit.Attack.UsePhysicalStats);
        Assert.Equal(healing, hit.Attack.Healing);
        Assert.Equal(ignoreDefense, hit.Attack.IgnoreDefense);
        Assert.Equal(!physical, hit.Attack.SuppressCritical);
        Assert.Equal(!physical, hit.Attack.LongRange);
        Assert.False(hit.Attack.HasStatusOperation);
        Assert.Null(hit.StatusRecord);
        Assert.Null(hit.PreparedContact);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(8)]
    [InlineData(9)]
    [InlineData(21)]
    [InlineData(22)]
    public void IdentityAndPresentationNeverSelectNumericMechanics(byte type)
    {
        CrossoverFf8BattleKit kit = Kit();
        BattleNativeAction original = Action(type);
        BattleNativeAction other = original with { Command = 236, ActionId = 217, MagicId = 511, KernelSection = 19, KernelIndex = 217, Name = "Different identity", DamagePresentation = 6, RawRecord = [7, 8, 9], SupplementalRecord = [10] };

        Assert.Equal(CrossoverBattleAbilityRules.Describe(kit, original), CrossoverBattleAbilityRules.Describe(kit, other));
        Assert.Equal(CrossoverBattleAbilityRules.UnsupportedReason(original), CrossoverBattleAbilityRules.UnsupportedReason(other));
    }

    [Theory]
    [InlineData(2, 18, 8)]
    [InlineData(2, 36, 16)]
    [InlineData(2, 90, 40)]
    [InlineData(3, 18, 5)]
    [InlineData(3, 36, 10)]
    [InlineData(3, 90, 25)]
    public void MagicAndHealingKeepOneCalibrationAcrossEverySourcePower(byte type, byte nativePower, byte hostPower)
    {
        Ff7BattleGuestPhysicalHit hit = CrossoverBattleAbilityRules.Describe(Kit(), Action(type) with { Power = nativePower });
        Assert.Equal(hostPower, hit.Attack.Power);
    }

    [Theory]
    [InlineData(1, 1)]
    [InlineData(2, 2)]
    [InlineData(4, 4)]
    [InlineData(8, 8)]
    [InlineData(16, 16)]
    [InlineData(32, 128)]
    [InlineData(64, 64)]
    [InlineData(128, 256)]
    public void NativeElementsAreTranslatedByIdentityForEveryAction(byte nativeElement, ushort hostElement)
    {
        Ff7BattleGuestPhysicalHit hit = CrossoverBattleAbilityRules.Describe(Kit(), Action(2) with { Element = nativeElement });
        Assert.Equal(hostElement, hit.Elements);
        Assert.Null(hit.ElementPercent);
        Ff7BattleGuestPhysicalHit physical = CrossoverBattleAbilityRules.Describe(Kit(), Action(9) with { Element = nativeElement, ElementPercent = 35 });
        Assert.Equal(hostElement, physical.Elements);
        Assert.Equal((byte)35, physical.ElementPercent);
    }

    [Theory]
    [InlineData(3, 1)]
    [InlineData(96, 128)]
    [InlineData(192, 64)]
    public void MultipleNativeElementsSelectTheLowestNativeBit(byte nativeElements, ushort hostElement)
    {
        Ff7BattleGuestPhysicalHit hit = CrossoverBattleAbilityRules.Describe(Kit(), Action(2) with { Element = nativeElements });
        Assert.Equal(hostElement, hit.Elements);
    }

    [Theory]
    [InlineData(3)]
    [InlineData(21)]
    public void HealingUsesTheSharedRestorativeElementAndRemovesStatuses(byte type)
    {
        Ff7BattleGuestPhysicalHit hit = CrossoverBattleAbilityRules.Describe(Kit(), Action(type) with { Element = 128, Statuses0 = 2, StatusAttack = 100 });
        Assert.Equal((ushort)0x200, hit.Elements);
        Assert.True(hit.Attack.HasStatusOperation);
        Assert.False(hit.Attack.AddsStatuses);
        Assert.Equal(1, hit.StatusRecord!.StatusChange >> 6);
        Assert.Equal(Ff7GuestBattleStatuses.FromOrigin(2), hit.StatusRecord.StatusMask);
    }

    [Fact]
    public void CommonStatusBanksReachTheHostWithoutChangingTheirIdentity()
    {
        BattleNativeAction action = Action(2) with { Statuses0 = 0x3F, Statuses1 = 0x6F, StatusAttack = 100 };
        Assert.Null(CrossoverBattleAbilityRules.UnsupportedReason(action));
        Ff7BattleGuestPhysicalHit hit = CrossoverBattleAbilityRules.Describe(Kit(), action);
        Assert.Equal(Ff7GuestBattleStatuses.SupportedHost, hit.StatusRecord!.StatusMask);
        Assert.True(hit.Attack.HasStatusOperation);
        Assert.True(hit.Attack.AddsStatuses);
        Assert.Equal(0, hit.StatusRecord.StatusChange >> 6);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(25)]
    [InlineData(50)]
    [InlineData(99)]
    [InlineData(100)]
    public void StatusChancePreservesTheHostFourPercentQuantization(byte chance)
    {
        Ff7BattleGuestPhysicalHit hit = CrossoverBattleAbilityRules.Describe(Kit(), Action(2) with { Statuses0 = 2, StatusAttack = chance });
        int threshold = (hit.StatusRecord!.StatusChange & 63) * 4;
        Assert.InRange(threshold, (int)chance, chance + 3);
    }

    [Fact]
    public void UnsupportedOperationsAndStatusBitsFailBeforeProducingMechanics()
    {
        List<BattleNativeAction> unsupported = [Action(2) with { PresentationOnly = true }, Action(2) with { HitCount = 2 }, Action(2) with { Statuses0 = 0x40 }, Action(2) with { Statuses1 = 0x10 }];
        for (byte type = 0; type <= 36; type++)
        {
            if (type is not (1 or 2 or 3 or 8 or 9 or 21 or 22))
            {
                unsupported.Add(Action(type));
            }
        }
        foreach (BattleNativeAction action in unsupported)
        {
            Assert.False(string.IsNullOrWhiteSpace(CrossoverBattleAbilityRules.UnsupportedReason(action)));
            Assert.Throws<NotSupportedException>(() => CrossoverBattleAbilityRules.Describe(Kit(), action));
        }
    }

    [Fact]
    public void PhysicalCriticalBonusComesFromTheOriginalAction()
    {
        Ff7BattleGuestPhysicalHit normal = CrossoverBattleAbilityRules.Describe(Kit(), Action(9) with { Critical = 0 });
        Ff7BattleGuestPhysicalHit increased = CrossoverBattleAbilityRules.Describe(Kit(), Action(9) with { Critical = 128 });
        Assert.Equal((byte)0, normal.Source.CriticalBonus);
        Assert.Equal((byte)50, increased.Source.CriticalBonus);
        Assert.Equal(normal.Attack, increased.Attack);
    }

    private static CrossoverFf8BattleKit Kit()
    {
        BattlePlayerStats stats = new(17, 8, 6, 8, 20, 16, 100, 0, new int[8], 0, 0);
        return new(0, 100, 100, 7, 0, stats, new(0, 0, []), Action(9), new(0, 5, 0, 10, []), 0, [2], []);
    }

    private static BattleNativeAction Action(byte type)
    {
        return new(2, 1, 1, 1)
        {
            MagicId = 1, AttackType = type, Power = 36, HitCount = 1, TargetFlags = 0x10, AttackFlags = 1, DamagePresentation = 0, Element = 1, ElementPercent = null, StatusAttack = 0, Statuses0 = 0, Statuses1 = 0, Hit = 100, Critical = 0, PowerModifier = null, LevelModifier = null, RawRecord = [], SupplementalRecord = []
        };
    }
}
