using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeBattleGuardianPathMigrationTests
{
    [Fact]
    public void MigrateOnlyNamed24PathMetadataAndProvedCold1774ThenRun257()
    {
        string? source = Environment.GetEnvironmentVariable("MEMORIA_FF8_GF_PATH_SOURCE");
        string? output = Environment.GetEnvironmentVariable("MEMORIA_FF8_GF_PATH_OUTPUT");
        Assert.SkipUnless(source is not null && output is not null, "The exact24 standalone and a distinct output are required.");
        Assert.False(string.Equals(Path.GetFullPath(source!), Path.GetFullPath(output!), StringComparison.OrdinalIgnoreCase));
        byte[] originalBytes = File.ReadAllBytes(source!);
        const string originalHash = "866984747DD69649EAA4497BE61F8B8D0958CCE1CAB4B31D42BF24FABACC4F6C";
        Assert.Equal(originalHash, Convert.ToHexString(SHA256.HashData(originalBytes)));
        BattleSnapshot original = JsonSerializer.Deserialize<BattleSnapshot>(originalBytes)!;
        Assert.Equal(255L, original.Frame);
        Assert.Equal(397L, original.NativeGraphics!.Callback);
        Assert.Equal((ushort)257, original.NativeGraphics.OriginalEffect!.Pools.Single(pool => pool.DescriptorAddress == 0x2521738).Slots[0].ReadWord(12));
        Assert.Equal(0, original.Definition.NativeRetainedMemory!.EffectMemory!.SelectedIndex);
        Assert.Null(original.NativeEffectMemory!.QuezacotlTargetY1774);
        NativeBattleQuezacotlResources retained = original.NativeGraphics.OriginalEffect.Binding.NativeQuezacotlResources!;
        Assert.Null(retained.Paths);
        using GameAssets game = new(Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR") ?? LaunchOptions.DefaultGameDirectory);
        NativeBattleQuezacotlResources authenticated = game.ReadNativeQuezacotlResources();
        Assert.Equal(JsonSerializer.Serialize(retained), JsonSerializer.Serialize(authenticated with { Paths = null, Frame277Sound = retained.Frame277Sound }));
        Assert.Equal(JsonSerializer.Serialize(retained), JsonSerializer.Serialize(original.Definition.NativeEffectCatalog[115].NativeQuezacotlResources));
        Assert.Equal(0, game.ReadColdProcessNativeBattleMemory().EffectMemory!.QuezacotlTargetY1774);
        //Only this named first-GF cold/manual-header lineage is admitted. Entry6C3640 does not reset1774;
        //the complete native writer set is276@6C5060=-4096 and302@6C50DD=0, neither reached by concrete24.
        // No journal or general old-save inference is used.
        JsonNode originalNode = JsonNode.Parse(originalBytes)!;
        JsonNode migratedNode = originalNode.DeepClone();
        JsonObject catalog = migratedNode["Definition"]!["NativeEffectCatalog"]![115]!["NativeQuezacotlResources"]!.AsObject();
        JsonObject binding = migratedNode["NativeGraphics"]!["OriginalEffect"]!["Binding"]!["NativeQuezacotlResources"]!.AsObject();
        foreach (JsonObject owner in new[] { catalog, binding })
        {
            Assert.False(owner.ContainsKey("Paths"));
            owner["Paths"] = JsonSerializer.SerializeToNode(authenticated.Paths);
        }
        JsonObject memory = migratedNode["NativeEffectMemory"]!.AsObject();
        Assert.False(memory.ContainsKey("QuezacotlTargetY1774"));
        memory["QuezacotlTargetY1774"] = 0;
        JsonNode reverted = migratedNode.DeepClone();
        Assert.True(reverted["Definition"]!["NativeEffectCatalog"]![115]!["NativeQuezacotlResources"]!.AsObject().Remove("Paths"));
        Assert.True(reverted["NativeGraphics"]!["OriginalEffect"]!["Binding"]!["NativeQuezacotlResources"]!.AsObject().Remove("Paths"));
        Assert.True(reverted["NativeEffectMemory"]!.AsObject().Remove("QuezacotlTargetY1774"));
        Assert.True(JsonNode.DeepEquals(originalNode, reverted));
        BattleSnapshot migrated = migratedNode.Deserialize<BattleSnapshot>()!;
        BattleSession first = BattleSession.Restore(migrated);
        BattleSession second = BattleSession.Restore(JsonSerializer.Deserialize<BattleSnapshot>(JsonSerializer.Serialize(migrated))!);
        Assert.Equal(JsonSerializer.Serialize(migrated), JsonSerializer.Serialize(first.Capture()));
        TimeSpan callback = TimeSpan.FromTicks((TimeSpan.TicksPerSecond + 29) / 30);
        first.AdvanceNativeGraphicsPresentation(callback);
        second.AdvanceNativeGraphicsPresentation(callback);
        Assert.Equal(255L, first.Frame);
        Assert.Equal(398L, first.NativeGraphics!.Callback);
        Assert.Equal((ushort)258, first.NativeGraphics.OriginalEffect!.Pools.Single(pool => pool.DescriptorAddress == 0x2521738).Slots[0].ReadWord(12));
        Assert.NotEqual(original.NativeGraphics.OriginalEffect.Quezacotl!.Random17A0, first.NativeGraphics.OriginalEffect.Quezacotl!.Random17A0);
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
        Console.WriteLine($"Exact24 pathbank+cold1774 output={Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(output!)))}; saved255/397/257 unchanged; actual independent257 callbacks reach255/398/258 with equal captures/postrestore/copiedrenders. No audio/RNG history or Definition cold owner was added.");
    }
}
