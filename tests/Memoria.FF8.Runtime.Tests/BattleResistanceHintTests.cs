using System.Collections.Immutable;

namespace Memoria.FF8.Runtime.Tests;

public sealed class BattleResistanceHintTests
{
    [Fact]
    public void OnlyPartyScanExposesCurrentMonsterDefenses()
    {
        BattleSession battle = Create(scanned: false);
        BattleCombatant enemy = battle.Combatants[1];
        BattleResistanceHintProfile profile = Profile(battle, Contact() with { Element = 1 });
        Assert.False(profile.IsEmpty);
        Assert.Null(battle.GetKnownElementResistance(enemy, 0));
        Assert.Null(battle.GetKnownStatusResistance(enemy, NativeStatusId.Poison));
        Assert.Equal(BattleResistanceHintRating.Neutral, battle.RateResistanceHints(enemy, profile));

        BattleSnapshot snapshot = battle.Capture();
        ImmutableArray<BattleKnownAffinity> oldAffinities = Enumerable.Repeat(BattleKnownAffinity.FromNativeDefense(700), 8).ToImmutableArray();
        BattleScanKnowledge enemyScan = new BattleScanKnowledge().Observe(new(BattleObserverSide.Enemy, enemy.Id, enemy.Id, 0, oldAffinities));
        BattleSession enemyObserver = BattleSession.Restore(snapshot with { ScanKnowledge = enemyScan });
        Assert.False(enemyObserver.IsPartyScanKnown(enemyObserver.Combatants[1]));
        BattleScanKnowledge partyScan = enemyScan.Observe(new(BattleObserverSide.Party, enemy.Id, battle.Combatants[0].Id, 0, oldAffinities));
        BattleSession observed = BattleSession.Restore(snapshot with { ScanKnowledge = partyScan });
        Assert.Equal(100, observed.GetKnownElementResistance(observed.Combatants[1], 0));
        Assert.Equal(0, observed.GetKnownStatusResistance(observed.Combatants[1], NativeStatusId.Poison));

        BattleSession knownModel = Create();
        Assert.True(knownModel.IsPartyScanKnown(knownModel.Combatants[1]));
        Assert.False(knownModel.IsPartyScanKnown(knownModel.Combatants[0]));
        BattleSession hidden = Create(flags: 0x2000);
        Assert.Null(hidden.GetKnownElementResistance(hidden.Combatants[1], 0));
        Assert.Null(hidden.GetKnownStatusResistance(hidden.Combatants[1], NativeStatusId.Poison));
    }

    [Fact]
    public void PhysicalAttackReadsItsWeaponAndJunctionContact()
    {
        BattleNativeAction attack = Contact(1) with { Element = 3, ElementPercent = 50, Statuses0 = 2, Statuses1 = 5 };
        BattleSession battle = Create(attack: attack);
        BattleResistanceHintProfile profile = battle.GetResistanceHintProfile(battle.Combatants[0], BattleAbility.Attack);
        Assert.Equal(0, profile.ElementIndex);
        Assert.Equal(1UL << (int)NativeStatusId.Poison | 1UL << (int)NativeStatusId.Sleep | 1UL << (int)NativeStatusId.Slow, profile.StatusMask);
        Assert.Equal(BattleResistanceHintRating.Favorable, battle.RateResistanceHints(battle.Combatants[1], profile));

        BattleSession partial = Create(attack: attack with { Statuses0 = 0, Statuses1 = 0 });
        Assert.Equal(BattleResistanceHintRating.Neutral, partial.RateResistanceHints(partial.Combatants[1], partial.GetResistanceHintProfile(partial.Combatants[0], BattleAbility.Attack)));
        BattleSession depleted = Create(attack: attack with { ElementPercent = 0 });
        Assert.Null(depleted.GetResistanceHintProfile(depleted.Combatants[0], BattleAbility.Attack).ElementIndex);
    }

