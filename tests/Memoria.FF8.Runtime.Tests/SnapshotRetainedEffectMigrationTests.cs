using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime.Tests;

public sealed class SnapshotRetainedEffectMigrationTests
{
    private static readonly JsonSerializerOptions Options = new() { Converters = { new JsonStringEnumConverter(), new ImmutableBytesJsonConverter() } };
    private static readonly JsonSerializerOptions ArrayOptions = new() { Converters = { new JsonStringEnumConverter() } };

    private sealed record Contract(string Name, int Effect, uint Entry, Func<bool, JsonObject> Unknown, Func<bool, JsonObject> Cold);

    private static readonly Contract[] Contracts =
    [
        Owner("OilBlast137", 137, NativeBattleOilBlast137Profile.Entry, NativeBattleOilBlast137State.FromUnknown, NativeBattleOilBlast137State.FromColdProcess),
        Owner("BadBreath131", 131, NativeBattleBadBreath131Profile.Entry, NativeBattleBadBreath131State.FromUnknown, NativeBattleBadBreath131State.FromColdProcess),
        Owner("Saliva138", 138, NativeBattleSaliva138Profile.Entry, NativeBattleSaliva138State.FromUnknown, NativeBattleSaliva138State.FromColdProcess),
        Owner("MagmaBreath48", 48, NativeBattleMagmaBreath48Profile.Entry, NativeBattleMagmaBreath48State.FromUnknown, NativeBattleMagmaBreath48State.FromColdProcess),
        Owner("BladeShot12", 12, NativeBattleBladeShot12Profile.Entry, NativeBattleBladeShot12State.FromUnknown, NativeBattleBladeShot12State.FromColdProcess),
        Owner("OilShot136", 136, NativeBattleOilShot136Profile.Entry, NativeBattleOilShot136State.FromUnknown, NativeBattleOilShot136State.FromColdProcess),
        Owner("ElectricDischarge126", 126, NativeBattleElectricDischarge126Profile.Entry, NativeBattleElectricDischarge126State.FromUnknown, NativeBattleElectricDischarge126State.FromColdProcess),
        Owner("Effect213", 213, NativeBattleEffect213Profile.Entry, NativeBattleEffect213State.FromUnknown, NativeBattleEffect213State.FromColdProcess),
        Owner("Effect301", 301, NativeBattleEffect301Profile.Entry, NativeBattleEffect301State.FromUnknown, NativeBattleEffect301State.FromColdProcess),
        Owner("Effect290", 290, NativeBattleEffect290Profile.Entry, NativeBattleEffect290State.FromUnknown, NativeBattleEffect290State.FromColdProcess),
        Owner("Effect54", 54, NativeBattleEffect54Profile.Entry, NativeBattleEffect54State.FromUnknown, NativeBattleEffect54State.FromColdProcess),
        Owner("Effect320", 320, NativeBattleEffect320Profile.Entry, NativeBattleEffect320State.FromUnknown, NativeBattleEffect320State.FromColdProcess),
        Owner("Effect308", 308, NativeBattleEffect308Profile.Entry, NativeBattleEffect308State.FromUnknown, NativeBattleEffect308State.FromColdProcess),
        Owner("Effect251", 251, NativeBattleEffect251Profile.Entry, NativeBattleEffect251State.FromUnknown, NativeBattleEffect251State.FromColdProcess),
        Owner("Effect83", 83, NativeBattleEffect83Profile.Entry, NativeBattleEffect83State.FromUnknown, NativeBattleEffect83State.FromColdProcess),
        Owner("Effect79", 79, NativeBattleEffect79Profile.Entry, NativeBattleEffect79State.FromUnknown, NativeBattleEffect79State.FromColdProcess),
        Owner("Effect80", 80, NativeBattleEffect80Profile.Entry, NativeBattleEffect80State.FromUnknown, NativeBattleEffect80State.FromColdProcess),
        Owner("Effect68", 68, NativeBattleEffect68Profile.Entry, NativeBattleEffect68State.FromUnknown, NativeBattleEffect68State.FromColdProcess),
        Owner("Effect345", 345, NativeBattleEffect345Profile.Entry, NativeBattleEffect345State.FromUnknown, NativeBattleEffect345State.FromColdProcess)
    ];

