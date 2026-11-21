using Memoria.AutoPlay;
using Memoria.FF7.Events;

namespace Memoria.FF7.Runtime;

public sealed partial class Ff7AutoPlayPolicy
{
    private bool IsSettledPresentationChild(Ff7AutoPlayActiveEvent active, FieldVmSnapshot vm, Ff7AutoPlayExecutionOwner owner)
    {
        FieldScriptAnalysis? analysis = (uint)owner.Entity < vm.Entities.Length && vm.Entities[owner.Entity].Priority == owner.Priority ? session.Machine.AnalyzeScriptAtProgramCounter(owner.Entity, vm.Entities[owner.Entity].ProgramCounter) : null;
        return CanSettlePresentationChild(active, session.FieldGeneration, vm, session.Host.Process, owner, analysis) || CanSettleSupersededPlayerAnimation(active, session.FieldGeneration, vm, session.Host.Process, owner, session.Field.Scripts.Data.Span) || CanSettleGuardedNpcAnimation(active, session.FieldGeneration, vm, session.Host.Process, owner, session.Field.Scripts.Data.Span) || CanSettleLoopingNpcAnimations(active, session.FieldGeneration, vm, session.Host.Process, owner, session.Field.Scripts.Data.Span, analysis) || CanSettlePassiveNpcPolling(active, session.FieldGeneration, vm, session.Host.Process, owner, session.Field.Scripts.Data.Span);
    }

    internal static bool CanSettlePassiveNpcPolling(Ff7AutoPlayActiveEvent active, long generation, FieldVmSnapshot vm, Ff7FieldProcess process, Ff7AutoPlayExecutionOwner owner, ReadOnlySpan<byte> script)
    {
        if (active is not { Accepted: true, Returned: true, CompletionRecorded: false } || active.ExecutionProvenance?.RootReturned != true || !Ff7AutoPlayExecutionOwners.Matches(active, generation, vm.ProgramIdentity) || !active.ExecutionProvenance.Owners.Contains(owner) || owner.Queued || owner.ParentEntity < 0 || (uint)owner.ParentEntity >= vm.Entities.Length || owner.ParentPriority is < 0 or > 7 || (uint)owner.Entity >= vm.Entities.Length || owner.Priority is < 0 or >= 7 || owner.Script is < 0 or > 31 || owner.DispatchTick < active.ExecutionProvenance.RootDispatchTick || owner.DispatchTick > vm.Tick || owner.SourceProgramCounter < 0 || owner.SourceProgramCounter + 2 >= script.Length || script[owner.SourceProgramCounter] is not (0x01 or 0x02) || script[owner.SourceProgramCounter + 1] != owner.Entity || script[owner.SourceProgramCounter + 2] != ((owner.Priority << 5) | owner.Script))
        {
            return false;
        }
        return vm.Entities[owner.Entity].Priority == owner.Priority && ReadPassiveNpcPoll(vm, process, owner.Entity, owner.Entry, script) is not null;
    }

    internal static bool HasIndependentNpcFacingPoll(FieldVmSnapshot vm, Ff7FieldProcess process, int entity, ReadOnlySpan<byte> script, FieldScriptAnalysis? analysis)
    {
        if (analysis is not { Complete: true, ChangesStory: false, MayChangeNavigation: false, DestinationFields.Length: 0 } || ReadPassiveNpcPoll(vm, process, entity, 0, script) is not { } poll || script[poll.Head + 6] != 0xB6)
        {
            return false;
        }
        int pc = poll.Exit;
        if (pc + 3 < script.Length && script[pc] == 0xB3 && script[pc + 1] == 0)
        {
            pc += 3;
        }
        return (uint)pc < script.Length && script[pc] == 0;
    }

