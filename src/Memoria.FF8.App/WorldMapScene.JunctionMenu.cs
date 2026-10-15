using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8;

internal sealed partial class WorldMapScene
{
    private PlayerActionResult ApplyJunctionMenuAction(PlayerAction action)
    {
        NativeJunctionMenuInput.Validate(action);
        if (HasPendingBattle?.Invoke() == true || CurrentDialogue is not null)
        {
            return new PlayerActionResult(false, new PlayerTrigger(HasPendingBattle?.Invoke() == true ? PlayerTriggerKind.Battle : PlayerTriggerKind.Dialogue, session.State.FieldId, State.Frame, CurrentDialogue?.MessageId, CurrentDialogue?.Text), 0, "The current world interaction suspends the main menu.");
        }
        NativeCharacterVitals source = session.State.CharacterVitals ?? throw new NotSupportedException("Menu input requires authentic saved characters.");
        NativeCharacterVitals next = NativeJunctionMenuInput.Apply(source, action);
        session.SetCharacterVitals(next);
        PlayerActionResult result = new(true, new PlayerTrigger(PlayerTriggerKind.WorldMap, session.State.FieldId, State.Frame, null, null), 0);
        session.AppendReplayEntry(new ReplayJournalEntry(ReplayEntryKind.WorldAction, SceneKind.WorldMap, session.State.FieldId, State.Frame) { Code = (int)action.Kind, Value = action.Value, Text = action.Text, Quantity = action.Quantity, Frames = 1, Advanced = 0, Accepted = true, ResultScene = session.State.Scene, ResultFieldId = session.State.FieldId, ResultFrame = State.Frame, ResultTrigger = (int)result.Trigger.Kind });
        return result;
    }
}
