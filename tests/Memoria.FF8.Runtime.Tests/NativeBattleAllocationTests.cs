using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleAllocationTests
{
    [Fact]
    public void PhysicalRemovalPreservesTargetHpGaugeAndVisibilityWhileResettingTheCallerGauge()
    {
        NativeBattleAiAbility[] abilities = Enumerable.Repeat(new NativeBattleAiAbility(0, 0, 0), 48).ToArray();
        BattleSession battle = new(new()
        {
            NativeRetainedMemory = NativeBattleRetainedMemory.FromColdProcess(),
            Combatants =
            [
                new() { Id = "party", Name = "Party", IsPlayer = true },
                new() { Id = "caller", Name = "Caller", InitialAtb = 1234, NativeAi = new([[29, 1, 0], [0], [0], [0], [0]], abilities, 0, []) },
                new() { Id = "removed", Name = "Removed", InitialAtb = 4567 }
            ]
        }, BattleAtbMode.Normal);
        battle.Tick();
        BattleCombatant removed = battle.Combatants.Single(actor => actor.Id == "removed");
        Assert.Equal(0, battle.Combatants.Single(actor => actor.Id == "caller").Atb);
        Assert.Equal(4567 + removed.Speed, removed.Atb);
        Assert.Equal(100, removed.CurrentHp);
        Assert.False(removed.IsActive);
        Assert.False(removed.Hidden);
        Assert.True(removed.Untargetable);
        Assert.Equal((ushort)1, (ushort)(battle.Capture().Combatants.Single(actor => actor.Id == "removed").NativeStatus!.Saved & 1));
        Assert.Equal(JsonSerializer.Serialize(battle.Capture()), JsonSerializer.Serialize(BattleSession.Restore(battle.Capture()).Capture()));
    }

    [Fact]
    public void SameScenePrototypeCanOccupySeveralPhysicalSlotsAndRestoreWithoutReinitializing()
    {
        BattleSession battle = Create([52, 6, 52, 6, 0]);
        BattleSnapshot saved = battle.Capture();
        Assert.Equal(new[] { 4, 5 }, saved.Combatants.Where(actor => actor.Id.StartsWith("native-allocation-")).Select(actor => actor.NativeParticipant!.ActorIndex));
        Assert.Equal(2, saved.NativeAllocations.Length);
        Assert.All(saved.NativeAllocations, allocation => Assert.Equal(6, allocation.SceneSlot));
        Assert.False(battle.Combatants.Single(actor => actor.Id == "enemy-6").IsActive);
        Assert.Equal(JsonSerializer.Serialize(saved), JsonSerializer.Serialize(BattleSession.Restore(saved).Capture()));
    }

    [Fact]
    public void ExplicitReplacementRetainsOldAllocationFactsAndUsesNewGeneration()
    {
        BattleSession battle = Create([52, 6, 59, 6, 4, 0]);
        BattleSnapshot saved = battle.Capture();
        BattleCombatant released = battle.Combatants.Single(actor => actor.Id == "native-allocation-1");
        BattleCombatant replacement = battle.Combatants.Single(actor => actor.Id == "native-allocation-2");
        Assert.False(released.IsActive);
        Assert.True(replacement.IsActive);
        Assert.Equal(4, saved.Combatants.Single(actor => actor.Id == replacement.Id).NativeParticipant!.ActorIndex);
        Assert.Equal(replacement.Id, Assert.Single(battle.Events, item => item.Kind == "Activated").ActorId);
        Assert.Equal(2, saved.NativeAllocations[1].Generation);
        Assert.Equal(JsonSerializer.Serialize(saved), JsonSerializer.Serialize(BattleSession.Restore(saved).Capture()));
    }

    [Fact]
    public void FifthEnemyCannotEscapeTheFourPhysicalEnemySlots()
    {
        InvalidDataException error = Assert.Throws<InvalidDataException>(() => Create([52, 6, 52, 6, 52, 6, 52, 6, 0]));
        Assert.Contains("physical slots3..6", error.Message);
    }

    [Fact]
    public void PhysicalRemovalDoesNotInterpretItsOperandAsAScenePrototype()
    {
        BattleSession battle = Create([52, 6, 29, 1, 52, 6, 0]);
        Assert.False(battle.Combatants.Single(actor => actor.Id == "native-allocation-1").IsActive);
        Assert.True(battle.Combatants.Single(actor => actor.Id == "native-allocation-2").IsActive);
        Assert.Equal(4, battle.Capture().NativeLastAllocatedIndex);
    }

    private static BattleSession Create(byte[] init)
    {
        NativeBattleAiAbility[] abilities = Enumerable.Repeat(new NativeBattleAiAbility(0, 0, 0), 48).ToArray();
        NativeBattleEnemyLevel level = new(100, 10, 10, 10, 10, 10, new int[8], [], [], 0);
        NativeBattleEnemyTemplate template = new(5, Enumerable.Repeat(level, 101).ToArray()) { NativeStatusAllocation = new(0, 0, Enumerable.Repeat((byte)100, 40).ToImmutableArray()) };
        BattleSession battle = new(new()
        {
            NativeRetainedMemory = NativeBattleRetainedMemory.FromColdProcess(),
            Combatants =
            [
                new() { Id = "party-0", Name = "Party", IsPlayer = true },
                new() { Id = "enemy-2", Name = "Caller", SceneSlot = 2, NativeAi = new([init, [0], [0], [0], [0]], abilities, 0, []) },
                new() { Id = "enemy-6", Name = "Prototype", SceneSlot = 6, InitialActive = false, NativeTemplate = template, NativeAi = new([[0], [0], [0], [0], [0]], abilities, 0, []) }
            ]
        }, BattleAtbMode.Normal);
        battle.Tick();
        return battle;
    }
}
