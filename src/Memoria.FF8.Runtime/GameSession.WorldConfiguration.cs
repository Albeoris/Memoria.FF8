using System.Text.Json;

namespace Memoria.FF8.Runtime;

public sealed partial class GameSession
{
    private static void ValidateAllocatedWorldEntityCheckpoint(GameState state, JsonElement? currentVm)
    {
        if (state.Scene == SceneKind.WorldMap || currentVm is not JsonElement vm || !vm.TryGetProperty("Scene", out JsonElement scene))
        {
            return;
        }
        byte? allocated = scene.TryGetProperty("PreviousWorldEntityByte86", out JsonElement entity) && entity.ValueKind != JsonValueKind.Null ? entity.GetByte() : null;
        if (allocated != state.WorldConfiguration?.EntityByte)
        {
            throw new InvalidDataException("Root process world configuration and allocated field BYTE86 disagree.");
        }
    }
}
