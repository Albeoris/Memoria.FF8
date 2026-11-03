using System.Text.Json;
using System.Text.Json.Nodes;

namespace Memoria.FF8.Runtime;

/// <summary>Initializes the visual animation phase absent from historical world process snapshots.</summary>
internal static class SnapshotWorldTextureAnimationMigration
{
    internal static void Apply(JsonObject saved, JsonSerializerOptions options)
    {
        if (saved[nameof(GameState.WorldProcess)] is not JsonObject process || process[nameof(NativeWorldProcess.TextureAnimation)] is not null)
        {
            return;
        }
        // Historical saves did not record either independent clock. Reset only
        // the visual phase; a recorded owner, including partial data, is preserved.
        process[nameof(NativeWorldProcess.TextureAnimation)] = JsonSerializer.SerializeToNode(NativeWorldTextureAnimationState.FromColdProcess(), options);
    }
}
