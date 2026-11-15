using Memoria.FF7.Events;

namespace Memoria.FF7.Runtime;

/// <summary>Supplies separate physical button edges only for a currently observed native wait loop.</summary>
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
        CurrentDemand = SelectDemand(session.Machine.ReadCurrentInputDemands().Where(demand => locked || IsInteractionInputOwner(demand, vm, active, session.FieldGeneration)));
        return CurrentDemand is null ? null : SelectInput(CurrentDemand, session.Machine.Input);
    }

    internal static bool IsInteractionInputOwner(FieldScriptInputDemand demand, FieldVmSnapshot vm, Ff7AutoPlayActiveEvent? active, long generation)
    {
        if (active is not { Accepted: true, Returned: false } || active.Generation != generation || (uint)active.Entity >= vm.Entities.Length)
        {
            return false;
        }
        HashSet<int> owners = [active.Entity];
        bool added;
        do
        {
            added = false;
            for (int entity = 0; entity < vm.Entities.Length; entity++)
            {
                FieldEntitySnapshot target = vm.Entities[entity];
                for (int priority = 0; priority < target.SyncOwners.Length; priority++)
                {
                    if (target.Priority == priority && target.SyncStates[priority] == FieldSyncState.Waiting && owners.Contains(target.SyncOwners[priority]))
                    {
                        added |= owners.Add(entity);
                    }
                }
            }
        }
        while (added);
        return owners.Contains(demand.Entity) && (demand.Entity != active.Entity || demand.Priority < 7);
    }

    internal static FieldScriptInputDemand? SelectDemand(IEnumerable<FieldScriptInputDemand> demands)
    {
        return demands.Where(demand => demand.AwaitingInput).OrderBy(demand => demand.Priority).ThenBy(demand => demand.Entity).ThenBy(demand => demand.Mapped && (demand.Mask & 0x40) != 0 ? 0 : demand.Mapped && (demand.Mask & 0x20) != 0 ? 1 : 2).FirstOrDefault();
    }

    public static Ff7AutoplayInput SelectInput(FieldScriptInputDemand demand, FieldInputState input)
    {
        if (!demand.AwaitingInput || demand.Mask == 0)
        {
            throw new ArgumentException("A scripted button requires a proved current native wait loop.", nameof(demand));
        }
        ushort bit = (ushort)(demand.Mask & -demand.Mask);
        ushort current = demand.Mapped ? input.Held : input.RawHeld;
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
