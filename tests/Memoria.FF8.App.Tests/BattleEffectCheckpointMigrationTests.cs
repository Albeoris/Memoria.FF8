using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class BattleEffectCheckpointMigrationTests
{
    [Fact]
    public void Explicitly_migrate_the_named_fresh_reward_owner_and_validate_one_tick_without_replaying_travel()
    {
        string? input = Environment.GetEnvironmentVariable("MEMORIA_FF8_EFFECT_CHECKPOINT_MIGRATION_SOURCE");
        string? output = Environment.GetEnvironmentVariable("MEMORIA_FF8_EFFECT_CHECKPOINT_MIGRATION_OUTPUT");
        Assert.SkipUnless(input is not null && output is not null, "Explicit original and separately named effect-checkpoint migration paths are required.");
        Assert.NotEqual(Path.GetFullPath(input!), Path.GetFullPath(output!));
        Assert.False(File.Exists(output!), "Preserve existing standalone snapshots; choose a distinct output path.");
        byte[] source = File.ReadAllBytes(input!);
        const string sourceHash = "1C5AE8FC9180276579A613604B3A6F52B4CC0FA8993A0039E658C112FD37AE6F";
        Assert.Equal(sourceHash, Convert.ToHexString(SHA256.HashData(source)));
        JsonObject document = JsonNode.Parse(source)!.AsObject();
        Assert.Equal(0, document["Frame"]!.GetValue<int>());
        Assert.Equal(512, document["Definition"]!["EncounterId"]!.GetValue<int>());
        Assert.Equal(0, document["Outcome"]!.GetValue<int>());
        Assert.Empty(document["Events"]!.AsArray());
        Assert.Empty(document["EnemyDefeats"]!.AsArray());
        Assert.Empty(document["ScriptedRewards"]!.AsArray());
        Assert.Null(document["ActiveAction"]);
        Assert.Empty(document["QueuedActions"]!.AsArray());
        Assert.False(document.ContainsKey("NativeRewardItems"));
        Assert.Null(document["NativeRewardItems"]);
        Assert.Null(document["NativeEffectMemory"]!["Textures"]);
        Assert.Null(document["NativeEffectMemory"]!["Parameter80"]);
        JsonObject original = document.DeepClone().AsObject();

        //The exact frame0 source has never produced a defeat or scripted reward; the constructor's ordered reward bank is empty.
        document["NativeRewardItems"] = new JsonArray();
        JsonObject comparison = document.DeepClone().AsObject();
        comparison.Remove("NativeRewardItems");
        Assert.True(JsonNode.DeepEquals(original, comparison), "Migration changed more than the proved empty ordered reward owner.");
        BattleSnapshot snapshot = JsonSerializer.Deserialize<BattleSnapshot>(document)!;
        BattleSession battle = BattleSession.Restore(snapshot);
        Assert.Equal(JsonSerializer.Serialize(snapshot), JsonSerializer.Serialize(battle.Capture()));
        BattleSession independent = BattleSession.Restore(battle.Capture());
        battle.Tick();
        independent.Tick();
        Assert.Equal(1, battle.Frame);
        Assert.Equal(JsonSerializer.Serialize(battle.Capture()), JsonSerializer.Serialize(independent.Capture()));
        Assert.Equal(sourceHash, Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(input!))));
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(output!))!);
        using (FileStream stream = new(output!, FileMode.CreateNew, FileAccess.Write))
        {
            JsonSerializer.Serialize(stream, document, new JsonSerializerOptions { WriteIndented = true });
        }
        Console.WriteLine($"Migrated snapshot: {Path.GetFullPath(output!)}; SHA256={Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(output!)))}; NativeRewardItems:absent->[]; frame0 load/capture and independent frame1 continuation passed.");
    }
}