    public static IEnumerable<object[]> Owners() => Contracts.Select(contract => new object[] { contract.Name });

    [Theory]
    [MemberData(nameof(Owners))]
    public void Historical_missing_and_exact_unknown_owners_upgrade_in_all_five_copies(string name)
    {
        Contract contract = Find(name);
        foreach (int schema in new[] { 0, 1, 2, 3 })
        {
            JsonObject saved = Snapshot(schema);
            JsonObject[] copies = EffectCopies(saved);
            for (int index = 0; index < copies.Length; index++)
            {
                if (index % 2 != 0)
                {
                    copies[index][name] = contract.Unknown(index == 1);
                }
            }
            saved["Battle"]!["Frame"] = 123L;

            SnapshotMigrations.Apply(saved, Options);

            Assert.Equal(GameState.CurrentSnapshotSchemaVersion, saved["SnapshotSchemaVersion"]!.GetValue<int>());
            Assert.Equal(123L, saved["Battle"]!["Frame"]!.GetValue<long>());
            foreach (JsonObject copy in EffectCopies(saved))
            {
                AssertOwner(contract.Cold(false), copy[name], contract);
            }
            JsonObject once = (JsonObject)saved.DeepClone();
            SnapshotMigrations.Apply(saved, Options);
            Assert.True(JsonNode.DeepEquals(once, saved), name);
        }
    }

    [Theory]
    [MemberData(nameof(Owners))]
    public void Null_warm_partial_changed_and_storage_referenced_owners_remain_atomic(string name)
    {
        Contract contract = Find(name);
        JsonObject saved = Snapshot(3);
        JsonObject[] copies = EffectCopies(saved);
        copies[0][name] = null;
        copies[1][name] = contract.Cold(false);
        JsonObject partial = contract.Unknown(false);
        partial.Remove(partial.First().Key);
        copies[2][name] = partial;
        JsonObject changed = contract.Unknown(false);
        JsonObject changedArena = FirstArena(changed);
        changedArena["UnknownRegions"] = new JsonArray();
        copies[3][name] = changed;
        JsonObject referenced = contract.Unknown(false);
        FirstArena(referenced)["StorageReference"] = JsonSerializer.SerializeToNode(new NativeBattleStorageReference(7, 0, NativeBattleEffectArena.Length), Options);
        copies[4][name] = referenced;
        JsonNode?[] originals = copies.Select(copy => copy[name]?.DeepClone()).ToArray();

        SnapshotMigrations.Apply(saved, Options);

        for (int index = 0; index < copies.Length; index++)
        {
            Assert.True(JsonNode.DeepEquals(originals[index], EffectCopies(saved)[index][name]), name);
        }
    }

    [Theory]
    [MemberData(nameof(Owners))]
    public void Compact_warm_arena_bytes_are_preserved_instead_of_read_as_array_only_json(string name)
    {
        Contract contract = Find(name);
        JsonObject saved = Snapshot(3);
        JsonObject warm = contract.Cold(true);
        JsonObject arena = FirstArena(warm);
        arena["Regions"] = new JsonArray(new JsonObject { ["Offset"] = 0, ["Data"] = Convert.ToBase64String(new byte[] { 0x5A }) });
        foreach (JsonObject copy in EffectCopies(saved))
        {
            copy[name] = warm.DeepClone();
        }

        SnapshotMigrations.Apply(saved, Options);

        foreach (JsonObject copy in EffectCopies(saved))
        {
            Assert.True(JsonNode.DeepEquals(warm, copy[name]), name);
        }
    }

