using System.Text.Json;

namespace Memoria.FF8.Runtime;

public sealed partial class GameSession
{
    public NativeFieldFadeState? FieldFade => State.FieldFade;
    public bool CanStoreFieldFade => true;

    public void SetFieldFade(NativeFieldFadeState fade)
    {
        ArgumentNullException.ThrowIfNull(fade);
        fade.Validate();
        ValidateLiveEventMemoryOwners(State with { FieldFade = fade }, State.EventMemory);
        State = State with { FieldFade = fade };
    }

    private static void ValidateFieldFadeCheckpoint(GameState state, JsonElement? currentVm)
    {
        state.FieldFade?.Validate();
        if (currentVm is not JsonElement vm)
        {
            return;
        }
        if (!vm.TryGetProperty("FieldFade", out JsonElement captured))
        {
            throw new InvalidDataException("Saved VM lacks its native physical/saved field fade process; migrate this save explicitly.");
        }
        NativeFieldFadeState? fade = captured.ValueKind == JsonValueKind.Null ? null : captured.Deserialize<NativeFieldFadeState>(JsonOptions);
        fade?.Validate();
        if (state.Scene != SceneKind.WorldMap && !JsonElement.DeepEquals(captured, JsonSerializer.SerializeToElement(state.FieldFade, JsonOptions)))
        {
            throw new InvalidDataException("Root and VM native field fade owners disagree.");
        }
    }
}
