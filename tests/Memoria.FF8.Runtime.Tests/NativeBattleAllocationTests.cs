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
    public void Dynamic_allocation_uses_current_speed_entry_policy_and_preserves_the_single_draw()
    {
        BattleSession battle = Create([31, 6, 0]);
        NativeBattleRandomState expected = NativeBattleRandomState.Initialize(0).Draw(out byte entryRoll);
        BattleCombatant spawned = battle.Combatants.Single(actor => actor.Id == "native-allocation-1");
        // DispatchFrame advances existing gauges before DispatchAction drains the queued INIT callback and allocates this actor.
        int initialAtb = BattleInitialAtb.Calculate(spawned.Speed, entryRoll);
        Assert.Equal(initialAtb, spawned.Atb);
        Assert.Equal(JsonSerializer.Serialize(expected), JsonSerializer.Serialize(battle.Capture().Random));
        BattleSession restored = BattleSession.Restore(JsonSerializer.Deserialize<BattleSnapshot>(JsonSerializer.Serialize(battle.Capture()))!);
        battle.Tick();
        restored.Tick();
        Assert.Equal(Math.Min(BattleSession.AtbMaximum, initialAtb + spawned.Speed), spawned.Atb);
        Assert.Equal(JsonSerializer.Serialize(battle.Capture()), JsonSerializer.Serialize(restored.Capture()));
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

    [Fact]
    public void ExpandedOriginalSlotConditionAndActivationUsePackedActors()
    {
        byte[] init = [2, 15, 0, 0, 4, 0, 2, 0, 53, 4, 0];
        BattleSession battle = CreateExpanded(init, hiddenActor: true);
        BattleCombatant activated = battle.Combatants.Single(actor => actor.Id == "enemy-7");
        Assert.True(activated.IsActive);
        Assert.False(activated.Hidden);
        Assert.False(activated.Untargetable);
        Assert.False(activated.Suspended);
        Assert.Equal(7, activated.SceneSlot);
        Assert.Equal(BattleRosterSlot.Enemy(1), activated.Slot);
        Assert.Equal(1u, battle.Capture().Combatants.Single(actor => actor.Id == activated.Id).NativeParticipant!.Variables[0]);
        Assert.Equal(1, battle.Events.Count(entry => entry.Kind == "Activated" && entry.ActorId == activated.Id));
        AssertExpandedContinuation(battle);
    }

    [Fact]
    public void ExpandedReserveReplacementKeepsScenePrototypeAndRestoresWithoutReinitializing()
    {
        BattleSession battle = CreateExpanded([52, 6, 59, 6, 4, 52, 6, 0]);
        BattleCombatant prototype = battle.Combatants.Single(actor => actor.Id == "enemy-6");
        BattleCombatant released = battle.Combatants.Single(actor => actor.Id == "native-allocation-1");
        BattleCombatant replacement = battle.Combatants.Single(actor => actor.Id == "native-allocation-2");
        BattleCombatant reserved = battle.Combatants.Single(actor => actor.Id == "native-allocation-3");
        Assert.False(prototype.IsActive);
        Assert.Null(prototype.Slot);
        Assert.False(released.IsActive);
        Assert.Null(released.Slot);
        Assert.True(replacement.IsActive);
        Assert.False(replacement.Suspended);
        Assert.Equal(BattleRosterSlot.Enemy(1), replacement.Slot);
        Assert.True(reserved.IsActive);
        Assert.True(reserved.Hidden && reserved.Suspended && reserved.Untargetable);
        Assert.Equal(BattleRosterSlot.Enemy(2), reserved.Slot);
        Assert.Equal(6, replacement.SceneSlot);
        Assert.Equal(5, replacement.Level);
        BattleSnapshot snapshot = battle.Capture();
        Assert.Equal(5, snapshot.NativeLastAllocatedIndex);
        Assert.Equal(1u, snapshot.Combatants.Single(actor => actor.Id == replacement.Id).NativeParticipant!.Variables[0]);
        Assert.Equal(0u, snapshot.Combatants.Single(actor => actor.Id == reserved.Id).NativeParticipant!.Variables[0]);
        NativeBattleRandomState expected = NativeBattleRandomState.Initialize(0).Draw(out _).Draw(out _).Draw(out _);
        Assert.Equal(JsonSerializer.Serialize(expected), JsonSerializer.Serialize(snapshot.Random));
        AssertExpandedContinuation(battle);
    }

    [Fact]
    public void ExpandedDirectTargetResolvesAnOriginalPartySlotAlongsideExtraAllies()
    {
        byte[] turn = [43, 2, 7, 1, 6, 0];
        BattleSession battle = CreateExpanded([0], turn: turn);
        battle.Tick();
        Assert.Equal("party-2", battle.ActiveAction?.TargetId);
        Assert.Equal(0, battle.ActiveAction!.NativeTargetMask);
        AssertExpandedContinuation(battle);
    }

    [Fact]
    public void ExpandedGraphicsBarrierOrdersEarlierActionThenAllocationAndActivationAcrossJson()
    {
        byte[] turn = [43, 2, 7, 1, 6, 51, 52, 6, 53, 209, 0];
        BattleSession battle = CreateExpanded([0], turn: turn);
        battle.Tick();
        Assert.NotNull(battle.ActiveAction);
        Assert.DoesNotContain(battle.Combatants, actor => actor.Id == "native-allocation-1");
        BattleSession restored = BattleSession.Restore(JsonSerializer.Deserialize<BattleSnapshot>(JsonSerializer.Serialize(battle.Capture()))!);
        for (int callback = 0; callback < 120 && !battle.Events.Any(entry => entry.Kind == "Activated"); callback++)
        {
            battle.Tick();
            restored.Tick();
        }
        int contact = Array.FindIndex(battle.Events.ToArray(), entry => entry.Kind == "EffectContact");
        int allocation = Array.FindIndex(battle.Events.ToArray(), entry => entry.Kind == "NativeSpawn");
        int activation = Array.FindIndex(battle.Events.ToArray(), entry => entry.Kind == "Activated");
        Assert.True(contact >= 0 && allocation > contact && activation > allocation);
        BattleCombatant actor = Assert.Single(battle.Combatants, candidate => candidate.Id == "native-allocation-1");
        Assert.False(actor.Hidden);
        Assert.False(actor.Suspended);
        Assert.Equal(JsonSerializer.Serialize(battle.Capture()), JsonSerializer.Serialize(restored.Capture()));
    }

    private static BattleSession CreateExpanded(byte[] init, bool hiddenActor = false, byte[]? turn = null)
    {
        NativeBattleAiAbility[] abilities = Enumerable.Repeat(new NativeBattleAiAbility(0, 0, 0), 48).ToArray();
        NativeBattleEnemyLevel level = new(100, 10, 10, 10, 10, 10, new int[8], [], [], 0);
        NativeBattleEnemyTemplate template = new(5, Enumerable.Repeat(level, 101).ToArray()) { NativeStatusAllocation = new(0, 0, Enumerable.Repeat((byte)100, 40).ToImmutableArray()) };
        BattleCombatantDefinition prototype = new() { Id = "enemy-6", Name = "Prototype", SceneSlot = 6, Attack = level.Strength, Defense = level.Vitality, Magic = level.Magic, Spirit = level.Spirit, Speed = level.Speed, ElementDefenses = level.ElementDefenses, InitialActive = false, NativeTemplate = template, NativeAi = new([[18, 220, 1, 0], [0], [0], [0], [0]], abilities, 0, []) };
        BattleCombatantDefinition caller = prototype with { Id = "enemy-2", Name = "Caller", SceneSlot = 2, Level = 5, InitialActive = true, InitialAtb = turn is null ? 0 : BattleSession.AtbMaximum, NativeAi = new([init, turn ?? [0], [0], [0], [0]], abilities, 0, []) };
        BattleCombatantDefinition[] combatants = [.. Enumerable.Range(0, 4).Select(index => new BattleCombatantDefinition { Id = $"party-{index}", Name = $"Party {index}", IsPlayer = true, ModelId = index }), caller, prototype];
        if (hiddenActor)
        {
            combatants = [.. combatants, prototype with { Id = "enemy-7", SceneSlot = 7, Level = 5, InitialActive = true, InitialHidden = true, InitialUntargetable = true, InitialSuspended = true }];
        }
        BattleNativeAction action = new(8, 1, 3, 0)
        {
            Name = "Test attack", MagicId = 0, AttackType = 1, Power = 10, HitCount = 1, TargetFlags = null, AttackFlags = 0, DamagePresentation = 4,
            Element = 0, ElementPercent = null, StatusAttack = 0, Statuses0 = 0, Statuses1 = 0, Hit = 255, Critical = 0, PowerModifier = null,
            LevelModifier = null, RawRecord = [], SupplementalRecord = []
        };
        return new(new() { UsesExpandedRoster = true, UsesOriginalEnemySlots = true, NativeRetainedMemory = NativeBattleRetainedMemory.FromColdProcess(), Combatants = combatants, NativeActions = [action] }, BattleAtbMode.Normal);
    }

    private static void AssertExpandedContinuation(BattleSession battle)
    {
        BattleSession restored = BattleSession.Restore(JsonSerializer.Deserialize<BattleSnapshot>(JsonSerializer.Serialize(battle.Capture()))!);
        battle.Tick();
        restored.Tick();
        Assert.Equal(JsonSerializer.Serialize(battle.Capture()), JsonSerializer.Serialize(restored.Capture()));
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
