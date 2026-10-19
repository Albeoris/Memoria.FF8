using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeBattleSirenNodeMigrationTests
{
    [Fact]
    public void Migrate_only_named_Siren_node_resources_and_current_static_bytes_and_continue()
    {
        string? source = Environment.GetEnvironmentVariable("MEMORIA_FF8_SIREN_NODES_SOURCE");
        string? output = Environment.GetEnvironmentVariable("MEMORIA_FF8_SIREN_NODES_OUTPUT");
        Assert.SkipUnless(source is not null && output is not null, "Select the standalone Siren scheduled-node boundary and a distinct migrated output.");
        Assert.False(string.Equals(Path.GetFullPath(source!), Path.GetFullPath(output!), StringComparison.OrdinalIgnoreCase));
        byte[] bytes = File.ReadAllBytes(source!);
        string hash = Convert.ToHexString(SHA256.HashData(bytes));
        JsonNode original = JsonNode.Parse(bytes)!;
        BattleSnapshot retained = original.Deserialize<BattleSnapshot>()!;
        Assert.Equal((ushort)95, retained.NativeGraphics!.OriginalEffect!.RequestedEffect);
        NativeBattleSiren95Resources resources = retained.NativeGraphics.OriginalEffect!.Binding.Siren95Resources!;
        Assert.Null(resources.NodeSchedule);
        Assert.Null(resources.StaticNodeSource);
        using GameAssets game = new(Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR") ?? TestResources.GameDirectory);
        NativeBattleSiren95Resources authenticated = game.ReadNativeEffectCatalogForEffects([95])[94].Siren95Resources!;
        Assert.NotNull(authenticated.NodeSchedule);
        Assert.NotNull(authenticated.StaticNodeSource);
        Assert.Equal(0xBE, authenticated.NodeSchedule.Data.Length);
        Assert.Equal(0x1294, authenticated.StaticNodeSource.Data.Length);
        Assert.Equal(JsonSerializer.Serialize(resources), JsonSerializer.Serialize(authenticated with { NodeSchedule = null, StaticNodeSource = null, ParticleResources = null }));
        JsonNode migrated = original.DeepClone();
        JsonObject live = migrated["NativeGraphics"]!["OriginalEffect"]!["Binding"]!["Siren95Resources"]!.AsObject();
        JsonObject entry = migrated["NativeEffectMemory"]!["Entry21DFEC4"]!["Siren95Resources"]!.AsObject();
        JsonObject catalog = migrated["Definition"]!["NativeEffectCatalog"]![94]!["Siren95Resources"]!.AsObject();
        foreach (string property in new[] { "NodeSchedule", "StaticNodeSource" })
        {
            Assert.False(live.ContainsKey(property));
            Assert.False(catalog.ContainsKey(property));
            Assert.False(entry.ContainsKey(property));
            JsonNode value = JsonSerializer.SerializeToNode(property == "NodeSchedule" ? authenticated.NodeSchedule : authenticated.StaticNodeSource)!;
            live[property] = value;
            catalog[property] = value.DeepClone();
            entry[property] = value.DeepClone();
        }
        JsonObject module = migrated["NativeEffectMemory"]!["Siren95"]!.AsObject();
        Assert.False(module.ContainsKey("StaticNodeData"));
        Assert.False(module.ContainsKey("StaticNodeDataKnownCold"));
        Assert.DoesNotContain(retained.NativeGraphics.OriginalEffect.Pools.SelectMany(pool => pool.Slots), task => task.Callback == 0x745180);
        module["StaticNodeData"] = JsonSerializer.SerializeToNode(authenticated.StaticNodeSource.Data);
        JsonNode reverted = migrated.DeepClone();
        foreach (string property in new[] { "NodeSchedule", "StaticNodeSource" })
        {
            Assert.True(reverted["NativeGraphics"]!["OriginalEffect"]!["Binding"]!["Siren95Resources"]!.AsObject().Remove(property));
            Assert.True(reverted["Definition"]!["NativeEffectCatalog"]![94]!["Siren95Resources"]!.AsObject().Remove(property));
            Assert.True(reverted["NativeEffectMemory"]!["Entry21DFEC4"]!["Siren95Resources"]!.AsObject().Remove(property));
        }
        Assert.True(reverted["NativeEffectMemory"]!["Siren95"]!.AsObject().Remove("StaticNodeData"));
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
            Assert.Equal(snapshot.Frame + 1, first.Frame);
            Assert.Equal(snapshot.NativeGraphics!.Callback + 1, first.NativeGraphics!.Callback);
            using BattleAssets assets = new(game.GameDirectory);
            byte[] pixels = new BattleScenePresentation(assets, snapshot.Definition, true).Render(first, 320, 224).ToArray();
            Assert.Equal(pixels, new BattleScenePresentation(assets, snapshot.Definition, true).Render(second, 320, 224).ToArray());
            using (FileStream stream = new(output!, FileMode.CreateNew, FileAccess.Write))
            {
                JsonSerializer.Serialize(stream, migrated);
            }
            NativeBattleRestorationPresentationTests.Publish(directory, Path.GetFileNameWithoutExtension(output!) + ".after-nodes", first.Capture(), pixels);
            Assert.Equal(JsonSerializer.Serialize(snapshot), JsonSerializer.Serialize(BattleSession.Restore(JsonSerializer.Deserialize<BattleSnapshot>(File.ReadAllBytes(output!))!).Capture()));
            Assert.Equal(hash, Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(source!))));
        }
        catch
        {
            NativeBattleRestorationPresentationTests.Publish(directory, Path.GetFileNameWithoutExtension(output!) + ".before-boundary", before, null);
            NativeBattleRestorationPresentationTests.Publish(directory, Path.GetFileNameWithoutExtension(output!) + ".failed-live", first.Capture(), null);
            throw;
        }
    }
}
