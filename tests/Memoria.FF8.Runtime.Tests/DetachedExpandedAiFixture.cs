using System.Reflection;
using System.Runtime.ExceptionServices;

namespace Memoria.FF8.Runtime.Tests;

internal static class DetachedExpandedAiFixture
{
    private static readonly MethodInfo DispatchAction = typeof(BattleSession).GetMethod("DispatchAction", BindingFlags.Instance | BindingFlags.NonPublic)!;

    internal static void InterpretScalarCallback(BattleSession battle, string actorId, int stage)
    {
        BattleCombatant actor = Assert.Single(battle.Combatants, candidate => candidate.Id == actorId);
        MethodInfo interpret = typeof(BattleSession).GetMethod("RunNativeAi", BindingFlags.Instance | BindingFlags.NonPublic)!;
        try
        {
            interpret.Invoke(battle, [actor, stage]);
        }
        catch (TargetInvocationException error) when (error.InnerException is not null)
        {
            ExceptionDispatchInfo.Throw(error.InnerException);
        }
    }

    internal static void DispatchCallback(BattleSession battle, string actor, byte stage)
    {
        BattleAttackCommand head = Assert.IsType<BattleAttackCommand>(battle.QueuedActions.FirstOrDefault());
        BattleExpandedAiCallbackRequest callback = Assert.IsType<BattleExpandedAiCallbackRequest>(head.ExpandedAiCallback);
        Assert.Equal(actor, callback.Actor.ActorId);
        Assert.Equal(stage, callback.Stage);
        try
        {
            DispatchAction.Invoke(battle, null);
        }
        catch (TargetInvocationException error) when (error.InnerException is not null)
        {
            ExceptionDispatchInfo.Throw(error.InnerException);
        }
        Assert.DoesNotContain(battle.QueuedActions, command => ReferenceEquals(command, head));
    }

    internal static void DispatchLeadingInitializers(BattleSession battle)
    {
        int queued = battle.QueuedActions.Count;
        for (int index = 0; index < queued && battle.QueuedActions.FirstOrDefault()?.ExpandedAiCallback is { Stage: 0 } request; index++)
        {
            DispatchCallback(battle, request.Actor.ActorId, 0);
        }
    }

    internal static void AdvanceTo(BattleSession battle, Func<bool> boundary, string description)
    {
        for (int frame = 0; frame < 64 && !boundary(); frame++)
        {
            battle.Tick();
        }
        Assert.True(boundary(), description);
    }

    internal static BattleSession Restore(BattleSnapshot snapshot)
    {
        return BattleSession.Restore(NativeBattleStorageSnapshots.Capture(snapshot));
    }

    internal static void AssertSameRandom(NativeBattleRandomState? expected, NativeBattleRandomState? actual)
    {
        if (expected is null)
        {
            Assert.Null(actual);
            return;
        }
        NativeBattleRandomState restored = Assert.IsType<NativeBattleRandomState>(actual);
        Assert.Equal(expected.Lane, restored.Lane);
        Assert.Equal(expected.Indexes.ToArray(), restored.Indexes.ToArray());
    }

    internal static void AssertSameContinuation(BattleSession live, BattleSession copy)
    {
        BattleSnapshot expected = live.Capture();
        BattleSnapshot actual = copy.Capture();
        Assert.Equal(expected.Frame, actual.Frame);
        Assert.Equal(expected.Outcome, actual.Outcome);
        AssertSameRandom(expected.Random, actual.Random);
        Assert.Equal(expected.Events, actual.Events);
        Assert.Equal(expected.QueuedActions, actual.QueuedActions);
        Assert.Equal(expected.NativeAllocations.Select(allocation => (allocation.Id, allocation.SceneSlot, allocation.Generation)),
            actual.NativeAllocations.Select(allocation => (allocation.Id, allocation.SceneSlot, allocation.Generation)));
        Assert.Equal(live.Presentation!.NativeActionPackets.Roster.Owners, copy.Presentation!.NativeActionPackets.Roster.Owners);
        Assert.Equal(live.Combatants.Count, copy.Combatants.Count);
        foreach ((BattleCombatant expectedActor, BattleCombatant actualActor) in live.Combatants.Zip(copy.Combatants))
        {
            Assert.Equal(expectedActor.Id, actualActor.Id);
            Assert.Equal(expectedActor.Slot, actualActor.Slot);
            Assert.Equal(expectedActor.NativeActorIndex, actualActor.NativeActorIndex);
            Assert.Equal(expectedActor.CurrentHp, actualActor.CurrentHp);
            Assert.Equal(expectedActor.Atb, actualActor.Atb);
            Assert.Equal(expectedActor.IsBusy, actualActor.IsBusy);
            Assert.Equal(expectedActor.NativeCombatantFlags, actualActor.NativeCombatantFlags);
            Assert.Equal(expectedActor.NativeStatus0, actualActor.NativeStatus0);
            Assert.Equal(expectedActor.NativeStatus1, actualActor.NativeStatus1);
            Assert.Equal(expectedActor.AiVariables, actualActor.AiVariables);
        }
    }
}
