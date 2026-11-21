using Memoria.FF7.Events;
using Memoria.AutoPlay;

namespace Memoria.FF7.Runtime;

public sealed partial class Ff7AutoPlayPolicy
{
    internal static Ff7AutoPlayActiveEvent? ReadCompletedSynchronousActivation(Ff7AutoPlayState state, FieldVmSnapshot vm, Ff7FieldProcess process, ReadOnlySpan<byte> script, IReadOnlyList<Ff7NativeScriptActivity> activities, Func<int, int, int> readEntry, long generation)
    {
        if (state.Active is not { Accepted: true, Returned: false, TransitionOwned: false, CompletionRecorded: false, ExecutionProvenance: not null } active || active.Kind is < Ff7AutoPlayTargetKind.ActorTalk or > Ff7AutoPlayTargetKind.LineLeave || active.SpatialOwner is not null || active.DirectEffectOwner is not null || state.Choice is not null || state.PendingRestore is not null || active.TravelSelections is { Count: > 0 } || active.TravelRoute is not null || process.Transition is null || process.PlayerLocked || process.MenuDisabled || process.Join is not null || process.Movie is { Playing: true } || process.Menu is { Completed: false } || process.Dialogues.Any(dialogue => !dialogue.Completed) || !Ff7AutoPlayExecutionOwners.Matches(active, generation, vm.ProgramIdentity) || (uint)active.Entity >= vm.Entities.Length)
        {
            return null;
        }
        var coverage = state.Coverage.Visits.SingleOrDefault(visit => visit.Visit.Key == active.VisitId)?.Events.SingleOrDefault(item => item.Event.Id == active.EventId);
        if (coverage?.Conditions is not { } conditions || conditions.ActiveEntry != active.ConditionFingerprint || conditions.HasExecutedEntry(active.ConditionFingerprint) || active.BeforeStory != new FieldVariables(vm.PersistentVariables, vm.MapVariables).ReadWord(FieldBank.Persistent0Word, 0))
        {
            return null;
        }
        FieldEntitySnapshot root = vm.Entities[active.Entity];
        int pc = root.ProgramCounter;
        int scriptLength = script.Length;
        if (root.Priority != 1 || pc < 0 || pc + 3 >= script.Length || script[pc + 3] != 0 || Ff7AutoPlayNativeReadiness.ReadCompletedSynchronousReturn(vm, script, active.Entity) is not { Priority: < 7 } completed || active.ExecutionProvenance.Owners.SingleOrDefault(owner => owner.ParentEntity == -1) is not { Queued: false, Priority: 1 } rootOwner || rootOwner.Entity != active.Entity || rootOwner.Script != active.Script || rootOwner.Entry != readEntry(active.Entity, active.Script) || root.SavedProgramCounters.Length != 8 || !root.SavedProgramCounters.Skip(2).Any(saved => saved > 0 && saved < scriptLength))
        {
            return null;
        }
        Ff7AutoPlayExecutionOwner? returnedChild = null;
        for (int index = 0; index < activities.Count; index++)
        {
            if (activities[index].Return is not { } returned || returned.Entity != completed.Entity || returned.Priority != completed.Priority || returned.Tick != vm.Tick - 1 || (uint)returned.ProgramCounter >= script.Length || script[returned.ProgramCounter] != 0)
            {
                continue;
            }
            var beforeReturn = Ff7AutoPlayExecutionOwners.Observe(active, generation, vm.ProgramIdentity, vm.Tick, activities.Take(index).ToArray(), readEntry);
            returnedChild = beforeReturn.ExecutionProvenance?.Owners.SingleOrDefault(owner => !owner.Queued && owner.Entity == completed.Entity && owner.Priority == completed.Priority && owner.Script == completed.Script && owner.Entry == readEntry(completed.Entity, completed.Script) && owner.ParentEntity == active.Entity && owner.ParentPriority == 1 && owner.SourceProgramCounter == pc && owner.DispatchTick <= returned.Tick);
        }
        if (returnedChild is null)
        {
            return null;
        }
        var tracked = Ff7AutoPlayExecutionOwners.Observe(active, generation, vm.ProgramIdentity, vm.Tick, activities, readEntry);
        bool OwnedRequest(FieldHostRequest request) => request.Entity == active.Entity && request.Priority == 1 || request.Entity == completed.Entity && request.Priority == completed.Priority;
        bool OwnedHostWork(long requestId) => (vm.PendingRequests ?? []).Concat(process.Receipts.Select(receipt => receipt.Request)).Any(request => request.Id == requestId && OwnedRequest(request));
        if (tracked.ExecutionProvenance is not { RootReturned: false, Owners.Length: 1 } tail || tail.Owners[0] != rootOwner || (vm.PendingRequests ?? []).Any(OwnedRequest))
        {
            return null;
        }
        FieldEntitySnapshot child = vm.Entities[completed.Entity];
        if (child.Priority == completed.Priority || child.SavedProgramCounters.Length != 8 || child.SavedProgramCounters[completed.Priority] != 0 || process.Motions.Any(motion => !motion.Completed && OwnedHostWork(motion.RequestId)) || process.AnimationRequests.Any(request => OwnedHostWork(request.RequestId)) || process.Actors.Any(actor => actor.Ladder is { Completed: false } ladder && OwnedHostWork(ladder.RequestId) || actor.Jump is { Completed: false } jump && OwnedHostWork(jump.RequestId) || (actor.Entity == active.Entity || actor.Entity == completed.Entity) && actor.Offset is { Completed: false }))
        {
            return null;
        }
        for (int entity = 0; entity < vm.Entities.Length; entity++)
        {
            for (int priority = 0; priority < vm.Entities[entity].SyncStates.Length; priority++)
            {
                int owner = priority < vm.Entities[entity].SyncOwners.Length ? vm.Entities[entity].SyncOwners[priority] : -1;
                if ((owner == active.Entity || owner == completed.Entity) && vm.Entities[entity].SyncStates[priority] != FieldSyncState.None && (entity != completed.Entity || priority != completed.Priority || owner != active.Entity || vm.Entities[entity].SyncStates[priority] != FieldSyncState.Done))
                {
                    return null;
                }
            }
        }
        return tracked;
    }

