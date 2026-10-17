using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeBattleGuardianActionCatalogMigrationTests
{
    [Fact]
    public void MigrateOnlyNamed20RequiredGuardianKernelCatalogAndRunItsRealBoostCallback()
    {
        string? source = Environment.GetEnvironmentVariable("MEMORIA_FF8_GF_ACTION_CATALOG_SOURCE");
        string? output = Environment.GetEnvironmentVariable("MEMORIA_FF8_GF_ACTION_CATALOG_OUTPUT");
        Assert.SkipUnless(source is not null && output is not null, "The exact standalone20 source and a distinct output are required.");
        Assert.False(string.Equals(Path.GetFullPath(source!), Path.GetFullPath(output!), StringComparison.OrdinalIgnoreCase));
        byte[] originalBytes = File.ReadAllBytes(source!);
        const string expectedHash = "F820DC30F1DADF874F9B9D68E4015508FC2E2C00B6C01A8ED540F19176FE913B";
        Assert.Equal(expectedHash, Convert.ToHexString(SHA256.HashData(originalBytes)));
        BattleSnapshot original = JsonSerializer.Deserialize<BattleSnapshot>(originalBytes)!;
        Assert.Equal(253L, original.Frame);
        Assert.Equal(333L, original.NativeGraphics!.Callback);
        Assert.Equal((ushort)193, original.NativeGraphics.OriginalEffect!.Pools.Single(pool => pool.DescriptorAddress == 0x2521738).Slots[0].ReadWord(12));
        Assert.Empty(original.Definition.NativeActions);
        Assert.Null(original.ActiveAction);
        NativeBattleActionHeaderSlot header = original.NativeActionPackets!.Headers[0];
        Assert.Equal((byte)254, header.ReadByte(1));
        Assert.Equal((ushort)64, NativeBattleActionGraphics.Word(header, 4));
        BattleNativeAction retained = Assert.Single(original.Definition.ActionDefinitions, action => action.Ability == BattleAbility.GuardianForce && action.Id == 0).NativeAction!;
        using GameAssets game = new(Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR") ?? LaunchOptions.DefaultGameDirectory);
        using BattleAssets assets = new(game.GameDirectory);
        BattleNativeAction authenticated = Assert.Single(assets.NativeActions, action => action.Command == 254 && action.ActionId == 64);
        Assert.Equal(JsonSerializer.Serialize(authenticated), JsonSerializer.Serialize(retained));
        Assert.Equal(132, authenticated.RawRecord.Length);
        JsonNode originalNode = JsonNode.Parse(originalBytes)!;
        JsonNode migratedNode = originalNode.DeepClone();
        migratedNode["Definition"]!["NativeActions"] = JsonSerializer.SerializeToNode(new[] { authenticated });
        JsonNode reverted = migratedNode.DeepClone();
        reverted["Definition"]!["NativeActions"] = originalNode["Definition"]!["NativeActions"]!.DeepClone();
        Assert.True(JsonNode.DeepEquals(originalNode, reverted));
        BattleSnapshot migrated = migratedNode.Deserialize<BattleSnapshot>()!;
        BattleSession first = BattleSession.Restore(migrated);
        BattleSession second = BattleSession.Restore(JsonSerializer.Deserialize<BattleSnapshot>(JsonSerializer.Serialize(migrated))!);
        Assert.Equal(JsonSerializer.Serialize(migrated), JsonSerializer.Serialize(first.Capture()));
        TimeSpan callback = TimeSpan.FromTicks((TimeSpan.TicksPerSecond + 29) / 30);
        first.AdvanceNativeGraphicsPresentation(callback);
        second.AdvanceNativeGraphicsPresentation(callback);
        Assert.Equal(254L, first.Frame);
        Assert.Equal(334L, first.NativeGraphics!.Callback);
        Assert.Equal((ushort)194, first.NativeGraphics.OriginalEffect!.Pools.Single(pool => pool.DescriptorAddress == 0x2521738).Slots[0].ReadWord(12));
        Assert.Null(first.ActiveAction);
        Assert.True(first.Capture().NativeActionScratch!.GuardianFormulaE1E);
        Assert.Equal((byte)100, first.Capture().NativeActionScratch!.GuardianBoostPercent);
        Assert.Equal((byte)6, first.NativeGuardianBoost!.Phase);
        Assert.Equal(JsonSerializer.Serialize(first.Capture()), JsonSerializer.Serialize(second.Capture()));
        BattleScenePresentation scene = new(assets, migrated.Definition, true);
        byte[] pixels = scene.Render(first, 320, 224).ToArray();
        Assert.Equal(pixels, scene.Render(second, 320, 224).ToArray());
        Assert.Equal(320 * 224 * 4, pixels.Length);
        using (FileStream stream = new(output!, FileMode.CreateNew, FileAccess.Write))
        {
            JsonSerializer.Serialize(stream, migratedNode);
        }
        Assert.Equal(expectedHash, Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(source!))));
        Assert.Equal(JsonSerializer.Serialize(migrated), JsonSerializer.Serialize(BattleSession.Restore(JsonSerializer.Deserialize<BattleSnapshot>(File.ReadAllBytes(output!))!).Capture()));
        Console.WriteLine($"GF20 catalog-only migration source={expectedHash}; output={Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(output!)))}; saved253/333/193 unchanged; two independent callbacks reach254/334/194 and execute native Boost100 with equal copied renders and no ActiveAction.");
    }
}
