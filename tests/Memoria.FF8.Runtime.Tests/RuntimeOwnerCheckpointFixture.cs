using System.Text.Json;
using System.Text.Json.Nodes;

namespace Memoria.FF8.Runtime.Tests;

/// <summary>Owner-only current Runtime checkpoint fixtures, without executable field VMs.</summary>
internal static class RuntimeOwnerCheckpointFixture
{
    public static JsonElement Capture(GameState state)
    {
        JsonObject owners = JsonSerializer.SerializeToNode(NativeBattleStorageSnapshots.Capture(state))!.AsObject();
        owners.Remove(nameof(GameState.Scene));
        owners.Remove(nameof(GameState.EventState));
        owners.Remove(nameof(GameState.DepartingFieldState));
        return JsonSerializer.SerializeToElement(owners);
    }
}
