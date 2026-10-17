using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeBattleGuardianSharedRandomMigrationTests
{
    [Fact]
    public void MigrateOnlyNamed27ProvedFirstGuardianSharedRandomThenRun303()
    {
        string? source = Environment.GetEnvironmentVariable("MEMORIA_FF8_GF_SHARED_RANDOM_SOURCE");
        string? output = Environment.GetEnvironmentVariable("MEMORIA_FF8_GF_SHARED_RANDOM_OUTPUT");
        Assert.SkipUnless(source is not null && output is not null, "The exact27 standalone and a distinct output are required.");
        Assert.False(string.Equals(Path.GetFullPath(source!), Path.GetFullPath(output!), StringComparison.OrdinalIgnoreCase));
        byte[] originalBytes = File.ReadAllBytes(source!);
        const string originalHash = "909EFB213C809E236D0D29D0E6CB72576CCE6969F431C209FE5148A78267FF0F";
        Assert.Equal(originalHash, Convert.ToHexString(SHA256.HashData(originalBytes)));
        BattleSnapshot original = JsonSerializer.Deserialize<BattleSnapshot>(originalBytes)!;
        Assert.Equal(255L, original.Frame);
        Assert.Equal(443L, original.NativeGraphics!.Callback);
        Assert.Equal((ushort)303, original.NativeGraphics.OriginalEffect!.Pools.Single(pool => pool.DescriptorAddress == 0x2521738).Slots[0].ReadWord(12));
        Assert.Null(original.NativeEffectMemory!.SharedEffectRandom);
        Assert.Null(original.Definition.NativeRetainedMemory!.EffectMemory!.SharedEffectRandom);
        Assert.Equal(0, original.Definition.NativeRetainedMemory.EffectMemory.SelectedIndex);
        Assert.Equal(NativeFieldRuntimeProfile.FF8EnExecutableHash, original.Definition.NativeRetainedMemory.ColdExecutableSha256);
        using GameAssets game = new(Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR") ?? LaunchOptions.DefaultGameDirectory);
        NativeBattleSharedEffectRandomState cold = Assert.IsType<NativeBattleSharedEffectRandomState>(game.ReadColdProcessNativeBattleMemory().EffectMemory!.SharedEffectRandom);
        Assert.Equal(0u, cold.State);
        //Only the exact first-guardian component lineage is eligible after its complete writer/caller proof.
        //The Definition cold copy remains unknown; this is not a restore fallback or a seed event replay.
        JsonNode originalNode = JsonNode.Parse(originalBytes)!;
        JsonNode migratedNode = originalNode.DeepClone();
        JsonObject memory = migratedNode["NativeEffectMemory"]!.AsObject();
        Assert.False(memory.ContainsKey("SharedEffectRandom"));
        memory["SharedEffectRandom"] = JsonSerializer.SerializeToNode(new NativeBattleSharedEffectRandomState(0));
        JsonNode reverted = migratedNode.DeepClone();
        Assert.True(reverted["NativeEffectMemory"]!.AsObject().Remove("SharedEffectRandom"));
        Assert.True(JsonNode.DeepEquals(originalNode, reverted));
        BattleSnapshot migrated = migratedNode.Deserialize<BattleSnapshot>()!;
        Assert.Equal(JsonSerializer.Serialize(original.Definition), JsonSerializer.Serialize(migrated.Definition));
        BattleSession first = BattleSession.Restore(migrated);
        BattleSession second = BattleSession.Restore(JsonSerializer.Deserialize<BattleSnapshot>(JsonSerializer.Serialize(migrated))!);
        Assert.Equal(JsonSerializer.Serialize(migrated), JsonSerializer.Serialize(first.Capture()));
        TimeSpan callback = TimeSpan.FromTicks((TimeSpan.TicksPerSecond + 29) / 30);
        first.AdvanceNativeGraphicsPresentation(callback);
        second.AdvanceNativeGraphicsPresentation(callback);
        Assert.Equal(255L, first.Frame);
        Assert.Equal(444L, first.NativeGraphics!.Callback);
        Assert.Equal((ushort)304, first.NativeGraphics.OriginalEffect!.Pools.Single(pool => pool.DescriptorAddress == 0x2521738).Slots[0].ReadWord(12));
        NativeBattleSharedEffectRandomState expected = new(0);
        for (int draw = 0; draw < 22; draw++)
        {
            expected = expected.Draw7059E0(out _);
        }
        Assert.Equal(expected, first.Capture().NativeEffectMemory!.SharedEffectRandom);
        Assert.Equal(JsonSerializer.Serialize(first.Capture()), JsonSerializer.Serialize(second.Capture()));
        Assert.Equal(JsonSerializer.Serialize(first.Capture()), JsonSerializer.Serialize(BattleSession.Restore(first.Capture()).Capture()));
        using BattleAssets assets = new(game.GameDirectory);
        BattleScenePresentation scene = new(assets, migrated.Definition, true);
        byte[] pixels = scene.Render(first, 320, 224).ToArray();
        Assert.Equal(pixels, scene.Render(second, 320, 224).ToArray());
        Assert.Equal(320 * 224 * 4, pixels.Length);
        using (FileStream stream = new(output!, FileMode.CreateNew, FileAccess.Write))
        {
            JsonSerializer.Serialize(stream, migratedNode);
        }
        Assert.Equal(originalHash, Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(source!))));
        Console.WriteLine($"Exact27 first-guardian SharedEffectRandom output={Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(output!)))}; only live state0 added; saved255/443/303 unchanged; independent303 callbacks reach255/444/304 with22 shared draws and equal states, postrestore and copied renders. No Definition/history/default was added.");
    }
}
