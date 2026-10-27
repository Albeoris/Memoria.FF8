using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime.Tests;

public sealed class SnapshotSigh46MigrationTests
{
    private static readonly JsonSerializerOptions Options = new() { Converters = { new JsonStringEnumConverter(), new ImmutableBytesJsonConverter() } };

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void Historical_missing_and_exact_unknown_owners_are_initialized_in_all_five_copies(int schema)
    {
        JsonObject saved = Snapshot(schema);
        JsonObject[] copies = EffectCopies(saved);
        for (int index = 0; index < copies.Length; index++)
        {
            if (index % 2 == 0)
            {
                copies[index].Remove("Sigh46");
            }
            else
            {
                copies[index]["Sigh46"] = State(NativeBattleSigh46State.FromUnknown());
            }
        }
        saved["Battle"]!["Frame"] = 123L;
        JsonObject original = (JsonObject)saved.DeepClone();

        SnapshotMigrations.Apply(saved, Options);

        Assert.Equal(GameState.CurrentSnapshotSchemaVersion, saved["SnapshotSchemaVersion"]!.GetValue<int>());
        Assert.Equal(123L, saved["Battle"]!["Frame"]!.GetValue<long>());
        foreach (JsonObject copy in EffectCopies(saved))
        {
            AssertCold(copy["Sigh46"]);
        }
        if (schema == 2)
        {
            JsonObject reverted = (JsonObject)saved.DeepClone();
            reverted["SnapshotSchemaVersion"] = schema;
            JsonObject[] oldCopies = EffectCopies(original);
            JsonObject[] revertedCopies = EffectCopies(reverted);
            for (int index = 0; index < oldCopies.Length; index++)
            {
                if (oldCopies[index].TryGetPropertyValue("Sigh46", out JsonNode? owner))
                {
                    revertedCopies[index]["Sigh46"] = owner?.DeepClone();
                }
                else
                {
                    revertedCopies[index].Remove("Sigh46");
                }
            }
            Assert.True(JsonNode.DeepEquals(original, reverted));
        }
        JsonObject once = (JsonObject)saved.DeepClone();
        SnapshotMigrations.Apply(saved, Options);
        Assert.True(JsonNode.DeepEquals(once, saved));
    }

