using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeBattleTonberryProgramMigrationTests
{
    [Fact]
    public void Migrate_only_named_Tonberry_programme_resource_and_continue_its_actual_callbacks()
    {
        string? source = Environment.GetEnvironmentVariable("MEMORIA_FF8_TONBERRY_PROGRAM_SOURCE");
        string? output = Environment.GetEnvironmentVariable("MEMORIA_FF8_TONBERRY_PROGRAM_OUTPUT");
        Assert.SkipUnless(source is not null && output is not null, "Select the standalone Tonberry programme boundary and a distinct migrated output.");
        Assert.False(string.Equals(Path.GetFullPath(source!), Path.GetFullPath(output!), StringComparison.OrdinalIgnoreCase));
        byte[] bytes = File.ReadAllBytes(source!);
        string hash = Convert.ToHexString(SHA256.HashData(bytes));
        JsonNode original = JsonNode.Parse(bytes)!;
        BattleSnapshot retained = original.Deserialize<BattleSnapshot>()!;
        Assert.Equal(205L, retained.Frame);
        Assert.Equal(285L, retained.NativeGraphics!.Callback);
        NativeBattleTonberry90Resources resources = retained.NativeGraphics.OriginalEffect!.Binding.Tonberry90Resources!;
        Assert.NotNull(resources.CreatureModel);
        Assert.NotNull(resources.CreatureEntrySound);
        Assert.Null(resources.CreatureProgramSource);
        using GameAssets game = new(Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR") ?? TestResources.GameDirectory);
        NativeBattleTonberry90Resources authenticated = game.ReadNativeEffectCatalogForEffects([90])[89].Tonberry90Resources!;
        Assert.NotNull(authenticated.CreatureProgramSource);
        Assert.Equal(0x16F0F40u, authenticated.CreatureProgramSource.Address);
        Assert.Equal(0x104C, authenticated.CreatureProgramSource.Data.Length);
        Assert.Equal(JsonSerializer.Serialize(resources), JsonSerializer.Serialize(authenticated with { CreatureProgramSource = null }));
        JsonNode migrated = original.DeepClone();
        JsonObject live = migrated["NativeGraphics"]!["OriginalEffect"]!["Binding"]!["Tonberry90Resources"]!.AsObject();
        JsonObject catalog = migrated["Definition"]!["NativeEffectCatalog"]![89]!["Tonberry90Resources"]!.AsObject();
        const string property = "CreatureProgramSource";
        Assert.False(live.ContainsKey(property));
        Assert.False(catalog.ContainsKey(property));
        JsonNode value = JsonSerializer.SerializeToNode(authenticated.CreatureProgramSource)!;
        live[property] = value;
        catalog[property] = value.DeepClone();
        JsonNode reverted = migrated.DeepClone();
        Assert.True(reverted["NativeGraphics"]!["OriginalEffect"]!["Binding"]!["Tonberry90Resources"]!.AsObject().Remove(property));
        Assert.True(reverted["Definition"]!["NativeEffectCatalog"]![89]!["Tonberry90Resources"]!.AsObject().Remove(property));
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
            for (int tick = 0; tick < 3; tick++)
            {
                before = first.Capture();
                first.AdvanceNativeGraphicsPresentation(callback, TimeSpan.Zero);
                second.AdvanceNativeGraphicsPresentation(callback, TimeSpan.Zero);
                Assert.Equal(JsonSerializer.Serialize(first.Capture()), JsonSerializer.Serialize(second.Capture()));
            }
            Assert.Contains(first.NativeGraphics!.OriginalEffect!.Pools.SelectMany(pool => pool.Slots), task => task.Callback == 0x767B40 && task.ReadDword(0x98) >= 3);
            Assert.Contains(first.NativeGraphics.PrimitiveFrame!.LastCommands, command => command.Command.Packet.Bank == "20DFAB8");
            Assert.Equal(unchecked(0x20DFAB8u + (uint)first.NativeGraphics.OriginalEffect.DrawArenaOffset!.Value), first.NativeEffectMemory!.Tonberry90!.ReadDword(0x259EEA8));
            using BattleAssets assets = new(game.GameDirectory);
            byte[] pixels = new BattleScenePresentation(assets, snapshot.Definition, true).Render(first, 320, 224).ToArray();
            Assert.Equal(pixels, new BattleScenePresentation(assets, snapshot.Definition, true).Render(second, 320, 224).ToArray());
            using (FileStream stream = new(output!, FileMode.CreateNew, FileAccess.Write))
            {
                JsonSerializer.Serialize(stream, migrated);
            }
            NativeBattleRestorationPresentationTests.Publish(directory, Path.GetFileNameWithoutExtension(output!) + ".after-programme", first.Capture(), pixels);
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
