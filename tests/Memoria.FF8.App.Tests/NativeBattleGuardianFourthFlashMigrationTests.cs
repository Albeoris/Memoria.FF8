using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeBattleGuardianFourthFlashMigrationTests
{
    [Fact]
    public void MigrateOnlyNamed23FourthFlashColorThenRun227()
    {
        string? source = Environment.GetEnvironmentVariable("MEMORIA_FF8_GF_FLASH4_SOURCE");
        string? output = Environment.GetEnvironmentVariable("MEMORIA_FF8_GF_FLASH4_OUTPUT");
        Assert.SkipUnless(source is not null && output is not null, "The exact23 standalone and a distinct output are required.");
        Assert.False(string.Equals(Path.GetFullPath(source!), Path.GetFullPath(output!), StringComparison.OrdinalIgnoreCase));
        byte[] originalBytes = File.ReadAllBytes(source!);
        const string originalHash = "B4006D9EBBFE45793B14C93B0499C8EEF2EFB30BF5A3F79E0BFECD80D0F85AA4";
        Assert.Equal(originalHash, Convert.ToHexString(SHA256.HashData(originalBytes)));
        BattleSnapshot original = JsonSerializer.Deserialize<BattleSnapshot>(originalBytes)!;
        Assert.Equal(255L, original.Frame);
        Assert.Equal(367L, original.NativeGraphics!.Callback);
        Assert.Equal((ushort)227, original.NativeGraphics.OriginalEffect!.Pools.Single(pool => pool.DescriptorAddress == 0x2521738).Slots[0].ReadWord(12));
        NativeBattleQuezacotlResources retained = original.NativeGraphics.OriginalEffect.Binding.NativeQuezacotlResources!;
        Assert.Null(retained.Frame229FlashColor);
        using GameAssets game = new(Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR") ?? LaunchOptions.DefaultGameDirectory);
        NativeBattleQuezacotlResources authenticated = game.ReadNativeQuezacotlResources();
        Assert.Equal(0x00505050u, authenticated.Frame229FlashColor);
        Assert.Equal(JsonSerializer.Serialize(retained), JsonSerializer.Serialize(authenticated with { Frame229FlashColor = null, Frame277Sound = retained.Frame277Sound, Paths = retained.Paths }));
        Assert.Equal(JsonSerializer.Serialize(retained), JsonSerializer.Serialize(original.Definition.NativeEffectCatalog[115].NativeQuezacotlResources));
        const string property = "Frame229FlashColor";
        JsonNode originalNode = JsonNode.Parse(originalBytes)!;
        JsonNode migratedNode = originalNode.DeepClone();
        JsonObject catalog = migratedNode["Definition"]!["NativeEffectCatalog"]![115]!["NativeQuezacotlResources"]!.AsObject();
        JsonObject binding = migratedNode["NativeGraphics"]!["OriginalEffect"]!["Binding"]!["NativeQuezacotlResources"]!.AsObject();
        foreach (JsonObject owner in new[] { catalog, binding })
        {
            Assert.False(owner.ContainsKey(property));
            owner[property] = JsonSerializer.SerializeToNode(authenticated.Frame229FlashColor);
        }
        JsonNode reverted = migratedNode.DeepClone();
        Assert.True(reverted["Definition"]!["NativeEffectCatalog"]![115]!["NativeQuezacotlResources"]!.AsObject().Remove(property));
        Assert.True(reverted["NativeGraphics"]!["OriginalEffect"]!["Binding"]!["NativeQuezacotlResources"]!.AsObject().Remove(property));
        Assert.True(JsonNode.DeepEquals(originalNode, reverted));
        BattleSnapshot migrated = migratedNode.Deserialize<BattleSnapshot>()!;
        BattleSession first = BattleSession.Restore(migrated);
        BattleSession second = BattleSession.Restore(JsonSerializer.Deserialize<BattleSnapshot>(JsonSerializer.Serialize(migrated))!);
        Assert.Equal(JsonSerializer.Serialize(migrated), JsonSerializer.Serialize(first.Capture()));
        TimeSpan callback = TimeSpan.FromTicks((TimeSpan.TicksPerSecond + 29) / 30);
        first.AdvanceNativeGraphicsPresentation(callback);
        second.AdvanceNativeGraphicsPresentation(callback);
        Assert.Equal(255L, first.Frame);
        Assert.Equal(368L, first.NativeGraphics!.Callback);
        Assert.Equal((ushort)228, first.NativeGraphics.OriginalEffect!.Pools.Single(pool => pool.DescriptorAddress == 0x2521738).Slots[0].ReadWord(12));
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
        Console.WriteLine($"Exact23 fourth-flash metadata output={Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(output!)))}; saved255/367/227 unchanged; independent actual227 callbacks reach255/368/228 with equal captures/copiedrenders and postcallbackrestore.");
    }
}