    [Theory]
    [MemberData(nameof(Owners))]
    public void Field_default_completion_cannot_bypass_detached_execution_guard_by_inserting_parent_memory(string name)
    {
        Contract contract = Find(name);
        foreach (int schema in new[] { 2, 3, 4 })
        {
            foreach (bool entireMemory in new[] { false, true })
            {
                JsonObject saved = FieldSnapshot(schema);
                JsonObject eventEffects = saved["EventState"]!["Scene"]!["NativeEyeProcess"]!["Memory"]!["EffectMemory"]!.AsObject();
                eventEffects[name] = contract.Unknown(true);
                JsonObject active = Selected(contract, true);
                active["ReturnedDescriptor"] = 1;
                eventEffects["DetachedReturnedTask"] = active;
                RemoveRootMemory(saved, entireMemory);

                GameState restored = ReadFieldSnapshot(saved);

                Assert.NotNull(restored.NativeBattleMemory!.EffectMemory);
                Assert.Null(typeof(NativeBattleEffectMemory).GetProperty(name)!.GetValue(restored.NativeBattleMemory.EffectMemory));
                JsonObject actualEvent = JsonNode.Parse(restored.EventState!.Value.GetRawText())!.AsObject();
                Assert.True(JsonNode.DeepEquals(contract.Unknown(true), actualEvent["Scene"]!["NativeEyeProcess"]!["Memory"]!["EffectMemory"]![name]), name);
            }
        }
    }

    [Fact]
    public void Historical_field_missing_parent_memories_receive_eligible_owners_after_default_completion_once()
    {
        foreach (int schema in new[] { 2, 3 })
        {
            foreach (bool entireMemory in new[] { false, true })
            {
                JsonObject saved = FieldSnapshot(schema);
                RemoveRootMemory(saved, entireMemory);

                GameState restored = ReadFieldSnapshot(saved);

                JsonObject actual = JsonSerializer.SerializeToNode(restored, Options)!.AsObject();
                JsonObject effects = actual["NativeBattleMemory"]!["EffectMemory"]!.AsObject();
                foreach (Contract contract in Contracts)
                {
                    AssertOwner(contract.Cold(false), effects[contract.Name], contract);
                }
                GameState reloaded = ReadFieldSnapshot(actual);
                Assert.True(JsonNode.DeepEquals(actual, JsonSerializer.SerializeToNode(reloaded, Options)));
            }
        }
    }

    [Fact]
    public void Current_field_missing_parent_memories_do_not_receive_versioned_owner_defaults()
    {
        foreach (bool entireMemory in new[] { false, true })
        {
            JsonObject saved = FieldSnapshot(4);
            RemoveRootMemory(saved, entireMemory);

            GameState restored = ReadFieldSnapshot(saved);

            NativeBattleEffectMemory effects = restored.NativeBattleMemory!.EffectMemory!;
            Assert.NotNull(effects);
            foreach (Contract contract in Contracts)
            {
                Assert.Null(typeof(NativeBattleEffectMemory).GetProperty(contract.Name)!.GetValue(effects));
            }
            Assert.Null(effects.Sigh46);
        }
    }

