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
        if (NativeJunctionMenuInput.IsMenuAction(action.Kind))
        {
            NativeJunctionMenuInput.Validate(action);
        }
        if (action.CameraHorizontal != 0)
        {
            throw new NotSupportedException("Manual camera input belongs to the ordinary world motor; field cameras require their executed script contract.");
        }
        if (action.Kind == PlayerActionKind.BuyShopItem && (action.Value is < 0 or > byte.MaxValue || action.Quantity is null or < 1 or > byte.MaxValue))
        {
            throw new ArgumentException("Shop purchase requires a BYTE item and an explicit positive BYTE quantity.", nameof(action));
        }
        if (action.Kind == PlayerActionKind.SelectPartySlot && (uint)action.Value >= 3 || action.Kind == PlayerActionKind.SelectPartyMember && (uint)action.Value >= 8 && action.Value != 255)
        {
            throw new ArgumentException("Party menu input requires an explicit slot0..2 or native member0..7/emptyFF.", nameof(action));
        }
        PlayerActionResult result = ApplyCore(action);
        OperationCompleted?.Invoke();
        return result;
    }
}
