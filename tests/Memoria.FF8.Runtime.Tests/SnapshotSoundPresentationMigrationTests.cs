using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using System.Reflection;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Runtime.Tests;

public sealed class SnapshotSoundPresentationMigrationTests
{
    private static readonly JsonSerializerOptions Options = new() { Converters = { new JsonStringEnumConverter() } };

    [Fact]
    public void OptionalHistoricalEndingHasConsistentMigratedSoundOwners()
    {
        string? path = Environment.GetEnvironmentVariable("MEMORIA_FF8_TEST_ENDING_SNAPSHOT");
        Assert.SkipUnless(!string.IsNullOrWhiteSpace(path) && File.Exists(path), "Set MEMORIA_FF8_TEST_ENDING_SNAPSHOT to an authorized historical ending checkpoint.");
        JsonSerializerOptions compact = (JsonSerializerOptions)typeof(GameSession).GetField("CompactJsonOptions", BindingFlags.Static | BindingFlags.NonPublic)!.GetValue(null)!;
        using FileStream stream = File.OpenRead(path!);
        GameState state = SnapshotDefaults.Deserialize(stream, compact);
        JsonNode expected = JsonSerializer.SerializeToNode(state.GlobalSoundProcess, Options)!;
        JsonNode actual = JsonNode.Parse(state.EventState!.Value.GetProperty("GlobalSoundProcess").GetRawText())!;
        string[] names = expected.AsObject().Select(entry => entry.Key).Union(actual.AsObject().Select(entry => entry.Key)).ToArray();
        foreach (string name in names)
        {
            Assert.True(JsonNode.DeepEquals(expected[name], actual[name]), $"Sound member {name}: expected {expected[name]?.ToJsonString()}, actual {actual[name]?.ToJsonString()}");
        }
    }

    [Fact]
    public void MissingMusicPresentationUsesSavedSceneFactsAndRetainsOtherOwners()
    {
        JsonObject sound = JsonSerializer.SerializeToNode(NativeGlobalSoundProcess.FromBufferlessColdProcess() with { Rotation = 4 }, Options)!.AsObject();
        sound.Remove("MusicPlayback");
        JsonObject saved = new()
        {
            ["SnapshotSchemaVersion"] = 9,
            ["GlobalSoundProcess"] = sound.DeepClone(),
            ["EventState"] = new JsonObject { ["GlobalSoundProcess"] = sound.DeepClone(), ["Scene"] = new JsonObject { ["MusicPlaying"] = true, ["LoadedMusic"] = 95, ["MusicVolume"] = 87 }, ["Fibers"] = new JsonArray { new JsonObject { ["Position"] = 42 } } },
            ["CrtRandom"] = new JsonObject { ["Seed"] = 12345 },
            ["CharacterVitals"] = new JsonObject { ["Progress"] = 98765 }
        };
        string fibers = saved["EventState"]!["Fibers"]!.ToJsonString();
        string random = saved["CrtRandom"]!.ToJsonString();
        string characters = saved["CharacterVitals"]!.ToJsonString();
        DefaultJsonTypeInfoResolver resolver = new();
        resolver.Modifiers.Add(type =>
        {
            foreach (JsonPropertyInfo property in type.Properties)
            {
                if (property.AttributeProvider?.GetCustomAttributes(typeof(JsonIgnoreAttribute), true).OfType<JsonIgnoreAttribute>().Any(attribute => attribute.Condition == JsonIgnoreCondition.WhenWritingNull) == true)
                {
                    property.ShouldSerialize = (_, _) => true;
                }
            }
        });
        JsonSerializerOptions snapshotOptions = new(Options) { TypeInfoResolver = resolver };
        SnapshotMigrations.Apply(saved, snapshotOptions);
        NativeGlobalSoundProcess root = saved["GlobalSoundProcess"]!.Deserialize<NativeGlobalSoundProcess>(Options)!;
        Assert.Equal((byte?)4, root.Rotation);
        Assert.Equal(95, root.MusicPlayback!.CurrentMusicId0);
        Assert.Equal(87, root.MusicPlayback.Gain0);
        Assert.Equal(NativeMusicPlaybackMode.Midi, root.MusicPlayback.Mode0);
        Assert.Null(root.MusicPlayback.BattleFirstLoadB8B7D4);
        Assert.True(JsonNode.DeepEquals(JsonSerializer.SerializeToNode(root, Options), saved["EventState"]!["GlobalSoundProcess"]));
        Assert.True(saved["GlobalSoundProcess"]!["MusicPlayback"]!.AsObject().ContainsKey("BattleFirstLoadB8B7D4"));
        Assert.Null(saved["GlobalSoundProcess"]!["MusicPlayback"]!["BattleFirstLoadB8B7D4"]);
        Assert.Equal(fibers, saved["EventState"]!["Fibers"]!.ToJsonString());
        Assert.Equal(random, saved["CrtRandom"]!.ToJsonString());
        Assert.Equal(characters, saved["CharacterVitals"]!.ToJsonString());
    }

    [Fact]
    public void KnownMusicAndActualDisagreementAreNeverOverwritten()
    {
        JsonObject sound = JsonSerializer.SerializeToNode(NativeGlobalSoundProcess.FromBufferlessColdProcess(), Options)!.AsObject();
        JsonObject saved = new() { ["SnapshotSchemaVersion"] = 9, ["GlobalSoundProcess"] = sound.DeepClone(), ["EventState"] = new JsonObject { ["GlobalSoundProcess"] = sound.DeepClone() } };
        saved["EventState"]!["GlobalSoundProcess"]!["Rotation"] = 4;
        SnapshotMigrations.Apply(saved, Options);
        Assert.Equal(1, saved["GlobalSoundProcess"]!["Rotation"]!.GetValue<int>());
        Assert.Equal(4, saved["EventState"]!["GlobalSoundProcess"]!["Rotation"]!.GetValue<int>());
        Assert.True(saved["GlobalSoundProcess"]!["MusicPlayback"]!["BattleFirstLoadB8B7D4"]!.GetValue<bool>());
        Assert.Null(saved[nameof(GameState.Ff8Completion)]);
    }

    [Fact]
    public void CurrentWorldPresentationDoesNotUseFrozenDepartingFieldMusic()
    {
        JsonObject sound = JsonSerializer.SerializeToNode(NativeGlobalSoundProcess.FromBufferlessColdProcess(), Options)!.AsObject();
        sound.Remove("MusicPlayback");
        JsonObject saved = new()
        {
            ["SnapshotSchemaVersion"] = 9,
            ["Scene"] = "WorldMap",
            ["GlobalSoundProcess"] = sound.DeepClone(),
            ["EventState"] = new JsonObject { ["GlobalSoundProcess"] = sound.DeepClone(), ["Scene"] = new JsonObject { ["MusicPlaying"] = true, ["LoadedMusic"] = 95, ["MusicVolume"] = 87 } }
        };
        SnapshotMigrations.Apply(saved, Options);
        NativeGlobalSoundProcess root = saved["GlobalSoundProcess"]!.Deserialize<NativeGlobalSoundProcess>(Options)!;
        Assert.Null(root.MusicPlayback!.CurrentMusicId0);
        Assert.Equal(95, saved["EventState"]!["GlobalSoundProcess"]!["MusicPlayback"]!["CurrentMusicId0"]!.GetValue<int>());
    }
}