    [Theory]
    [MemberData(nameof(Owners))]
    public void Active_or_incomplete_live_and_detached_effects_block_every_copy(string name)
    {
        Contract contract = Find(name);
        foreach (bool detached in new[] { false, true })
        {
            foreach (bool binding in new[] { false, true })
            {
                for (int incomplete = 0; incomplete < 4; incomplete++)
                {
                    JsonObject saved = Snapshot(3);
                    JsonObject[] copies = EffectCopies(saved);
                    foreach (JsonObject copy in copies.Skip(1))
                    {
                        copy[name] = contract.Unknown(false);
                    }
                    JsonObject effect = Selected(contract, binding);
                    switch (incomplete)
                    {
                        case 0:
                            effect["ReturnedDescriptor"] = 1;
                            break;
                        case 1:
                            effect["Pools"] = new JsonArray(new JsonObject());
                            break;
                        case 2:
                            effect.Remove("ReturnedDescriptor");
                            break;
                        case 3:
                            effect.Remove("Pools");
                            break;
                    }
                    if (detached)
                    {
                        copies[3]["DetachedReturnedTask"] = effect;
                    }
                    else
                    {
                        saved["Battle"]!["NativeGraphics"]!["OriginalEffect"] = effect;
                    }

                    SnapshotMigrations.Apply(saved, Options);

                    Assert.False(EffectCopies(saved)[0].ContainsKey(name), name);
                    foreach (JsonObject copy in EffectCopies(saved).Skip(1))
                    {
                        Assert.True(JsonNode.DeepEquals(contract.Unknown(false), copy[name]), name);
                    }
                }
            }
        }
    }

    [Theory]
    [MemberData(nameof(Owners))]
    public void Selected_unentered_effect_allows_migration_for_both_selectors_and_completion_values(string name)
    {
        Contract contract = Find(name);
        foreach (bool binding in new[] { false, true })
        {
            foreach (bool nullDescriptor in new[] { false, true })
            {
                JsonObject saved = Snapshot(3);
                JsonObject effect = Selected(contract, binding);
                if (nullDescriptor)
                {
                    effect["ReturnedDescriptor"] = null;
                }
                saved["Battle"]!["NativeGraphics"]!["OriginalEffect"] = effect;
                EffectCopies(saved)[0][name] = contract.Unknown(false);

                SnapshotMigrations.Apply(saved, Options);

                foreach (JsonObject copy in EffectCopies(saved))
                {
                    AssertOwner(contract.Cold(false), copy[name], contract);
                }
            }
        }
    }

    [Theory]
    [MemberData(nameof(Owners))]
    public void Missing_parent_memories_in_early_schemas_cannot_bypass_active_execution_guard(string name)
    {
        Contract contract = Find(name);
        foreach (int schema in new[] { 0, 1 })
        {
            JsonObject saved = Snapshot(schema);
            foreach (JsonObject parent in MemoryParents(saved))
            {
                parent.Remove("EffectMemory");
            }
            saved["Battle"]!.AsObject().Remove("NativeEffectMemory");
            JsonObject effect = Selected(contract, true);
            effect["ReturnedDescriptor"] = 1;
            saved["Battle"]!["NativeGraphics"]!["OriginalEffect"] = effect;

            SnapshotMigrations.Apply(saved, Options);

            foreach (JsonObject copy in EffectCopies(saved))
            {
                Assert.False(copy.ContainsKey(name), name);
            }
        }
    }

    [Fact]
    public void Current_version_preserves_missing_and_unknown_owners()
    {
        JsonObject saved = Snapshot(GameState.CurrentSnapshotSchemaVersion);
        foreach (Contract contract in Contracts)
        {
            EffectCopies(saved)[1][contract.Name] = contract.Unknown(true);
        }
        JsonObject original = (JsonObject)saved.DeepClone();

        SnapshotMigrations.Apply(saved, Options);

        Assert.True(JsonNode.DeepEquals(original, saved));
    }

    private static Contract Owner<T>(string name, int effect, uint entry, Func<T> unknown, Func<T> cold) => new(name, effect, entry, compact => State(unknown(), compact), compact => State(cold(), compact));

    private static JsonObject State<T>(T state, bool compact) => JsonSerializer.SerializeToNode(NativeBattleStorageSnapshots.Capture(state), compact ? Options : ArrayOptions)!.AsObject();

    private static Contract Find(string name) => Contracts.Single(contract => contract.Name == name);