    [Fact]
    public void MixedDamageAndStatusRequiresEveryChannelToBeIneffective()
    {
        byte[] resistances = Enumerable.Repeat((byte)200, 40).ToArray();
        resistances[18] = 150;
        resistances[24] = 151;
        BattleSession battle = Create(resistances: resistances);
        BattleCombatant target = battle.Combatants[1];
        BattleNativeAction spell = Contact() with { Element = 1, Statuses1 = 1u << 24 };
        Assert.Equal(100, battle.GetKnownStatusResistance(target, NativeStatusId.Vit0));
        Assert.Equal(50, battle.GetKnownStatusResistance(target, NativeStatusId.Doom));
        Assert.Equal(51, battle.GetKnownStatusResistance(target, NativeStatusId.Eject));
        Assert.Equal(BattleResistanceHintRating.Ineffective, battle.RateResistanceHints(target, Profile(battle, spell)));
        Assert.Equal(BattleResistanceHintRating.Neutral, battle.RateResistanceHints(target, Profile(battle, spell with { Element = 0 })));
        Assert.Equal(BattleResistanceHintRating.Neutral, battle.RateResistanceHints(target, Profile(battle, spell with { StatusAttack = 255 })));
        Assert.Equal(BattleResistanceHintRating.Neutral, battle.RateResistanceHints(target, Profile(battle, spell with { Statuses1 = 1u << 16 })));
        Assert.Equal(BattleResistanceHintRating.Favorable, battle.RateResistanceHints(target, Profile(battle, spell with { Statuses1 = spell.Statuses1 | 1u << 10 })));
        Assert.Equal(BattleResistanceHintRating.Favorable, battle.RateResistanceHints(target, Profile(battle, spell with { Element = 2 })));
        BattleSession missingBank = Create(resistances: []);
        Assert.Equal(BattleResistanceHintRating.Neutral, missingBank.RateResistanceHints(missingBank.Combatants[1], Profile(missingBank, spell)));
    }

    [Fact]
    public void GuardianUsesItsOwnContactAndHolyUsesTheZombieDefense()
    {
        BattleSession battle = Create(savedStatuses: 0x40);
        BattleNativeAction summon = Contact(11) with { Command = 254, ActionId = 65, KernelSection = 2, Element = 0x80, Statuses0 = 2, Statuses1 = 0x4000000u };
        BattleResistanceHintProfile profile = Profile(battle, summon, BattleAbility.GuardianForce);
        Assert.Equal(7, profile.ElementIndex);
        Assert.Equal(1UL << (int)NativeStatusId.Poison, profile.StatusMask);
        Assert.Equal(-100, battle.GetKnownElementResistance(battle.Combatants[1], 7));
        Assert.Equal(BattleResistanceHintRating.Favorable, battle.RateResistanceHints(battle.Combatants[1], profile));
        Assert.True(Profile(battle, summon with { AttackType = 29 }, BattleAbility.GuardianForce).IsEmpty);
    }

    [Fact]
    public void FloatBlocksEarthMagicBeforeStatusesAndPreservesTheEarthJunctionDefense()
    {
        BattleNativeAction earth = Contact() with { Element = 8, Statuses0 = 2 };
        BattleSession battle = Create(battleStatuses: 0x2000, attack: earth with { AttackType = 1, ElementPercent = 100 });
        BattleCombatant target = battle.Combatants[1];
        Assert.Equal(0, battle.GetKnownElementResistance(target, 3));
        Assert.Equal(BattleResistanceHintRating.Ineffective, battle.RateResistanceHints(target, Profile(battle, earth)));
        Assert.Equal(BattleResistanceHintRating.Favorable, battle.RateResistanceHints(target, Profile(battle, earth with { Statuses1 = 0x4000000 })));
        Assert.Equal(BattleResistanceHintRating.Favorable, battle.RateResistanceHints(target, battle.GetResistanceHintProfile(battle.Combatants[0], BattleAbility.Attack)));
    }

    [Fact]
    public void KnownWholeContactImmunityTakesPriorityOverWeaknessAndIncludesNonElementalDamage()
    {
        BattleNativeAction elemental = Contact() with { Element = 2, Statuses0 = 2 };
        BattleSession battle = Create(battleStatuses: 0x800);
        BattleCombatant target = battle.Combatants[1];
        Assert.Equal(-50, battle.GetKnownElementResistance(target, 1));
        Assert.Equal(BattleResistanceHintRating.Ineffective, battle.RateResistanceHints(target, Profile(battle, elemental)));
        Assert.Equal(BattleResistanceHintRating.Favorable, battle.RateResistanceHints(target, Profile(battle, elemental with { Statuses1 = 0x4000000 })));
        BattleResistanceHintProfile nonElemental = Profile(battle, Contact());
        Assert.True(nonElemental.CanRate);
        Assert.True(nonElemental.IsEmpty);
        Assert.Equal(BattleResistanceHintRating.Ineffective, battle.RateResistanceHints(target, nonElemental));
        Assert.False(Profile(battle, Contact(3)).CanRate);
        BattleSession unknown = Create(scanned: false, battleStatuses: 0x800);
        Assert.Equal(BattleResistanceHintRating.Neutral, unknown.RateResistanceHints(unknown.Combatants[1], nonElemental));
    }

