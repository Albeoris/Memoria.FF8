using System.Text.Json;

namespace Memoria.FF8.Runtime;

public sealed partial class GameSession
{
    public NativeFieldBarsState? FieldBars => State.FieldBars;
    public bool CanStoreFieldBars => true;

    public void SetFieldBars(NativeFieldBarsState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        state.Validate();
        State = State with { FieldBars = state };
    }

    private static void ValidateFieldBarsCheckpoint(GameState state, JsonElement? currentVm)
    {
        state.FieldBars?.Validate();
        if (currentVm is not JsonElement vm)
        {
            return;
        }
        if (!vm.TryGetProperty("FieldBars", out JsonElement value))
        {
            throw new InvalidDataException("Saved VM lacks its explicit global field bar owner; migrate this save explicitly.");
        }
        NativeFieldBarsState? captured = value.ValueKind == JsonValueKind.Null ? null : value.Deserialize<NativeFieldBarsState>(JsonOptions);
        captured?.Validate();
        if (captured != state.FieldBars)
        {
            throw new InvalidDataException("Root and VM global field bar owners disagree.");
        }
    }
}