    private static (int Head, int Exit)? ReadPassiveNpcPoll(FieldVmSnapshot vm, Ff7FieldProcess process, int owner, int entry, ReadOnlySpan<byte> script)
    {
        if ((uint)owner >= vm.Entities.Length)
        {
            return null;
        }
        FieldEntitySnapshot entity = vm.Entities[owner];
        int priority = entity.Priority;
        int pc = entity.ProgramCounter;
        if (priority is < 0 or >= 7 || entity.SyncStates.Length <= priority || entity.SyncOwners.Length <= priority || entity.SyncStates[priority] != FieldSyncState.None || entity.SyncOwners[priority] != -1 || pc < entry || (uint)pc >= script.Length || process.PlayerLocked || process.MenuDisabled || process.GlobalCommand != Ff7GlobalCommand.None || process.Join is not null || process.Transition is not null || process.Movie is { Playing: true } || process.Menu is { Completed: false } || process.Dialogues.Any(dialogue => !dialogue.Completed) || (vm.PendingRequests ?? []).Any(request => request.Entity == owner && request.Priority == priority))
        {
            return null;
        }
        int head = script[pc] == 0x12 && pc + 1 < script.Length ? pc - script[pc + 1] : pc;
        if (head < entry || head < 0 || head + 7 >= script.Length || script[head] != 0x14 || script[head + 1] != 0x50 || script[head + 4] != 0 || vm.MapVariables.Length != 256 || vm.MapVariables[script[head + 2]] != script[head + 3])
        {
            return null;
        }
        int loopEnd = head + 6;
        if (script[head + 5] == 5 && head + 9 < script.Length && script[head + 6] == 0xB6)
        {
            int target = script[head + 7];
            if ((uint)target >= vm.Entities.Length || !vm.Entities[target].ModelBound || !process.Actors.Any(actor => actor.EntityBound && actor.PlayerControlled && actor.Entity == target))
            {
                return null;
            }
            loopEnd += 2;
        }
        if (script[head + 5] != loopEnd - head - 3 || script[loopEnd] != 0x12 || script[loopEnd + 1] != loopEnd - head || pc != head && pc != loopEnd)
        {
            return null;
        }
        return process.Actors.Any(actor => actor.EntityBound && actor.Entity == owner && !actor.PlayerControlled && actor.Jump is null && actor.Ladder is null) && !process.Actors.Any(actor => actor.Entity == owner && actor.PlayerControlled) && !process.Motions.Any(motion => motion.Entity == owner) ? (head, loopEnd + 2) : null;
    }

    internal static bool CanSettleLoopingNpcAnimations(Ff7AutoPlayActiveEvent active, long generation, FieldVmSnapshot vm, Ff7FieldProcess process, Ff7AutoPlayExecutionOwner owner, ReadOnlySpan<byte> script, FieldScriptAnalysis? analysis)
    {
        if (!active.Returned || active.ExecutionProvenance?.RootReturned != true || !Ff7AutoPlayExecutionOwners.Matches(active, generation, vm.ProgramIdentity) || !active.ExecutionProvenance.Owners.Contains(owner) || owner.Queued || owner.ParentEntity < 0 || (uint)owner.ParentEntity >= vm.Entities.Length || (uint)owner.Entity >= vm.Entities.Length || owner.Priority is < 0 or >= 8 || owner.Script is < 0 or > 31 || owner.SourceProgramCounter < 0 || owner.SourceProgramCounter + 2 >= script.Length || script[owner.SourceProgramCounter] is not (0x01 or 0x02) || script[owner.SourceProgramCounter + 1] != owner.Entity || script[owner.SourceProgramCounter + 2] != ((owner.Priority << 5) | owner.Script))
        {
            return false;
        }
        return vm.Entities[owner.Entity].Priority == owner.Priority && vm.Entities[owner.Entity].ProgramCounter >= owner.Entry && HasIndependentNpcAnimationLoop(vm, process, owner.Entity, script, analysis);
    }

