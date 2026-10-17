using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeBattleGuardianExecutableWorkspaceMigrationTests
{
    [Fact]
    public void MigrateOnlyNamed26AuthenticatedSoundMetadataThenRun277()
    {
        string? source = Environment.GetEnvironmentVariable("MEMORIA_FF8_GF_WORKSPACE_SOURCE");
        string? output = Environment.GetEnvironmentVariable("MEMORIA_FF8_GF_WORKSPACE_OUTPUT");
        Assert.SkipUnless(source is not null && output is not null, "The exact26 standalone and a distinct output are required.");
        Assert.False(string.Equals(Path.GetFullPath(source!), Path.GetFullPath(output!), StringComparison.OrdinalIgnoreCase));
        byte[] originalBytes = File.ReadAllBytes(source!);
        const string originalHash = "0083BD173765E6A7F582FE0F4262BF427C70F812C5FC962033051C737312A187";
        Assert.Equal(originalHash, Convert.ToHexString(SHA256.HashData(originalBytes)));
        BattleSnapshot original = JsonSerializer.Deserialize<BattleSnapshot>(originalBytes)!;
        Assert.Equal(255L, original.Frame);
        Assert.Equal(417L, original.NativeGraphics!.Callback);
        Assert.Equal((ushort)277, original.NativeGraphics.OriginalEffect!.Pools.Single(pool => pool.DescriptorAddress == 0x2521738).Slots[0].ReadWord(12));
        NativeBattleQuezacotlResources retained = original.NativeGraphics.OriginalEffect.Binding.NativeQuezacotlResources!;
        Assert.Null(retained.Frame277Sound);
        using GameAssets game = new(Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR") ?? TestResources.GameDirectory);
        NativeBattleQuezacotlResources authenticated = game.ReadNativeQuezacotlResources();
        Assert.Equal((uint?)240039, authenticated.Frame277Sound);
        Assert.Equal(JsonSerializer.Serialize(retained), JsonSerializer.Serialize(authenticated with { Frame277Sound = null }));
        Assert.Equal(JsonSerializer.Serialize(retained), JsonSerializer.Serialize(original.Definition.NativeEffectCatalog[115].NativeQuezacotlResources));
        JsonNode originalNode = JsonNode.Parse(originalBytes)!;
        JsonNode migratedNode = originalNode.DeepClone();
        foreach (JsonObject owner in new[]
        {
            migratedNode["Definition"]!["NativeEffectCatalog"]![115]!["NativeQuezacotlResources"]!.AsObject(),
            migratedNode["NativeGraphics"]!["OriginalEffect"]!["Binding"]!["NativeQuezacotlResources"]!.AsObject()
        })
        {
            Assert.False(owner.ContainsKey("Frame277Sound"));
            owner["Frame277Sound"] = authenticated.Frame277Sound!.Value;
        }
        JsonNode reverted = migratedNode.DeepClone();
        Assert.True(reverted["Definition"]!["NativeEffectCatalog"]![115]!["NativeQuezacotlResources"]!.AsObject().Remove("Frame277Sound"));
        Assert.True(reverted["NativeGraphics"]!["OriginalEffect"]!["Binding"]!["NativeQuezacotlResources"]!.AsObject().Remove("Frame277Sound"));
        Assert.True(JsonNode.DeepEquals(originalNode, reverted));
        BattleSnapshot migrated = migratedNode.Deserialize<BattleSnapshot>()!;
        BattleSession first = BattleSession.Restore(migrated);
        BattleSession second = BattleSession.Restore(JsonSerializer.Deserialize<BattleSnapshot>(JsonSerializer.Serialize(migrated))!);
        Assert.Equal(JsonSerializer.Serialize(migrated), JsonSerializer.Serialize(first.Capture()));
        TimeSpan callback = TimeSpan.FromTicks((TimeSpan.TicksPerSecond + 29) / 30);
        first.AdvanceNativeGraphicsPresentation(callback);
        second.AdvanceNativeGraphicsPresentation(callback);
        Assert.Equal(255L, first.Frame);
        Assert.Equal(418L, first.NativeGraphics!.Callback);
        Assert.Equal((ushort)278, first.NativeGraphics.OriginalEffect!.Pools.Single(pool => pool.DescriptorAddress == 0x2521738).Slots[0].ReadWord(12));
        Assert.Single(first.Events.Skip(migrated.Events.Length), item => item.NativeModuleSound is { Sound: 240039, Flags: 1, Pan: 128 });
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
        Console.WriteLine($"Exact26 authenticated Frame277Sound metadata output={Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(output!)))}; saved255/417/277 unchanged; independent actual277 callbacks reach255/418/278 with equal captures, postrestore and copied renders. No runtime workspace bytes or process history added.");
    }
}
