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
    [InlineData(0x44, 6)]
    [InlineData(0x04, 4)]
    [InlineData(0x48, 0x16)]
    [InlineData(0x08, 0x14)]
    [InlineData(0x20, 4)]
    [InlineData(0x34, 0)]
    public void TargetSideUsesTheOriginalEnemyBitAndSideSelectionMode(byte native, byte host)
    {
        Assert.Equal(host, CrossoverBattleAbilityRules.HostTargetFlags(native));
    }

    [Theory]
    [InlineData(1, 0x11, true, false, false, 28)]
    [InlineData(2, 0x22, false, false, false, 16)]
    [InlineData(3, 0x25, false, true, false, 10)]
    [InlineData(4, 0x26, false, true, false, 10)]
    [InlineData(5, 0x24, false, true, false, 4)]
    [InlineData(6, 0x24, false, true, false, 32)]
    [InlineData(7, 0x11, true, false, false, 28)]
    [InlineData(8, 0x23, false, false, false, 72)]
    [InlineData(9, 0x11, true, false, false, 28)]
    [InlineData(11, 0x22, false, false, false, 16)]
    [InlineData(15, 0x22, false, false, true, 16)]
    [InlineData(20, 0x24, false, false, false, 72)]
    [InlineData(21, 0x24, false, true, false, 72)]
    [InlineData(22, 0x22, false, false, true, 16)]
    [InlineData(25, 0x26, false, true, false, 10)]
    [InlineData(26, 0x22, false, false, false, 16)]
    [InlineData(27, 0x26, false, false, false, 16)]
    [InlineData(29, 0x26, false, false, false, 16)]
    [InlineData(32, 0x24, false, true, false, 72)]
    [InlineData(35, 0x26, false, false, false, 16)]
    [InlineData(36, 0x11, true, false, true, 28)]
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
        Assert.Equal(type is 5 or 6, hit.Attack.HasStatusOperation);
        Assert.Equal(type is 5 or 6, hit.StatusRecord is not null);
        Assert.Null(hit.PreparedContact);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(6)]
    [InlineData(7)]
    [InlineData(8)]
    [InlineData(9)]
    [InlineData(11)]
    [InlineData(15)]
    [InlineData(20)]
    [InlineData(21)]
    [InlineData(22)]
    [InlineData(25)]
    [InlineData(26)]
    [InlineData(27)]
    [InlineData(29)]
    [InlineData(32)]
    [InlineData(35)]
    [InlineData(36)]
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
    [InlineData(25)]
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
        BattleNativeAction action = Action(2) with { Statuses0 = 0x3F, Statuses1 = Ff7GuestBattleStatuses.SupportedOriginBattle & ~8u, StatusAttack = 100 };
        Assert.Null(CrossoverBattleAbilityRules.UnsupportedReason(action));
        Ff7BattleGuestPhysicalHit hit = CrossoverBattleAbilityRules.Describe(Kit(), action);
        Assert.Equal(Ff7GuestBattleStatuses.SupportedHost & ~0x400u, hit.StatusRecord!.StatusMask);
        Assert.True(hit.Attack.HasStatusOperation);
        Assert.True(hit.Attack.AddsStatuses);
        Assert.Equal(0, hit.StatusRecord.StatusChange >> 6);
    }

    [Theory]
    [InlineData(0x10u, 0x8000u)]
    [InlineData(0x80u, 0x40000u)]
    [InlineData(0x400u, 0x200000u)]
    [InlineData(0x800u, 0x1000000u)]
    [InlineData(0x1000u, 0x2000u)]
    [InlineData(0x4000u, 0x40u)]
    public void EveryAdditionalExactStatusCounterpartUsesTheSharedMapping(uint origin, uint host)
    {
        BattleNativeAction action = Action(2) with { Statuses1 = origin, StatusAttack = 100 };
        Assert.Null(CrossoverBattleAbilityRules.UnsupportedReason(action));
        Ff7BattleGuestPhysicalHit hit = CrossoverBattleAbilityRules.Describe(Kit(), action);
        Assert.Equal(host, hit.StatusRecord!.StatusMask);
        Assert.Equal(hit, CrossoverBattleAbilityRules.Describe(Kit(), action with { MagicId = 511, ActionId = 200, Name = "Different status producer" }));
        Ff7BattleGuestPhysicalHit cure = CrossoverBattleAbilityRules.Describe(Kit(), action with { AttackType = 3 });
        Assert.Equal(host, cure.StatusRecord!.StatusMask);
        Assert.Equal(1, cure.StatusRecord.StatusChange >> 6);
    }

    [Theory]
    [InlineData(0, 0x200u)]
    public void OriginOnlyStatusesReportMissingCounterpartsWithoutInventingHostAliases(ushort saved, uint battle)
    {
        BattleNativeAction action = Action(2) with { Statuses0 = saved, Statuses1 = battle };
        Assert.Contains("no direct FFVII counterpart", CrossoverBattleAbilityRules.UnsupportedReason(action));
        Assert.Throws<NotSupportedException>(() => CrossoverBattleAbilityRules.Describe(Kit(), action));
        Assert.Equal(0u, Ff7GuestBattleStatuses.ToHostStatuses(saved, battle));
    }

    [Theory]
    [InlineData(0x100u)]
    [InlineData(0x2000u)]
    [InlineData(0x20000u)]
    [InlineData(0x40000u)]
    [InlineData(0x60000u)]
    public void SupportedOriginStatusesUseTheSourceBankWithoutHostStatusCollisions(uint mask)
    {
        BattleNativeAction action = Action(2) with { Power = 0, Element = 0, Statuses1 = mask, StatusAttack = 255 };
        Assert.Null(CrossoverBattleAbilityRules.UnsupportedReason(action));
        Ff7BattleGuestPhysicalHit hit = CrossoverBattleAbilityRules.Describe(Kit(), action);
        Assert.Equal(mask, hit.Attack.GuestOriginBattleStatuses);
        Assert.Equal(0u, hit.StatusRecord!.StatusMask);
        Ff7BattleActionReceipt receipt = Resolve(hit, new(100, 100, 0, 0, 0), new(100, 100, 0, 0, 0));
        Assert.False(receipt.Damage.Missed);
        Assert.Equal(mask, receipt.AddedOriginBattleStatuses);
        Assert.Equal(0u, receipt.Target.Statuses);
        Assert.Equal(0, receipt.Damage.Damage);
    }

    [Fact]
    public void DrainUsesTheNativeStatusOperationAndCommonSourceReceipt()
    {
        BattleNativeAction action = Action(2) with { Statuses1 = 0x8000, StatusAttack = 200 };
        Assert.Null(CrossoverBattleAbilityRules.UnsupportedReason(action));
        Ff7BattleGuestPhysicalHit hit = CrossoverBattleAbilityRules.Describe(Kit(), action);
        Assert.Null(hit.StatusRecord);
        Assert.Equal((byte)200, hit.Attack.GuestDrainStatusAttack);
        Ff7BattleActionVitals source = new(1, 9999, 0, 0, 0);
        Ff7BattleActionReceipt receipt = Resolve(hit, source, new(9999, 9999, 0, 0, 0));
        Assert.False(receipt.Damage.Missed);
        Assert.True(receipt.Damage.Damage > 0);
        Assert.Equal(-receipt.Damage.Damage, receipt.SourceHitPointDamage);
        Assert.Equal(1u + (uint)receipt.Damage.Damage, receipt.Source.Hp);
        Assert.Equal(0u, receipt.AddedOriginBattleStatuses);
        Assert.Equal(hit.Attack, CrossoverBattleAbilityRules.Describe(Kit(), action with { MagicId = 511, ActionId = 200, Name = "Other source record" }).Attack);
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
        List<BattleNativeAction> unsupported = [Action(2) with { PresentationOnly = true }, Action(2) with { Statuses1 = 0x10000 }];
        Assert.Null(CrossoverBattleAbilityRules.UnsupportedReason(Action(2) with { HitCount = 2 }));
        for (byte type = 0; type <= 36; type++)
        {
            if (type is not (1 or 2 or 3 or 4 or 5 or 6 or 8 or 9 or 11 or 15 or 20 or 21 or 22 or 25 or 26 or 27 or 29 or 32 or 35 or 36))
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

    [Theory]
    [InlineData(5, 100)]
    [InlineData(6, 800)]
    public void RevivalRestoresTheNativeFractionAndRemovesDeathOnlyOnKnockedOutTargets(byte type, uint restored)
    {
        Ff7BattleGuestPhysicalHit hit = CrossoverBattleAbilityRules.Describe(Kit(), Action(type));
        Ff7BattleActionVitals source = new(100, 100, 0, 0, 0);
        Ff7BattleActionVitals knockedOut = new(0, 800, 0, 0, Ff7NativeBattleActionResolver.Death);
        Ff7BattleActionReceipt receipt = Resolve(hit, source, knockedOut);
        Assert.False(receipt.Damage.Missed);
        Assert.Equal(restored, receipt.Target.Hp);
        Assert.Equal(1u, receipt.RemovedStatuses);
        Assert.Equal(0u, receipt.Target.Statuses);
        Assert.Equal(restored, Resolve(hit, source, knockedOut with { Hp = 400 }).Target.Hp);
        Assert.Equal(restored, Resolve(hit, source, knockedOut, targetLevel: 99, magicEvade: 100).Target.Hp);
        Assert.Equal(restored, Resolve(hit, source, knockedOut with { Statuses = 0x1004001 }).Target.Hp);
        Assert.True(Resolve(hit, source, knockedOut with { Hp = 1, Statuses = 0 }).Damage.Missed);
        Assert.Equal(source, receipt.Source);
        Assert.Equal(type == 5 ? 1u : 7u, Resolve(hit, source, knockedOut with { MaxHp = 7 }).Target.Hp);
    }

    [Fact]
    public void FullRecoveryRemovesOnlyHostStatusesAndUsesMaximumHpWithoutMagicBarrier()
    {
        BattleNativeAction action = Action(32) with { Power = 16, Statuses0 = 0x7E, Statuses1 = uint.MaxValue, StatusAttack = 255 };
        Assert.Null(CrossoverBattleAbilityRules.UnsupportedReason(action));
        Ff7BattleGuestPhysicalHit hit = CrossoverBattleAbilityRules.Describe(Kit(), action);
        Ff7BattleActionReceipt receipt = Resolve(hit, new(100, 100, 0, 0, 0), new(10, 800, 0, 0, 0x20008));
        Assert.False(receipt.Damage.Missed);
        Assert.Equal(-800, receipt.Damage.Damage);
        Assert.Equal(800u, receipt.Target.Hp);
        Assert.Equal(0u, receipt.Target.Statuses);
        Assert.False(hit.Attack.AddsStatuses);
    }

    [Theory]
    [InlineData(0u, -1000)]
    [InlineData(250u, -750)]
    [InlineData(1000u, 0)]
    public void MissingSourceHpRecoveryUsesExactSourceVitalsWithoutMagicDefenseShellOrVariance(uint sourceHp, int damage)
    {
        Ff7BattleGuestPhysicalHit hit = CrossoverBattleAbilityRules.Describe(Kit(), Action(25) with { Power = 0 });
        Assert.True(hit.Attack.SourceMissingHitPoints);
        Assert.Null(hit.Attack.FixedDamage);
        Ff7BattleActionVitals source = new(sourceHp, 1000, 0, 0, sourceHp == 0 ? Ff7NativeBattleActionResolver.Death : 0u);
        Ff7BattleActionReceipt receipt = Resolve(hit, source, new(100, 2000, 0, 0, 0x20000), magicDefense: 500);
        Assert.False(receipt.Damage.Missed);
        Assert.Equal(damage, receipt.Damage.Damage);
        Assert.Equal((uint)(100 - damage), receipt.Target.Hp);
        Assert.Equal(source, receipt.Source);
    }

    [Fact]
    public void SourceOperandsAreCapturedOnceBeforeSelfRecoveryAndRemainStableAfterJsonContinuation()
    {
        Ff7BattleGuestPhysicalHit hit = CrossoverBattleAbilityRules.Describe(Kit(), Action(25));
        Ff7BattleDamageAction captured = Ff7NativeBattleDamage.BindSourceVitals(hit.Attack, 250, 1000);
        Assert.False(captured.SourceMissingHitPoints);
        Assert.Equal(750, captured.FixedDamage);
        captured = System.Text.Json.JsonSerializer.Deserialize<Ff7BattleDamageAction>(System.Text.Json.JsonSerializer.Serialize(captured));
        Ff7BattleGuestPhysicalHit prepared = hit with { Attack = captured };
        Ff7BattleActionVitals source = new(250, 1000, 0, 0, 0);
        Ff7BattleActionReceipt self = Ff7NativeBattleActionResolver.ResolveGuestContact(prepared.Source, source, prepared.Source, source, captured, null, new(new byte[3988], new(new byte[8])), sameActor: true);
        Assert.Equal(1000u, self.Source.Hp);
        Ff7BattleActionReceipt next = Resolve(prepared, self.Source, new(100, 2000, 0, 0, 0));
        Assert.Equal(-750, next.Damage.Damage);
        Assert.Equal(850u, next.Target.Hp);
    }

    [Theory]
    [InlineData(0u, false, -750)]
    [InlineData(0u, true, 750)]
    [InlineData(0x4000u, false, 0)]
    [InlineData(0x1000000u, false, 0)]
    public void MissingSourceHpRecoveryUsesTheCommonZombieAndHostProtectionNormalization(uint statuses, bool zombie, int damage)
    {
        Ff7BattleGuestPhysicalHit hit = CrossoverBattleAbilityRules.Describe(Kit(), Action(25));
        Ff7BattleActionVitals target = new(1000, 2000, 0, 0, statuses) { OriginSavedStatuses = zombie ? Ff7OriginSavedStatusRules.Zombie : (ushort)0 };
        Ff7BattleActionReceipt receipt = Resolve(hit, new(250, 1000, 0, 0, 0), target);
        Assert.False(receipt.Damage.Missed);
        Assert.Equal(damage, receipt.Damage.Damage);
        Assert.Equal((uint)(1000 - damage), receipt.Target.Hp);
    }

    [Fact]
    public void MissingSourceHpRecoveryRejectsUnknownCorruptOrConflictingOperands()
    {
        Ff7BattleDamageAction action = CrossoverBattleAbilityRules.Describe(Kit(), Action(25)).Attack;
        Assert.Throws<InvalidDataException>(() => Ff7NativeBattleDamage.BindSourceVitals(action, null, 1000));
        Assert.Throws<InvalidDataException>(() => Ff7NativeBattleDamage.BindSourceVitals(action, 100, null));
        Assert.Throws<InvalidDataException>(() => Ff7NativeBattleDamage.BindSourceVitals(action, 1001, 1000));
        Assert.Throws<InvalidDataException>(() => Ff7NativeBattleDamage.BindSourceVitals(action with { FixedDamage = 10 }, 250, 1000));
        Assert.Throws<InvalidDataException>(() => Ff7NativeBattleDamage.BindSourceVitals(action with { Formula = 0x22 }, 250, 1000));
    }

    [Theory]
    [InlineData(11, false)]
    [InlineData(15, true)]
    public void GuardianForceBaseUsesItsOwnLevelAndLearnedBonusBeforeHostMitigation(byte type, bool ignoresDefense)
    {
        BattleNativeAction action = Action(type) with { LevelModifier = 30, PowerModifier = 20 };
        Ff7BattleGuestPhysicalHit hit = CrossoverBattleAbilityRules.Describe(Kit(), action, 50, 20);
        Assert.Equal(NativeBattleDamage.GuardianForce(9, 36, 50, 30, 20, 20, 16), hit.Attack.BaseDamage);
        Assert.Equal(ignoresDefense, hit.Attack.IgnoreDefense);
        Ff7BattleActionVitals source = new(100, 100, 0, 0, 0);
        Ff7BattleActionVitals target = new(9000, 9000, 0, 0, 0);
        Ff7BattleActionReceipt plain = Resolve(hit, source, target);
        Ff7BattleActionReceipt defended = Resolve(hit, source, target, magicDefense: 256);
        if (ignoresDefense)
        {
            Assert.Equal(plain.Damage.Damage, defended.Damage.Damage);
        }
        else
        {
            Assert.InRange(defended.Damage.Damage, plain.Damage.Damage / 2 - 1, plain.Damage.Damage / 2 + 1);
        }
        Assert.Equal(hit.Attack, CrossoverBattleAbilityRules.Describe(Kit() with { Stats = Kit().Stats with { Magic = 200 } }, action, 50, 20).Attack);
    }

    [Fact]
    public void MaximumHpGravityAndFixedLevelDamageDoNotUseCasterMagic()
    {
        Ff7BattleGuestPhysicalHit gravity = CrossoverBattleAbilityRules.Describe(Kit(), Action(20) with { PowerModifier = 10, LevelModifier = 10 }, 35);
        Ff7BattleActionVitals source = new(100, 100, 0, 0, 0);
        Ff7BattleActionVitals target = new(700, 1000, 0, 0, 0);
        Assert.Equal(350, Resolve(gravity, source, target).Damage.Damage);
        Ff7BattleGuestPhysicalHit fixedHit = CrossoverBattleAbilityRules.Describe(Kit(), Action(29) with { Power = 100 }, 29);
        Assert.Equal(3000, Resolve(fixedHit, source, target, magicDefense: 500).Damage.Damage);
        Assert.Equal(1, Resolve(CrossoverBattleAbilityRules.Describe(Kit(), Action(35) with { Power = 0 }), source, target).Damage.Damage);
    }

    [Fact]
    public void LevelConditionRejectsIneligibleTargetsBeforeDamageAndStopUsesTheSharedStatusDescriptor()
    {
        Ff7BattleGuestPhysicalHit hit = CrossoverBattleAbilityRules.Describe(Kit(), Action(26) with { Hit = 5 });
        Assert.Equal((byte)5, hit.Attack.RequiredTargetLevelMultiple);
        Assert.True(Resolve(hit, new(100, 100, 0, 0, 0), new(700, 1000, 0, 0, 0), targetLevel: 9).Damage.Missed);
        Assert.False(Resolve(hit, new(100, 100, 0, 0, 0), new(700, 1000, 0, 0, 0), targetLevel: 10).Damage.Missed);
        BattleNativeAction stop = Action(2) with { Statuses1 = 8 };
        Assert.Null(CrossoverBattleAbilityRules.UnsupportedReason(stop));
        Assert.Equal(0x400u, CrossoverBattleAbilityRules.Describe(Kit(), stop).StatusRecord!.StatusMask);
        Assert.Null(CrossoverBattleAbilityRules.UnsupportedReason(Action(3) with { Statuses1 = 8 }));
    }

    [Theory]
    [InlineData(27)]
    [InlineData(29)]
    [InlineData(35)]
    public void NativeFixedDamageFamiliesDoNotProduceTheirUnusedStatusBanks(byte type)
    {
        BattleNativeAction action = Action(type) with { Statuses0 = ushort.MaxValue, Statuses1 = uint.MaxValue, StatusAttack = 255 };
        Assert.Null(CrossoverBattleAbilityRules.UnsupportedReason(action));
        Assert.Null(CrossoverBattleAbilityRules.Describe(Kit(), action).StatusRecord);
        Ff7BattleGuestPhysicalHit hit = CrossoverBattleAbilityRules.Describe(Kit(), action with { Hit = 1 });
        Assert.False(Resolve(hit, new(100, 100, 0, 0, 0), new(9000, 9000, 0, 0, 0), targetLevel: 99, magicEvade: 100).Damage.Missed);
    }

    [Fact]
    public void Original_full_source_dispel_removes_both_host_and_source_dispellable_categories_without_changing_identity()
    {
        using BattleAssets assets = new(TestResources.GameDirectory);
        BattleNativeAction action = Assert.Single(assets.NativeActions, action => action.Command == 2 && action.Name == "Dispel");
        Assert.Equal(Ff7GuestBattleStatuses.DispellableOriginBattle, action.Statuses1);
        Ff7BattleGuestPhysicalHit hit = CrossoverBattleAbilityRules.Describe(Kit(), action);
        Assert.Equal(Ff7NativeBattleStatuses.BeneficialDispelMask, hit.StatusRecord!.StatusMask);
        Assert.Equal(hit, CrossoverBattleAbilityRules.Describe(Kit(), action with { Name = "Another identity", ActionId = 201, MagicId = 511 }));
        uint removable = Ff7NativeBattleStatuses.BeneficialDispelMask & ~Ff7NativeBattleStatuses.Resist;
        Ff7BattleActionVitals target = new(100, 100, 0, 0, removable | Ff7NativeBattleActionResolver.Poison);
        Ff7BattleRng random = new(new byte[3988], new(new byte[8]));
        Ff7BattleActionReceipt receipt = Ff7NativeBattleActionResolver.ResolveGuestContact(hit.Source, new(100, 100, 0, 0, 0), new(10, 10, 10, 0, 0, 0, 0, 0, 0), target, hit.Attack, hit.StatusRecord, random, originBattleStatuses: Ff7GuestBattleStatuses.SupportedOwnedOriginBattle | Ff7GuestBattleStatuses.OriginCurse);
        Assert.False(receipt.Damage.Missed);
        Assert.Equal(Ff7NativeBattleActionResolver.Poison, receipt.Target.Statuses);
        Assert.Equal(Ff7GuestBattleStatuses.SupportedOwnedOriginBattle, receipt.RemovedOriginBattleStatuses);
        Ff7BattleGuestPhysicalHit narrow = CrossoverBattleAbilityRules.Describe(Kit(), action with { Statuses1 = 0x20 });
        Assert.Equal(Ff7NativeBattleStatuses.Barrier, narrow.StatusRecord!.StatusMask);
    }

    [Fact]
    public void Physical_fraction_family_projects_current_hp_and_original_record_without_identity_gates()
    {
        using BattleAssets assets = new(TestResources.GameDirectory);
        BattleNativeAction original = Assert.Single(assets.NativeActions, action => action.Command == 241 && action.ActionId == 5);
        Assert.Equal((byte)7, original.AttackType);
        Ff7BattleGuestPhysicalHit hit = CrossoverBattleAbilityRules.Describe(Kit(), original);
        Assert.Equal((byte)0x11, hit.Attack.Formula);
        Assert.True(hit.Attack.UsePhysicalStats);
        Assert.False(hit.Attack.SuppressCritical);
        Assert.Equal(original.Power, hit.Attack.FractionNumerator);
        Assert.Equal(16, hit.Attack.FractionDenominator);
        Assert.Equal(hit, CrossoverBattleAbilityRules.Describe(Kit(), original with { Command = 236, ActionId = 65500, MagicId = 511, Name = "Different identity" }));
    }

    [Theory]
    [InlineData(0u, false, 0u, 1, 400)]
    [InlineData(Ff7NativeBattleStatuses.Barrier, false, 0u, 1, 200)]
    [InlineData(Ff7NativeBattleStatuses.MagicBarrier, false, 0u, 1, 400)]
    [InlineData(0u, true, 0u, 1, 800)]
    [InlineData(0u, false, 0x100u, 1, 0)]
    [InlineData(0u, false, 0x200u, 1, 400)]
    [InlineData(0u, false, 0u, 5, 2000)]
    public void Physical_fraction_uses_host_modifiers_and_immunity_without_damage_variance(uint statuses, bool critical, uint conditions, int multiplier, int expected)
    {
        Ff7BattleDamageAction action = CrossoverBattleAbilityRules.Describe(Kit(), Action(7) with { Power = 8 }).Attack with { AlwaysHits = true, SuppressCritical = true, ForceCritical = critical, OriginDamageMultiplier = multiplier };
        Ff7BattleDamageActor target = new(10, 10, 10, 0, 0, 0, 0, 512, 512, Statuses: statuses, CurrentHitPoints: 800, MaximumHitPoints: 1000) { NativeConditions = conditions };
        Ff7BattleRng first = new(new byte[3988], new(new byte[8]));
        Ff7BattleRng second = new(Enumerable.Repeat((byte)255, 3988).ToArray(), new(new byte[8]));
        Assert.Equal(expected, Ff7NativeBattleDamage.Resolve(KitSource(), target, action, first).Damage);
        Assert.Equal(expected, Ff7NativeBattleDamage.Resolve(KitSource(), target, action, second).Damage);
        Ff7BattleDamageActor large = target with { CurrentHitPoints = 10000, MaximumHitPoints = 10000 };
        if (multiplier == 5)
        {
            Assert.Equal(9999, Ff7NativeBattleDamage.Resolve(KitSource(), large, action, first).Damage);
        }

        static Ff7BattleDamageActor KitSource() => new(10, 1, 1, 0, 0, 0, 0, 0, 0);
    }

    private static Ff7BattleActionReceipt Resolve(Ff7BattleGuestPhysicalHit hit, Ff7BattleActionVitals source, Ff7BattleActionVitals target, byte targetLevel = 10, ushort magicDefense = 0, byte magicEvade = 0)
    {
        Ff7BattleDamageActor targetStats = new(targetLevel, 10, 10, 0, 0, 0, magicEvade, 0, magicDefense);
        Ff7BattleRng random = new(new byte[3988], new(new byte[8]));
        return Ff7NativeBattleActionResolver.ResolveGuestContact(hit.Source, source, targetStats, target, hit.Attack, hit.StatusRecord, random);
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
