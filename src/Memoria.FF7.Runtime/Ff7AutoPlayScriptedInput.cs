using Memoria.FF7.Events;

namespace Memoria.FF7.Runtime;

/// <summary>Projects physical buttons from observed native waits and owned interactive continuations.</summary>
public sealed partial class Ff7AutoPlayScriptedInput(Ff7Session session)
{
    public FieldScriptInputDemand? CurrentDemand { get; private set; }

    public bool IsEntryInputSatisfied(int entity, int script)
    {
        if (entity == 5 && script == 4 && session.Machine.ProgramIdentity == Ff7AutoPlayPolicy.GaeaMovieProgram && session.AutoPlayState is { } state && IsNativeGaeaMovieNeutralEntry(state, session.FieldGeneration, session.Machine.Capture(), session.Host.Process))
        {
            return true;
        }
        FieldScriptInputDemand? demand = session.Machine.ReadEntryInputDemand(entity, script);
        return demand is null || IsInputSatisfied(demand, session.Machine.Input);
    }

    internal static bool IsInputSatisfied(FieldScriptInputDemand demand, FieldInputState input)
    {
        ushort keys = demand.Kind switch
        {
            FieldScriptInputKind.Held => demand.Mapped ? input.Held : input.RawHeld,
            FieldScriptInputKind.Pressed => demand.Mapped ? input.Pressed : input.RawPressed,
            FieldScriptInputKind.Released => demand.Mapped ? input.Released : input.RawReleased,
            _ => throw new ArgumentOutOfRangeException(nameof(demand))
        };
        return (keys & demand.Mask) != 0;
    }

    /// <summary>Projects a source-proved entry gate; the caller owns physical contact readiness.</summary>
    public Ff7AutoplayInput? ChooseEntryInput(int entity, int script)
    {
        CurrentDemand = null;
        if (session.World is not null || session.HasPendingTick || session.Host.Process.Transition is not null || session.Host.Process.Dialogues.Any(dialogue => !dialogue.Completed && session.Host.CanConfirmDialogue(dialogue.RequestId)))
        {
            return null;
        }
        if (entity == 5 && script == 4 && session.Machine.ProgramIdentity == Ff7AutoPlayPolicy.GaeaMovieProgram && session.AutoPlayState is { } state && IsNativeGaeaMovieNeutralEntry(state, session.FieldGeneration, session.Machine.Capture(), session.Host.Process))
        {
            CurrentDemand = null;
            return new(Held: 0, Run: false);
        }
        CurrentDemand = session.Machine.ReadEntryInputDemand(entity, script);
        if (CurrentDemand is null || CurrentDemand.Mask == 0)
        {
            return null;
        }
        // Entry admission is scoped by the caller rather than a native wait loop.
        // The existing edge projection supplies the same separate physical press/release ticks.
        return SelectInput(CurrentDemand with { AwaitingInput = true }, session.Machine.Input);
    }

    /// <summary>Projects the selected autonomous branch only at its proved current native spatial predicate.</summary>
    public Ff7AutoplayInput? ChooseSpatialEntryInput(Ff7AutoPlayTarget target)
    {
        CurrentDemand = null;
        Ff7AutoPlayActiveEvent? active = session.AutoPlayState?.Active;
        if (target.Kind != Ff7AutoPlayTargetKind.AutonomousSpatial || target.SpatialInputGuards.Length == 0 || active is not { Accepted: false } || active.EventId != target.Id || active.Generation != session.FieldGeneration || session.World is not null || session.HasPendingTick || session.Host.Process.Transition is not null || session.Host.Process.PlayerLocked || session.Host.Process.Dialogues.Any(dialogue => !dialogue.Completed && session.Host.CanConfirmDialogue(dialogue.RequestId)))
        {
            return null;
        }
        Ff7Actor? player = session.Host.Process.Actors.SingleOrDefault(actor => actor.PlayerControlled);
        if (player is null)
        {
            return null;
        }
        if (target.TriangleOnly)
        {
            if (player.Triangle != target.Triangle)
            {
                return null;
            }
        }
        else
        {
            if (target.SpatialSourceProgramCounter is not int source)
            {
                return null;
            }
            FieldActorPosition position = new(checked((short)(player.X4096 >> 12)), checked((short)(player.Y4096 >> 12)), checked((short)(player.Z4096 >> 12)), checked((short)player.Triangle));
            FieldSpatialObservation observed = session.Machine.ReadCurrentSpatialDemands(player.Entity, [new(player.Triangle, position)], randomDomain: session.ReadSpatialRandomDomain());
            if (!observed.Demands.Any(demand => demand.Entity == target.Entity && demand.ProgramCounter == source && demand.TargetEntity == (target.SpatialOwner?.TargetEntity ?? demand.TargetEntity) && demand.TargetScript == (target.SpatialOwner?.TargetScript ?? demand.TargetScript) && Ff7AutoPlayDiscovery.SpatialInputGuardSignature(demand.InputGuards) == Ff7AutoPlayDiscovery.SpatialInputGuardSignature(target.SpatialInputGuards)))
            {
                return null;
            }
        }
        CurrentDemand = target.SpatialInputGuards.FirstOrDefault(guard => guard.Matched)?.Demand ?? target.SpatialInputGuards[0].Demand;
        return SelectSpatialEntryInput(target.SpatialInputGuards, session.Machine.Input);
    }

