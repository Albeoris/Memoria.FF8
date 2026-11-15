using Memoria.FF7.Events;

namespace Memoria.FF7.Runtime;

/// <summary>Supplies separate physical button edges only for a currently observed native wait loop.</summary>
public sealed class Ff7AutoPlayScriptedInput(Ff7Session session)
{
    public FieldScriptInputDemand? CurrentDemand { get; private set; }

    public Ff7AutoplayInput? ChooseInput()
    {
        CurrentDemand = null;
        if (session.World is not null || session.HasPendingTick || session.Host.Process.Transition is not null || session.Host.Process.Dialogues.Any(dialogue => !dialogue.Completed && session.Host.CanConfirmDialogue(dialogue.RequestId)))
        {
            return null;
        }
        CurrentDemand = session.Machine.ReadCurrentInputDemands().Where(demand => demand.AwaitingInput).OrderBy(demand => demand.Priority).ThenBy(demand => demand.Entity).FirstOrDefault();
        return CurrentDemand is null ? null : SelectInput(CurrentDemand, session.Machine.Input);
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
