using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeBattleGuardianAnchorMigrationTests
{
    [Fact]
    public void MigrateOnlyNamed18AnchorMetadataAndValidateOneActual157Callback()
    {
        string? source = Environment.GetEnvironmentVariable("MEMORIA_FF8_GF_ANCHOR_MIGRATION_SOURCE");
        string? output = Environment.GetEnvironmentVariable("MEMORIA_FF8_GF_ANCHOR_MIGRATION_OUTPUT");
        Assert.SkipUnless(source is not null && output is not null, "The exact standalone18 source and a distinct output are required.");
        Assert.False(string.Equals(Path.GetFullPath(source!), Path.GetFullPath(output!), StringComparison.OrdinalIgnoreCase));
        byte[] originalBytes = File.ReadAllBytes(source!);
        const string expectedHash = "1443954EC4143F76C457F58770862A0269931DC1FEA7EB4E92B7D3A9E1E7E338";
        Assert.Equal(expectedHash, Convert.ToHexString(SHA256.HashData(originalBytes)));
        BattleSnapshot original = JsonSerializer.Deserialize<BattleSnapshot>(originalBytes)!;
        Assert.Equal(217L, original.Frame);
        Assert.Equal(297L, original.NativeGraphics!.Callback);
        NativeBattleOriginalEffectState effect = original.NativeGraphics.OriginalEffect!;
        Assert.Equal((ushort)116, effect.Binding.Effect);
        Assert.Equal((ushort)157, effect.Pools.Single(pool => pool.DescriptorAddress == 0x2521738).Slots[0].ReadWord(12));
        NativeBattleQuezacotlResources retained = effect.Binding.NativeQuezacotlResources!;
        Assert.Null(retained.AnchorVertexIndices11);
        using GameAssets game = new(Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR") ?? LaunchOptions.DefaultGameDirectory);
        NativeBattleQuezacotlResources authenticated = game.ReadNativeQuezacotlResources();
        Assert.Equal(new short[] { 93, 33, 696, 562, 578, 450, 9, 240, 388, 408, 265 }, authenticated.AnchorVertexIndices11!.Value.ToArray());
        Assert.Equal(JsonSerializer.Serialize(retained), JsonSerializer.Serialize(authenticated with { AnchorVertexIndices11 = null, Frame209Sound = retained.Frame209Sound, Frame277Sound = retained.Frame277Sound, Frame229FlashColor = retained.Frame229FlashColor, Paths = retained.Paths, Ribbon = authenticated.Ribbon! with { SecondaryBank = retained.Ribbon?.SecondaryBank } }));
        int index = effect.Binding.Effect - 1;
        NativeBattleEffectBinding catalog = original.Definition.NativeEffectCatalog[index];
        Assert.Equal(JsonSerializer.Serialize(retained), JsonSerializer.Serialize(catalog.NativeQuezacotlResources));
        const string property = "AnchorVertexIndices11";
        JsonNode originalNode = JsonNode.Parse(originalBytes)!;
        JsonNode migratedNode = originalNode.DeepClone();
        JsonObject catalogNode = migratedNode["Definition"]!["NativeEffectCatalog"]![index]!["NativeQuezacotlResources"]!.AsObject();
        JsonObject bindingNode = migratedNode["NativeGraphics"]!["OriginalEffect"]!["Binding"]!["NativeQuezacotlResources"]!.AsObject();
        Assert.False(catalogNode.ContainsKey(property));
        Assert.False(bindingNode.ContainsKey(property));
        JsonNode table = JsonSerializer.SerializeToNode(authenticated.AnchorVertexIndices11!.Value)!;
        catalogNode[property] = table;
        bindingNode[property] = table.DeepClone();
        JsonNode revertedNode = migratedNode.DeepClone();
        Assert.True(revertedNode["Definition"]!["NativeEffectCatalog"]![index]!["NativeQuezacotlResources"]!.AsObject().Remove(property));
        Assert.True(revertedNode["NativeGraphics"]!["OriginalEffect"]!["Binding"]!["NativeQuezacotlResources"]!.AsObject().Remove(property));
        Assert.True(JsonNode.DeepEquals(originalNode, revertedNode));
        BattleSnapshot migrated = migratedNode.Deserialize<BattleSnapshot>()!;
        BattleSession first = BattleSession.Restore(migrated);
        Assert.Equal(JsonSerializer.Serialize(migrated), JsonSerializer.Serialize(first.Capture()));
        BattleSession second = BattleSession.Restore(JsonSerializer.Deserialize<BattleSnapshot>(JsonSerializer.Serialize(migrated))!);
        TimeSpan callback = TimeSpan.FromTicks((TimeSpan.TicksPerSecond + 29) / 30);
        first.AdvanceNativeGraphicsPresentation(callback);
        second.AdvanceNativeGraphicsPresentation(callback);
        Assert.Equal(218L, first.Frame);
        Assert.Equal(298L, first.NativeGraphics!.Callback);
        Assert.Equal((ushort)158, first.NativeGraphics.OriginalEffect!.Pools.Single(pool => pool.DescriptorAddress == 0x2521738).Slots[0].ReadWord(12));
        Assert.Equal(JsonSerializer.Serialize(first.Capture()), JsonSerializer.Serialize(second.Capture()));
        using BattleAssets assets = new(game.GameDirectory);
        BattleScenePresentation scene = new(assets, migrated.Definition, true);
        byte[] firstPixels = scene.Render(first, 320, 224).ToArray();
        byte[] secondPixels = scene.Render(second, 320, 224).ToArray();
        Assert.Equal(firstPixels, secondPixels);
        Assert.Equal(320 * 224 * 4, firstPixels.Length);
        using (FileStream stream = new(output!, FileMode.CreateNew, FileAccess.Write))
        {
            JsonSerializer.Serialize(stream, migratedNode);
        }
        Assert.Equal(expectedHash, Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(source!))));
        Assert.Equal(JsonSerializer.Serialize(migrated), JsonSerializer.Serialize(BattleSession.Restore(JsonSerializer.Deserialize<BattleSnapshot>(File.ReadAllBytes(output!))!).Capture()));
        Console.WriteLine($"GF18 anchor-only migration source={expectedHash}; output={Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(output!)))}; saved217/297/157 unchanged; two independent one-callback runs reach218/298/158 with equal copied renders.");
    }
}
