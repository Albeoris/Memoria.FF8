using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeBattleGuardianSoundOwnerMigrationTests
{
    [Fact]
    public void Migrate_only_GF17_live_unknown_sound_history_then_render_and_continue_one_actual_callback()
    {
        string? source = Environment.GetEnvironmentVariable("MEMORIA_FF8_GF_SOUND_OWNER_MIGRATION_SOURCE");
        string? output = Environment.GetEnvironmentVariable("MEMORIA_FF8_GF_SOUND_OWNER_MIGRATION_OUTPUT");
        Assert.SkipUnless(source is not null && output is not null, "Exact GF17 source and distinct CreateNew output are required for the explicit unknown sound-owner migration.");
        Assert.False(string.Equals(Path.GetFullPath(source!), Path.GetFullPath(output!), StringComparison.OrdinalIgnoreCase));
        byte[] bytes = File.ReadAllBytes(source!);
        const string sourceHash = "5642C1E0A8FC9F9C755C2A8B972FCE8815FA2E0F9084A8CF3ADC9D219CE5DDCC";
        Assert.Equal(sourceHash, Convert.ToHexString(SHA256.HashData(bytes)));
        BattleSnapshot original = JsonSerializer.Deserialize<BattleSnapshot>(bytes)!;
        Assert.Equal(149L, original.Frame);
        Assert.Equal(229L, original.NativeGraphics!.Callback);
        Assert.Equal((ushort)116, original.NativeGraphics.OriginalEffect!.Binding.Effect);
        Assert.Equal((ushort)89, Phase(original));
        Assert.Null(original.NativeGlobalSoundProcess);
        Assert.Null(original.Definition.NativeGlobalSoundProcess);
        JsonObject document = JsonNode.Parse(bytes)!.AsObject();
        bool existed = document.ContainsKey("NativeGlobalSoundProcess");
        Assert.Null(document["NativeGlobalSoundProcess"]);
        JsonObject changed = document.DeepClone().AsObject();
        changed["NativeGlobalSoundProcess"] = JsonSerializer.SerializeToNode(NativeGlobalSoundProcess.UnknownBufferless());
        JsonObject restoredDocument = changed.DeepClone().AsObject();
        if (existed)
        {
            restoredDocument["NativeGlobalSoundProcess"] = null;
        }
        else
        {
            restoredDocument.Remove("NativeGlobalSoundProcess");
        }
        Assert.True(JsonNode.DeepEquals(document, restoredDocument));
        byte[] migratedBytes = JsonSerializer.SerializeToUtf8Bytes(changed);
        BattleSnapshot migrated = JsonSerializer.Deserialize<BattleSnapshot>(migratedBytes)!;
        Assert.Equal(JsonSerializer.Serialize(original), JsonSerializer.Serialize(migrated with { NativeGlobalSoundProcess = null }));
        BattleSession first = BattleSession.Restore(migrated);
        BattleSession second = BattleSession.Restore(JsonSerializer.Deserialize<BattleSnapshot>(JsonSerializer.Serialize(migrated))!);
        Assert.Equal(JsonSerializer.Serialize(migrated), JsonSerializer.Serialize(first.Capture()));
        using BattleAssets assets = new(Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR") ?? LaunchOptions.DefaultGameDirectory);
        BattleScenePresentation scene = new(assets, migrated.Definition, true);
        byte[] beforePixels = scene.Render(first, 320, 224).ToArray();
        Assert.Equal(320 * 224 * 4, beforePixels.Length);
        TimeSpan callback = TimeSpan.FromTicks((TimeSpan.TicksPerSecond + 29) / 30);
        first.AdvanceNativeGraphicsPresentation(callback);
        second.AdvanceNativeGraphicsPresentation(callback);
        Assert.Equal(JsonSerializer.Serialize(first.Capture()), JsonSerializer.Serialize(second.Capture()));
        Assert.Equal(230L, first.NativeGraphics!.Callback);
        Assert.Equal((ushort)90, Phase(first.Capture()));
        Assert.Null(first.NativeGlobalSoundProcess!.Rotation);
        Assert.All(first.NativeGlobalSoundProcess.OriginalEffectIds, value => Assert.Null(value));
        byte[] afterPixels = scene.Render(first, 320, 224).ToArray();
        Assert.Equal(320 * 224 * 4, afterPixels.Length);
        using (FileStream stream = new(output!, FileMode.CreateNew, FileAccess.Write))
        {
            stream.Write(migratedBytes);
        }
        Assert.Equal(sourceHash, Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(source!))));
        Assert.Equal(JsonSerializer.Serialize(migrated), JsonSerializer.Serialize(BattleSession.Restore(JsonSerializer.Deserialize<BattleSnapshot>(File.ReadAllBytes(output!))!).Capture()));
        Console.WriteLine($"GF17 sound-owner migration: only live unknown history added; source{sourceHash}, output{Convert.ToHexString(SHA256.HashData(migratedBytes))}. Saved149/229/89; independent actual continuation frame{first.Frame}/callback230/phase90 and before/after renders agree. Output={Path.GetFullPath(output!)}");
    }

    private static ushort Phase(BattleSnapshot snapshot) => snapshot.NativeGraphics!.OriginalEffect!.Pools.Single(pool => pool.DescriptorAddress == 0x2521738).Slots[0].ReadWord(12);
}