    internal static bool HasIndependentNpcAnimationLoop(FieldVmSnapshot vm, Ff7FieldProcess process, int entityId, ReadOnlySpan<byte> script, FieldScriptAnalysis? analysis)
    {
        if ((uint)entityId >= vm.Entities.Length || analysis is not { Complete: true, ChangesStory: false, MayChangeNavigation: false, Reads.Length: 0, HostReads.Length: 0, DestinationFields.Length: 0, ExitProgramCounters.Length: 0 } || analysis.InstructionSites.Length == 0 || analysis.InstructionSites.Any(site => site.Entity != entityId))
        {
            return false;
        }
        FieldEntitySnapshot entity = vm.Entities[entityId];
        int pc = entity.ProgramCounter;
        if (entity.Priority is < 0 or >= 8 || entity.SyncStates.Length <= entity.Priority || entity.SyncOwners.Length <= entity.Priority || entity.SyncStates[entity.Priority] != FieldSyncState.None || entity.SyncOwners[entity.Priority] != -1 || !IsClosedNpcAnimationCycle(script, pc) || process.PlayerLocked || process.MenuDisabled || process.GlobalCommand != Ff7GlobalCommand.None || process.Join is not null || process.Transition is not null || process.Movie is { Playing: true } || process.Menu is { Completed: false } || process.Dialogues.Any(dialogue => !dialogue.Completed))
        {
            return false;
        }
        FieldHostRequest[] requests = (vm.PendingRequests ?? []).Where(request => request.Entity == entityId && request.Priority == entity.Priority).ToArray();
        if (requests.Length > 1 || requests.Length == 1 && (requests[0].ProgramCounter != pc || script[pc] != 0xA3 || requests[0].Command != new FieldAnimation(script[pc + 1], script[pc + 2], true, false) || !process.AnimationRequests.Any(item => item.RequestId == requests[0].Id && item.Entity == entityId)))
        {
            return false;
        }
        return process.Actors.Any(actor => actor.Entity == entityId && actor.EntityBound && !actor.PlayerControlled && actor.Jump is null && actor.Ladder is null) && !process.Motions.Any(motion => motion.Entity == entityId);
    }

    internal static bool IsClosedNpcAnimationCycle(ReadOnlySpan<byte> script, int start)
    {
        HashSet<int> visited = [];
        bool animation = false;
        bool wait = false;
        int pc = start;
        while (visited.Add(pc))
        {
            if ((uint)pc >= script.Length)
            {
                return false;
            }
            switch (script[pc])
            {
                case 0xA3 when pc + 2 < script.Length && script[pc + 2] != 0:
                    animation = true;
                    pc += 3;
                    break;
                case 0x24 when pc + 2 < script.Length && (script[pc + 1] | script[pc + 2] << 8) != 0:
                    wait = true;
                    pc += 3;
                    break;
                case 0x12 when pc + 1 < script.Length && script[pc + 1] > 0:
                    pc -= script[pc + 1];
                    break;
                case 0x13 when pc + 2 < script.Length && (script[pc + 1] | script[pc + 2] << 8) != 0:
                    pc -= script[pc + 1] | script[pc + 2] << 8;
                    break;
                default:
                    return false;
            }
        }
        return pc == start && animation && wait;
    }

    internal static bool CanSettleGuardedNpcAnimation(Ff7AutoPlayActiveEvent active, long generation, FieldVmSnapshot vm, Ff7FieldProcess process, Ff7AutoPlayExecutionOwner owner, ReadOnlySpan<byte> script)
    {
        if (!active.Returned || active.ExecutionProvenance?.RootReturned != true || !Ff7AutoPlayExecutionOwners.Matches(active, generation, vm.ProgramIdentity) || !active.ExecutionProvenance.Owners.Contains(owner) || owner.Queued || owner.ParentEntity != active.Entity || owner.ParentPriority != 1 || (uint)owner.Entity >= vm.Entities.Length || owner.Priority is < 0 or >= 8 || owner.Script is < 0 or > 31 || owner.SourceProgramCounter < 0 || owner.SourceProgramCounter + 2 >= script.Length || script[owner.SourceProgramCounter] is not (0x01 or 0x02) || script[owner.SourceProgramCounter + 1] != owner.Entity || script[owner.SourceProgramCounter + 2] != ((owner.Priority << 5) | owner.Script))
        {
            return false;
        }
        FieldEntitySnapshot entity = vm.Entities[owner.Entity];
        int pc = entity.ProgramCounter;
        if (entity.Priority != owner.Priority || entity.SyncStates.Length <= owner.Priority || entity.SyncOwners.Length <= owner.Priority || entity.SyncStates[owner.Priority] != FieldSyncState.None || entity.SyncOwners[owner.Priority] != -1 || pc < owner.Entry || pc < 0 || pc + 11 >= script.Length || script[pc] != 0x14 || script[pc + 1] != 0x50 || script[pc + 4] != 0 || script[pc + 5] != 6 || script[pc + 6] != 0xBA || script[pc + 8] == 0 || script[pc + 9] != 0x12 || script[pc + 10] != 9 || script[pc + 11] != 0 || process.PlayerLocked || process.MenuDisabled || process.GlobalCommand != Ff7GlobalCommand.None || process.Join is not null || process.Transition is not null || process.Movie is { Playing: true } || process.Menu is { Completed: false } || process.Dialogues.Any(dialogue => !dialogue.Completed) || (vm.PendingRequests ?? []).Any(request => request.Entity == owner.Entity && request.Priority == owner.Priority))
        {
            return false;
        }
        Ff7Actor? actor = process.Actors.SingleOrDefault(actor => actor.Entity == owner.Entity && actor.EntityBound && !actor.PlayerControlled);
        return actor is { AnimationStatus: 3 or 4, RequestedAnimation: null, Jump: null, Ladder: null } && actor.Animation == script[pc + 7] && !process.Motions.Any(motion => motion.Entity == owner.Entity);
    }

