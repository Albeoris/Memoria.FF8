using Memoria.AutoPlay;
using Memoria.FF7.Events;

namespace Memoria.FF7.Runtime;

public sealed partial class Ff7AutoPlayPolicy
{
    private bool IsSettledPresentationChild(Ff7AutoPlayActiveEvent active, FieldVmSnapshot vm, Ff7AutoPlayExecutionOwner owner)
    {
        FieldScriptAnalysis? analysis = (uint)owner.Entity < vm.Entities.Length && vm.Entities[owner.Entity].Priority == owner.Priority ? session.Machine.AnalyzeScriptAtProgramCounter(owner.Entity, vm.Entities[owner.Entity].ProgramCounter) : null;
        return CanSettlePresentationChild(active, session.FieldGeneration, vm, session.Host.Process, owner, analysis);
    }

    internal static bool CanSettlePresentationChild(Ff7AutoPlayActiveEvent active, long generation, FieldVmSnapshot vm, Ff7FieldProcess process, Ff7AutoPlayExecutionOwner owner, FieldScriptAnalysis? analysis)
    {
        if (!active.Returned || !Ff7AutoPlayExecutionOwners.Matches(active, generation, vm.ProgramIdentity) || active.ExecutionProvenance?.Owners.Contains(owner) != true || owner.Queued || owner.ParentEntity < 0 || (uint)owner.Entity >= vm.Entities.Length || vm.Entities[owner.Entity].Priority != owner.Priority || owner.Priority < 0 || owner.Priority >= vm.Entities[owner.Entity].SyncStates.Length || owner.Priority >= vm.Entities[owner.Entity].SyncOwners.Length || vm.Entities[owner.Entity].SyncStates[owner.Priority] != FieldSyncState.None || vm.Entities[owner.Entity].SyncOwners[owner.Priority] != -1 || analysis is not { Complete: true, RepeatingConstantFade: { Type: 8, Speed: 255, Adjustment: 0, NewFade: false, Wait: false } fade })
        {
            return false;
        }
        return !process.PlayerLocked && !process.MenuDisabled && process.GlobalCommand == Ff7GlobalCommand.None && process.Join is null && process.Transition is null && process.Movie is not { Playing: true } && process.Menu is not { Completed: false } && !process.Dialogues.Any(dialogue => !dialogue.Completed) && !(vm.PendingRequests ?? []).Any(request => request.Entity == owner.Entity && request.Priority == owner.Priority) && Ff7AutoPlayNativeReadiness.HasCompletedConstantFade(process, fade);
    }

    private bool CompletePresentationActivation(Ff7AutoPlayObservation observation)
    {
        if (State.Active is not { Accepted: true, Returned: false, TransitionOwned: false, CompletionRecorded: false } active || active.Kind is < Ff7AutoPlayTargetKind.LineTalk or > Ff7AutoPlayTargetKind.LineLeave || active.BeforeStory != Story() || session.Battle is not null || session.World is not null || !CanCompletePresentationActivation(State, observation, session.Machine.Capture(), session.Host.Process, session.Field.Scripts.Data.Span))
        {
            return false;
        }
        FieldScriptAnalysis entry = session.Machine.AnalyzeScript(active.Entity, active.Script);
        int pc = session.Machine.Capture().Entities[active.Entity].ProgramCounter;
        if (!entry.Complete || entry.ChangesStory || entry.MayChangeNavigation || entry.DestinationFields.Length != 0 || !entry.InstructionSites.Any(site => site.Entity == active.Entity && site.ProgramCounter == pc))
        {
            return false;
        }
        RecordControlReturn(planner, active, observation, navigationReceipt: false, exitSignature: null, progressChanged: false, selections: []);
        State.Active = null;
        if (State.NavigationGoal is { } goal && IsNavigationGoalCompleted(goal, active, observation.NavigationKey))
        {
            State.NavigationGoal = null;
            if (State.TravelCommitment?.Goal == goal)
            {
                State.TravelCommitment = null;
            }
        }
        CancelNativePlanning("native-presentation-activation");
        State.Status = "Native activation recorded; its presentation continues";
        Publish();
        return true;
    }

