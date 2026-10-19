using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeBattleTonberryParticleMigrationTests
{
    [Fact]
    public void Migrate_only_named_Tonberry_particle_schedules_and_continue_actual_model_and_sprite_callbacks()
    {
        string? source = Environment.GetEnvironmentVariable("MEMORIA_FF8_TONBERRY_SCHEDULE_SOURCE");
        string? output = Environment.GetEnvironmentVariable("MEMORIA_FF8_TONBERRY_SCHEDULE_OUTPUT");
        Assert.SkipUnless(source is not null && output is not null, "Select the standalone Tonberry particle boundary and a distinct migrated output.");
        Assert.False(string.Equals(Path.GetFullPath(source!), Path.GetFullPath(output!), StringComparison.OrdinalIgnoreCase));
        byte[] bytes = File.ReadAllBytes(source!);
        string hash = Convert.ToHexString(SHA256.HashData(bytes));
        JsonNode original = JsonNode.Parse(bytes)!;
        BattleSnapshot retained = original.Deserialize<BattleSnapshot>()!;
        Assert.Equal(115L, retained.Frame);
        Assert.Equal(195L, retained.NativeGraphics!.Callback);
        NativeBattleTonberry90Resources resources = retained.NativeGraphics.OriginalEffect!.Binding.Tonberry90Resources!;
        Assert.NotNull(resources.CreatureModel);
        Assert.NotNull(resources.CreatureEntrySound);
        Assert.Null(resources.CreatureParticleSchedules);
        using GameAssets game = new(Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR") ?? TestResources.GameDirectory);
        NativeBattleTonberry90Resources authenticated = game.ReadNativeEffectCatalogForEffects([90])[89].Tonberry90Resources!;
        Assert.NotNull(authenticated.CreatureParticleSchedules);
        Assert.Equal(64, authenticated.CreatureParticleSchedules.Data.Length);
        Assert.Equal(JsonSerializer.Serialize(resources), JsonSerializer.Serialize(authenticated with { CreatureParticleSchedules = null, CreatureProgramSource = null }));
        JsonNode migrated = original.DeepClone();
        JsonObject live = migrated["NativeGraphics"]!["OriginalEffect"]!["Binding"]!["Tonberry90Resources"]!.AsObject();
        JsonObject catalog = migrated["Definition"]!["NativeEffectCatalog"]![89]!["Tonberry90Resources"]!.AsObject();
        const string property = "CreatureParticleSchedules";
        Assert.False(live.ContainsKey(property));
        Assert.False(catalog.ContainsKey(property));
        JsonNode value = JsonSerializer.SerializeToNode(authenticated.CreatureParticleSchedules)!;
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
            Assert.Contains(first.NativeGraphics!.OriginalEffect!.Pools.SelectMany(pool => pool.Slots), task => task.Callback == 0x767290);
            Assert.Contains(first.NativeGraphics.PrimitiveFrame!.LastCommands, command => command.Command.Packet.Bank == "20DFAB8");
            Assert.Equal(unchecked(0x20DFAB8u + (uint)first.NativeGraphics.OriginalEffect.DrawArenaOffset!.Value), first.NativeEffectMemory!.Tonberry90!.ReadDword(0x259EEA8));
            using BattleAssets assets = new(game.GameDirectory);
            byte[] pixels = new BattleScenePresentation(assets, snapshot.Definition, true).Render(first, 320, 224).ToArray();
            Assert.Equal(pixels, new BattleScenePresentation(assets, snapshot.Definition, true).Render(second, 320, 224).ToArray());
            using (FileStream stream = new(output!, FileMode.CreateNew, FileAccess.Write))
            {
                JsonSerializer.Serialize(stream, migrated);
            }
            NativeBattleRestorationPresentationTests.Publish(directory, Path.GetFileNameWithoutExtension(output!) + ".after-particles", first.Capture(), pixels);
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
