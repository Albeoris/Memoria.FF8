using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime.Tests;

public sealed class SnapshotEffect30MigrationTests
{
    private static readonly JsonSerializerOptions Options = new() { Converters = { new JsonStringEnumConverter(), new ImmutableBytesJsonConverter() } };
    private static readonly JsonSerializerOptions ArrayOptions = new() { Converters = { new JsonStringEnumConverter() } };

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    public void Historical_missing_null_and_exact_unknown_owners_migrate_independently(int schema)
    {
        JsonObject saved = Snapshot(schema);
        JsonObject[] copies = EffectCopies(saved);
        copies[1]["Effect30"] = null;
        copies[2]["Effect30"] = State(NativeBattleEffect30State.FromUnknown(), false);
        copies[3]["Effect30"] = State(NativeBattleEffect30State.FromUnknown(), true);
        copies[4]["Effect30"] = null;
        saved["Battle"]!["Frame"] = 268L;
        saved["Battle"]!["NativeGraphics"]!["Callback"] = 349L;
        for (int index = 0; index < copies.Length; index++)
        {
            copies[index]["Parameter80"] = index + 11;
        }

        SnapshotMigrations.Apply(saved, Options);

        Assert.Equal(5, saved["SnapshotSchemaVersion"]!.GetValue<int>());
        Assert.Equal(268L, saved["Battle"]!["Frame"]!.GetValue<long>());
        Assert.Equal(349L, saved["Battle"]!["NativeGraphics"]!["Callback"]!.GetValue<long>());
        JsonObject expected = State(NativeBattleEffect30State.FromColdProcess(), false);
        for (int index = 0; index < copies.Length; index++)
        {
            JsonObject copy = EffectCopies(saved)[index];
            NativeBattleEffect30State actual = copy["Effect30"]!.Deserialize<NativeBattleEffect30State>(Options)!;
            Assert.True(JsonNode.DeepEquals(expected, State(actual, false)));
            Assert.Equal(index + 11, copy["Parameter80"]!.GetValue<int>());
        }
        JsonObject once = (JsonObject)saved.DeepClone();
        SnapshotMigrations.Apply(saved, Options);
        Assert.True(JsonNode.DeepEquals(once, saved));
    }