    private static void AssertOwner(JsonObject expected, JsonNode? actual, Contract contract)
    {
        Type type = typeof(NativeBattleEffectMemory).GetProperty(contract.Name)!.PropertyType;
        object restored = actual!.Deserialize(type, Options)!;
        JsonNode normalized = JsonSerializer.SerializeToNode(NativeBattleStorageSnapshots.Capture(restored), type, ArrayOptions)!;
        Assert.True(JsonNode.DeepEquals(expected, normalized), contract.Name);
    }

    private static JsonObject FirstArena(JsonNode node)
    {
        if (node is JsonObject owner)
        {
            if (owner.ContainsKey("UnknownRegions"))
            {
                return owner;
            }
            foreach (JsonNode? child in owner.Select(property => property.Value))
            {
                if (child is not null && FindArena(child) is JsonObject arena)
                {
                    return arena;
                }
            }
        }
        throw new InvalidOperationException("Owner has no arena.");
    }

    private static JsonObject? FindArena(JsonNode node)
    {
        if (node is JsonObject owner)
        {
            if (owner.ContainsKey("UnknownRegions"))
            {
                return owner;
            }
            return owner.Select(property => property.Value).Where(child => child is not null).Select(child => FindArena(child!)).FirstOrDefault(arena => arena is not null);
        }
        return node is JsonArray array ? array.Where(child => child is not null).Select(child => FindArena(child!)).FirstOrDefault(arena => arena is not null) : null;
    }

    private static JsonObject Selected(Contract contract, bool binding)
    {
        JsonObject effect = new() { ["Pools"] = new JsonArray(), ["ReturnedDescriptor"] = 0 };
        if (binding)
        {
            effect["Binding"] = new JsonObject { ["Entry"] = contract.Entry };
        }
        else
        {
            effect["RequestedEffect"] = contract.Effect;
        }
        return effect;
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

    private static JsonObject FieldSnapshot(int schema)
    {
        JsonObject saved = JsonSerializer.SerializeToNode(GameSession.CreateColdProcess().State, Options)!.AsObject();
        saved["SnapshotSchemaVersion"] = schema;
        saved["Battle"] = null;
        saved["EventState"] = Event();
        saved["EventState"]!["Scene"]!["NativeEyeProcess"]!["Memory"] = saved["NativeBattleMemory"]!.DeepClone();
        return saved;
    }

    private static void RemoveRootMemory(JsonObject saved, bool entireMemory)
    {
        if (entireMemory)
        {
            saved.Remove("NativeBattleMemory");
        }
        else
        {
            saved["NativeBattleMemory"]!.AsObject().Remove("EffectMemory");
        }
    }

    private static GameState ReadFieldSnapshot(JsonObject saved)
    {
        using MemoryStream input = new(System.Text.Encoding.UTF8.GetBytes(saved.ToJsonString(Options)));
        return SnapshotDefaults.Deserialize(input, Options);
    }

    private static JsonObject[] MemoryParents(JsonObject saved) =>
    [
        saved["NativeBattleMemory"]!.AsObject(),
        saved["EventState"]!["Scene"]!["NativeEyeProcess"]!["Memory"]!.AsObject(),
        saved["DepartingFieldState"]!["Scene"]!["NativeEyeProcess"]!["Memory"]!.AsObject(),
        saved["Battle"]!["Definition"]!["NativeRetainedMemory"]!.AsObject()
    ];

    private static JsonObject[] EffectCopies(JsonObject saved) =>
    [
        saved["NativeBattleMemory"]!["EffectMemory"]!.AsObject(),
        saved["EventState"]!["Scene"]!["NativeEyeProcess"]!["Memory"]!["EffectMemory"]!.AsObject(),
        saved["DepartingFieldState"]!["Scene"]!["NativeEyeProcess"]!["Memory"]!["EffectMemory"]!.AsObject(),
        saved["Battle"]!["NativeEffectMemory"]!.AsObject(),
        saved["Battle"]!["Definition"]!["NativeRetainedMemory"]!["EffectMemory"]!.AsObject()
    ];
}
