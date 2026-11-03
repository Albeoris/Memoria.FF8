using System.Text.Json.Nodes;

namespace Memoria.FF8.Runtime;

/// <summary>Defers historical particle initialization until the saved field's original resources are available.</summary>
internal static class SnapshotFieldParticleMigration
{
    internal static void Apply(JsonObject saved)
    {
        foreach (string owner in new[] { nameof(GameState.EventState), nameof(GameState.DepartingFieldState) })
        {
            if (saved[owner] is not JsonObject checkpoint || checkpoint["Scene"] is not JsonObject scene || scene["PendingNativeFieldParticleMigrationVersion"] is not null || scene["NativeFieldParticles"] is not null || scene["NativeFieldParticleRandomPhase"] is not null || scene["NativeFieldParticleSprites"] is not null)
            {
                continue;
            }
            // Only an entirely absent historical owner may restart its visual
            // phase. Resource loading supplies the original pool and warmup.
            scene["PendingNativeFieldParticleMigrationVersion"] = 1;
        }
    }
}
