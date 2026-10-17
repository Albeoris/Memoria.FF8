using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeBattleGuardianActionCatalogMigrationTests
{
    private const string OriginalHash = "F820DC30F1DADF874F9B9D68E4015508FC2E2C00B6C01A8ED540F19176FE913B";
    private static readonly TimeSpan Callback = TimeSpan.FromTicks((TimeSpan.TicksPerSecond + 29) / 30);

    [Fact]
    public void Named20CatalogOnlyStillRejectsTheUnobservedRemainingHitOwner()
    {
        string? source = Environment.GetEnvironmentVariable("MEMORIA_FF8_GF_ACTION_CATALOG_SOURCE");
        Assert.SkipUnless(source is not null, "The exact standalone20 source is required.");
        using GameAssets game = new(Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR") ?? LaunchOptions.DefaultGameDirectory);
        using BattleAssets assets = new(game.GameDirectory);
        (_, JsonNode migratedNode) = Prepare(source!, assets);
        BattleSnapshot migrated = migratedNode.Deserialize<BattleSnapshot>()!;
        BattleSession first = BattleSession.Restore(migrated);
        BattleSession second = BattleSession.Restore(JsonSerializer.Deserialize<BattleSnapshot>(JsonSerializer.Serialize(migrated))!);
        NotSupportedException firstFailure = Assert.Throws<NotSupportedException>(() => first.AdvanceNativeGraphicsPresentation(Callback));
        NotSupportedException secondFailure = Assert.Throws<NotSupportedException>(() => second.AdvanceNativeGraphicsPresentation(Callback));
        Assert.Contains("48EB40 remaining-hit", firstFailure.Message);
        Assert.Equal(firstFailure.Message, secondFailure.Message);
        Assert.Equal(JsonSerializer.Serialize(first.Capture()), JsonSerializer.Serialize(second.Capture()));
        Assert.Equal(OriginalHash, Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(source!))));
        Console.WriteLine("Named20 catalog-only correction still requires the unknown physical BD9 owner; no output is written.");
    }

    [Fact]
    public void MigrateOnlyNamed20CatalogAndProvedColdPhysicalThreeBd9ThenRunTheRealBoostCallback()
    {
        string? source = Environment.GetEnvironmentVariable("MEMORIA_FF8_GF_ACTION_CATALOG_SOURCE");
        string? output = Environment.GetEnvironmentVariable("MEMORIA_FF8_GF_ACTION_CATALOG_OUTPUT");
        Assert.SkipUnless(source is not null && output is not null, "The exact standalone20 source and a distinct output are required.");
        Assert.False(string.Equals(Path.GetFullPath(source!), Path.GetFullPath(output!), StringComparison.OrdinalIgnoreCase));
        Assert.EndsWith("-action-catalog-cold-bd9.battle.json", Path.GetFileName(output!));
        using GameAssets game = new(Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR") ?? LaunchOptions.DefaultGameDirectory);
        using BattleAssets assets = new(game.GameDirectory);
        (JsonNode originalNode, JsonNode migratedNode) = Prepare(source!, assets);
        BattleSnapshot original = originalNode.Deserialize<BattleSnapshot>()!;
        Assert.Equal(NativeFieldRuntimeProfile.FF8EnExecutableHash, original.Definition.NativeRetainedMemory!.ColdExecutableSha256);
        int targetIndex = Array.FindIndex(original.Combatants, actor => actor.NativeParticipant?.ActorIndex == 3);
        Assert.True(targetIndex >= 0);
        Assert.Null(original.Combatants[targetIndex].NativeParticipant!.RemainingHits);
        NativeBattleActionPacketsState packets = original.NativeActionPackets!;
        Assert.Equal((byte)1, packets.Headers[0].ReadByte(16));
        Assert.Single(Enumerable.Range(0, packets.Headers[0].ReadByte(16)), index => packets.ReadContact(index).Primary.ActorIndex == 3);
        Assert.Equal((byte)0, NativeBattleGuardianFixtureInputs.ReadColdPhysicalThreeRemainingHits(game));
        JsonObject targetParticipant = migratedNode["Combatants"]![targetIndex]!["NativeParticipant"]!.AsObject();
        Assert.True(targetParticipant.ContainsKey("RemainingHits"));
        Assert.Null(targetParticipant["RemainingHits"]);
        targetParticipant["RemainingHits"] = 0;
        JsonNode reverted = migratedNode.DeepClone();
        reverted["Definition"]!["NativeActions"] = originalNode["Definition"]!["NativeActions"]!.DeepClone();
        reverted["Combatants"]![targetIndex]!["NativeParticipant"]!["RemainingHits"] = null;
        Assert.True(JsonNode.DeepEquals(originalNode, reverted));
        BattleSnapshot migrated = migratedNode.Deserialize<BattleSnapshot>()!;
        BattleSession first = BattleSession.Restore(migrated);
        BattleSession second = BattleSession.Restore(JsonSerializer.Deserialize<BattleSnapshot>(JsonSerializer.Serialize(migrated))!);
        Assert.Equal(JsonSerializer.Serialize(migrated), JsonSerializer.Serialize(first.Capture()));
        first.AdvanceNativeGraphicsPresentation(Callback);
        second.AdvanceNativeGraphicsPresentation(Callback);
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
        Assert.Equal(OriginalHash, Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(source!))));
        Assert.Equal(JsonSerializer.Serialize(migrated), JsonSerializer.Serialize(BattleSession.Restore(JsonSerializer.Deserialize<BattleSnapshot>(File.ReadAllBytes(output!))!).Capture()));
        Console.WriteLine($"GF20 catalog+physical3 coldBD9 migration source={OriginalHash}; output={Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(output!)))}; saved253/333/193 unchanged; two independent callbacks reach254/334/194 and execute native Boost100 with equal copied renders and no ActiveAction.");
    }

    private static (JsonNode Original, JsonNode Migrated) Prepare(string source, BattleAssets assets)
    {
        byte[] bytes = File.ReadAllBytes(source);
        Assert.Equal(OriginalHash, Convert.ToHexString(SHA256.HashData(bytes)));
        BattleSnapshot original = JsonSerializer.Deserialize<BattleSnapshot>(bytes)!;
        Assert.Equal(253L, original.Frame);
        Assert.Equal(333L, original.NativeGraphics!.Callback);
        Assert.Equal((ushort)193, original.NativeGraphics.OriginalEffect!.Pools.Single(pool => pool.DescriptorAddress == 0x2521738).Slots[0].ReadWord(12));
        Assert.Empty(original.Definition.NativeActions);
        Assert.Null(original.ActiveAction);
        NativeBattleActionHeaderSlot header = original.NativeActionPackets!.Headers[0];
        Assert.Equal((byte)254, header.ReadByte(1));
        Assert.Equal((ushort)64, NativeBattleActionGraphics.Word(header, 4));
        BattleNativeAction retained = Assert.Single(original.Definition.ActionDefinitions, action => action.Ability == BattleAbility.GuardianForce && action.Id == 0).NativeAction!;
        BattleNativeAction authenticated = Assert.Single(assets.NativeActions, action => action.Command == 254 && action.ActionId == 64);
        Assert.Equal(JsonSerializer.Serialize(authenticated), JsonSerializer.Serialize(retained));
        Assert.Equal(132, authenticated.RawRecord.Length);
        JsonNode originalNode = JsonNode.Parse(bytes)!;
        JsonNode migratedNode = originalNode.DeepClone();
        migratedNode["Definition"]!["NativeActions"] = JsonSerializer.SerializeToNode(new[] { authenticated });
        JsonNode reverted = migratedNode.DeepClone();
        reverted["Definition"]!["NativeActions"] = originalNode["Definition"]!["NativeActions"]!.DeepClone();
        Assert.True(JsonNode.DeepEquals(originalNode, reverted));
        return (originalNode, migratedNode);
    }
}
