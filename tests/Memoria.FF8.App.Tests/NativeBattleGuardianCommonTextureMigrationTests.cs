using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeBattleGuardianCommonTextureMigrationTests
{
    [Fact]
    public void MigrateOnlyNamed30AuthenticatedA3MetadataThenRunTheActualAuxiliaryRequest()
    {
        string? source = Environment.GetEnvironmentVariable("MEMORIA_FF8_GF_COMMON_TEXTURE_SOURCE");
        string? output = Environment.GetEnvironmentVariable("MEMORIA_FF8_GF_COMMON_TEXTURE_OUTPUT");
        Assert.SkipUnless(source is not null && output is not null, "The exact30 standalone and a distinct output are required.");
        Assert.False(string.Equals(Path.GetFullPath(source!), Path.GetFullPath(output!), StringComparison.OrdinalIgnoreCase));
        byte[] originalBytes = File.ReadAllBytes(source!);
        const string originalHash = "32FC43314CEF1E71A0588E51C535EA6101F64694EDFC0FD87D86F084EA5A7BA2";
        Assert.Equal(originalHash, Convert.ToHexString(SHA256.HashData(originalBytes)));
        BattleSnapshot original = JsonSerializer.Deserialize<BattleSnapshot>(originalBytes)!;
        Assert.Equal(255L, original.Frame);
        Assert.Equal(496L, original.NativeGraphics!.Callback);
        Assert.Equal((ushort)355, original.NativeGraphics.OriginalEffect!.Pools.Single(pool => pool.DescriptorAddress == 0x2521738).Slots[0].ReadWord(12));
        Assert.Null(original.NativeGraphics.OriginalEffect.Pools.Single(pool => pool.DescriptorAddress == 0x2521738).Head);
        Assert.Null(original.Definition.NativeCommonEffectTexture);
        Assert.Equal((byte?)0, original.NativeEffectMemory!.QuezacotlCompletion17A8);
        NativeBattleAuxiliaryTask pending = Assert.Single(original.NativeGraphics.Auxiliary.Slots, task => (task.Flags & 1) != 0 && task.Kind == NativeBattleAuxiliaryKind.CommonTextureLoad508660);
        Assert.Equal((byte)0, pending.Payload[0]);
        using GameAssets game = new(Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR") ?? LaunchOptions.DefaultGameDirectory);
        NativeBattleEffectInitializerAsset authenticated = game.ReadNativeCommonEffectTexture();
        Assert.Equal("a8def.tim", authenticated.Name);
        Assert.NotEmpty(authenticated.Data);
        JsonNode originalNode = JsonNode.Parse(originalBytes)!;
        JsonNode migratedNode = originalNode.DeepClone();
        JsonObject definition = migratedNode["Definition"]!.AsObject();
        Assert.True(definition.ContainsKey("NativeCommonEffectTexture"));
        Assert.Null(definition["NativeCommonEffectTexture"]);
        definition["NativeCommonEffectTexture"] = JsonSerializer.SerializeToNode(authenticated);
        JsonNode reverted = migratedNode.DeepClone();
        reverted["Definition"]!["NativeCommonEffectTexture"] = null;
        Assert.True(JsonNode.DeepEquals(originalNode, reverted));
        BattleSnapshot migrated = migratedNode.Deserialize<BattleSnapshot>()!;
        BattleSession first = BattleSession.Restore(migrated);
        BattleSession second = BattleSession.Restore(JsonSerializer.Deserialize<BattleSnapshot>(JsonSerializer.Serialize(migrated))!);
        Assert.Equal(JsonSerializer.Serialize(migrated), JsonSerializer.Serialize(first.Capture()));
        ushort waitingCounter = original.NativeGraphics.OriginalEffect.Pools.Single(pool => pool.DescriptorAddress == 0x2521748).Slots[0].ReadWord(12);
        TimeSpan callback = TimeSpan.FromTicks((TimeSpan.TicksPerSecond + 29) / 30);
        first.AdvanceNativeGraphicsPresentation(callback);
        second.AdvanceNativeGraphicsPresentation(callback);
        BattleSnapshot after = first.Capture();
        Assert.Equal(255L, after.Frame);
        Assert.Equal(497L, after.NativeGraphics!.Callback);
        NativeBattleAuxiliaryTask requested = Assert.Single(after.NativeGraphics.Auxiliary.Slots, task => (task.Flags & 1) != 0 && task.Kind == NativeBattleAuxiliaryKind.CommonTextureLoad508660);
        Assert.Equal((byte)1, requested.Payload[0]);
        Assert.Equal(authenticated.Data.ToArray(), after.NativeEffectMemory!.Arena!.Read(0x2E50, authenticated.Data.Length).ToArray());
        Assert.Equal((byte?)0, after.NativeEffectMemory.QuezacotlCompletion17A8);
        Assert.Equal(waitingCounter, after.NativeGraphics.OriginalEffect!.Pools.Single(pool => pool.DescriptorAddress == 0x2521748).Slots[0].ReadWord(12));
        Assert.NotEqual(JsonSerializer.Serialize(original.NativeFileIo), JsonSerializer.Serialize(after.NativeFileIo));
        Assert.Contains(after.NativeFileIo!.Rows, row => row.FileResult == authenticated.Data.Length && row.CompletionCallback == 0x508470);
        Assert.Equal(JsonSerializer.Serialize(after), JsonSerializer.Serialize(second.Capture()));
        Assert.Equal(JsonSerializer.Serialize(after), JsonSerializer.Serialize(BattleSession.Restore(after).Capture()));
        using BattleAssets assets = new(game.GameDirectory);
        BattleScenePresentation scene = new(assets, migrated.Definition, true);
        byte[] pixels = scene.Render(first, 320, 224).ToArray();
        Assert.Equal(pixels, scene.Render(second, 320, 224).ToArray());
        using (FileStream stream = new(output!, FileMode.CreateNew, FileAccess.Write))
        {
            JsonSerializer.Serialize(stream, migratedNode);
        }
        Assert.Equal(originalHash, Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(source!))));
        Console.WriteLine($"Exact30 A3 metadata output={Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(output!)))}; only Definition.NativeCommonEffectTexture changed from null to original a8def.tim ({authenticated.Data.Length} bytes, SHA{Convert.ToHexString(SHA256.HashData(authenticated.Data.AsSpan()))}); actual auxiliary0→1/current arena write/shared482610→508470 request validated independently with unchanged main wait counter, whole states, JSON and copied pixels. No runtime or process history was migrated.");
    }
}