    internal static bool CanSettleSupersededPlayerAnimation(Ff7AutoPlayActiveEvent active, long generation, FieldVmSnapshot vm, Ff7FieldProcess process, Ff7AutoPlayExecutionOwner owner, ReadOnlySpan<byte> script)
    {
        if (!active.Returned || active.ExecutionProvenance?.RootReturned != true || !Ff7AutoPlayExecutionOwners.Matches(active, generation, vm.ProgramIdentity) || active.ExecutionProvenance.Owners.Contains(owner) != true || owner.Queued || owner.ParentEntity != active.Entity || owner.ParentPriority != 1 || (uint)owner.Entity >= vm.Entities.Length || owner.Priority is < 0 or >= 8 || owner.Script is < 0 or > 31 || owner.SourceProgramCounter < 0 || owner.SourceProgramCounter + 2 >= script.Length || script[owner.SourceProgramCounter] is not (0x01 or 0x02) || script[owner.SourceProgramCounter + 1] != owner.Entity || script[owner.SourceProgramCounter + 2] != ((owner.Priority << 5) | owner.Script))
        {
            return false;
        }
        FieldEntitySnapshot entity = vm.Entities[owner.Entity];
        int pc = entity.ProgramCounter;
        if (entity.Priority != owner.Priority || entity.SyncStates.Length <= owner.Priority || entity.SyncOwners.Length <= owner.Priority || entity.SyncStates[owner.Priority] != FieldSyncState.None || entity.SyncOwners[owner.Priority] != -1 || pc < owner.Entry || pc < 0 || pc + 3 >= script.Length || process.PlayerLocked || process.MenuDisabled || process.GlobalCommand != Ff7GlobalCommand.None || process.Join is not null || process.Transition is not null || process.Movie is { Playing: true } || process.Menu is { Completed: false } || process.Dialogues.Any(dialogue => !dialogue.Completed))
        {
            return false;
        }
        FieldAnimation? expected = script[pc] switch
        {
            0xA3 when script[pc + 2] != 0 && script[pc + 3] == 0 => new(script[pc + 1], script[pc + 2], true, false),
            0xBC when pc + 5 < script.Length && script[pc + 4] != 0 && script[pc + 5] == 0 => new(script[pc + 1], script[pc + 4], true, true, FirstFrame: script[pc + 2], LastFrame: script[pc + 3]),
            _ => null
        };
        FieldHostRequest[] requests = (vm.PendingRequests ?? []).Where(request => request.Entity == owner.Entity && request.Priority == owner.Priority).ToArray();
        if (expected is null || requests is not [{ Command: FieldAnimation animation } request] || request.ProgramCounter != pc || animation != expected || !process.AnimationRequests.Any(item => item.RequestId == request.Id && item.Entity == owner.Entity))
        {
            return false;
        }
        Ff7Actor? actor = process.Actors.SingleOrDefault(actor => actor.Entity == owner.Entity && actor.EntityBound && actor.PlayerControlled);
        return actor is { AnimationStatus: 2, RequestedAnimation: null, Jump: null, Ladder: null } && actor.Animation != animation.Animation && !process.Motions.Any(motion => motion.Entity == owner.Entity);
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