    [Fact]
    public void Current_schema_leaves_missing_and_unknown_owners_unchanged()
    {
        JsonObject saved = Snapshot(GameState.CurrentSnapshotSchemaVersion);
        EffectCopies(saved)[0].Remove("Sigh46");
        EffectCopies(saved)[1]["Sigh46"] = State(NativeBattleSigh46State.FromUnknown());
        JsonObject original = (JsonObject)saved.DeepClone();

        SnapshotMigrations.Apply(saved, Options);

        Assert.True(JsonNode.DeepEquals(original, saved));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Normal_container_reads_upgrade_without_modifying_source_and_reload_idempotently(bool compact)
    {
        JsonObject saved = Snapshot(2);
        saved["Battle"]!["NativeGraphics"] = null;
        foreach (JsonObject copy in EffectCopies(saved))
        {
            copy["Sigh46"] = State(NativeBattleSigh46State.FromUnknown());
        }
        GameState historical = saved.Deserialize<GameState>(Options)!;
        string sourcePath = Path.Combine(Path.GetTempPath(), "Memoria.FF8-sigh-source-" + Guid.NewGuid().ToString("N"));
        string currentPath = Path.Combine(Path.GetTempPath(), "Memoria.FF8-sigh-current-" + Guid.NewGuid().ToString("N"));
        try
        {
            Write(sourcePath, historical);
            byte[] original = File.ReadAllBytes(sourcePath);
            GameState upgraded;
            using (FileStream source = File.OpenRead(sourcePath))
            {
                upgraded = SnapshotStorage.Read(source, Options);
            }
            Assert.Equal(GameState.CurrentSnapshotSchemaVersion, upgraded.SnapshotSchemaVersion);
            JsonObject upgradedJson = JsonSerializer.SerializeToNode(upgraded, Options)!.AsObject();
            foreach (JsonObject copy in EffectCopies(upgradedJson))
            {
                AssertCold(copy["Sigh46"]);
            }
            Write(currentPath, upgraded);
            using (FileStream current = File.OpenRead(currentPath))
            {
                GameState reloaded = SnapshotStorage.Read(current, Options);
                Assert.True(JsonNode.DeepEquals(upgradedJson, JsonSerializer.SerializeToNode(reloaded, Options)));
            }
            Assert.Equal(original, File.ReadAllBytes(sourcePath));
        }
        finally
        {
            File.Delete(sourcePath);
            File.Delete(currentPath);
        }

        void Write(string path, GameState state)
        {
            using FileStream output = File.Create(path);
            if (compact)
            {
                SnapshotStorage.Write(output, state, Options);
            }
            else
            {
                JsonSerializer.Serialize(output, state, Options);
            }
        }
    }

    [Fact]
    public void Current_field_default_completion_does_not_repair_missing_or_partial_sigh()
    {
        JsonObject saved = Snapshot(GameState.CurrentSnapshotSchemaVersion);
        saved["Battle"] = null;
        JsonObject effects = saved["NativeBattleMemory"]!["EffectMemory"]!.AsObject();
        effects.Remove("Sigh46");
        GameState restored = Deserialize(saved);
        Assert.Null(restored.NativeBattleMemory!.EffectMemory!.Sigh46);

        JsonObject partial = State(NativeBattleSigh46State.FromUnknown());
        partial.Remove("SourcesLoaded");
        effects["Sigh46"] = partial;
        Assert.Throws<JsonException>(() => Deserialize(saved));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void Explicit_null_warm_changed_and_partial_owners_remain_atomic(int schema)
    {
        JsonObject saved = Snapshot(schema);
        JsonObject[] copies = EffectCopies(saved);
        copies[0]["Sigh46"] = null;
        JsonObject warm = State(NativeBattleSigh46State.FromColdProcess());
        warm["SourcesLoaded"] = true;
        copies[1]["Sigh46"] = warm;
        copies[2]["Sigh46"] = State(NativeBattleSigh46State.FromUnknown().WriteDword(0x26527A0, 123));
        JsonObject partial = State(NativeBattleSigh46State.FromUnknown());
        partial.Remove("SourcesLoaded");
        copies[3]["Sigh46"] = partial;
        JsonObject bankChanged = State(NativeBattleSigh46State.FromUnknown());
        bankChanged["InactiveBanks"]![0] = null;
        copies[4]["Sigh46"] = bankChanged;
        JsonNode?[] original = copies.Select(copy => copy["Sigh46"]?.DeepClone()).ToArray();

        SnapshotMigrations.Apply(saved, Options);

        JsonObject[] actual = EffectCopies(saved);
        for (int index = 0; index < original.Length; index++)
        {
            Assert.True(JsonNode.DeepEquals(original[index], actual[index]["Sigh46"]));
        }
        Assert.Throws<JsonException>(() => actual[3]["Sigh46"]!.Deserialize<NativeBattleSigh46State>(Options));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Exact_unknown_matching_accepts_both_byte_encodings_and_optional_arena_metadata(bool compactBytes)
    {
        JsonObject saved = Snapshot(2);
        JsonObject unknown = State(NativeBattleSigh46State.FromUnknown(), compactBytes);
        foreach (string name in new[] { "Globals", "SourceHeader", "Channels", "MutablePe" })
        {
            JsonObject arena = unknown[name]!.AsObject();
            arena["LogicalLength"] = NativeBattleEffectArena.Length;
            arena["ResourcePointers"] = null;
            arena["TextureFiles"] = null;
        }
        EffectCopies(saved)[0]["Sigh46"] = unknown;

        SnapshotMigrations.Apply(saved, Options);

        AssertCold(EffectCopies(saved)[0]["Sigh46"]);
    }

    [Fact]
    public void Storage_references_are_preserved_instead_of_discarded_for_unknown_matching()
    {
        JsonObject saved = Snapshot(2);
        JsonObject unknown = State(NativeBattleSigh46State.FromUnknown());
        unknown["Channels"]!["StorageReference"] = JsonSerializer.SerializeToNode(new NativeBattleStorageReference(7, 0, NativeBattleEffectArena.Length), Options);
        EffectCopies(saved)[0]["Sigh46"] = unknown;
        JsonObject original = (JsonObject)unknown.DeepClone();

        SnapshotMigrations.Apply(saved, Options);

        Assert.True(JsonNode.DeepEquals(original, EffectCopies(saved)[0]["Sigh46"]));
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void Initialized_or_incomplete_sigh_execution_blocks_all_owner_replacements(bool detached, bool bindingSelector)
    {
        JsonObject saved = Snapshot(2);
        JsonObject[] copies = EffectCopies(saved);
        foreach (JsonObject copy in copies)
        {
            copy["Sigh46"] = State(NativeBattleSigh46State.FromUnknown());
        }
        copies[0].Remove("Sigh46");
        JsonObject effect = new() { ["Pools"] = new JsonArray(), ["ReturnedDescriptor"] = 1 };
        if (bindingSelector)
        {
            effect["Binding"] = new JsonObject { ["Entry"] = NativeBattleSigh46Profile.Entry };
            effect.Remove("Pools");
        }
        else
        {
            effect["RequestedEffect"] = 46;
        }
        if (detached)
        {
            copies[3]["DetachedReturnedTask"] = effect;
        }
        else
        {
            saved["Battle"]!["NativeGraphics"]!["OriginalEffect"] = effect;
        }
        JsonNode?[] original = copies.Select(copy => copy["Sigh46"]?.DeepClone()).ToArray();

        SnapshotMigrations.Apply(saved, Options);

        JsonObject[] actual = EffectCopies(saved);
        Assert.False(actual[0].ContainsKey("Sigh46"));
        for (int index = 1; index < original.Length; index++)
        {
            Assert.True(JsonNode.DeepEquals(original[index], actual[index]["Sigh46"]));
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Selected_but_unentered_sigh_can_initialize_unknown_owners(bool nullDescriptor)
    {
        JsonObject saved = Snapshot(2);
        EffectCopies(saved)[0]["Sigh46"] = State(NativeBattleSigh46State.FromUnknown());
        saved["Battle"]!["NativeGraphics"]!["OriginalEffect"] = new JsonObject { ["RequestedEffect"] = 46, ["Pools"] = new JsonArray(), ["ReturnedDescriptor"] = nullDescriptor ? null : JsonValue.Create(0) };

        SnapshotMigrations.Apply(saved, Options);

        AssertCold(EffectCopies(saved)[0]["Sigh46"]);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public void Missing_parent_effect_memories_do_not_bypass_the_execution_guard_in_earlier_schema_steps(int schema)
    {
        JsonObject saved = Snapshot(schema);
        saved["NativeBattleMemory"]!.AsObject().Remove("EffectMemory");
        saved["EventState"]!["Scene"]!["NativeEyeProcess"]!["Memory"]!.AsObject().Remove("EffectMemory");
        saved["DepartingFieldState"]!["Scene"]!["NativeEyeProcess"]!["Memory"]!.AsObject().Remove("EffectMemory");
        saved["Battle"]!.AsObject().Remove("NativeEffectMemory");
        saved["Battle"]!["Definition"]!["NativeRetainedMemory"]!.AsObject().Remove("EffectMemory");
        saved["Battle"]!["NativeGraphics"]!["OriginalEffect"] = new JsonObject { ["RequestedEffect"] = 46, ["Pools"] = new JsonArray(new JsonObject()), ["ReturnedDescriptor"] = 1 };

        SnapshotMigrations.Apply(saved, Options);

        foreach (JsonObject copy in EffectCopies(saved))
        {
            Assert.False(copy.ContainsKey("Sigh46"));
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Nonempty_pools_or_missing_completion_descriptor_prevent_cold_initialization(bool missingDescriptor)
    {
        JsonObject saved = Snapshot(2);
        JsonObject unknown = State(NativeBattleSigh46State.FromUnknown());
        EffectCopies(saved)[0]["Sigh46"] = unknown.DeepClone();
        JsonObject effect = new() { ["RequestedEffect"] = 46, ["Pools"] = new JsonArray(), ["ReturnedDescriptor"] = 0 };
        if (missingDescriptor)
        {
            effect.Remove("ReturnedDescriptor");
        }
        else
        {
            effect["Pools"] = new JsonArray(new JsonObject());
        }
        saved["Battle"]!["NativeGraphics"]!["OriginalEffect"] = effect;

        SnapshotMigrations.Apply(saved, Options);

        Assert.True(JsonNode.DeepEquals(unknown, EffectCopies(saved)[0]["Sigh46"]));
    }

    private static JsonObject State(NativeBattleSigh46State state, bool compactBytes = false)
    {
        JsonSerializerOptions options = compactBytes ? Options : new JsonSerializerOptions { Converters = { new JsonStringEnumConverter() } };
        return JsonSerializer.SerializeToNode(NativeBattleStorageSnapshots.Capture(state), options)!.AsObject();
    }

    private static void AssertCold(JsonNode? node)
    {
        NativeBattleSigh46State actual = node!.Deserialize<NativeBattleSigh46State>(Options)!;
        Assert.True(JsonNode.DeepEquals(State(NativeBattleSigh46State.FromColdProcess()), State(actual)));
    }

    private static JsonObject Snapshot(int schema)
    {
        GameSession session = GameSession.CreateColdProcess();
        BattleSession battle = new(new BattleDefinition
        {
            NativeRetainedMemory = session.State.NativeBattleMemory,
            Combatants = [new() { Id = "party", Name = "Party", IsPlayer = true, MaximumHp = 1000, CurrentHp = 1000 }, new() { Id = "enemy", Name = "Enemy", MaximumHp = 1000, CurrentHp = 1000 }]
        }, BattleAtbMode.Normal);
        JsonObject saved = JsonSerializer.SerializeToNode(session.State with { Battle = battle.Capture() }, Options)!.AsObject();
        saved["SnapshotSchemaVersion"] = schema;
        JsonNode memory = saved["NativeBattleMemory"]!.DeepClone();
        saved["EventState"] = new JsonObject { ["Scene"] = new JsonObject { ["NativeEyeProcess"] = new JsonObject { ["Memory"] = memory } } };
        saved["DepartingFieldState"] = saved["EventState"]!.DeepClone();
        saved["Battle"]!["NativeGraphics"] ??= new JsonObject();
        return saved;
    }

    private static JsonObject[] EffectCopies(JsonObject saved) =>
    [
        saved["NativeBattleMemory"]!["EffectMemory"]!.AsObject(),
        saved["EventState"]!["Scene"]!["NativeEyeProcess"]!["Memory"]!["EffectMemory"]!.AsObject(),
        saved["DepartingFieldState"]!["Scene"]!["NativeEyeProcess"]!["Memory"]!["EffectMemory"]!.AsObject(),
        saved["Battle"]!["NativeEffectMemory"]!.AsObject(),
        saved["Battle"]!["Definition"]!["NativeRetainedMemory"]!["EffectMemory"]!.AsObject()
    ];

    private static GameState Deserialize(JsonObject saved)
    {
        using MemoryStream input = new(Encoding.UTF8.GetBytes(saved.ToJsonString(Options)));
        return SnapshotDefaults.Deserialize(input, Options);
    }
}