    internal static Ff7AutoplayInput SelectSpatialEntryInput(IReadOnlyList<FieldSpatialInputGuard> guards, FieldInputState input)
    {
        if (guards.Count == 0)
        {
            throw new ArgumentException("A spatial entry button requires source-proved native input predicates.", nameof(guards));
        }
        ushort combined = 0;
        foreach (FieldSpatialInputGuard guard in guards)
        {
            if (guard.Matched && guard.Demand.Mask != 0)
            {
                Ff7AutoplayInput selected = SelectInput(guard.Demand with { AwaitingInput = true }, input);
                if (FieldMachine.AreSpatialInputGuardsSatisfied(guards, ProjectSpatialInput(selected.Held, input)))
                {
                    return selected;
                }
                combined |= selected.Held;
            }
        }
        if (FieldMachine.AreSpatialInputGuardsSatisfied(guards, ProjectSpatialInput(combined, input)))
        {
            return new(Held: combined, Run: false);
        }
        if (FieldMachine.TryFindSpatialInputWitness(guards, out FieldInputState immediate, sharedPhysicalButtons: true, previousInput: input))
        {
            return new(Held: immediate.Held, Run: false);
        }
        if (FieldMachine.TryFindSpatialInputWitness(guards, out FieldInputState prepared, sharedPhysicalButtons: true))
        {
            return new(Held: prepared.PreviousHeld ?? throw new InvalidDataException("A native input witness omitted its physical preparation state."), Run: false);
        }
        throw new NotSupportedException("The selected native spatial input predicates have no coherent physical press/release sequence.");
    }

    private static FieldInputState ProjectSpatialInput(ushort held, FieldInputState previous)
    {
        ushort pressed = (ushort)(held & ~previous.Held);
        return new(held, pressed, (ushort)(previous.Held & ~held), held, pressed, (ushort)(previous.RawHeld & ~held), PreviousHeld: previous.Held, PreviousRawHeld: previous.RawHeld);
    }

