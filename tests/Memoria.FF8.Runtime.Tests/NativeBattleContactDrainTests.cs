using System.Reflection;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleContactDrainTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void SquallFinisherTypeNineDoesNotDrainWithoutTheDrainStatus(int actionId)
    {
        BattleSession session = Create();
        BattleNativeAction native = Action(9) with { Command = 249, ActionId = checked((ushort)actionId) };
        BattleSession restored = Restore(session);

        NativeBattleContactPacket packet = Resolve(session, native);
        NativeBattleContactPacket independent = Resolve(restored, native);

        Assert.True(packet.Primary.Amount6 > 0);
        Assert.Equal(10_000 - packet.Primary.Amount6, session.Combatants[1].CurrentHp);
        Assert.Equal(5_000, session.Combatants[0].CurrentHp);
        Assert.Equal((byte)255, packet.Secondary.ActorIndex);
        Assert.Single(session.Events, item => item.Kind == "Damage");
        Assert.DoesNotContain(session.Events, item => item.Kind == "Healing");
        Assert.Equal(packet, independent);
        Assert.Equal(JsonSerializer.Serialize(session.Capture()), JsonSerializer.Serialize(restored.Capture()));
    }

    [Theory]
    [InlineData((byte)1, (byte)0, false, false)]
    [InlineData((byte)2, (byte)0, false, false)]
    [InlineData((byte)9, (byte)0, false, false)]
    [InlineData((byte)1, (byte)25, false, false)]
    [InlineData((byte)2, (byte)25, false, false)]
    [InlineData((byte)9, (byte)25, false, false)]
    [InlineData((byte)1, (byte)100, false, false)]
    [InlineData((byte)9, (byte)100, false, false)]
    [InlineData((byte)1, (byte)150, false, false)]
    [InlineData((byte)9, (byte)150, false, false)]
    [InlineData((byte)1, (byte)25, true, false)]
    [InlineData((byte)9, (byte)25, true, false)]
    [InlineData((byte)1, (byte)25, false, true)]
    [InlineData((byte)2, (byte)25, false, true)]
    [InlineData((byte)9, (byte)25, false, true)]
    [InlineData((byte)1, (byte)25, true, true)]
    [InlineData((byte)9, (byte)25, true, true)]
    public void DrainStatusUsesResistanceAndZombieParity(byte attackType, byte resistance, bool actorZombie, bool targetZombie)
    {
        BattleSession session = Create(resistance, actorZombie, targetZombie);
        BattleNativeAction native = Action(attackType) with { Statuses1 = 0x8000, StatusAttack = 100 };
        BattleSession restored = Restore(session);

        NativeBattleContactPacket packet = Resolve(session, native);
        NativeBattleContactPacket independent = Resolve(restored, native);

        int drain = packet.Primary.Amount6 * Math.Max(0, 100 - resistance) / 100;
        bool healing = drain == 0 || actorZombie == targetZombie;
        Assert.True(packet.Primary.Amount6 > 0);
        Assert.Equal((byte)0, packet.Secondary.ActorIndex);
        Assert.Equal(drain, packet.Secondary.Amount6);
        Assert.Equal(healing ? (byte)1 : (byte)0, packet.Secondary.HitFlags3);
        Assert.Equal(5_000 + (healing ? drain : -drain), session.Combatants[0].CurrentHp);
        Assert.Equal(10_000 - packet.Primary.Amount6, session.Combatants[1].CurrentHp);
        Assert.Equal(packet, independent);
        Assert.Equal(JsonSerializer.Serialize(session.Capture()), JsonSerializer.Serialize(restored.Capture()));
    }

    [Theory]
    [InlineData((byte)1)]
    [InlineData((byte)9)]
    public void DrainUsesZombieStateBeforeTheSameHitAppliesStatuses(byte attackType)
    {
        BattleSession session = Create();
        BattleNativeAction native = Action(attackType) with { Statuses0 = 0x40, Statuses1 = 0x8000, StatusAttack = 255 };

        NativeBattleContactPacket packet = Resolve(session, native);

        int drain = packet.Primary.Amount6 * 255 / 100;
        Assert.Equal((ushort)0x40, session.Combatants[1].NativeStatus0);
        Assert.Equal((byte)1, packet.Secondary.HitFlags3);
        Assert.Equal(drain, packet.Secondary.Amount6);
        Assert.Equal(5_000 + drain, session.Combatants[0].CurrentHp);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void DrainSecondaryCapsAt9999BeforePrimaryClamping(bool targetZombie)
    {
        BattleSession session = Create(targetZombie: targetZombie, actorHp: 10_000);
        BattleNativeAction native = Action(18) with { Statuses1 = 0x8000, StatusAttack = 50 };

        NativeBattleContactPacket packet = Resolve(session, native);

        Assert.Equal((ushort)9_999, packet.Primary.Amount6);
        Assert.Equal((ushort)9_999, packet.Secondary.Amount6);
        Assert.Equal(targetZombie ? (byte)0 : (byte)1, packet.Secondary.HitFlags3);
        Assert.Equal(targetZombie ? 1 : 10_000, session.Combatants[0].CurrentHp);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public void ElementalAbsorptionReversesDrainBeforeZombieParity(bool actorZombie, bool targetZombie)
    {
        BattleSession session = Create(actorZombie: actorZombie, targetZombie: targetZombie, absorb: true);
        BattleNativeAction native = Action(9) with { Element = 1, ElementPercent = 100, Statuses1 = 0x8000, StatusAttack = 50 };

        NativeBattleContactPacket packet = Resolve(session, native);

        int drain = packet.Primary.Amount6 / 2;
        bool healing = actorZombie != targetZombie;
        Assert.True(drain > 0);
        Assert.Equal((byte)1, (byte)(packet.Primary.HitFlags3 & 1));
        Assert.Equal(5_000 + packet.Primary.Amount6, session.Combatants[1].CurrentHp);
        Assert.Equal(drain, packet.Secondary.Amount6);
        Assert.Equal(healing ? (byte)1 : (byte)0, packet.Secondary.HitFlags3);
        Assert.Equal(5_000 + (healing ? drain : -drain), session.Combatants[0].CurrentHp);
    }

    [Fact]
    public void ReverseDrainKeepsItsIndependentCapWhenPrimaryDamageIsCustomized()
    {
        BattleSnapshot snapshot = Create(targetZombie: true, actorHp: 10_000).Capture();
        BattleSession session = BattleSession.Restore(snapshot with { Definition = snapshot.Definition with { Tuning = new() { MaximumDamage = 1234 } } });
        BattleNativeAction native = Action(18) with { Statuses1 = 0x8000, StatusAttack = 50 };

        NativeBattleContactPacket packet = Resolve(session, native);

        Assert.Equal((ushort)1234, packet.Primary.Amount6);
        Assert.Equal((ushort)9999, packet.Secondary.Amount6);
        Assert.Equal((byte)0, packet.Secondary.HitFlags3);
        Assert.Equal(1, session.Combatants[0].CurrentHp);
    }

    [Fact]
    public void PositiveDrainPercentRoundingToZeroRetainsDamagePresentation()
    {
        BattleSession session = Create();
        BattleNativeAction native = Action(9) with { Power = 1, Statuses1 = 0x8000, StatusAttack = 1 };

        NativeBattleContactPacket packet = Resolve(session, native);

        Assert.InRange(packet.Primary.Amount6, (ushort)1, (ushort)99);
        Assert.Equal((byte)0, packet.Secondary.ActorIndex);
        Assert.Equal((ushort)0, packet.Secondary.Amount6);
        Assert.Equal((byte)0, packet.Secondary.HitFlags3);
        Assert.Equal(5_000, session.Combatants[0].CurrentHp);
    }

    [Fact]
    public void FixedDamageTypeIgnoresTheDrainStatus()
    {
        BattleSession session = Create();
        BattleNativeAction native = Action(27) with { Power = 1, Statuses1 = 0x8000, StatusAttack = 100, Hit = 0 };

        NativeBattleContactPacket packet = Resolve(session, native);

        Assert.Equal((ushort)100, packet.Primary.Amount6);
        Assert.Equal((byte)255, packet.Secondary.ActorIndex);
        Assert.Equal(5_000, session.Combatants[0].CurrentHp);
        Assert.DoesNotContain(session.Events, item => item.Kind == "Healing");
    }

    private static BattleSession Create(byte resistance = 0, bool actorZombie = false, bool targetZombie = false, int actorHp = 5_000, bool absorb = false)
    {
        byte[] resistances = new byte[40];
        resistances[23] = resistance;
        return new(new BattleDefinition
        {
            NativeRetainedMemory = NativeBattleRetainedMemory.FromColdProcess(),
            Combatants =
            [
                new() { Id = "party", Name = "Party", IsPlayer = true, ModelId = 0, WeaponId = 0, MaximumHp = 10_000, CurrentHp = actorHp, Attack = 40, NativeStatus0 = actorZombie ? (ushort)0x40 : (ushort)0, NativeStatusResistances = new byte[40] },
                new() { Id = "enemy", Name = "Enemy", MaximumHp = 10_000, CurrentHp = absorb ? 5_000 : 10_000, Defense = 5, NativeStatus0 = targetZombie ? (ushort)0x40 : (ushort)0, NativeStatusResistances = resistances, ElementDefenses = [absorb ? 1000 : 800, 800, 800, 800, 800, 800, 800, 800] }
            ]
        }, BattleAtbMode.Normal);
    }

    private static BattleNativeAction Action(byte attackType)
    {
        return new(249, 0, -1, -1) { MagicId = 0, AttackType = attackType, Power = 20, HitCount = 1, AttackFlags = 0, TargetFlags = 0, PowerModifier = 0, LevelModifier = 0, DamagePresentation = 4, Element = 0, ElementPercent = 0, StatusAttack = 0, Statuses0 = 0, Statuses1 = 0, Hit = 255, Critical = 0, RawRecord = [], SupplementalRecord = [] };
    }

    private static BattleSession Restore(BattleSession session)
    {
        return BattleSession.Restore(JsonSerializer.Deserialize<BattleSnapshot>(JsonSerializer.Serialize(session.Capture()))!);
    }

    private static NativeBattleContactPacket Resolve(BattleSession session, BattleNativeAction native)
    {
        BattleCombatant actor = session.Combatants.Single(item => item.Id == "party");
        BattleCombatant target = session.Combatants.Single(item => item.Id == "enemy");
        BattleActionPresentation presentation = new(actor.Id, target.Id, BattleActionPhase.Windup, 0, BattleAbility.Attack, 0) { NativeAction = native, NativeQueuedCommand = native.Command };
        BattleActionDefinition action = new(BattleAbility.Attack, 0, "Contact", BattleActionEffect.Native, 20, false) { NativeAction = native };
        return (NativeBattleContactPacket)typeof(BattleSession).GetMethod("ResolveInstalledNativeHitWithContact", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(session, [actor, target, presentation, action, native, (byte)255, false])!;
    }
}
