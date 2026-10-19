using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeBattleDoomtrainSkyMigrationTests
{
    [Fact]
    public void Migrate_only_named_Doomtrain_sky_resources_and_continue_the_actual_sky_factory()
    {
        string? source = Environment.GetEnvironmentVariable("MEMORIA_FF8_DOOMTRAIN_SKY_SOURCE");
        string? output = Environment.GetEnvironmentVariable("MEMORIA_FF8_DOOMTRAIN_SKY_OUTPUT");
        Assert.SkipUnless(source is not null && output is not null, "Select the standalone Doomtrain sky factory boundary and a distinct migrated output.");
        Assert.False(string.Equals(Path.GetFullPath(source!), Path.GetFullPath(output!), StringComparison.OrdinalIgnoreCase));
        byte[] bytes = File.ReadAllBytes(source!);
        string hash = Convert.ToHexString(SHA256.HashData(bytes));
        JsonNode original = JsonNode.Parse(bytes)!;
        BattleSnapshot retained = original.Deserialize<BattleSnapshot>()!;
        Assert.Equal((ushort)191, retained.NativeGraphics!.OriginalEffect!.RequestedEffect);
        NativeBattleDoomtrain191Resources resources = retained.NativeGraphics.OriginalEffect!.Binding.Doomtrain191Resources!;
        Assert.Null(resources.Sky);
        using GameAssets game = new(Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR") ?? TestResources.GameDirectory);
        NativeBattleDoomtrain191Resources authenticated = game.ReadNativeEffectCatalogForEffects([191])[190].Doomtrain191Resources!;
        Assert.NotNull(authenticated.Sky);
        Assert.Equal(0xE3496Cu, authenticated.Sky.Source.Address);
        Assert.Equal(0x590, authenticated.Sky.TargetVertices.Data.Length);
        Assert.Equal(JsonSerializer.Serialize(resources), JsonSerializer.Serialize(authenticated with { Sky = null }));
        JsonNode migrated = original.DeepClone();
        JsonObject live = migrated["NativeGraphics"]!["OriginalEffect"]!["Binding"]!["Doomtrain191Resources"]!.AsObject();
        JsonObject entry = migrated["NativeEffectMemory"]!["Entry21DFEC4"]!["Doomtrain191Resources"]!.AsObject();
        JsonObject catalog = migrated["Definition"]!["NativeEffectCatalog"]![190]!["Doomtrain191Resources"]!.AsObject();
        foreach (string property in new[] { "Sky" })
        {
            Assert.False(live.ContainsKey(property));
            Assert.False(catalog.ContainsKey(property));
            Assert.False(entry.ContainsKey(property));
            JsonNode value = JsonSerializer.SerializeToNode(authenticated.Sky)!;
            live[property] = value;
            catalog[property] = value.DeepClone();
            entry[property] = value.DeepClone();
        }
        JsonNode reverted = migrated.DeepClone();
        foreach (string property in new[] { "Sky" })
        {
            Assert.True(reverted["NativeGraphics"]!["OriginalEffect"]!["Binding"]!["Doomtrain191Resources"]!.AsObject().Remove(property));
            Assert.True(reverted["Definition"]!["NativeEffectCatalog"]![190]!["Doomtrain191Resources"]!.AsObject().Remove(property));
            Assert.True(reverted["NativeEffectMemory"]!["Entry21DFEC4"]!["Doomtrain191Resources"]!.AsObject().Remove(property));
        }
        Assert.True(JsonNode.DeepEquals(original, reverted));
        BattleSnapshot snapshot = migrated.Deserialize<BattleSnapshot>()!;
        BattleSession first = BattleSession.Restore(snapshot);
        BattleSession second = BattleSession.Restore(JsonSerializer.Deserialize<BattleSnapshot>(JsonSerializer.Serialize(snapshot))!);
        Assert.Equal(JsonSerializer.Serialize(snapshot), JsonSerializer.Serialize(first.Capture()));
        string directory = Path.GetDirectoryName(Path.GetFullPath(output!))!;
        Directory.CreateDirectory(directory);
        BattleSnapshot before = snapshot;
        TimeSpan callback = TimeSpan.FromTicks((TimeSpan.TicksPerSecond + 29) / 30);
        try
        {
            for (int tick = 0; tick < 1; tick++)
            {
                before = first.Capture();
                first.AdvanceNativeGraphicsPresentation(callback, TimeSpan.Zero);
                second.AdvanceNativeGraphicsPresentation(callback, TimeSpan.Zero);
                Assert.Equal(JsonSerializer.Serialize(first.Capture()), JsonSerializer.Serialize(second.Capture()));
            }
            Assert.Equal(2, first.NativeGraphics!.OriginalEffect!.Pools.SelectMany(pool => pool.Slots).Count(task => (task.Flags & 1) != 0 && task.Callback == 0x640580));
            for (int vertex = 0; vertex < 178; vertex++)
            {
                var beforePadding = snapshot.NativeEffectMemory!.Arena!.ReadSnapshot(0x37000 + vertex * 8 + 6, 2);
                var afterPadding = first.NativeEffectMemory!.Arena!.ReadSnapshot(0x37000 + vertex * 8 + 6, 2);
                Assert.Equal(beforePadding.Data, afterPadding.Data);
                Assert.Equal(beforePadding.Known, afterPadding.Known);
            }
            Assert.Equal(snapshot.Frame + 1, first.Frame);
            Assert.Equal(snapshot.NativeGraphics!.Callback + 1, first.NativeGraphics!.Callback);
            using BattleAssets assets = new(game.GameDirectory);
            byte[] pixels = new BattleScenePresentation(assets, snapshot.Definition, true).Render(first, 320, 224).ToArray();
            Assert.Equal(pixels, new BattleScenePresentation(assets, snapshot.Definition, true).Render(second, 320, 224).ToArray());
            using (FileStream stream = new(output!, FileMode.CreateNew, FileAccess.Write))
            {
                JsonSerializer.Serialize(stream, migrated);
            }
            NativeBattleRestorationPresentationTests.Publish(directory, Path.GetFileNameWithoutExtension(output!) + ".after-sky", first.Capture(), pixels);
            Assert.Equal(JsonSerializer.Serialize(snapshot), JsonSerializer.Serialize(BattleSession.Restore(JsonSerializer.Deserialize<BattleSnapshot>(File.ReadAllBytes(output!))!).Capture()));
            Assert.Equal(hash, Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(source!))));
            File.WriteAllText(output! + ".migration.json", JsonSerializer.Serialize(new { SourceHash = hash, SourceFrame = snapshot.Frame, ResultFrame = first.Frame, ChangedResourceProperty = "Sky", ChangedBindings = 3, ActualSkyActors = 2, IndependentJsonAndRgba = true }));
        }
        catch
        {
            NativeBattleRestorationPresentationTests.Publish(directory, Path.GetFileNameWithoutExtension(output!) + ".before-boundary", before, null);
            NativeBattleRestorationPresentationTests.Publish(directory, Path.GetFileNameWithoutExtension(output!) + ".failed-live", first.Capture(), null);
            throw;
        }
    }
}
