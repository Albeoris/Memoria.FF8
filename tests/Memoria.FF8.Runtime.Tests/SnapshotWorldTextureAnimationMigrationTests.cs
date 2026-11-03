using System.Text.Json;
using System.Text.Json.Nodes;

namespace Memoria.FF8.Runtime.Tests;

public sealed class SnapshotWorldTextureAnimationMigrationTests
{
    private static readonly JsonSerializerOptions Options = new();

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Historical_missing_or_null_clocks_reset_only_visual_phase(bool explicitNull)
    {
        JsonObject saved = Snapshot(5);
        JsonObject process = saved["WorldProcess"]!.AsObject();
        process.Remove("TextureAnimation");
        if (explicitNull)
        {
            process["TextureAnimation"] = null;
        }
        JsonObject expected = (JsonObject)saved.DeepClone();
        expected["SnapshotSchemaVersion"] = GameState.CurrentSnapshotSchemaVersion;
        expected["WorldProcess"]!["TextureAnimation"] = JsonSerializer.SerializeToNode(NativeWorldTextureAnimationState.FromColdProcess());

        SnapshotMigrations.Apply(saved, Options);

        Assert.True(JsonNode.DeepEquals(expected, saved));
        JsonObject once = (JsonObject)saved.DeepClone();
        SnapshotMigrations.Apply(saved, Options);
        Assert.True(JsonNode.DeepEquals(once, saved));
    }

    [Theory]
    [InlineData("{\"TextureClock\":383,\"PaletteClock\":27}")]
    [InlineData("{\"TextureClock\":3}")]
    [InlineData("{\"TextureClock\":384,\"PaletteClock\":0}")]
    [InlineData("17")]
    public void Recorded_owners_are_preserved_verbatim(string owner)
    {
        JsonObject saved = Snapshot(5);
        saved["WorldProcess"]!["TextureAnimation"] = JsonNode.Parse(owner);

        SnapshotMigrations.Apply(saved, Options);

        Assert.True(JsonNode.DeepEquals(JsonNode.Parse(owner), saved["WorldProcess"]!["TextureAnimation"]));
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("{\"TextureClock\":3}")]
    [InlineData("17")]
    public void Partial_and_wrong_type_historical_clocks_remain_invalid(string owner)
    {
        JsonObject saved = Snapshot(5);
        saved["WorldProcess"]!["TextureAnimation"] = JsonNode.Parse(owner);
        SnapshotMigrations.Apply(saved, Options);
        Assert.Throws<JsonException>(() => saved["WorldProcess"]!.Deserialize<NativeWorldProcess>());
    }

    [Fact]
    public void Historical_recorded_out_of_range_clocks_are_not_reset()
    {
        JsonObject saved = Snapshot(5);
        saved["WorldProcess"]!["TextureAnimation"] = JsonSerializer.SerializeToNode(new NativeWorldTextureAnimationState(384, 0));
        SnapshotMigrations.Apply(saved, Options);
        Assert.Throws<InvalidDataException>(() => saved["WorldProcess"]!.Deserialize<NativeWorldProcess>()!.Validate());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Current_schema_does_not_reset_missing_or_null_clocks(bool explicitNull)
    {
        JsonObject saved = Snapshot(GameState.CurrentSnapshotSchemaVersion);
        saved["WorldProcess"]!.AsObject().Remove("TextureAnimation");
        if (explicitNull)
        {
            saved["WorldProcess"]!["TextureAnimation"] = null;
        }
        JsonObject expected = (JsonObject)saved.DeepClone();

        SnapshotMigrations.Apply(saved, Options);
        SnapshotMigrations.CompleteInitialOwners(saved, Options, GameState.CurrentSnapshotSchemaVersion);

        Assert.True(JsonNode.DeepEquals(expected, saved));
    }

    [Fact]
    public void Missing_parent_is_not_created_and_later_historical_completion_is_migrated()
    {
        JsonObject saved = new() { ["SnapshotSchemaVersion"] = 5 };
        SnapshotMigrations.Apply(saved, Options);
        Assert.False(saved.ContainsKey("WorldProcess"));
        saved["WorldProcess"] = Snapshot(5)["WorldProcess"]!.DeepClone();
        saved["WorldProcess"]!.AsObject().Remove("TextureAnimation");

        SnapshotMigrations.CompleteInitialOwners(saved, Options, 5);

        Assert.Equal(NativeWorldTextureAnimationState.FromColdProcess(), saved["WorldProcess"]!["TextureAnimation"]!.Deserialize<NativeWorldTextureAnimationState>());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Plain_and_compact_files_upgrade_without_modifying_the_source(bool compact)
    {
        GameState historical = GameSession.CreateColdProcess().State with
        {
            SnapshotSchemaVersion = 5,
            WorldProcess = NativeWorldProcess.FromColdProcess().AdvanceMainLoopCallbacks(517) with { TextureAnimation = null }
        };
        string path = Path.Combine(Path.GetTempPath(), "Memoria.FF8-world-animation-migration-" + Guid.NewGuid().ToString("N"));
        try
        {
            using (FileStream output = File.Create(path))
            {
                if (compact)
                {
                    SnapshotStorage.Write(output, historical, Options);
                }
                else
                {
                    JsonSerializer.Serialize(output, historical, Options);
                }
            }
            byte[] source = File.ReadAllBytes(path);
            GameState migrated = GameSession.Load(path);
            Assert.Equal(GameState.CurrentSnapshotSchemaVersion, migrated.SnapshotSchemaVersion);
            Assert.True(JsonNode.DeepEquals(JsonSerializer.SerializeToNode(historical.WorldProcess with { TextureAnimation = NativeWorldTextureAnimationState.FromColdProcess() }), JsonSerializer.SerializeToNode(migrated.WorldProcess)));
            Assert.Equal(source, File.ReadAllBytes(path));
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static JsonObject Snapshot(int schema)
    {
        return new JsonObject
        {
            ["SnapshotSchemaVersion"] = schema,
            ["WorldProcess"] = JsonSerializer.SerializeToNode(NativeWorldProcess.FromColdProcess().AdvanceMainLoopCallbacks(517)),
            ["EventState"] = new JsonObject { ["FieldId"] = 200, ["Frame"] = 57 }
        };
    }
}
