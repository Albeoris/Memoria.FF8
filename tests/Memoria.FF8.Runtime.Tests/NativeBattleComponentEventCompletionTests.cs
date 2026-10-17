using System.Reflection;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleComponentEventCompletionTests
{
    [Fact]
    public void Same_event_header_is_acknowledged_after_its_actor_retires_and_the_slot_is_reallocated()
    {
        BattleSession battle = new(Definition(), BattleAtbMode.Normal);
        battle.Tick();
        BattleCombatant retiring = battle.Combatants.Single(actor => actor.Id == "retiring");
        typeof(BattleCombatant).GetProperty(nameof(BattleCombatant.CurrentHp))!.SetValue(retiring, 0);
        string saved = JsonSerializer.Serialize(battle.Capture());
        BattleSession restored = BattleSession.Restore(JsonSerializer.Deserialize<BattleSnapshot>(saved)!);

        DispatchDeath(battle);
        DispatchDeath(restored);

        BattleSnapshot after = battle.Capture();
        Assert.Equal(-1, after.Combatants.Single(actor => actor.Id == "retiring").NativeParticipant!.ActorIndex);
        NativeBattleEnemyAllocation allocation = Assert.Single(after.NativeAllocations);
        Assert.Equal(3, after.Combatants.Single(actor => actor.Id == allocation.Id).NativeParticipant!.ActorIndex);
        Assert.Equal((byte)3, battle.NativeActionPackets.Headers[0].ReadByte(0));
        Assert.Equal((byte)0, battle.NativeActionPackets.ActionCount);
        Assert.DoesNotContain(battle.QueuedActions, command => command.NativeHeaderIndex is not null);
        Assert.Null(battle.ActiveAction);
        Assert.Single(battle.Events, item => item.Kind == "ActionComplete" && item.ActorId == "retiring");
        Assert.DoesNotContain(battle.Events, item => item.Kind == "Windup" && item.ActorId == "retiring");
        Assert.Equal(JsonSerializer.Serialize(after), JsonSerializer.Serialize(restored.Capture()));

        battle.Tick();
        restored.Tick();
        Assert.Equal(JsonSerializer.Serialize(battle.Capture()), JsonSerializer.Serialize(restored.Capture()));
    }

    [Fact]
    public void Component_acknowledgment_keeps_unproduced_commands_and_unrelated_active_presentation()
    {
        BattleSession battle = new(Definition(), BattleAtbMode.Normal);
        battle.Tick();
        BattleAttackCommand pending = new("party", "retiring");
        Queue(battle).Enqueue(pending);
        BattleActionPresentation unrelated = new("party", "retiring", BattleActionPhase.Recovery, 4, BattleAbility.Attack, 0);
        typeof(BattleSession).GetProperty(nameof(BattleSession.ActiveAction))!.SetValue(battle, unrelated);
        DispatchDeath(battle);
        Assert.Same(pending, Assert.Single(Queue(battle)));
        Assert.Same(unrelated, battle.ActiveAction);
        Assert.Equal((byte)0, battle.NativeActionPackets.ActionCount);
    }

    [Theory]
    [InlineData((byte)0, (byte)3)]
    [InlineData((byte)3, (byte)4)]
    public void Explicit_event_identity_never_acknowledges_a_contradictory_physical_header(byte physicalActor, byte eventActor)
    {
        BattleSession battle = new(Definition(), BattleAtbMode.Normal);
        battle.Tick();
        NativeBattleActionPacketsState packets = battle.NativeActionPackets;
        (packets, _) = packets.Prepare(new(physicalActor, 252, 0, -1, 1, 1, null));
        (packets, _) = packets.Publish(0);
        Field("nativeActionPackets").SetValue(battle, packets);
        Field("nativeActionScratch").SetValue(battle, battle.Capture().NativeActionScratch! with { EventActorB0F = eventActor, ComponentCompletion = 0x47DDA0 });
        BattleAttackCommand command = new("retiring", "retiring", BattleAbility.EnemyAbility, 1) { NativeHeaderIndex = 0, NativeQueuedCommand = 252, NativeGraphic = new(1) };
        Queue(battle).Enqueue(command);
        TargetInvocationException error = Assert.Throws<TargetInvocationException>(() => Method("TryCompleteNativeComponentCommand").Invoke(battle, ["retiring", (byte?)3]));
        Assert.IsType<InvalidDataException>(error.InnerException);
        Assert.Same(command, Assert.Single(Queue(battle)));
        Assert.Equal((byte)1, battle.NativeActionPackets.ActionCount);
    }

    [Fact]
    public void Live_actor_keeps_its_factual_header_until_the_declared_component_presentation_finishes()
    {
        BattleSession battle = new(Definition(), BattleAtbMode.Normal);
        battle.Tick();
        NativeBattleActionPacketsState packets = battle.NativeActionPackets;
        (packets, _) = packets.Prepare(new(3, 252, 0, -1, 1, 1, null));
        (packets, _) = packets.Publish(0);
        Field("nativeActionPackets").SetValue(battle, packets);
        Field("nativeActionScratch").SetValue(battle, battle.Capture().NativeActionScratch! with { EventActorB0F = 3, ComponentCompletion = 0x47DDA0 });
        BattleAttackCommand command = new("retiring", "retiring", BattleAbility.EnemyAbility, 1) { NativeHeaderIndex = 0, NativeQueuedCommand = 252, NativeGraphic = new(1) };
        Queue(battle).Enqueue(command);
        Method("TryCompleteNativeComponentCommand").Invoke(battle, ["retiring", (byte?)3]);
        Assert.Same(command, Assert.Single(Queue(battle)));
        Assert.Equal((byte)1, battle.NativeActionPackets.ActionCount);
        Assert.Equal((uint?)0x47DDA0, battle.Capture().NativeActionScratch!.ComponentCompletion);
        Assert.DoesNotContain(battle.Events, item => item.Kind == "ActionComplete");
    }

    private static BattleDefinition Definition()
    {
        NativeBattleAiDefinition death = new([[0], [0], [0], [30, 1, 29, 200, 59, 1, 3, 0], [0]], Enumerable.Repeat(new NativeBattleAiAbility(0, 0, 0), 48).ToArray(), 0, []);
        NativeBattleAiDefinition idle = new([[0], [0], [0], [0], [0]], Enumerable.Repeat(new NativeBattleAiAbility(0, 0, 0), 48).ToArray(), 0, []);
        NativeBattleEnemyLevel level = new(100, 20, 5, 20, 5, 100, Enumerable.Repeat(800, 8).ToArray(), [], [], 0);
        return new()
        {
            NativeRetainedMemory = NativeBattleRetainedMemory.FromColdProcess(),
            NativeActions = [new(8, 1, -1, -1) { MagicId = 0, AttackType = 0, Power = 0, HitCount = 1, TargetFlags = null, AttackFlags = 0, DamagePresentation = 0, Element = 0, ElementPercent = null, StatusAttack = 0, Statuses0 = 0, Statuses1 = 0, Hit = null, Critical = null, PowerModifier = null, LevelModifier = null, RawRecord = [], SupplementalRecord = [] }],
            Combatants =
            [
                new() { Id = "party", Name = "Party", IsPlayer = true, MaximumHp = 100, CurrentHp = 100 },
                new() { Id = "retiring", Name = "Retiring", SceneSlot = 0, ModelId = 1, MaximumHp = 100, CurrentHp = 100, NativeAi = death },
                new() { Id = "reserve", Name = "Reserve", SceneSlot = 1, ModelId = 2, MaximumHp = 100, CurrentHp = 100, InitialActive = false, NativeAi = idle, NativeTemplate = new(1, Enumerable.Repeat(level, 101).ToArray()) { Information254 = 0 } }
            ]
        };
    }

    private static void DispatchDeath(BattleSession battle) => Method("DispatchNativeCallback").Invoke(battle, [new BattleNativeCallback(3, 3)]);
    private static Queue<BattleAttackCommand> Queue(BattleSession battle) => (Queue<BattleAttackCommand>)Field("actions").GetValue(battle)!;
    private static FieldInfo Field(string name) => typeof(BattleSession).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!;
    private static MethodInfo Method(string name) => typeof(BattleSession).GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic)!;
}
