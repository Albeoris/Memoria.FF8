using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeBattleGuardianFivePairMigrationTests
{
    [Fact]
    public void MigrateOnlyNamed21StaticFivePairBankAnd209SoundThenRun207()
    {
        string? source = Environment.GetEnvironmentVariable("MEMORIA_FF8_GF_BANK2_SOURCE");
        string? output = Environment.GetEnvironmentVariable("MEMORIA_FF8_GF_BANK2_OUTPUT");
        Assert.SkipUnless(source is not null && output is not null, "The exact21 standalone and a distinct output are required.");
        Assert.False(string.Equals(Path.GetFullPath(source!), Path.GetFullPath(output!), StringComparison.OrdinalIgnoreCase));
        byte[] originalBytes = File.ReadAllBytes(source!);
        const string originalHash = "A79D5A93B2B634050C0801DCDFD058E4D67D4597A086C6C9D0967546446BA365";
        Assert.Equal(originalHash, Convert.ToHexString(SHA256.HashData(originalBytes)));
        BattleSnapshot original = JsonSerializer.Deserialize<BattleSnapshot>(originalBytes)!;
        Assert.Equal(255L, original.Frame);
        Assert.Equal(347L, original.NativeGraphics!.Callback);
        Assert.Equal((ushort)207, original.NativeGraphics.OriginalEffect!.Pools.Single(pool => pool.DescriptorAddress == 0x2521738).Slots[0].ReadWord(12));
        NativeBattleQuezacotlResources retained = original.NativeGraphics.OriginalEffect.Binding.NativeQuezacotlResources!;
        Assert.Null(retained.Frame209Sound);
        Assert.Null(retained.Ribbon!.SecondaryBank);
        using GameAssets game = new(Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR") ?? LaunchOptions.DefaultGameDirectory);
        NativeBattleQuezacotlResources authenticated = game.ReadNativeQuezacotlResources();
        Assert.Equal(JsonSerializer.Serialize(retained), JsonSerializer.Serialize(authenticated with { Frame209Sound = null, Frame277Sound = retained.Frame277Sound, Frame229FlashColor = retained.Frame229FlashColor, Paths = retained.Paths, Ribbon = authenticated.Ribbon! with { SecondaryBank = null } }));
        Assert.Equal(JsonSerializer.Serialize(retained), JsonSerializer.Serialize(original.Definition.NativeEffectCatalog[115].NativeQuezacotlResources));
        JsonNode originalNode = JsonNode.Parse(originalBytes)!;
        JsonNode migratedNode = originalNode.DeepClone();
        JsonObject catalog = migratedNode["Definition"]!["NativeEffectCatalog"]![115]!["NativeQuezacotlResources"]!.AsObject();
        JsonObject binding = migratedNode["NativeGraphics"]!["OriginalEffect"]!["Binding"]!["NativeQuezacotlResources"]!.AsObject();
        foreach (JsonObject owner in new[] { catalog, binding })
        {
            Assert.False(owner.ContainsKey("Frame209Sound"));
            Assert.False(owner["Ribbon"]!.AsObject().ContainsKey("SecondaryBank"));
            owner["Frame209Sound"] = JsonSerializer.SerializeToNode(authenticated.Frame209Sound);
            owner["Ribbon"]!["SecondaryBank"] = JsonSerializer.SerializeToNode(authenticated.Ribbon!.SecondaryBank);
        }
        JsonNode reverted = migratedNode.DeepClone();
        foreach (JsonObject owner in new[] { reverted["Definition"]!["NativeEffectCatalog"]![115]!["NativeQuezacotlResources"]!.AsObject(), reverted["NativeGraphics"]!["OriginalEffect"]!["Binding"]!["NativeQuezacotlResources"]!.AsObject() })
        {
            Assert.True(owner.Remove("Frame209Sound"));
            Assert.True(owner["Ribbon"]!.AsObject().Remove("SecondaryBank"));
        }
        Assert.True(JsonNode.DeepEquals(originalNode, reverted));
        BattleSnapshot migrated = migratedNode.Deserialize<BattleSnapshot>()!;
        BattleSession first = BattleSession.Restore(migrated);
        BattleSession second = BattleSession.Restore(JsonSerializer.Deserialize<BattleSnapshot>(JsonSerializer.Serialize(migrated))!);
        Assert.Equal(JsonSerializer.Serialize(migrated), JsonSerializer.Serialize(first.Capture()));
        TimeSpan callback = TimeSpan.FromTicks((TimeSpan.TicksPerSecond + 29) / 30);
        first.AdvanceNativeGraphicsPresentation(callback);
        second.AdvanceNativeGraphicsPresentation(callback);
        Assert.Equal(255L, first.Frame);
        Assert.Equal(348L, first.NativeGraphics!.Callback);
        Assert.Equal((ushort)208, first.NativeGraphics.OriginalEffect!.Pools.Single(pool => pool.DescriptorAddress == 0x2521738).Slots[0].ReadWord(12));
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
        Console.WriteLine($"Exact21 staticbank2/sound209 migration output={Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(output!)))}; saved255/347/207 unchanged; two actual207 callbacks reach255/348/208 with equal captures/copiedrenders and postcallbackrestore.");
    }
}
