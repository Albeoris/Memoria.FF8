using Memoria.FF7.Events;

namespace Memoria.FF7.Runtime;

/// <summary>Projects physical buttons from observed native waits and owned interactive continuations.</summary>
public sealed class Ff7AutoPlayScriptedInput(Ff7Session session)
{
    public FieldScriptInputDemand? CurrentDemand { get; private set; }

    public bool IsEntryInputSatisfied(int entity, int script)
    {
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
        CurrentDemand = session.Machine.ReadEntryInputDemand(entity, script);
        if (CurrentDemand is null || CurrentDemand.Mask == 0)
        {
            return null;
        }
        // Entry admission is scoped by the caller rather than a native wait loop.
        // The existing edge projection supplies the same separate physical press/release ticks.
        return SelectInput(CurrentDemand with { AwaitingInput = true }, session.Machine.Input);
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
            return null;
        }
        FieldVmSnapshot vm = session.Machine.Capture();
        IReadOnlyList<FieldScriptInputDemand> demands = session.Machine.ReadCurrentInputDemands();
        CurrentDemand = demands.Where(demand => IsOwnedSpatialContinuationDemand(demand, vm, active, session.FieldGeneration, locked)).OrderBy(demand => demand.Priority).ThenBy(demand => demand.Entity).FirstOrDefault();
        if (CurrentDemand is not null)
        {
            return SelectOwnedSpatialContinuationInput(CurrentDemand, vm, active, session.FieldGeneration, locked, session.Machine.Input);
        }
        CurrentDemand = SelectDemand(demands.Where(demand => locked || IsInteractionInputOwner(demand, vm, active, session.FieldGeneration)));
        return CurrentDemand is null ? null : SelectInput(CurrentDemand, session.Machine.Input);
    }

    internal static bool IsInteractionInputOwner(FieldScriptInputDemand demand, FieldVmSnapshot vm, Ff7AutoPlayActiveEvent? active, long generation)
    {
        return ReadInteractionOwners(vm, active, generation).TryGetValue(demand.Entity, out int priority) && priority == demand.Priority;
    }

    internal static IReadOnlyDictionary<int, int> ReadInteractionOwners(FieldVmSnapshot vm, Ff7AutoPlayActiveEvent? active, long generation)
    {
        Dictionary<int, int> owners = [];
        bool continuation = active is { Returned: true, SpatialOwner: not null };
        int root = continuation ? active!.Entity : active?.SpatialOwner?.TargetEntity ?? active?.Entity ?? -1;
        int rootPriority = continuation ? active!.SpatialOwner!.SourcePriority : active?.SpatialOwner?.TargetPriority ?? 1;
        if (active is not { Accepted: true } || active.Returned && !continuation || active.Generation != generation || (uint)root >= vm.Entities.Length || vm.Entities[root].Priority != rootPriority)
        {
            return owners;
        }
        owners.Add(root, rootPriority);
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
        return demands.Where(demand => demand.AwaitingInput).OrderBy(demand => demand.Priority).ThenBy(demand => demand.Entity).ThenBy(demand => demand.Mapped && (demand.Mask & 0x40) != 0 ? 0 : demand.Mapped && (demand.Mask & 0x20) != 0 ? 1 : 2).FirstOrDefault();
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
