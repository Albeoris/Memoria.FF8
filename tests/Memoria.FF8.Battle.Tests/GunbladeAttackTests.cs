using Memoria.FF8.Runtime;

namespace Memoria.FF8.Battle.Tests;

public sealed class GunbladeAttackTests
{
    [Theory]
    [InlineData(0, false)]
    [InlineData(1, false)]
    [InlineData(19, false)]
    [InlineData(20, true)]
    [InlineData(25, true)]
    [InlineData(32, true)]
    public void NativeTriggerAcceptanceUsesTheHitOwnedTimingWord(short timing, bool expected)
    {
        Assert.Equal(expected, NativeGunbladeAttack.TriggerSucceeded(timing));
    }

    [Theory]
    [InlineData(17, 11, 0, 23)]
    [InlineData(17, 11, 25, 35)]
    [InlineData(20, 20, 1, 54)]
    [InlineData(20, 20, 19, 54)]
    [InlineData(20, 20, 20, 82)]
    [InlineData(20, 20, 32, 82)]
    public void NativeGunbladeRetainsItsOwnIntegerOrderBeforeTheSharedDamageModifiers(int strength, int power, short timing, int expected)
    {
        Assert.Equal(expected, NativeGunbladeAttack.Damage(strength, 5, power, 242, timing));
    }

    [Fact]
    public void GunbladeDamageCannotBeReplacedByScalingRoundedOrdinaryPhysicalDamage()
    {
        Assert.Equal(54, NativeBattleDamage.Physical(20, 5, 20, 242));
        Assert.Equal(82, NativeGunbladeAttack.Damage(20, 5, 20, 242, 25));
        Assert.Equal(2, NativeBattleDamage.Physical(3, 5, 20, 2));
        Assert.Equal(1, NativeGunbladeAttack.Damage(3, 5, 20, 2, 0));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(6)]
    public void AutomaticAssistanceResolvesOneGunbladeTriggerAtContactWithoutExtraBattleRandomDraws(int model)
    {
        BattleSession session = Create(model);
        session.SetGunbladeMode(BattleGunbladeMode.Automatic);
        NativeBattleRandomState before = session.Capture().Random!;
        NativeBattleRandomState expected = before.Draw(out _);
        DispatchToContact(session);
        Assert.Single(session.Events, item => item.Kind == "GunbladeTrigger");
        Assert.Equal(82, Assert.Single(session.Events, item => item.Kind == "Damage").Amount);
        Assert.Equal(2, session.NativeActionPackets.ReadContact(0).Primary.HitFlags3 & 2);
        Assert.Equal(System.Text.Json.JsonSerializer.Serialize(expected), System.Text.Json.JsonSerializer.Serialize(session.Capture().Random));
        Assert.False(session.TriggerGunblade());
        for (int frame = 0; frame < BattleSession.AttackRecoveryFrames; frame++)
        {
            session.Tick();
        }
        Assert.Single(session.Events, item => item.Kind == "GunbladeTrigger");
    }

    [Fact]
    public void ManualEdgeIsConsumedByItsOwnActionAndRestoresBeforeContact()
    {
        BattleSession session = Create(0);
        for (int frame = 0; frame < 7; frame++)
        {
            session.Tick();
        }
        Assert.True(session.GunbladeTriggerWindowOpen);
        Assert.True(session.TriggerGunblade());
        Assert.False(session.TriggerGunblade());
        BattleSession restored = BattleSession.Restore(session.Capture());
        Assert.False(restored.TriggerGunblade());
        DispatchToContact(session);
        DispatchToContact(restored);
        Assert.Equal(82, Assert.Single(session.Events, item => item.Kind == "Damage").Amount);
        Assert.Equal(2, session.NativeActionPackets.ReadContact(0).Primary.HitFlags3 & 2);
        Assert.Equal(System.Text.Json.JsonSerializer.Serialize(session.Capture()), System.Text.Json.JsonSerializer.Serialize(restored.Capture()));
    }

    [Fact]
    public void EarlyManualEdgeLocksFailureAndCannotBoostALaterHit()
    {
        BattleSession session = Create(0);
        session.Tick();
        Assert.False(session.GunbladeTriggerWindowOpen);
        Assert.False(session.TriggerGunblade());
        Assert.Equal((short)1, session.ActiveAction!.GunbladeTriggerTiming);
        for (int frame = 0; frame < 6; frame++)
        {
            session.Tick();
        }
        Assert.False(session.TriggerGunblade());
        DispatchToContact(session);
        Assert.DoesNotContain(session.Events, item => item.Kind == "GunbladeTrigger");
        Assert.Equal(54, Assert.Single(session.Events, item => item.Kind == "Damage").Amount);
        Assert.Equal(0, session.NativeActionPackets.ReadContact(0).Primary.HitFlags3 & 2);
    }

