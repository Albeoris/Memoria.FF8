using System.Text.Json;
using System.Text.Json.Nodes;

namespace Memoria.FF8.Runtime.Tests;

public sealed class SnapshotFieldParticleMigrationTests
{
    [Theory]
    [InlineData("EventState")]
    [InlineData("DepartingFieldState")]
    public void Historical_absent_presentation_is_marked_without_changing_gameplay(string owner)
    {
        JsonObject saved = new()
        {
            ["SnapshotSchemaVersion"] = 6,
            [owner] = new JsonObject
            {
                ["FieldId"] = 200,
                ["Frame"] = 57,
                ["Actors"] = new JsonArray(),
                ["Scene"] = new JsonObject { ["NativeParticleSlots"] = new JsonObject { ["3"] = 2 } }
            }
        };
        JsonObject expected = (JsonObject)saved.DeepClone();
        expected["SnapshotSchemaVersion"] = GameState.CurrentSnapshotSchemaVersion;
        expected[owner]!["Scene"]!["PendingNativeFieldParticleMigrationVersion"] = 1;

        SnapshotMigrations.Apply(saved, new JsonSerializerOptions());

        Assert.True(JsonNode.DeepEquals(expected, saved));
        SnapshotMigrations.Apply(saved, new JsonSerializerOptions());
        Assert.True(JsonNode.DeepEquals(expected, saved));
    }

    [Theory]
    [InlineData("NativeFieldParticles", "{}")]
    [InlineData("NativeFieldParticleRandomPhase", "0")]
    [InlineData("NativeFieldParticleSprites", "[]")]
    [InlineData("PendingNativeFieldParticleMigrationVersion", "17")]
    public void Recorded_or_partial_owners_are_not_marked_or_reinterpreted(string name, string value)
    {
        JsonObject saved = new() { ["EventState"] = new JsonObject { ["Scene"] = new JsonObject { [name] = JsonNode.Parse(value) } } };
        JsonObject expected = (JsonObject)saved.DeepClone();

        SnapshotFieldParticleMigration.Apply(saved);

        Assert.True(JsonNode.DeepEquals(expected, saved));
    }

    [Fact]
    public void Current_schema_does_not_restart_an_absent_pool()
    {
        JsonObject saved = new() { ["SnapshotSchemaVersion"] = GameState.CurrentSnapshotSchemaVersion, ["EventState"] = new JsonObject { ["Scene"] = new JsonObject() } };
        JsonObject expected = (JsonObject)saved.DeepClone();
        SnapshotMigrations.Apply(saved, new JsonSerializerOptions());
        SnapshotMigrations.CompleteInitialOwners(saved, new JsonSerializerOptions(), GameState.CurrentSnapshotSchemaVersion);
        Assert.True(JsonNode.DeepEquals(expected, saved));
    }
}