    public Ff7AutoplayInput? ChooseInput()
    {
        CurrentDemand = null;
        if (session.World is not null || session.HasPendingTick || session.Host.Process.Transition is not null || session.Host.Process.Dialogues.Any(dialogue => !dialogue.Completed && session.Host.CanConfirmDialogue(dialogue.RequestId)))
        {
            return null;
        }
        Ff7AutoPlayActiveEvent? active = session.AutoPlayState?.Active;
        bool locked = session.Host.Process.PlayerLocked;
        if (!locked && (active is not { Accepted: true, Returned: false } || active.Generation != session.FieldGeneration))
        {
            return ChooseAltarInput(active) ?? ChooseAmbientPromptInput(active);
        }
        FieldVmSnapshot vm = session.Machine.Capture();
        if (session.AutoPlayState is { } state && IsNativeGaeaMovieNeutralReturn(state, session.FieldGeneration, vm, session.Host.Process))
        {
            return new(Held: 0, Run: false);
        }
        IReadOnlyList<FieldScriptInputDemand> demands = session.Machine.ReadCurrentInputDemands().Where(demand => !Ff7AutoPlayPolicy.IsNativeGaeaWarmthDemand(demand)).ToArray();
        if (ReadNativeJunonEscapeInput(vm, session.Host.Process, session.Field.Scripts.Data.Span) is Ff7AutoplayInput escapeInput)
        {
            CurrentDemand = demands.FirstOrDefault(demand => demand.Entity == 3 && demand.Priority == 6 && demand.AwaitingInput);
            return escapeInput;
        }
        if (ReadNativeCorelTrainInput(vm, session.Host.Process, session.Field.Scripts.Data.Span, session.Machine.Input) is Ff7AutoplayInput trainInput)
        {
            CurrentDemand = demands.FirstOrDefault(demand => demand.Entity == 11 && demand.Priority == 7 && demand.AwaitingInput);
            return trainInput;
        }
        CurrentDemand = demands.Where(demand => IsOwnedSpatialContinuationDemand(demand, vm, active, session.FieldGeneration, locked)).OrderBy(demand => demand.Priority).ThenBy(demand => demand.Entity).FirstOrDefault();
        if (CurrentDemand is not null)
        {
            return SelectOwnedSpatialContinuationInput(CurrentDemand, vm, active, session.FieldGeneration, locked, session.Machine.Input);
        }
        FieldScriptInputDemand[] ownedDemands = demands.Where(demand => locked || IsInteractionInputOwner(demand, vm, active, session.FieldGeneration)).ToArray();
        if (TryChooseClockInput(ownedDemands, out Ff7AutoplayInput clockInput))
        {
            return clockInput;
        }
        CurrentDemand = SelectAltarForwardInput(vm, session.Host.Process, ownedDemands) ?? SelectDemand(ownedDemands);
        return CurrentDemand is null ? null : SelectInput(CurrentDemand, session.Machine.Input);
    }

    private Ff7AutoplayInput? ChooseAmbientPromptInput(Ff7AutoPlayActiveEvent? active)
    {
        Ff7FieldProcess process = session.Host.Process;
        if (active is { Accepted: true } || !process.MenuDisabled || process.GlobalCommand != Ff7GlobalCommand.None || process.Join is not null || process.Movie is { Playing: true })
        {
            return null;
        }
        Ff7Dialogue[] prompts = process.Dialogues.Where(dialogue => !dialogue.Completed && dialogue.AwaitingInput && dialogue.FirstChoice is null && dialogue.LastChoice is null && !session.Host.CanConfirmDialogue(dialogue.RequestId)).ToArray();
        if (prompts.Length != 1)
        {
            return null;
        }
        FieldVmSnapshot vm = session.Machine.Capture();
        FieldHostRequest? request = vm.PendingRequests?.SingleOrDefault(request => request.Id == prompts[0].RequestId && request.Command is FieldDialogueCommand command && command.Window == prompts[0].Window && command.Text == prompts[0].Text);
        if (request is null)
        {
            return null;
        }
        FieldScriptInputDemand[] demands = session.Machine.ReadCurrentInputDemands().Where(demand => IsExcavationBombInput(vm, process, prompts[0], request, demand) || session.Machine.ProveCurrentPromptChoiceInput(demand, request)).ToArray();
        if (demands.Length != 1)
        {
            return null;
        }
        CurrentDemand = demands[0];
        return SelectInput(CurrentDemand, session.Machine.Input);
    }

    internal static bool IsInteractionInputOwner(FieldScriptInputDemand demand, FieldVmSnapshot vm, Ff7AutoPlayActiveEvent? active, long generation)
    {
        return ReadInteractionOwners(vm, active, generation).TryGetValue(demand.Entity, out int priority) && priority == demand.Priority;
    }

