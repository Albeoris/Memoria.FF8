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

    private static void RunPandemonaResourceMigration(string sourceHash, int sourceFrame, int callbacks, ushort nextAge, string[] fields)
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
        JsonObject reverted = document.DeepClone().AsObject();
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
        File.WriteAllText(Path.Combine(output!, "migrated.battle.json"), document.ToJsonString());
        BattleSnapshot snapshot = JsonSerializer.Deserialize<BattleSnapshot>(document)!;
        BattleSession live = BattleSession.Restore(snapshot);
        BattleSession independent = BattleSession.Restore(JsonSerializer.Deserialize<BattleSnapshot>(File.ReadAllBytes(Path.Combine(output!, "migrated.battle.json")))!);
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
        string boundaryName = nextAge == 18 ? "before-surface-animation" : "before-animated-layers";
        NativeBattleRestorationPresentationTests.Publish(output!, boundaryName, boundary, scene.Render(live, 320, 224).ToArray());
        if (nextAge == 25)
        {
            NotSupportedException next = Assert.Throws<NotSupportedException>(() => live.AdvanceNativeGraphicsPresentation(callback, TimeSpan.Zero));
            Assert.Contains("6F2A30", next.Message);
            NotSupportedException resumed = Assert.Throws<NotSupportedException>(() => independent.AdvanceNativeGraphicsPresentation(callback, TimeSpan.Zero));
            Assert.Equal(next.Message, resumed.Message);
        }
        Assert.Equal(sourceHash, Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(source!))));
        File.WriteAllText(Path.Combine(output!, "result.json"), JsonSerializer.Serialize(new { Status = "resources-only-migration-and-callbacks-validated", SourceHash = sourceHash, Changes = owners.SelectMany(owner => fields.Select(field => owner + "." + field)).ToArray(), RevertEqualsOriginal = true, Callbacks = callbacks, boundary.Frame, NativeCallback = boundary.NativeGraphics!.Callback, Boundary = boundaryName }));
    }
}
