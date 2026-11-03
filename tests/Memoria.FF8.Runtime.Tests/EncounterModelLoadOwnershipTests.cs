using System.Collections.Immutable;
using System.Reflection;

namespace Memoria.FF8.Runtime.Tests;

public sealed class EncounterModelLoadOwnershipTests
{
    [Fact]
    public void CurrentReservedLoadOwnerIsIndependentOfAiTargetEligibility()
    {
        (BattleSession battle, BattleCombatant copy, NativeBattleGraphicsState graphics) = Staged();
        Assert.Same(copy, Resolve(battle, graphics));
        Assert.False(copy.IsActive);
        Assert.True(copy.Untargetable);
        TargetInvocationException ai = Assert.Throws<TargetInvocationException>(() => typeof(BattleSession).GetMethod("FindNativeActor", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(battle, [4]));
        Assert.IsType<NotSupportedException>(ai.InnerException);
    }

    [Fact]
    public void CancelledOrReplacedGenerationCannotCompleteAnOldGraphicalRow()
    {
        (BattleSession battle, BattleCombatant copy, NativeBattleGraphicsState graphics) = Staged();
        Assert.IsType<InvalidDataException>(Assert.Throws<TargetInvocationException>(() => Resolve(Create(), graphics)).InnerException);
        typeof(BattleCombatant).GetProperty("NativeIndex", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(copy, -1);
        Assert.IsType<InvalidDataException>(Assert.Throws<TargetInvocationException>(() => Resolve(battle, graphics)).InnerException);
    }

    [Fact]
    public void InactiveOwnerMustMatchCurrentLoadAndHaveResolvedStatistics()
    {
        (BattleSession battle, BattleCombatant copy, NativeBattleGraphicsState graphics) = Staged();
        FieldInfo owner = typeof(BattleSession).GetField("encounterAllocation", BindingFlags.Instance | BindingFlags.NonPublic)!;
        BattleEncounterAllocationState batch = battle.EncounterAllocation!;
        owner.SetValue(battle, batch with { LoadingIndex = 1 });
        Assert.IsType<InvalidDataException>(Assert.Throws<TargetInvocationException>(() => Resolve(battle, graphics)).InnerException);
        owner.SetValue(battle, batch);
        typeof(BattleCombatant).GetProperty("NativeAllocationPendingStatistics", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(copy, true);
        Assert.IsType<InvalidDataException>(Assert.Throws<TargetInvocationException>(() => Resolve(battle, graphics)).InnerException);
    }

    private static BattleCombatant Resolve(BattleSession battle, NativeBattleGraphicsState graphics)
    {
        return (BattleCombatant)typeof(BattleSession).GetMethod("FindNativeModelLoadOwner", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(battle, [graphics, 4])!;
    }

    [Theory]
    [InlineData(4, 0, 8, false)]
    [InlineData(0, 1, 1, false)]
    [InlineData(0, 0, 8, false)]
    [InlineData(0, 0, 1, true)]
    public void EntireBatchWaitsForNativeEntryAndFadeBeforeArrival(int flags, int kind, int sequence, bool ready)
    {
        (BattleSession battle, _, NativeBattleGraphicsState graphics) = Staged();
        FieldInfo owner = typeof(BattleSession).GetField("encounterAllocation", BindingFlags.Instance | BindingFlags.NonPublic)!;
        BattleEncounterAllocationState batch = battle.EncounterAllocation!;
        owner.SetValue(battle, batch with { LoadingIndex = batch.ActorIds.Length });
        for (int index = 4; index <= 5; index++)
        {
            NativeBattleGraphicsModel model = graphics.Models[index] with { Flags0 = 7, Flags2 = 0, Kind5 = 0, EffectCounter97 = 2, Sequence = 1, SequenceState = new() { Default2 = 1 } };
            graphics = graphics with { Models = graphics.Models.SetItem(index, model) };
        }
        NativeBattleGraphicsModel last = graphics.Models[5] with { Flags2 = (ushort)flags, Kind5 = (byte)kind, Sequence = (byte)sequence };
        graphics = graphics with { Models = graphics.Models.SetItem(5, last) };
        typeof(BattleSession).GetMethod("AdvanceEncounterAllocationPreparation", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(battle, [graphics]);
        Assert.Equal(ready ? BattleEncounterAllocationPhase.Ready : BattleEncounterAllocationPhase.Loading, battle.EncounterAllocation!.Phase);
        Assert.All(batch.ActorIds, id => Assert.False(battle.Combatants.Single(actor => actor.Id == id).IsActive));
        if (!ready)
        {
            Assert.Throws<InvalidOperationException>(() => battle.Capture());
        }
    }

    private static (BattleSession Battle, BattleCombatant Copy, NativeBattleGraphicsState Graphics) Staged()
    {
        BattleSession battle = Create();
        BattleEncounterAllocationState batch = battle.BeginEncounterAllocation("fixture.batch", "original", "support", "assault");
        typeof(BattleSession).GetField("encounterAllocation", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(battle, batch with { Phase = BattleEncounterAllocationPhase.Loading, LoadingIndex = 0 });
        BattleCombatant copy = battle.Combatants.Single(actor => actor.Id == batch.ActorIds[0]);
        NativeBattleGraphicsState graphics = new(Enumerable.Range(0, 7).Select(index => new NativeBattleGraphicsModel(index, 0)).ToImmutableArray(), new string?[] { "party", null, null, "original", copy.Id, batch.ActorIds[1], null }.ToImmutableArray(), 0, 0);
        return (battle, copy, graphics);
    }

    private static BattleSession Create()
    {
        NativeBattleEnemyLevel level = new(100, 20, 5, 20, 5, 100, Enumerable.Repeat(800, 8).ToArray(), [], [], 0);
        NativeBattleEnemyTemplate template = new(1, Enumerable.Repeat(level, 101).ToArray()) { NativeStatusAllocation = new(0, 0, Enumerable.Repeat((byte)100, 40).ToImmutableArray()) };
        return new(new() { NativeRetainedMemory = NativeBattleRetainedMemory.FromColdProcess(), Combatants = [new() { Id = "party", Name = "Party", IsPlayer = true }, new() { Id = "original", Name = "Original", SceneSlot = 0, NativeTemplate = template }] }, BattleAtbMode.Normal);
    }
}