    internal static IReadOnlyDictionary<int, int> ReadInteractionOwners(FieldVmSnapshot vm, Ff7AutoPlayActiveEvent? active, long generation)
    {
        Dictionary<int, int> owners = Ff7AutoPlayExecutionOwners.ReadCurrentOwners(vm, active, generation).ToDictionary();
        if (active?.ExecutionProvenance is not null)
        {
            return owners;
        }
        bool continuation = active is { Returned: true, SpatialOwner: not null };
        int root = continuation ? active!.Entity : active?.DirectEffectOwner?.Entity ?? active?.SpatialOwner?.TargetEntity ?? active?.Entity ?? -1;
        int rootPriority = continuation ? active!.SpatialOwner!.SourcePriority : active?.DirectEffectOwner?.Priority ?? active?.SpatialOwner?.TargetPriority ?? 1;
        if (active is not { Accepted: true } || active.Returned && !continuation || active.Generation != generation || (uint)root >= vm.Entities.Length || vm.Entities[root].Priority != rootPriority)
        {
            return owners;
        }
        owners.TryAdd(root, rootPriority);
        bool added;
        do
        {
            added = false;
            for (int entity = 0; entity < vm.Entities.Length; entity++)
            {
                FieldEntitySnapshot target = vm.Entities[entity];
                int priority = target.Priority;
                if ((uint)priority < 7 && priority < target.SyncOwners.Length && priority < target.SyncStates.Length && target.SyncStates[priority] == FieldSyncState.Waiting && owners.ContainsKey(target.SyncOwners[priority]))
                {
                    added |= owners.TryAdd(entity, priority);
                }
            }
        }
        while (added);
        return owners;
    }

    internal static FieldScriptInputDemand? SelectDemand(IEnumerable<FieldScriptInputDemand> demands)
    {
        return demands.Where(demand => demand.AwaitingInput).OrderBy(demand => demand.Priority).ThenBy(demand => demand.Entity).ThenBy(demand => demand.MatchedContinuation == FieldScriptInputContinuation.LeavesPollingLoop ? 0 : 1).ThenBy(demand => demand.Mapped && (demand.Mask & 0x40) != 0 ? 0 : demand.Mapped && (demand.Mask & 0x20) != 0 ? 1 : 2).FirstOrDefault();
    }

    internal static bool IsOwnedSpatialContinuationDemand(FieldScriptInputDemand demand, FieldVmSnapshot vm, Ff7AutoPlayActiveEvent? active, long generation, bool locked)
    {
        return locked && active is { Kind: Ff7AutoPlayTargetKind.AutonomousSpatial, Accepted: true, Returned: true, SpatialOwner: not null } && demand.ProgramIdentity == vm.ProgramIdentity && demand.Mask != 0 && IsInteractionInputOwner(demand, vm, active, generation);
    }

    internal static Ff7AutoplayInput SelectOwnedSpatialContinuationInput(FieldScriptInputDemand demand, FieldVmSnapshot vm, Ff7AutoPlayActiveEvent? active, long generation, bool locked, FieldInputState input)
    {
        if (!IsOwnedSpatialContinuationDemand(demand, vm, active, generation, locked))
        {
            throw new ArgumentException("A native continuation input branch requires its accepted spatial owner and unresolved control lock.", nameof(demand));
        }
        return ProjectInput(demand, input);
    }

    public static Ff7AutoplayInput SelectInput(FieldScriptInputDemand demand, FieldInputState input)
    {
        if (!demand.AwaitingInput || demand.Mask == 0)
        {
            throw new ArgumentException("A scripted button requires a proved current native wait loop.", nameof(demand));
        }
        return ProjectInput(demand, input);
    }

    private static Ff7AutoplayInput ProjectInput(FieldScriptInputDemand demand, FieldInputState input)
    {
        ushort bit = (ushort)(demand.Mask & -demand.Mask);
        ushort current = demand.Mapped ? input.Held : input.RawHeld;
        if (demand.DelayTicks > 0 && demand.Kind != FieldScriptInputKind.Held)
        {
            return new(Held: demand.Kind == FieldScriptInputKind.Released ? bit : (ushort)0, Run: false);
        }
        ushort held = demand.Kind switch
        {
            FieldScriptInputKind.Held => bit,
            FieldScriptInputKind.Pressed => ((input.Held | input.RawHeld) & bit) == 0 ? bit : (ushort)0,
            FieldScriptInputKind.Released => (current & bit) != 0 ? (ushort)0 : bit,
            _ => throw new ArgumentOutOfRangeException(nameof(demand))
        };
        return new(Held: held, Run: false);
    }
}
