using System.Globalization;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Events;

public static class NativeRefinementInput
{
    public static (int Recipe, int Row) Validate(PlayerAction action)
    {
        string[] parts = action.Text?.Split(':') ?? [];
        if (action.Kind != PlayerActionKind.RefineItems || action.Value is < 97 or >= 116 || action.Quantity is null or < 1 or > 100 || parts.Length != 2 || !int.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out int recipe) || recipe is < 0 or > 142 || !int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out int row) || row is < 0 or > 7 || action.Frames != 1 || action.Horizontal != 0 || action.Vertical != 0 || action.CameraHorizontal != 0)
        {
            throw new ArgumentException("Refinement requires a native ability97..115, recipe:row text and explicit batch count1..100.", nameof(action));
        }
        return (recipe, row);
    }
}

public sealed partial class EventEngine
{
    public Func<NativeRefinementDefinitions>? ResolveNativeRefinementDefinitions { get; set; }

    private bool ApplyRefinementAction(PlayerAction action)
    {
        (int recipe, int row) = NativeRefinementInput.Validate(action);
        bool activeMenu = partyMenu is { Kind: NativeFieldMenuKind.Normal, Phase: NativePartyMenuPhase.Active };
        if (!activeMenu && (partyMenu is not null || !CanAcceptFieldMovement || CurrentDialogue is not null || !Scene.MenuEnabled))
        {
            return false;
        }
        NativeRefinementDefinitions definitions = ResolveNativeRefinementDefinitions?.Invoke() ?? throw new NotSupportedException("Refinement requires original installed recipe definitions.");
        if (definitions.Abilities[action.Value - 92].Kind is 0 or 2 && services.CharacterVitals?.IsMenuCharacterAvailable(row, services.WorldControls) != true)
        {
            throw new InvalidOperationException("Refinement selected a character outside the current native menu roster.");
        }
        NativeRefinementResult next = definitions.Refine(services.CharacterVitals ?? throw new NotSupportedException("Refinement has no character owner."), services.Economy ?? throw new NotSupportedException("Refinement has no inventory owner."), services.CardInventory, action.Value, recipe, row, action.Quantity!.Value);
        if (activeMenu)
        {
            PublishPartyMenuCallback(next.Vitals, next.Economy, services.ShotAvailability!, services.SalaryPresentation!.Advance(1), advanceVibration: true, cardInventory: next.Cards);
            partyMenu = partyMenu! with { Callbacks = checked(partyMenu.Callbacks + 1) };
        }
        else
        {
            services.SetNativePartyMenuOwners(next.Vitals, next.Economy, services.ShotAvailability!, services.SalaryPresentation!, cardInventory: next.Cards);
        }
        return true;
    }
}
