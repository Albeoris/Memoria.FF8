using System.Reflection;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleBackAttackTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Physical_class_zero_uses_rear_damage_before_clearing_the_recipient_and_packet(bool playerTarget)
    {
        BattleSession rear = Create(playerTarget, true);
        BattleSession front = Create(playerTarget, false);

        NativeBattleContactPacket rearPacket = Resolve(rear, Action(0));
        NativeBattleContactPacket frontPacket = Resolve(front, Action(0));

        Assert.True(frontPacket.Primary.PresentedAmount > 0);
        Assert.Equal(2 * frontPacket.Primary.PresentedAmount, rearPacket.Primary.PresentedAmount);
        Assert.Equal(10_000 - rearPacket.Primary.PresentedAmount, Target(rear).CurrentHp);
        Assert.Equal(0u, Target(rear).NativeStatus1 & 0x800000u);
        Assert.Equal(0u, rearPacket.Primary.BattleStatuses8 & 0x800000u);
    }

    [Theory]
    [InlineData(false, (byte)1)]
    [InlineData(false, (byte)2)]
    [InlineData(false, (byte)3)]
    [InlineData(true, (byte)1)]
    [InlineData(true, (byte)2)]
    [InlineData(true, (byte)3)]
    public void Other_attack_classes_preserve_rear_state_in_the_recipient_and_contact(bool playerTarget, byte attackClass)
    {
        BattleSession session = Create(playerTarget, true);

        NativeBattleContactPacket packet = Resolve(session, Action(attackClass));

        Assert.True(packet.Primary.PresentedAmount > 0);
        Assert.Equal(0x800000u, Target(session).NativeStatus1 & 0x800000u);
        Assert.Equal(0x800000u, packet.Primary.BattleStatuses8 & 0x800000u);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Physical_class_zero_miss_also_clears_rear_state(bool playerTarget)
    {
        BattleSession session = Create(playerTarget, true);

        NativeBattleContactPacket packet = Resolve(session, Action(0) with { Hit = 0 });

        Assert.Equal(0, packet.Primary.PresentedAmount);
        Assert.Equal(10_000, Target(session).CurrentHp);
        Assert.Contains(session.Events, entry => entry.Kind == "Miss");
        Assert.Equal(0u, Target(session).NativeStatus1 & 0x800000u);
        Assert.Equal(0u, packet.Primary.BattleStatuses8 & 0x800000u);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Cleared_rear_state_and_the_next_hit_survive_json_restore(bool playerTarget)
    {
        BattleSession session = Create(playerTarget, true);
        Resolve(session, Action(0));
        BattleSession restored = BattleSession.Restore(JsonSerializer.Deserialize<BattleSnapshot>(JsonSerializer.Serialize(session.Capture()))!);
        Assert.Equal(0u, Target(restored).NativeStatus1 & 0x800000u);

        NativeBattleContactPacket originalNext = Resolve(session, Action(0));
        NativeBattleContactPacket restoredNext = Resolve(restored, Action(0));

        Assert.Equal(originalNext, restoredNext);
        Assert.Equal(0u, restoredNext.Primary.BattleStatuses8 & 0x800000u);
        Assert.Equal(JsonSerializer.Serialize(session.Capture()), JsonSerializer.Serialize(restored.Capture()));
    }

    private static BattleSession Create(bool playerTarget, bool rear)
    {
        return new(new BattleDefinition
        {
            NativeRetainedMemory = NativeBattleRetainedMemory.FromColdProcess(),
            Combatants =
            [
                new() { Id = "actor", Name = "Actor", IsPlayer = !playerTarget, ModelId = 1, MaximumHp = 10_000, CurrentHp = 10_000, Attack = 40, Luck = 0, NativeStatusResistances = new byte[40] },
                new() { Id = "target", Name = "Target", IsPlayer = playerTarget, ModelId = 1, MaximumHp = 10_000, CurrentHp = 10_000, Defense = 5, Luck = 0, Evasion = 0, NativeStatus1 = rear ? 0x800000u : 0u, NativeStatusResistances = new byte[40] }
            ]
        }, BattleAtbMode.Normal);
    }

    private static BattleNativeAction Action(byte attackClass)
    {
        return new(8, 0, -1, -1) { MagicId = 0, AttackType = 1, Power = 20, HitCount = 1, AttackFlags = attackClass, TargetFlags = 0, PowerModifier = 0, LevelModifier = 0, DamagePresentation = 4, Element = 0, ElementPercent = 0, StatusAttack = 0, Statuses0 = 0, Statuses1 = 0, Hit = 255, Critical = 0, RawRecord = [], SupplementalRecord = [] };
    }

    private static BattleCombatant Target(BattleSession session) => session.Combatants.Single(actor => actor.Id == "target");

    private static NativeBattleContactPacket Resolve(BattleSession session, BattleNativeAction native)
    {
        BattleCombatant actor = session.Combatants.Single(item => item.Id == "actor");
        BattleCombatant target = Target(session);
        BattleActionPresentation presentation = new(actor.Id, target.Id, BattleActionPhase.Windup, 0, BattleAbility.Attack, 0) { NativeAction = native, NativeQueuedCommand = native.Command };
        BattleActionDefinition action = new(BattleAbility.Attack, 0, "Contact", BattleActionEffect.Native, 20, false) { NativeAction = native };
        typeof(BattleSession).GetMethod("BeginNativeReactionBatch", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(session, []);
        return (NativeBattleContactPacket)typeof(BattleSession).GetMethod("ResolveInstalledNativeHitWithContact", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(session, [actor, target, presentation, action, native, (byte)255, false])!;
    }
}
