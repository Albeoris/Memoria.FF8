using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Runtime.Tests;

public sealed class SnapshotDefaultsTests
{
    private static readonly JsonSerializerOptions Options = new() { Converters = { new JsonStringEnumConverter(), new ImmutableBytesJsonConverter() } };

    [Fact]
    public void Missing_root_properties_use_the_cold_process_state()
    {
        GameSession session = GameSession.CreateColdProcess();
        GameState restored = Deserialize(new JsonObject());

        Assert.Equal(JsonSerializer.Serialize(session.State, Options), JsonSerializer.Serialize(restored, Options));
    }

    [Fact]
    public void Missing_retained_process_properties_are_filled_recursively()
    {
        JsonObject snapshot = ColdSnapshot();
        JsonObject memory = snapshot["NativeBattleMemory"]!.AsObject();
        memory["EffectMemory"]!.AsObject().Remove("Shiva");
        memory["ActionScratch"]!.AsObject().Remove("GuardianBoostPercent");
        snapshot.Remove("Playtime");

        GameState restored = Deserialize(snapshot);
        GameState cold = GameSession.CreateColdProcess().State;

        Assert.Equal(JsonSerializer.Serialize(cold.NativeBattleMemory!.EffectMemory!.Shiva, Options), JsonSerializer.Serialize(restored.NativeBattleMemory!.EffectMemory!.Shiva, Options));
        Assert.Equal(cold.NativeBattleMemory.ActionScratch.GuardianBoostPercent, restored.NativeBattleMemory.ActionScratch.GuardianBoostPercent);
        Assert.Equal(cold.Playtime, restored.Playtime);
    }

    [Fact]
    public void Present_shiva_values_and_explicit_nulls_are_preserved()
    {
        JsonObject snapshot = ColdSnapshot();
        JsonObject memory = snapshot["NativeBattleMemory"]!.AsObject();
        JsonObject shiva = memory["EffectMemory"]!["Shiva"]!.AsObject();
        shiva["VibrationHandle"] = 17;
        shiva["SoundReady"] = 1;
        shiva["SpawnedChildD32508"] = 23;
        memory["ActionScratch"]!["DeferredEffectsE00"] = false;
        memory["ActionScratch"]!["GuardianBoostPercent"] = 0;
        snapshot["Playtime"] = null;
        snapshot["FieldName"] = "";
        snapshot["X"] = 0;

        GameState restored = Deserialize(snapshot);

        Assert.Equal(17, restored.NativeBattleMemory!.EffectMemory!.Shiva!.VibrationHandle);
        Assert.Equal((byte)1, restored.NativeBattleMemory.EffectMemory.Shiva.SoundReady);
        Assert.Equal(23u, restored.NativeBattleMemory.EffectMemory.Shiva.SpawnedChildD32508);
        Assert.False(restored.NativeBattleMemory.ActionScratch.DeferredEffectsE00);
        Assert.Equal((byte)0, restored.NativeBattleMemory.ActionScratch.GuardianBoostPercent);
        Assert.Null(restored.Playtime);
        Assert.Equal("", restored.FieldName);
        Assert.Equal(0, restored.X);

        memory["EffectMemory"]!["Shiva"] = null;
        Assert.Null(Deserialize(snapshot).NativeBattleMemory!.EffectMemory!.Shiva);
        snapshot["NativeBattleMemory"] = null;
        Assert.Null(Deserialize(snapshot).NativeBattleMemory);
    }

    [Fact]
    public void Present_arrays_are_never_extended_or_merged()
    {
        JsonObject snapshot = ColdSnapshot();
        JsonObject memory = snapshot["NativeBattleMemory"]!.AsObject();
        memory["ElementAttack"] = new JsonArray();
        memory["PartyStatusMirrors"]!["Saved"] = new JsonArray((JsonNode?)null);
        memory["ModelStatusTimers"] = new JsonArray(new JsonArray(JsonValue.Create((byte)9)));
        snapshot["AppliedBattleResultIds"] = new JsonArray();

        GameState restored = Deserialize(snapshot);

        Assert.Empty(restored.NativeBattleMemory!.ElementAttack);
        Assert.Null(Assert.Single(restored.NativeBattleMemory.PartyStatusMirrors.Saved));
        Assert.Equal((byte)9, Assert.Single(Assert.Single(restored.NativeBattleMemory.ModelStatusTimers!.Value)));
        Assert.Empty(restored.AppliedBattleResultIds);
    }

    [Theory]
    [InlineData("Version")]
    [InlineData("Playtime")]
    public void Invalid_present_types_still_fail_deserialization(string property)
    {
        JsonObject snapshot = ColdSnapshot();
        snapshot[property] = "invalid";

        Assert.Throws<JsonException>(() => Deserialize(snapshot));
    }

    [Fact]
    public void Missing_live_battle_contracts_are_not_created_from_process_defaults()
    {
        JsonObject snapshot = ColdSnapshot();
        snapshot["Battle"] = new JsonObject();

        Assert.Throws<JsonException>(() => Deserialize(snapshot));
    }

