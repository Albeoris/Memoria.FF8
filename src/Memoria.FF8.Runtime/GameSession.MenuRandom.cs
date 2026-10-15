using System.Text.Json;

namespace Memoria.FF8.Runtime;

public sealed partial class GameSession
{
    public NativeMenuRandomState? MenuRandom => State.MenuRandom;
    public bool CanStoreMenuRandom => true;

    public void SetMenuRandom(NativeMenuRandomState random)
    {
        ArgumentNullException.ThrowIfNull(random);
        random.Validate();
        State = State with { MenuRandom = random };
    }

    private static void ValidateMenuRandomCheckpoint(GameState state, JsonElement? currentVm)
    {
        state.MenuRandom?.Validate();
        if (currentVm is not JsonElement vm)
        {
            return;
        }
        if (!vm.TryGetProperty("MenuRandom", out JsonElement captured))
        {
            throw new InvalidDataException("Saved VM lacks its native menu random process; migrate this save explicitly.");
        }
        NativeMenuRandomState? random = captured.ValueKind == JsonValueKind.Null ? null : captured.Deserialize<NativeMenuRandomState>(JsonOptions);
        random?.Validate();
        if (state.Scene != SceneKind.WorldMap && !JsonElement.DeepEquals(captured, JsonSerializer.SerializeToElement(state.MenuRandom, JsonOptions)))
        {
            throw new InvalidDataException("Root and VM native menu RNG owners disagree.");
        }
    }
}