    [Fact]
    public void BlindKeepsTheGunbladeHitButForcesItsSingleHitTriggerToFail()
    {
        BattleSession session = Create(0, blind: true);
        session.SetGunbladeMode(BattleGunbladeMode.Automatic);
        NativeBattleRandomState expected = session.Capture().Random!.Draw(out _);
        DispatchToContact(session);
        Assert.Equal(54, Assert.Single(session.Events, item => item.Kind == "Damage").Amount);
        Assert.Equal(0, session.NativeActionPackets.ReadContact(0).Primary.HitFlags3 & 2);
        Assert.DoesNotContain(session.Events, item => item.Kind == "Miss");
        Assert.DoesNotContain(session.Events, item => item.Kind == "GunbladeTrigger");
        Assert.Equal(System.Text.Json.JsonSerializer.Serialize(expected), System.Text.Json.JsonSerializer.Serialize(session.Capture().Random));
    }

    [Fact]
    public void SuccessfulTriggerKeepsItsVisualFlagWhenPhysicalImmunitySkipsTheDamageFormula()
    {
        BattleSession session = Create(0, targetStatus: 0x800);
        session.SetGunbladeMode(BattleGunbladeMode.Automatic);
        NativeBattleRandomState before = session.Capture().Random!;
        DispatchToContact(session);
        NativeBattleContactHalf contact = session.NativeActionPackets.ReadContact(0).Primary;
        Assert.Equal(2, contact.HitFlags3 & 2);
        Assert.Equal((byte)6, contact.Sequence);
        Assert.Equal((ushort)0, contact.Amount6);
        Assert.Equal(before, session.Capture().Random);
        Assert.Contains(session.Events, item => item.Kind == "Miss" && item.Message.Contains("immune"));
    }

    [Fact]
    public void OtherWeaponsNeverConsumeGunbladeInputOrReceiveAssistance()
    {
        BattleSession session = Create(1);
        session.SetGunbladeMode(BattleGunbladeMode.Automatic);
        session.Tick();
        Assert.False(session.CanTriggerGunblade);
        Assert.False(session.TriggerGunblade());
        DispatchToContact(session);
        Assert.Equal(54, Assert.Single(session.Events, item => item.Kind == "Damage").Amount);
        Assert.DoesNotContain(session.Events, item => item.Kind == "GunbladeTrigger");
    }

    [Fact]
    public void InstalledOrdinaryWeaponUsesSharedNativeCriticalAndDamageRandomOwnership()
    {
        BattleSession session = Create(1, nativeWeapon: true);
        NativeBattleRandomState expected = session.Capture().Random!.Draw(out _).Draw(out byte damageRoll);
        DispatchToContact(session);
        Assert.Equal(2 * NativeBattleDamage.Physical(20, 5, 20, damageRoll), Assert.Single(session.Events, item => item.Kind == "Damage").Amount);
        Assert.Equal(System.Text.Json.JsonSerializer.Serialize(expected), System.Text.Json.JsonSerializer.Serialize(session.Capture().Random));
        Assert.Equal((byte)1, session.Capture().Combatants[1].NativeParticipant!.LastAction!.Command);
    }

    [Fact]
    public void SnapshotRejectsInvalidOrForeignGunbladeInput()
    {
        BattleSession session = Create(1);
        session.Tick();
        BattleSnapshot saved = session.Capture();
        Assert.Throws<InvalidDataException>(() => BattleSession.Restore(saved with { ActiveAction = saved.ActiveAction! with { GunbladeTriggerTiming = 25 } }));
        Assert.Throws<InvalidDataException>(() => BattleSession.Restore(saved with { GunbladeMode = (BattleGunbladeMode)255 }));
    }

    private static BattleSession Create(int model, bool blind = false, bool nativeWeapon = false, uint targetStatus = 0)
    {
        BattleNativeAction attack = new(1, 0, 4, 0) { Name = "Attack", MagicId = 0, AttackType = 1, Power = 20, HitCount = 1, TargetFlags = 0x50, AttackFlags = 0, DamagePresentation = 4, Element = 0, ElementPercent = 0, StatusAttack = 100, Statuses0 = 0, Statuses1 = 0, Hit = 255, Critical = 255, PowerModifier = null, LevelModifier = null, RawRecord = [], SupplementalRecord = [] };
        BattleSession session = new(new BattleDefinition
        {
            NativeCommandSequences = NativeBattleCommandSequenceFixture.Values,
            NativeRetainedMemory = NativeBattleRetainedMemory.FromColdProcess(),
            Combatants =
            [
                new() { Id = "party-0", Name = "Gunblade user", IsPlayer = true, ModelId = model, WeaponId = 0, Attack = 20, PhysicalPower = 20, NativeAttack = model is 0 or 6 || nativeWeapon ? attack : null, InitialAtb = BattleSession.AtbMaximum, StatusEffects = blind ? [new BattleStatusEffectDefinition { Kind = BattleStatusKind.Blind }] : [] },
                new() { Id = "enemy", Name = "Enemy", Speed = 1, MaximumHp = 1_000, CurrentHp = 1_000, Defense = 5, NativeStatus1 = targetStatus }
            ]
        }, BattleAtbMode.TurnBased);
        session.QueueAttack("party-0", "enemy");
        return session;
    }

    private static void DispatchToContact(BattleSession session)
    {
        while (session.Frame <= BattleSession.AttackWindupFrames)
        {
            session.Tick();
        }
    }
}
