using System.Globalization;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Events;

public static class NativeJunctionMenuInput
{
    public static bool IsMenuAction(PlayerActionKind kind) => kind is PlayerActionKind.JunctionGuardianForce or PlayerActionKind.SetJunctionCommand or PlayerActionKind.SetJunctionAbility or PlayerActionKind.SelectGuardianForceLearning or PlayerActionKind.SetJunctionMagic;

    public static void Validate(PlayerAction action)
    {
        if (!IsMenuAction(action.Kind))
        {
            throw new ArgumentException("Expected a junction or GF learning action.", nameof(action));
        }
        bool learning = action.Kind == PlayerActionKind.SelectGuardianForceLearning;
        if ((uint)action.Value >= (learning ? 16u : 8u) || !int.TryParse(action.Text, NumberStyles.None, CultureInfo.InvariantCulture, out int choice) || choice < 0 || choice >= (action.Kind == PlayerActionKind.JunctionGuardianForce ? 16 : action.Kind == PlayerActionKind.SetJunctionMagic ? 57 : 128) || action.Kind == PlayerActionKind.SetJunctionCommand && action.Quantity is not (>= 0 and < 3) || action.Kind == PlayerActionKind.SetJunctionAbility && action.Quantity is not (>= 0 and < 4) || action.Kind == PlayerActionKind.SetJunctionMagic && action.Quantity is not (>= 0 and < 19) || action.Kind is PlayerActionKind.JunctionGuardianForce or PlayerActionKind.SelectGuardianForceLearning && action.Quantity is not null || action.Frames != 1 || action.Horizontal != 0 || action.Vertical != 0 || action.CameraHorizontal != 0)
        {
            throw new ArgumentException("Menu input requires explicit native row/GF, numeric choice, valid slot and one ordinary menu action.", nameof(action));
        }
    }

    public static NativeCharacterVitals Apply(NativeCharacterVitals vitals, PlayerAction action)
    {
        Validate(action);
        int choice = int.Parse(action.Text!, CultureInfo.InvariantCulture);
        return action.Kind switch
        {
            PlayerActionKind.JunctionGuardianForce => vitals.ToggleGuardianForceJunction(action.Value, choice),
            PlayerActionKind.SetJunctionCommand => vitals.SetJunctionAbility(action.Value, action.Quantity!.Value, choice, true),
            PlayerActionKind.SetJunctionAbility => vitals.SetJunctionAbility(action.Value, action.Quantity!.Value, choice, false),
            PlayerActionKind.SelectGuardianForceLearning => vitals.SelectGuardianForceLearning(action.Value, choice),
            PlayerActionKind.SetJunctionMagic => vitals.SetJunctionMagic(action.Value, action.Quantity!.Value, choice),
            _ => throw new ArgumentOutOfRangeException(nameof(action))
        };
    }
}

public sealed partial class EventEngine
{
    private bool ApplyJunctionMenuAction(PlayerAction action)
    {
        bool activeMenu = partyMenu is { Kind: NativeFieldMenuKind.Normal, Phase: NativePartyMenuPhase.Active };
        if (!activeMenu && (partyMenu is not null || !CanAcceptFieldMovement || CurrentDialogue is not null || !Scene.MenuEnabled))
        {
            return false;
        }
        NativeCharacterVitals source = services.CharacterVitals ?? throw new NotSupportedException("Menu input requires authentic saved characters.");
        NativeCharacterVitals next = NativeJunctionMenuInput.Apply(source, action);
        if (activeMenu)
        {
            PublishPartyMenuCallback(next, services.Economy!, services.ShotAvailability!, services.SalaryPresentation!.Advance(1), advanceVibration: true);
            partyMenu = partyMenu! with { Callbacks = checked(partyMenu.Callbacks + 1) };
        }
        else
        {
            services.SetCharacterVitals(next);
        }
        return true;
    }
}
