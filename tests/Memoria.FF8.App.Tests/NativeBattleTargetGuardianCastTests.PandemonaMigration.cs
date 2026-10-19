using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed partial class NativeBattleTargetGuardianCastTests
{
    [Fact]
    public void Explicitly_migrate_Pandemona_surface_resources_and_continue_the_preserved_boundary()
    {
        RunPandemonaResourceMigration("941617FBACCE075D965C5C047C01E6F36DC8BDEA98FB30A6F92DE41119A6B443", 71, 8, 18, ["SurfaceProgram", "TrailModels", "TrailTrigonometry"]);
    }

    [Fact]
    public void Explicitly_migrate_Pandemona_surface_texture_selector_and_continue_the_preserved_boundary()
    {
        RunPandemonaResourceMigration("5352FA47035D42020E55C6ADA558CD471A158816E7DD71FA2CFD8BF4AF9FF040", 79, 7, 25, ["SurfaceTextureKinds"]);
    }

    [Fact]
    public void Explicitly_migrate_Pandemona_layer_resources_and_continue_the_preserved_boundary()
    {
        RunPandemonaResourceMigration("4E620CFE47B2300C464EBB282C4A3815F16591AAB40E27E5C751350B2C5677C9", 86, 15, 40, ["LayerGeometry", "LayerTargets", "LayerSprite"]);
    }

    [Fact]
    public void Explicitly_migrate_Pandemona_camera_and_vortex_resources_and_continue_the_preserved_boundary()
    {
        RunPandemonaResourceMigration("BAA772E5170D7D588D8C213B3341DD66470CA056FB03F8382629B61D7A7483B4", 101, 44, 84, ["VortexFloorGeometry", "VortexFloorTarget"], true);
    }

    [Fact]
    public void Explicitly_migrate_Pandemona_vortex_overlay_resource_and_continue_the_preserved_boundary()
    {
        RunPandemonaResourceMigration("C68079A585A8C33C4F61E5857A4F986742B462BBC53EADEEDAAC799FD45A7407", 145, 15, 99, ["VortexOverlayGeometry"]);
    }

    [Fact]
    public void Explicitly_migrate_Pandemona_body_sound_resource_and_continue_the_preserved_boundary()
    {
        RunPandemonaResourceMigration("B6C2412F7E3DA17BD6E6767A95C06EEEC2CDB248FA8EA3E56698C3CAE6CDFCBD", 160, 1, 100, ["BodySound"]);
    }

    [Fact]
    public void Continue_the_preserved_Pandemona_body_boundary_to_its_next_children()
    {
        RunPandemonaResourceMigration("7FB33D3940D52328889B72F1EDC18DB1636A069BC0FAAD5CC0D8D7DDCFF3C1BD", 161, 54, 154, ["RisingProgram"]);
    }

    private static void RunPandemonaResourceMigration(string sourceHash, int sourceFrame, int callbacks, ushort nextAge, string[] fields, bool migrateCamera = false)
    {
        string? source = Environment.GetEnvironmentVariable("MEMORIA_FF8_PANDEMONA_MIGRATION_SOURCE");
        string? output = Environment.GetEnvironmentVariable("MEMORIA_FF8_PANDEMONA_MIGRATION_OUTPUT");
        Assert.SkipUnless(source is not null && output is not null, "Select the preserved Pandemona boundary and a distinct migration evidence directory.");
        Assert.False(Directory.Exists(output!));
        byte[] originalBytes = File.ReadAllBytes(source!);
        Assert.Equal(sourceHash, Convert.ToHexString(SHA256.HashData(originalBytes)));
        JsonObject document = JsonNode.Parse(originalBytes)!.AsObject();
        JsonNode original = document.DeepClone();
        Assert.Equal(sourceFrame, document["Frame"]!.GetValue<int>());
        using GameAssets game = new(Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR") ?? TestResources.GameDirectory);
        using BattleAssets assets = new(game.GameDirectory);
        NativeBattlePandemona291Resources resources = game.ReadNativePandemona291Resources();
        JsonObject installed = JsonSerializer.SerializeToNode(resources)!.AsObject();
        JsonObject active = document["NativeGraphics"]!["OriginalEffect"]!["Binding"]!["Pandemona291Resources"]!.AsObject();
        JsonObject entry = document["NativeEffectMemory"]!["Entry21DFEC4"]!["Pandemona291Resources"]!.AsObject();
        JsonArray catalog = document["Definition"]!["NativeEffectCatalog"]!.AsArray();
        int catalogIndex = Enumerable.Range(0, catalog.Count).Single(index => catalog[index]!["Effect"]!.GetValue<int>() == 291);
        JsonObject definition = catalog[catalogIndex]!["Pandemona291Resources"]!.AsObject();
        string[] owners = ["NativeGraphics.OriginalEffect.Binding.Pandemona291Resources", "NativeEffectMemory.Entry21DFEC4.Pandemona291Resources", $"Definition.NativeEffectCatalog[{catalogIndex}].Pandemona291Resources"];
        JsonObject[] bundles = [active, entry, definition];
        foreach (JsonObject bundle in bundles)
        {
            foreach (string field in fields)
            {
                Assert.False(bundle.ContainsKey(field));
                bundle[field] = installed[field]!.DeepClone();
            }
        }
        JsonNode originalCamera = document["NativeCamera"]!.DeepClone();
        JsonNode originalArena = document["NativeEffectMemory"]!["Arena"]!.DeepClone();
        JsonNode originalPools = document["NativeGraphics"]!["OriginalEffect"]!["Pools"]!.DeepClone();
        if (migrateCamera)
        {
            BattleSnapshot prior = JsonSerializer.Deserialize<BattleSnapshot>(original)!;
            int center = prior.NativeEffectMemory!.Pandemona291!.Center25562E8!.Value;
            NativeBattleCameraState camera = prior.NativeCamera!;
            NativeBattleCameraState corrected = camera.WriteEffectGlobals(NativeBattlePandemona291Timeline.FinalOpeningCamera39(center, camera.Roll)).PublishProjection(prior.NativeGraphics!.Flags);
            JsonObject correctedCamera = JsonSerializer.SerializeToNode(corrected)!.AsObject();
            JsonObject currentCamera = document["NativeCamera"]!.AsObject();
            foreach (string field in new[] { "CurrentPose", "FocalLength", "Projection" })
            {
                currentCamera[field] = correctedCamera[field]!.DeepClone();
            }
            Assert.True(JsonNode.DeepEquals(correctedCamera, currentCamera));
            JsonObject master = document["NativeGraphics"]!["OriginalEffect"]!["Pools"]!.AsArray().Single(pool => pool!["DescriptorAddress"]!.GetValue<uint>() == 0x25562B8)!["Slots"]![0]!.AsObject();
            Assert.Equal((ushort)42, prior.NativeGraphics!.OriginalEffect!.Pools.Single(pool => pool.DescriptorAddress == 0x25562B8).Slots[0].ReadWord(0xC));
            Assert.Equal((ushort)40, prior.NativeGraphics.OriginalEffect.Pools.Single(pool => pool.DescriptorAddress == 0x25562A8).Slots[0].ReadWord(0xC));
            Assert.Equal(0u, prior.NativeEffectMemory.Pandemona291.Pause2556258);
            Assert.Equal(1u, prior.NativeGraphics.OriginalEffect.Pools.Single(pool => pool.DescriptorAddress == 0x25562B8).Slots[0].ReadDword(0x10));
            for (int index = 4; index < 8; index++)
            {
                master["Payload"]![index] = 0;
            }
            NativeBattleEffectFileAsset file = resources.Files.Single(file => file.FileId == 722);
            Assert.Equal(76616, file.Data.Length);
            Assert.Equal("E403EB4C8DD3C491A75B7179CD662ACCEA3743C62C64117C6B4DA33130B31A21", Convert.ToHexString(SHA256.HashData(file.Data.AsSpan())));
            NativeBattleEffectArena arena = prior.NativeEffectMemory.Arena!;
            Assert.Equal(new byte[] { 62, 94, 0, 0, 0 }, arena.Read(0x231D4, 5));
            NativeBattleEffectArena repaired = arena.Write(0x231D4, new byte[] { 0, 0, 1, 4, 4 }).Write(0x31220, file.Data.AsSpan());
            var beforeBytes = arena.ReadSnapshot(0, NativeBattleEffectArena.Length);
            var afterBytes = repaired.ReadSnapshot(0, NativeBattleEffectArena.Length);
            new byte[] { 0, 0, 1, 4, 4 }.CopyTo(beforeBytes.Data, 0x231D4);
            file.Data.AsSpan().CopyTo(beforeBytes.Data.AsSpan(0x31220));
            Array.Fill(beforeBytes.Known, true, 0x231D4, 5);
            Array.Fill(beforeBytes.Known, true, 0x31220, file.Data.Length);
            Assert.Equal(beforeBytes.Data, afterBytes.Data);
            Assert.Equal(beforeBytes.Known, afterBytes.Known);
            document["NativeEffectMemory"]!["Arena"] = JsonSerializer.SerializeToNode(repaired);
        }
        JsonObject reverted = document.DeepClone().AsObject();
        reverted["NativeCamera"] = originalCamera.DeepClone();
        reverted["NativeEffectMemory"]!["Arena"] = originalArena.DeepClone();
        reverted["NativeGraphics"]!["OriginalEffect"]!["Pools"] = originalPools.DeepClone();
        JsonObject[] revertedBundles = [reverted["NativeGraphics"]!["OriginalEffect"]!["Binding"]!["Pandemona291Resources"]!.AsObject(), reverted["NativeEffectMemory"]!["Entry21DFEC4"]!["Pandemona291Resources"]!.AsObject(), reverted["Definition"]!["NativeEffectCatalog"]![catalogIndex]!["Pandemona291Resources"]!.AsObject()];
        foreach (JsonObject bundle in revertedBundles)
        {
            foreach (string field in fields)
            {
                Assert.True(bundle.Remove(field));
            }
        }
        Assert.True(JsonNode.DeepEquals(original, reverted));
        Directory.CreateDirectory(output!);
        string startingName = fields.Length == 0 && !migrateCamera ? "source.battle.json" : "migrated.battle.json";
        File.WriteAllText(Path.Combine(output!, startingName), document.ToJsonString());
        BattleSnapshot snapshot = JsonSerializer.Deserialize<BattleSnapshot>(document)!;
        BattleSession live = BattleSession.Restore(snapshot);
        BattleSession independent = BattleSession.Restore(JsonSerializer.Deserialize<BattleSnapshot>(File.ReadAllBytes(Path.Combine(output!, startingName)))!);
        Assert.Equal(JsonSerializer.Serialize(snapshot), JsonSerializer.Serialize(live.Capture()));
        Assert.Equal(JsonSerializer.Serialize(live.Capture()), JsonSerializer.Serialize(independent.Capture()));
        BattleScenePresentation scene = new(assets, snapshot.Definition, true);
        TimeSpan callback = TimeSpan.FromTicks((TimeSpan.TicksPerSecond + 29) / 30);
        for (int index = 0; index < callbacks; index++)
        {
            BattleSnapshot before = live.Capture();
            try
            {
                live.AdvanceNativeGraphicsPresentation(callback, TimeSpan.Zero);
                independent.AdvanceNativeGraphicsPresentation(callback, TimeSpan.Zero);
            }
            catch (Exception error)
            {
                NativeBattleRestorationPresentationTests.Publish(output!, "before-boundary", before, null);
                File.WriteAllText(Path.Combine(output!, "result.json"), JsonSerializer.Serialize(new { Status = "blocked", before.Frame, Error = error.ToString() }));
                throw;
            }
            Assert.Equal(JsonSerializer.Serialize(live.Capture()), JsonSerializer.Serialize(independent.Capture()));
            Assert.Equal(scene.Render(live, 320, 224).ToArray(), scene.Render(independent, 320, 224).ToArray());
            if (index == 0)
            {
                NativeBattleRestorationPresentationTests.Publish(output!, "after-minimum-callback", live.Capture(), scene.Render(live, 320, 224).ToArray());
            }
        }
        BattleSnapshot boundary = live.Capture();
        Assert.Equal(nextAge, boundary.NativeGraphics!.OriginalEffect!.Pools.Single(pool => pool.DescriptorAddress == 0x25562A8).Slots[0].ReadWord(0xC));
        string boundaryName = nextAge == 114 ? "before-rising-children" : nextAge == 100 ? "before-model-renderer" : nextAge == 99 ? "before-next-model-scene" : nextAge == 84 ? "before-late-vortex-overlay" : nextAge == 18 ? "before-surface-animation" : nextAge == 25 ? "before-animated-layers" : "before-next-scene";
        NativeBattleRestorationPresentationTests.Publish(output!, boundaryName, boundary, scene.Render(live, 320, 224).ToArray());
        Assert.Equal(sourceHash, Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(source!))));
        File.WriteAllText(Path.Combine(output!, "result.json"), JsonSerializer.Serialize(new { Status = migrateCamera ? "camera-and-resources-migration-and-callbacks-validated" : fields.Length == 0 ? "standalone-continuation-validated" : "resources-only-migration-and-callbacks-validated", SourceHash = sourceHash, Changes = owners.SelectMany(owner => fields.Select(field => owner + "." + field)).Concat(migrateCamera ? new[] { "NativeCamera.CurrentPose", "NativeCamera.FocalLength", "NativeCamera.Projection", "NativeGraphics.OriginalEffect.Pools[descriptor25562B8].Slots[0].Payload[4..8]", "NativeEffectMemory.Arena[231D4..231D9]", "NativeEffectMemory.Arena[31220..43D68]" } : Array.Empty<string>()).ToArray(), RevertEqualsOriginal = true, Callbacks = callbacks, boundary.Frame, NativeCallback = boundary.NativeGraphics!.Callback, Boundary = boundaryName }));
    }
}
