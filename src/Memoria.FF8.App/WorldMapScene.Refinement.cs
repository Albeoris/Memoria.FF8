using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8;

internal sealed partial class WorldMapScene
{
    public PlayerActionResult RefineItems(PlayerAction action, NativeRefinementDefinitions definitions)
    {
        (int recipe, int row) = NativeRefinementInput.Validate(action);
        if (HasPendingBattle?.Invoke() == true || CurrentDialogue is not null)
        {
            return new PlayerActionResult(false, new PlayerTrigger(HasPendingBattle?.Invoke() == true ? PlayerTriggerKind.Battle : PlayerTriggerKind.Dialogue, session.State.FieldId, State.Frame, CurrentDialogue?.MessageId, CurrentDialogue?.Text), 0, "The current world interaction suspends refinement.");
        }
        NativeRefinementResult next = definitions.Refine(session.State.CharacterVitals ?? throw new NotSupportedException("World refinement has no character owner."), session.State.Economy ?? throw new NotSupportedException("World refinement has no inventory owner."), session.State.CardInventory, action.Value, recipe, row, action.Quantity!.Value);
        session.SetNativePartyMenuOwners(next.Vitals, next.Economy, session.State.ShotAvailability!, session.State.SalaryPresentation!, cardInventory: next.Cards);
        return new PlayerActionResult(true, new PlayerTrigger(PlayerTriggerKind.WorldMap, session.State.FieldId, State.Frame, null, null), 0);
    }
}
