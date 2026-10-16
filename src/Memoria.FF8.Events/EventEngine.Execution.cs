using Memoria.FF8.Runtime;
using System.Text.Json;

namespace Memoria.FF8.Events;

public sealed partial class EventEngine
{
    public event Action? OperationCompleted;
    public void Restore(JsonElement snapshot)
    {
        RestoreCore(snapshot);
    }

    public void LoadField(int fieldId, byte[] jsm, IReadOnlyList<string> fieldMessages, FieldTransition? arrival = null, int? previousFieldId = null, NativeFieldRuntimeProfile? nativeRuntimeProfile = null, JsonElement? worldEntityAllocationContext = null)
    {
        LoadFieldCore(fieldId, jsm, fieldMessages, arrival, previousFieldId, nativeRuntimeProfile, worldEntityAllocationContext);
        OperationCompleted?.Invoke();
    }

    public void Step()
    {
        StepCore();
        OperationCompleted?.Invoke();
    }

    public void Update(TimeSpan elapsed)
    {
        UpdateCore(elapsed);
        OperationCompleted?.Invoke();
    }

    public void ConfirmDialogue(int? choice = null, string? name = null)
    {
        ConfirmDialogueCore(choice, name);
        OperationCompleted?.Invoke();
    }

    public PlayerActionResult Apply(PlayerAction action)
    {
        ArgumentNullException.ThrowIfNull(action);
        if (action.Kind == PlayerActionKind.ReadMagazinePage && (action.Value is < 1 or > 198 || action.Quantity is null or < 0 or >= 69 || action.Frames != 1 || action.Horizontal != 0 || action.Vertical != 0 || action.Text is not null))
        {
            throw new ArgumentException("Magazine input requires an owned item and an original absolute page0..68.", nameof(action));
        }
        if (action.Kind == PlayerActionKind.RefineItems)
        {
            NativeRefinementInput.Validate(action);
        }
        if (action.Kind == PlayerActionKind.UseFieldItem && (action.Value is <= 0 or > 255 || action.Quantity is null or < 0 or >= 8 and < 16 or > 31 || action.Text is not null && (!int.TryParse(action.Text, out int forgotten) || forgotten is < 0 or > 127) || action.Frames != 1 || action.Horizontal != 0 || action.Vertical != 0))
        {
            throw new ArgumentException("Field item use requires an item BYTE and a physical character row0..7.", nameof(action));
        }
        if (NativeJunctionMenuInput.IsMenuAction(action.Kind))
        {
            NativeJunctionMenuInput.Validate(action);
        }
        if (action.CameraHorizontal != 0)
        {
            throw new NotSupportedException("Manual camera input belongs to the ordinary world motor; field cameras require their executed script contract.");
        }
        if (action.Kind is PlayerActionKind.BuyShopItem or PlayerActionKind.SellShopItem && (action.Value is < 0 or > byte.MaxValue || action.Quantity is null or < 1 or > byte.MaxValue))
        {
            throw new ArgumentException("Shop transaction requires a BYTE item and an explicit positive BYTE quantity.", nameof(action));
        }
        if (action.Kind == PlayerActionKind.SelectPartySlot && (uint)action.Value >= 3 || action.Kind == PlayerActionKind.SelectPartyMember && (uint)action.Value >= 8 && action.Value != 255)
        {
            throw new ArgumentException("Party menu input requires an explicit slot0..2 or native member0..7/emptyFF.", nameof(action));
        }
        if (action.Kind == PlayerActionKind.SelectWeaponShopCharacter && (uint)action.Value >= 6 || action.Kind == PlayerActionKind.RemodelWeapon && (uint)action.Value >= 28)
        {
            throw new ArgumentException("Weapon shop input requires a physical character0..5 or ordinary recipe0..27.", nameof(action));
        }
        PlayerActionResult result = ApplyCore(action);
        OperationCompleted?.Invoke();
        return result;
    }
}