    [Fact]
    public void Warm_partial_changed_and_storage_referenced_owners_are_preserved_verbatim()
    {
        JsonObject saved = Snapshot(4);
        JsonObject[] copies = EffectCopies(saved);
        copies[0]["Effect30"] = State(NativeBattleEffect30State.FromColdProcess().WriteDword(NativeBattleEffect30State.BaseAddress, 0x5A), true);
        JsonObject partial = State(NativeBattleEffect30State.FromUnknown(), false);
        partial.Remove("ColdSourcePending");
        copies[1]["Effect30"] = partial;
        JsonObject changed = State(NativeBattleEffect30State.FromUnknown(), false);
        changed["Globals"]!["UnknownRegions"] = new JsonArray();
        copies[2]["Effect30"] = changed;
        JsonObject referenced = State(NativeBattleEffect30State.FromUnknown(), false);
        referenced["Globals"]!["StorageReference"] = JsonSerializer.SerializeToNode(new NativeBattleStorageReference(7, 0, NativeBattleEffectArena.Length), Options);
        copies[3]["Effect30"] = referenced;
        JsonObject extra = State(NativeBattleEffect30State.FromUnknown(), false);
        extra["UnrecognizedHistory"] = 17;
        copies[4]["Effect30"] = extra;
        JsonNode?[] before = copies.Select(copy => copy["Effect30"]!.DeepClone()).ToArray();

        SnapshotMigrations.Apply(saved, Options);

        for (int index = 0; index < copies.Length; index++)
        {
            Assert.True(JsonNode.DeepEquals(before[index], EffectCopies(saved)[index]["Effect30"]));
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Initialized_or_incomplete_selected_and_detached_tasks_block_all_copies(bool binding)
    {
        for (int location = -1; location < 5; location++)
        {
            for (int incomplete = 0; incomplete < 4; incomplete++)
            {
                JsonObject saved = Snapshot(4);
                JsonObject[] copies = EffectCopies(saved);
                copies[1]["Effect30"] = null;
                copies[2]["Effect30"] = State(NativeBattleEffect30State.FromUnknown(), true);
                JsonObject selected = Selected(binding);
                switch (incomplete)
                {
                    case 0:
                        selected["ReturnedDescriptor"] = 1;
                        break;
                    case 1:
                        selected["Pools"] = new JsonArray(new JsonObject());
                        break;
                    case 2:
                        selected.Remove("ReturnedDescriptor");
                        break;
                    case 3:
                        selected.Remove("Pools");
                        break;
                }
                if (location == -1)
                {
                    saved["Battle"]!["NativeGraphics"]!["OriginalEffect"] = selected;
                }
                else
                {
                    copies[location]["DetachedReturnedTask"] = selected;
                }
                JsonNode?[] before = copies.Select(copy => copy["Effect30"]?.DeepClone()).ToArray();
                bool[] present = copies.Select(copy => copy.ContainsKey("Effect30")).ToArray();

                SnapshotMigrations.Apply(saved, Options);

                for (int index = 0; index < copies.Length; index++)
                {
                    JsonObject actual = EffectCopies(saved)[index];
                    Assert.Equal(present[index], actual.ContainsKey("Effect30"));
                    Assert.True(JsonNode.DeepEquals(before[index], actual["Effect30"]));
                }
            }
        }
    }

    [Fact]
    public void Uninitialized_selected_effect_allows_migration()
    {
        JsonObject saved = Snapshot(4);
        saved["Battle"]!["NativeGraphics"]!["OriginalEffect"] = Selected(true);

        SnapshotMigrations.Apply(saved, Options);

        Assert.All(EffectCopies(saved), copy => Assert.NotNull(copy["Effect30"]));
    }

    [Fact]
    public void Current_schema_preserves_missing_null_and_unknown_owners()
    {
        JsonObject saved = Snapshot(5);
        EffectCopies(saved)[1]["Effect30"] = null;
        EffectCopies(saved)[2]["Effect30"] = State(NativeBattleEffect30State.FromUnknown(), true);
        JsonObject before = (JsonObject)saved.DeepClone();

        SnapshotMigrations.Apply(saved, Options);

        Assert.True(JsonNode.DeepEquals(before, saved));
    }

    [Theory]
    [InlineData(4, false)]
    [InlineData(4, true)]
    [InlineData(5, false)]
    [InlineData(5, true)]
    public void Default_parent_completion_respects_schema_and_detached_guard(int schema, bool detached)
    {
        JsonObject saved = JsonSerializer.SerializeToNode(GameSession.CreateColdProcess().State, Options)!.AsObject();
        saved["SnapshotSchemaVersion"] = schema;
        saved["Battle"] = null;
        saved["EventState"] = Event();
        if (detached)
        {
            JsonObject selected = Selected(true);
            selected["ReturnedDescriptor"] = 1;
            saved["EventState"]!["Scene"]!["NativeEyeProcess"]!["Memory"]!["EffectMemory"]!["DetachedReturnedTask"] = selected;
        }
        saved.Remove("NativeBattleMemory");
        using MemoryStream input = new(Encoding.UTF8.GetBytes(saved.ToJsonString(Options)));

        GameState restored = SnapshotDefaults.Deserialize(input, Options);

        NativeBattleEffect30State? actual = restored.NativeBattleMemory!.EffectMemory!.Effect30;
        if (schema < 5 && !detached)
        {
            Assert.NotNull(actual);
            Assert.True(JsonNode.DeepEquals(State(NativeBattleEffect30State.FromColdProcess(), false), State(actual, false)));
        }
        else
        {
            Assert.Null(actual);
        }
    }

    private static JsonObject State(NativeBattleEffect30State state, bool compact) => JsonSerializer.SerializeToNode(NativeBattleStorageSnapshots.Capture(state), compact ? Options : ArrayOptions)!.AsObject();

    private static JsonObject Selected(bool binding)
    {
        JsonObject selected = new() { ["Pools"] = new JsonArray(), ["ReturnedDescriptor"] = 0 };
        if (binding)
        {
            selected["Binding"] = new JsonObject { ["Entry"] = NativeBattleEffect30Profile.Entry };
        }
        else
        {
            selected["RequestedEffect"] = 30;
        }
        return selected;
    }

    private static JsonObject Snapshot(int schema) => new()
    {
        ["SnapshotSchemaVersion"] = schema,
        ["NativeBattleMemory"] = new JsonObject { ["EffectMemory"] = new JsonObject() },
        ["EventState"] = Event(),
        ["DepartingFieldState"] = Event(),
        ["Battle"] = new JsonObject
        {
            ["NativeEffectMemory"] = new JsonObject(),
            ["NativeGraphics"] = new JsonObject(),
            ["Definition"] = new JsonObject { ["NativeRetainedMemory"] = new JsonObject { ["EffectMemory"] = new JsonObject() } }
        }
    };

    private static JsonObject Event() => new() { ["Scene"] = new JsonObject { ["NativeEyeProcess"] = new JsonObject { ["Memory"] = new JsonObject { ["EffectMemory"] = new JsonObject() } } } };

    private static JsonObject[] EffectCopies(JsonObject saved) =>
    [
        saved["NativeBattleMemory"]!["EffectMemory"]!.AsObject(),
        saved["EventState"]!["Scene"]!["NativeEyeProcess"]!["Memory"]!["EffectMemory"]!.AsObject(),
        saved["DepartingFieldState"]!["Scene"]!["NativeEyeProcess"]!["Memory"]!["EffectMemory"]!.AsObject(),
        saved["Battle"]!["NativeEffectMemory"]!.AsObject(),
        saved["Battle"]!["Definition"]!["NativeRetainedMemory"]!["EffectMemory"]!.AsObject()
    ];
}
