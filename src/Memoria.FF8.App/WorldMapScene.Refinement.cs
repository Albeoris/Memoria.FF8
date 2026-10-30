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
        if (definitions.Abilities[action.Value - 92].Kind is 0 or 2 && session.State.CharacterVitals?.IsMenuCharacterAvailable(row, session.State.WorldControls) != true)
        {
            throw new InvalidOperationException("Refinement selected a character outside the current native menu roster.");
        }
        NativeRefinementResult next = definitions.Refine(session.State.CharacterVitals ?? throw new NotSupportedException("World refinement has no character owner."), session.State.Economy ?? throw new NotSupportedException("World refinement has no inventory owner."), session.State.CardInventory, action.Value, recipe, row, action.Quantity!.Value, session.State.GameplayPreferences.CardModOnce, session.State.RefinedCardIds, NativeItemOrderAliases.From(session.State));
        session.SetNativePartyMenuOwners(next.Vitals, next.Economy, session.State.ShotAvailability!, session.State.SalaryPresentation!, cardInventory: next.Cards);
        if (definitions.Abilities[action.Value - 92].Kind == 4)
        {
            session.RecordCardRefinement(definitions.Recipe(action.Value, recipe).Ingredient);
        }
        return new PlayerActionResult(true, new PlayerTrigger(PlayerTriggerKind.WorldMap, session.State.FieldId, State.Frame, null, null), 0);
    }
}