    [Theory]
    [InlineData(8)]
    [InlineData(20)]
    public void PercentageDamageImmunityKeepsSecondaryStatusesEligible(byte type)
    {
        BattleSession battle = Create(flags: 0x10000);
        BattleCombatant target = battle.Combatants[1];
        BattleNativeAction gravity = Contact(type);
        Assert.Equal(BattleResistanceHintRating.Ineffective, battle.RateResistanceHints(target, Profile(battle, gravity)));
        Assert.Equal(BattleResistanceHintRating.Ineffective, battle.RateResistanceHints(target, Profile(battle, gravity with { Element = 2, Statuses1 = 0x8000 })));
        Assert.Equal(BattleResistanceHintRating.Favorable, battle.RateResistanceHints(target, Profile(battle, gravity with { Statuses0 = 2 })));
    }

    [Fact]
    public void DrainUsesItsOwnResistanceAndNeedsPrimaryDamage()
    {
        BattleNativeAction drain = Contact() with { Statuses1 = 0x8000, StatusAttack = 200 };
        BattleSession battle = Create(attack: drain with { AttackType = 1 });
        BattleCombatant target = battle.Combatants[1];
        Assert.Equal(1UL << (int)NativeStatusId.Drain, battle.GetResistanceHintProfile(battle.Combatants[0], BattleAbility.Attack).StatusMask);
        Assert.Equal(BattleResistanceHintRating.Favorable, battle.RateResistanceHints(target, Profile(battle, drain)));
        Assert.Equal(BattleResistanceHintRating.Ineffective, battle.RateResistanceHints(target, Profile(battle, drain with { Element = 1 })));
        Assert.True(Profile(battle, drain with { Power = 0 }).IsEmpty);

        BattleSession immuneDrain = Create(resistances: Enumerable.Repeat((byte)200, 40).ToArray());
        Assert.Equal(BattleResistanceHintRating.Neutral, immuneDrain.RateResistanceHints(immuneDrain.Combatants[1], Profile(immuneDrain, drain)));
    }

    [Theory]
    [InlineData(3)]
    [InlineData(5)]
    [InlineData(12)]
    [InlineData(32)]
    public void RestorativeAndScanFormulasDoNotAdvertiseTheirStatusMasks(byte type)
    {
        BattleSession battle = Create();
        Assert.True(Profile(battle, Contact(type) with { Element = 1, Statuses0 = 0x7F, Statuses1 = uint.MaxValue }).IsEmpty);
        Assert.True(Profile(battle, Contact() with { Statuses1 = 0x80 }, allies: true).IsEmpty);
    }

    private static BattleResistanceHintProfile Profile(BattleSession battle, BattleNativeAction native, BattleAbility ability = BattleAbility.Magic, bool allies = false)
    {
        BattleActionDefinition action = new(ability, 1, "Action", BattleActionEffect.Native, native.Power, allies) { NativeAction = native };
        return battle.GetResistanceHintProfile(battle.Combatants[0], ability, action);
    }

    private static BattleSession Create(bool scanned = true, uint flags = 0, ushort savedStatuses = 0, uint battleStatuses = 0, byte[]? resistances = null, BattleNativeAction? attack = null)
    {
        NativeScannedModels history = new(NativeInputConfiguration.OriginalInitHash, [0, 0, 0, 0, 0]);
        return new(new BattleDefinition
        {
            NativeRetainedMemory = NativeBattleRetainedMemory.FromColdProcess(),
            NativeScannedModels = scanned ? history.Add(2) : history,
            Combatants =
            [
                new() { Id = "party", Name = "Party", IsPlayer = true, NativeAttack = attack is null ? null : attack with { Command = 1, ActionId = 0, KernelSection = 4, KernelIndex = 0 } },
                new() { Id = "enemy", Name = "Enemy", ModelId = 2, NativeStatus0 = savedStatuses, NativeStatus1 = battleStatuses, NativeCombatantFlags = flags, ElementDefenses = [900, 750, 800, 800, 800, 800, 800, 1000], NativeStatusResistances = resistances ?? Enumerable.Repeat((byte)100, 40).ToArray() }
            ]
        }, BattleAtbMode.Normal);
    }

    private static BattleNativeAction Contact(byte type = 2)
    {
        return new(2, 1, 1, 1)
        {
            MagicId = 1, AttackType = type, Power = 20, HitCount = 1, TargetFlags = 0x50, AttackFlags = 1, DamagePresentation = 0,
            Element = 0, ElementPercent = null, StatusAttack = 180, Statuses0 = 0, Statuses1 = 0, Hit = 255, Critical = null,
            PowerModifier = null, LevelModifier = null, RawRecord = [], SupplementalRecord = []
        };
    }
}