    [Fact]
    public void Battle_continuations_do_not_backfill_either_retained_memory_copy()
    {
        BattleSession battle = new(new BattleDefinition
        {
            NativeRetainedMemory = NativeBattleRetainedMemory.FromColdProcess(),
            Combatants = [new() { Id = "party", Name = "Party", IsPlayer = true, MaximumHp = 1000, CurrentHp = 1000 }, new() { Id = "enemy", Name = "Enemy", MaximumHp = 1000, CurrentHp = 1000 }]
        }, BattleAtbMode.Normal);
        JsonObject snapshot = ColdSnapshot();
        snapshot["Battle"] = JsonSerializer.SerializeToNode(battle.Capture(), Options);
        snapshot["NativeBattleMemory"]!["EffectMemory"]!.AsObject().Remove("Shiva");
        snapshot["Battle"]!["NativeEffectMemory"]!.AsObject().Remove("Shiva");

        GameState restored = Deserialize(snapshot);

        Assert.Null(restored.NativeBattleMemory!.EffectMemory!.Shiva);
        Assert.Null(restored.Battle!.NativeEffectMemory!.Shiva);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Newly_written_saves_preserve_intentional_nulls_instead_of_omitting_them(bool compressed)
    {
        GameState state = GameSession.CreateColdProcess().State;
        state = state with { ExamTimer = null, Playtime = null, NativeBattleMemory = state.NativeBattleMemory! with { EffectMemory = state.NativeBattleMemory!.EffectMemory! with { Shiva = null } } };
        string path = Path.Combine(Path.GetTempPath(), "Memoria.FF8-null-defaults-" + Guid.NewGuid().ToString("N") + ".json");
        try
        {
            new GameSession(state).Save(path, compressed);
            GameState loaded = GameSession.Load(path);

            Assert.Null(loaded.ExamTimer);
            Assert.Null(loaded.NativeBattleMemory!.EffectMemory!.Shiva);
            Assert.Equal(JsonSerializer.Serialize(state, Options), JsonSerializer.Serialize(loaded, Options));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Executed_shiva_missing_a_required_process_counter_is_rejected()
    {
        JsonObject snapshot = ColdSnapshot();
        JsonObject shiva = snapshot["NativeBattleMemory"]!["EffectMemory"]!["Shiva"]!.AsObject();
        NativeBattleActionMatrix world = new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], 0, 0, 0, 4096);
        shiva["Entry"] = JsonSerializer.SerializeToNode(new NativeBattleShivaEntryState(0, 1, new(0, 0, 0), world, 7), Options);
        shiva.Remove("SpawnedChildD32508");

        JsonException exception = Assert.Throws<JsonException>(() => Deserialize(snapshot));

        Assert.Contains("SpawnedChildD32508", exception.Message);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Plain_and_compressed_saves_apply_the_same_missing_property_defaults(bool compressed)
    {
        JsonObject snapshot = ColdSnapshot();
        snapshot["NativeBattleMemory"]!["EffectMemory"]!.AsObject().Remove("Shiva");
        string path = Path.Combine(Path.GetTempPath(), "Memoria.FF8-defaults-" + Guid.NewGuid().ToString("N") + ".json");
        try
        {
            byte[] json = Encoding.UTF8.GetBytes(snapshot.ToJsonString(Options));
            using (FileStream output = File.Create(path))
            {
                if (compressed)
                {
                    WriteContainer(output, json);
                }
                else
                {
                    output.Write(json);
                }
            }
            using FileStream input = File.OpenRead(path);
            GameState restored = SnapshotStorage.Read(input, Options);

            Assert.Equal(JsonSerializer.Serialize(Deserialize(snapshot), Options), JsonSerializer.Serialize(restored, Options));
            Assert.NotNull(restored.NativeBattleMemory!.EffectMemory!.Shiva);
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static JsonObject ColdSnapshot()
    {
        return JsonSerializer.SerializeToNode(GameSession.CreateColdProcess().State, Options)!.AsObject();
    }

    private static GameState Deserialize(JsonObject snapshot)
    {
        using MemoryStream stream = new(Encoding.UTF8.GetBytes(snapshot.ToJsonString(Options)));
        return SnapshotDefaults.Deserialize(stream, Options);
    }

    private static void WriteContainer(FileStream output, byte[] json)
    {
        byte[] summary = JsonSerializer.SerializeToUtf8Bytes(SaveSummary.Capture(GameSession.CreateColdProcess().State), Options);
        using MemoryStream payload = new();
        using (GZipStream gzip = new(payload, CompressionLevel.Fastest, leaveOpen: true))
        {
            gzip.Write(json);
        }
        byte[] compressed = payload.ToArray();
        using BinaryWriter writer = new(output, Encoding.UTF8, leaveOpen: true);
        writer.Write("MFF8SAVE"u8);
        writer.Write(1);
        writer.Write(summary.Length);
        writer.Write((long)compressed.Length);
        writer.Write(SHA256.HashData(summary));
        writer.Write(SHA256.HashData(compressed));
        writer.Write(summary);
        writer.Write(compressed);
    }
}