    private bool ObserveCompletedSynchronousActivation(Ff7NativeTickObservation observation)
    {
        if (!RecordCompletedSynchronousActivation(State, planner, observation))
        {
            return false;
        }
        Publish();
        return true;
    }

    internal static bool RecordCompletedSynchronousActivation(Ff7AutoPlayState state, AutoPlayPlanner<Ff7AutoPlayDeferral> planner, Ff7NativeTickObservation observation)
    {
        if (observation.Transition is null || observation.CompletedSynchronousActivation is not { Accepted: true, Returned: false, TransitionOwned: false, ExecutionProvenance: { RootReturned: false, Owners.Length: 1 } completion } completed || state.Active is not { Accepted: true, Returned: false, TransitionOwned: false, CompletionRecorded: false, ExecutionProvenance: not null } active || active.Generation != observation.Generation || active.VisitId != completed.VisitId || active.EventId != completed.EventId || active.ConditionFingerprint != completed.ConditionFingerprint || active.Entity != completed.Entity || active.Script != completed.Script || active.Kind != completed.Kind || active.NavigationKey != completed.NavigationKey || active.ExecutionProvenance.ProgramIdentity != completion.ProgramIdentity || active.ExecutionProvenance.Owners.SingleOrDefault(owner => owner.ParentEntity == -1) != completion.Owners[0] || state.Choice is not null || state.PendingRestore is not null)
        {
            return false;
        }
        if (!Ff7AutoPlayExecutionOwners.Matches(active, observation.Generation, completion.ProgramIdentity) || !Ff7AutoPlayExecutionOwners.Matches(completed, observation.Generation, completion.ProgramIdentity))
        {
            return false;
        }
        var coverage = state.Coverage.Visits.SingleOrDefault(visit => visit.Visit.Key == active.VisitId)?.Events.SingleOrDefault(item => item.Event.Id == active.EventId);
        if (coverage?.Conditions is not { } conditions || conditions.ActiveEntry != active.ConditionFingerprint || conditions.HasExecutedEntry(active.ConditionFingerprint))
        {
            return false;
        }
        Ff7AutoPlayObservation source = new(active.VisitId, completed.ExecutionProvenance!.ProgramIdentity, active.Generation, [], false, true) { NavigationKey = active.NavigationKey! };
        RecordControlReturn(planner, active, source, navigationReceipt: false, exitSignature: null, progressChanged: false, selections: []);
        state.Active = null;
        if (state.NavigationGoal is { } goal && IsNavigationGoalCompleted(goal, active, source.NavigationKey))
        {
            state.NavigationGoal = null;
            state.TravelCommitment = null;
        }
        state.Status = "Recorded the completed native activation before field departure";
        return true;
    }
}
