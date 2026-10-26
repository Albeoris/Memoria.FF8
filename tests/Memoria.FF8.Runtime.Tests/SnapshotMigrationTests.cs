using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime.Tests;

public sealed class SnapshotMigrationTests
{
    private static readonly JsonSerializerOptions Options = new() { Converters = { new JsonStringEnumConverter(), new ImmutableBytesJsonConverter() } };

    [Fact]
    public void Unversioned_saves_upgrade_both_field_owners_without_changing_other_data()
    {
        JsonObject saved = LegacySnapshot();
        saved["DepartingFieldState"] = saved["EventState"]!.DeepClone();
        string original = saved.ToJsonString();

        GameState restored = Deserialize(saved);

        Assert.Equal(GameState.CurrentSnapshotSchemaVersion, restored.SnapshotSchemaVersion);
        foreach (JsonElement vm in new[] { restored.EventState!.Value, restored.DepartingFieldState!.Value })
        {
            JsonElement bank = vm.GetProperty("Scene").GetProperty("ModelParts");
            Assert.Empty(bank.GetProperty("Resources").GetProperty("2").GetProperty("Translations").EnumerateObject());
            JsonElement draw = bank.GetProperty("Draws").GetProperty("4");
            Assert.Equal(17, draw.GetProperty("Frame").GetInt64());
            Assert.Equal(2, draw.GetProperty("ModelId").GetInt32());
            Assert.Empty(draw.GetProperty("SampleState").GetProperty("Translations").EnumerateObject());
            Assert.False(draw.TryGetProperty("SkinTranslations", out _));
            Assert.Equal("retained", vm.GetProperty("Unrelated").GetString());
        }
        Assert.Equal(original, saved.ToJsonString());
    }

