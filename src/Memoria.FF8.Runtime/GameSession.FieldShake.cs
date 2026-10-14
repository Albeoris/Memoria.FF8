using System.Text.Json;

namespace Memoria.FF8.Runtime;

public sealed partial class GameSession
{
    public NativeFieldShake? FieldShake => State.FieldShake;
    public bool CanStoreFieldShake => true;

    public void SetFieldShake(NativeFieldShake state)
    {
        ArgumentNullException.ThrowIfNull(state);
        state.Validate();
        State = State with { FieldShake = state };
    }

    private static void ValidateFieldShakeCheckpoint(GameState state, JsonElement? currentVm)
    {
        state.FieldShake?.Validate();
        if (currentVm is not JsonElement vm)
        {
            return;
        }
        if (!vm.TryGetProperty("FieldShake", out JsonElement value))
        {
            throw new InvalidDataException("Saved VM lacks its current required field shake process owner; recreate authentic inputs.");
        }
        NativeFieldShake? captured = value.ValueKind == JsonValueKind.Null ? null : value.Deserialize<NativeFieldShake>(JsonOptions);
        captured?.Validate();
        if (captured != state.FieldShake)
        {
            // World callbacks do not execute4765C0; the departing field owner must remain exact.
            throw new InvalidDataException("Root and VM field shake process owners disagree.");
        }
    }
}
