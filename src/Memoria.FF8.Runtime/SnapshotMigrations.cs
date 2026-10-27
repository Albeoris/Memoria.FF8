using System.Text.Json;
using System.Text.Json.Nodes;

namespace Memoria.FF8.Runtime;

/// <summary>Upgrades saved JSON before constructing the single current state model.</summary>
internal static class SnapshotMigrations
{
    internal static void Apply(JsonObject saved, JsonSerializerOptions options)
    {
        int version = 0;
        if (saved.TryGetPropertyValue(nameof(GameState.SnapshotSchemaVersion), out JsonNode? schema))
        {
            if (schema is not JsonValue value || !value.TryGetValue(out version) || version < 0)
            {
                throw new InvalidDataException("Invalid snapshot schema version.");
            }
        }
        if (version > GameState.CurrentSnapshotSchemaVersion)
        {
            throw new InvalidDataException($"Snapshot schema {version} is newer than supported schema {GameState.CurrentSnapshotSchemaVersion}. Update the application to load this save.");
        }
        while (version < GameState.CurrentSnapshotSchemaVersion)
        {
            switch (version)
            {
                case 0:
                    SnapshotModelPartMigration.Apply(saved);
                    break;
                case 1:
                    SnapshotFileStagingMigration.Apply(saved);
                    SnapshotWorldMigration.Apply(saved);
                    SnapshotBattleMetadataMigration.Apply(saved, options);
                    SnapshotInitialStateMigration.Apply(saved, options);
                    break;
                case 2:
                    SnapshotSigh46Migration.Apply(saved, options);
                    break;
                default:
                    throw new InvalidDataException($"No forward migration is available for snapshot schema {version}.");
            }
            saved[nameof(GameState.SnapshotSchemaVersion)] = ++version;
        }
    }
}
