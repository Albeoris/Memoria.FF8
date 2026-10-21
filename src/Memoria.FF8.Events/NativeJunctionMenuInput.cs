using System.Globalization;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Events;

public static class NativeJunctionMenuInput
{
    public static bool IsMenuAction(PlayerActionKind kind) => kind is PlayerActionKind.JunctionGuardianForce or PlayerActionKind.SetJunctionCommand or PlayerActionKind.SetJunctionAbility or PlayerActionKind.SelectGuardianForceLearning or PlayerActionKind.SetJunctionMagic or PlayerActionKind.RemoveJunctions or PlayerActionKind.AutoJunction or PlayerActionKind.RemoveAllMagicJunctions or PlayerActionKind.ReassignJunctionMagic;

    public static void Validate(PlayerAction action)
    {
        if (!IsMenuAction(action.Kind))
        {
            throw new ArgumentException("Expected a junction or GF learning action.", nameof(action));
        }
        if (action.Kind == PlayerActionKind.RemoveAllMagicJunctions)
        {
            if (action.Value != 0 || action.Text is not null || action.Quantity is not null || action.Frames != 1 || action.Horizontal != 0 || action.Vertical != 0 || action.CameraHorizontal != 0 || action.WorldButtons != 0 || action.WaitWhilePlayable)
            {
                throw new ArgumentException("Party magic removal requires one ordinary menu action without character or category operands.", nameof(action));
            }
            return;
        }
        bool learning = action.Kind == PlayerActionKind.SelectGuardianForceLearning;
        if (action.Kind is PlayerActionKind.RemoveJunctions or PlayerActionKind.AutoJunction)
        {
            bool validChoice = action.Text is "0" or "1" || action.Kind == PlayerActionKind.AutoJunction && action.Text == "2";
            if ((uint)action.Value >= 8u || !validChoice || action.Quantity is not null || action.Frames != 1 || action.Horizontal != 0 || action.Vertical != 0 || action.CameraHorizontal != 0)
            {
                throw new ArgumentException("Junction input requires an explicit character row, a valid category or Auto priority and one ordinary menu action.", nameof(action));
            }
            return;
        }
        if ((uint)action.Value >= (learning ? 16u : 8u) || !int.TryParse(action.Text, NumberStyles.None, CultureInfo.InvariantCulture, out int choice) || choice < 0 || choice >= (action.Kind == PlayerActionKind.JunctionGuardianForce ? 16 : action.Kind is PlayerActionKind.SetJunctionMagic or PlayerActionKind.ReassignJunctionMagic ? 57 : 128) || action.Kind == PlayerActionKind.SetJunctionCommand && action.Quantity is not (>= 0 and < 3) || action.Kind == PlayerActionKind.SetJunctionAbility && action.Quantity is not (>= 0 and < 4) || action.Kind is PlayerActionKind.SetJunctionMagic or PlayerActionKind.ReassignJunctionMagic && action.Quantity is not (>= 0 and < 19) || action.Kind is PlayerActionKind.JunctionGuardianForce or PlayerActionKind.SelectGuardianForceLearning && action.Quantity is not null || action.Frames != 1 || action.Horizontal != 0 || action.Vertical != 0 || action.CameraHorizontal != 0)
        {
            throw new ArgumentException("Menu input requires explicit native row/GF, numeric choice, valid slot and one ordinary menu action.", nameof(action));
        }
    }

    public static NativeCharacterVitals Apply(NativeCharacterVitals vitals, PlayerAction action, Func<NativeCharacterVitals, int, int, NativeCharacterVitals>? autoJunction = null)
    {
        Validate(action);
        if (action.Kind == PlayerActionKind.RemoveAllMagicJunctions)
        {
            return vitals.RemoveAllMagicJunctions();
        }
        int choice = int.Parse(action.Text!, CultureInfo.InvariantCulture);
        return action.Kind switch
        {
            PlayerActionKind.JunctionGuardianForce => vitals.ToggleGuardianForceJunction(action.Value, choice),
            PlayerActionKind.SetJunctionCommand => vitals.SetJunctionAbility(action.Value, action.Quantity!.Value, choice, true),
            PlayerActionKind.SetJunctionAbility => vitals.SetJunctionAbility(action.Value, action.Quantity!.Value, choice, false),
            PlayerActionKind.SelectGuardianForceLearning => vitals.SelectGuardianForceLearning(action.Value, choice),
            PlayerActionKind.ReassignJunctionMagic => vitals.SetJunctionMagic(action.Value, action.Quantity!.Value, choice, true),
            PlayerActionKind.SetJunctionMagic => vitals.SetJunctionMagic(action.Value, action.Quantity!.Value, choice),
            PlayerActionKind.RemoveJunctions => vitals.RemoveJunctions(action.Value, choice == 1),
            PlayerActionKind.AutoJunction => (autoJunction ?? throw new NotSupportedException("Auto junction requires its authenticated original priority tables and magic kernel records."))(vitals, action.Value, choice),
            _ => throw new ArgumentOutOfRangeException(nameof(action))
        };
    }
}

public sealed partial class EventEngine
{
    public Func<NativeCharacterVitals, int, int, NativeCharacterVitals>? ResolveNativeAutoJunction { get; set; }

    private bool ApplyJunctionMenuAction(PlayerAction action)
    {
        bool activeMenu = partyMenu is { Kind: NativeFieldMenuKind.Normal, Phase: NativePartyMenuPhase.Active };
        if (!activeMenu && (partyMenu is not null || !CanAcceptFieldMovement || CurrentDialogue is not null || !Scene.MenuEnabled))
        {
            return false;
        }
        NativeCharacterVitals source = services.CharacterVitals ?? throw new NotSupportedException("Menu input requires authentic saved characters.");
        NativeCharacterVitals next = NativeJunctionMenuInput.Apply(source, action, ResolveNativeAutoJunction);
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