    internal static bool CanCompletePresentationActivation(Ff7AutoPlayState state, Ff7AutoPlayObservation observation, FieldVmSnapshot vm, Ff7FieldProcess process, ReadOnlySpan<byte> script)
    {
        if (!observation.CanAcceptInput || state.Active is not { Accepted: true, Returned: false, TransitionOwned: false, CompletionRecorded: false, DestinationField: null, TravelRoute: null, SpatialOwner: null, DirectEffectOwner: null, NavigationProbeKey: null, WorldControl: null, WorldModeGoal: null, TransportEntry: null } active || active.Script is < 0 or > 31 || active.Kind is < Ff7AutoPlayTargetKind.LineTalk or > Ff7AutoPlayTargetKind.LineLeave || active.Generation != observation.Generation || active.VisitId != observation.VisitId || active.NavigationKey != observation.NavigationKey || active.TravelSelections is { Count: > 0 } || state.Choice is not null || state.PendingRestore is not null || state.NativeRoute is not null || state.NativeRevalidation is not null || process.PlayerLocked || process.MenuDisabled || process.Join is not null || process.GlobalCommand != Ff7GlobalCommand.None || process.Transition is not null || process.Movie is not null || process.Menu is not null || process.Dialogues.Any(dialogue => !dialogue.Completed) || !process.Actors.Any(actor => actor.PlayerControlled && actor.EntityBound) || !process.Lines.Any(line => line.Entity == active.Entity && line.Enabled == 0) || process.Lines.Any(line => line.Entity == active.Entity && line.Enabled != 0) || (uint)active.Entity >= vm.Entities.Length)
        {
            return false;
        }
        if (vm.ProgramIdentity != observation.ProgramIdentity)
        {
            return false;
        }
        AutoPlayEventCoverage<Ff7AutoPlayDeferral>? coverage = state.Coverage.Visits.SingleOrDefault(visit => visit.Visit.Key == active.VisitId)?.Events.SingleOrDefault(item => item.Event.Id == active.EventId);
        if (coverage?.Conditions is not { } conditions || conditions.ActiveEntry != active.ConditionFingerprint || conditions.HasExecutedEntry(active.ConditionFingerprint))
        {
            return false;
        }
        FieldEntitySnapshot owner = vm.Entities[active.Entity];
        int scriptLength = script.Length;
        if (owner.Priority != 1 || owner.SavedProgramCounters.Length != 8 || !owner.SavedProgramCounters.Skip(2).Any(saved => saved > 0 && saved < scriptLength) || owner.SyncStates.Length != 8 || owner.SyncOwners.Length != 8 || owner.SyncStates[1] != FieldSyncState.None || owner.SyncOwners[1] != -1)
        {
            return false;
        }
        if (vm.Entities.Any(entity => entity.SyncOwners.Where((caller, priority) => caller == active.Entity && priority < entity.SyncStates.Length && entity.SyncStates[priority] != FieldSyncState.None).Any()))
        {
            return false;
        }
        FieldHostRequest[] owned = (vm.PendingRequests ?? []).Where(request => request.Entity == active.Entity).ToArray();
        return owned is [{ Priority: 1, Command: FieldFadeCommand { Wait: true } } request] && request.ProgramCounter == owner.ProgramCounter && IsClosedPresentationCycle(script, owner.ProgramCounter, process.FadeState);
    }

    internal static bool IsClosedPresentationCycle(ReadOnlySpan<byte> script, int start, Ff7FadeProcess? fade)
    {
        if ((uint)start >= script.Length || script[start] != 0x6C || fade is not { Type: 5 or 6, Speed: > 0 and <= byte.MaxValue, Position: >= 0 and <= 256, EntryCurve: false })
        {
            return false;
        }
        Dictionary<int, int> next = [];
        Dictionary<int, FieldFadeCommand> fades = [];
        int pc = start;
        while (!next.ContainsKey(pc))
        {
            if ((uint)pc >= script.Length)
            {
                return false;
            }
            int successor;
            switch (script[pc])
            {
                case 0x6B:
                    if (pc + 8 >= script.Length || script[pc + 1] != 0 || script[pc + 2] != 0 || script[pc + 6] == 0 || script[pc + 7] is not (5 or 6))
                    {
                        return false;
                    }
                    fades.Add(pc, new(script[pc + 7], script[pc + 3], script[pc + 4], script[pc + 5], script[pc + 6], script[pc + 8]));
                    successor = pc + 9;
                    break;
                case 0x6C:
                    successor = pc + 1;
                    break;
                case 0x12:
                case 0x13:
                    int width = script[pc] == 0x12 ? 1 : 2;
                    if (pc + width >= script.Length)
                    {
                        return false;
                    }
                    int offset = script[pc + 1] | (width == 2 ? script[pc + 2] << 8 : 0);
                    if (offset == 0 || offset > pc)
                    {
                        return false;
                    }
                    successor = pc - offset;
                    break;
                default:
                    return false;
            }
            next.Add(pc, successor);
            pc = successor;
        }
        if (pc != start || fades.Count == 0)
        {
            return false;
        }
        foreach (int wait in next.Keys)
        {
            if (script[wait] == 0x6C && !fades.Keys.Any(producer => next[producer] == wait))
            {
                return false;
            }
        }
        FieldFadeCommand preceding = fades.Single(pair => next[pair.Key] == start).Value;
        return fade.Type == preceding.Type && fade.Speed == preceding.Speed && fade.Red == preceding.Red && fade.Green == preceding.Green && fade.Blue == preceding.Blue;
    }
}