    [Theory]
    [InlineData(1)]
    [InlineData(4)]
    public void Legacy_dynamic_words_are_preserved_without_advancing_a_callback(short mode)
    {
        JsonObject saved = LegacySnapshot();
        JsonObject bank = Bank(saved);
        bank["Draws"] = new JsonObject();
        short[] words = [0, 1, 7, 0, -101, 102, -103, mode, 0, 0, 0, 0, 201, -202, 203, 15];
        bank["Resources"]!["2"]!["Parts"] = JsonSerializer.SerializeToNode(new[] { words });

        GameState restored = Deserialize(saved);
        JsonElement json = restored.EventState!.Value.GetProperty("Scene").GetProperty("ModelParts").GetProperty("Resources").GetProperty("2");
        NativeModelPartResourceState state = json.Deserialize<NativeModelPartResourceState>()!;

        Assert.Equal(new NativeModelPartTranslation(7, -101, 102, -103, 201, -202, 203, 15, mode), state.Translations[0]);
        Assert.Equal(new[] { "Translations" }, json.EnumerateObject().Select(property => property.Name));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Plain_and_compact_containers_upgrade_and_current_payloads_are_idempotent(bool compact)
    {
        JsonObject saved = LegacySnapshot();
        saved["SnapshotSchemaVersion"] = 0;
        GameState legacy = saved.Deserialize<GameState>(Options)!;
        string path = Path.Combine(Path.GetTempPath(), "Memoria.FF8-migration-" + Guid.NewGuid().ToString("N"));
        try
        {
            using (FileStream output = File.Create(path))
            {
                if (compact)
                {
                    SnapshotStorage.Write(output, legacy, Options);
                }
                else
                {
                    JsonSerializer.Serialize(output, legacy, Options);
                }
            }
            byte[] original = File.ReadAllBytes(path);
            GameState upgraded;
            using (FileStream input = File.OpenRead(path))
            {
                upgraded = SnapshotStorage.Read(input, Options);
            }
            Assert.Equal(GameState.CurrentSnapshotSchemaVersion, upgraded.SnapshotSchemaVersion);
            string current = JsonSerializer.Serialize(upgraded, Options);
            Assert.Equal(current, JsonSerializer.Serialize(Deserialize(JsonNode.Parse(current)!.AsObject()), Options));
            Assert.Equal(original, File.ReadAllBytes(path));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(2)]
    public void Invalid_or_future_schema_versions_fail_without_downgrading(int version)
    {
        JsonObject saved = LegacySnapshot();
        saved["SnapshotSchemaVersion"] = version;
        Assert.Throws<InvalidDataException>(() => Deserialize(saved));
    }

    [Theory]
    [InlineData("null")]
    [InlineData("\"1\"")]
    [InlineData("true")]
    public void Noninteger_schema_versions_are_rejected(string schema)
    {
        JsonObject saved = LegacySnapshot();
        saved["SnapshotSchemaVersion"] = JsonNode.Parse(schema);
        Assert.Throws<InvalidDataException>(() => Deserialize(saved));
    }

    [Fact]
    public void Current_schema_does_not_reinterpret_legacy_rows()
    {
        JsonObject saved = LegacySnapshot();
        saved["SnapshotSchemaVersion"] = GameState.CurrentSnapshotSchemaVersion;
        JsonObject original = (JsonObject)saved.DeepClone();
        SnapshotMigrations.Apply(saved);
        Assert.True(JsonNode.DeepEquals(original, saved));
    }

    [Fact]
    public void Incomplete_current_translation_is_not_filled_by_migration()
    {
        JsonObject saved = LegacySnapshot();
        Bank(saved)["Resources"]!["2"] = new JsonObject { ["Translations"] = new JsonObject { ["0"] = new JsonObject { ["Mode"] = 1 } } };
        GameState restored = Deserialize(saved);
        JsonElement resource = restored.EventState!.Value.GetProperty("Scene").GetProperty("ModelParts").GetProperty("Resources").GetProperty("2");
        Assert.Throws<JsonException>(() => resource.Deserialize<NativeModelPartResourceState>());
    }

    [Fact]
    public void Nonempty_legacy_draws_fail_with_a_migration_error_instead_of_resetting_samples()
    {
        JsonObject saved = LegacySnapshot();
        Bank(saved)["Draws"]!["4"]!["SkinTranslations"] = new JsonObject { ["0"] = new JsonObject { ["X"] = 1, ["Y"] = 2, ["Z"] = 3 } };
        Assert.Throws<InvalidDataException>(() => Deserialize(saved));
    }

    [Theory]
    [InlineData("SourceHash")]
    [InlineData("SourceOffset")]
    [InlineData("Parts")]
    [InlineData("SkinFlags")]
    public void Incomplete_legacy_resources_are_rejected(string missing)
    {
        JsonObject saved = LegacySnapshot();
        Bank(saved)["Resources"]!["2"]!.AsObject().Remove(missing);
        Assert.Throws<InvalidDataException>(() => Deserialize(saved));
    }

    [Theory]
    [InlineData(0, -1)]
    [InlineData(1, 2)]
    [InlineData(2, 32768)]
    [InlineData(7, 2)]
    public void Invalid_legacy_words_are_not_discarded_as_inactive_state(int index, int value)
    {
        JsonObject saved = LegacySnapshot();
        Bank(saved)["Resources"]!["2"]!["Parts"]![0]![index] = value;
        Assert.Throws<InvalidDataException>(() => Deserialize(saved));
    }

    [Fact]
    public void Failed_departing_field_migration_leaves_both_owners_and_schema_unchanged()
    {
        JsonObject saved = LegacySnapshot();
        saved["DepartingFieldState"] = saved["EventState"]!.DeepClone();
        saved["DepartingFieldState"]!["Scene"]!["ModelParts"]!["Resources"]!["2"]!.AsObject().Remove("Parts");
        string original = saved.ToJsonString();
        Assert.Throws<InvalidDataException>(() => SnapshotMigrations.Apply(saved));
        Assert.Equal(original, saved.ToJsonString());
    }

    [Fact]
    public void Runtime_state_cannot_be_saved_as_an_older_or_future_schema()
    {
        GameState current = GameSession.CreateColdProcess().State;
        Assert.Throws<InvalidDataException>(() => new GameSession(current with { SnapshotSchemaVersion = 0 }));
        Assert.Throws<InvalidDataException>(() => new GameSession(current with { SnapshotSchemaVersion = GameState.CurrentSnapshotSchemaVersion + 1 }));
    }

    private static JsonObject LegacySnapshot()
    {
        JsonObject saved = JsonSerializer.SerializeToNode(GameSession.CreateColdProcess().State, Options)!.AsObject();
        saved.Remove("SnapshotSchemaVersion");
        saved["EventState"] = new JsonObject
        {
            ["Unrelated"] = "retained",
            ["Scene"] = new JsonObject
            {
                ["ModelParts"] = new JsonObject
                {
                    ["Resources"] = new JsonObject
                    {
                        ["2"] = new JsonObject
                        {
                            ["SourceHash"] = new string('A', 64),
                            ["SourceOffset"] = 100,
                            ["Parts"] = JsonSerializer.SerializeToNode(new short[][] { [0, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0] }),
                            ["SkinFlags"] = new JsonArray(0)
                        }
                    },
                    ["Instances"] = new JsonArray(new JsonObject { ["ObjectId"] = 4, ["ModelId"] = 2 }),
                    ["Draws"] = new JsonObject { ["4"] = new JsonObject { ["Frame"] = 17L, ["ModelId"] = 2, ["SkinTranslations"] = new JsonObject() } }
                }
            }
        };
        return saved;
    }

    private static JsonObject Bank(JsonObject saved) => saved["EventState"]!["Scene"]!["ModelParts"]!.AsObject();

    private static GameState Deserialize(JsonObject saved)
    {
        using MemoryStream stream = new(Encoding.UTF8.GetBytes(saved.ToJsonString(Options)));
        return SnapshotDefaults.Deserialize(stream, Options);
    }
}
