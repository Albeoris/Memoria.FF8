using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeBattleSirenSurfaceMigrationTests
{
    [Fact]
    public void Migrate_only_named_Siren_surface_resource_and_continue_its_actual_callbacks()
    {
        string? source = Environment.GetEnvironmentVariable("MEMORIA_FF8_SIREN_SURFACE_SOURCE");
        string? output = Environment.GetEnvironmentVariable("MEMORIA_FF8_SIREN_SURFACE_OUTPUT");
        Assert.SkipUnless(source is not null && output is not null, "Select the standalone Siren surface boundary and a distinct migrated output.");
        Assert.False(string.Equals(Path.GetFullPath(source!), Path.GetFullPath(output!), StringComparison.OrdinalIgnoreCase));
        byte[] bytes = File.ReadAllBytes(source!);
        string hash = Convert.ToHexString(SHA256.HashData(bytes));
        JsonNode original = JsonNode.Parse(bytes)!;
        BattleSnapshot retained = original.Deserialize<BattleSnapshot>()!;
        Assert.Equal(69L, retained.Frame);
        Assert.Equal(149L, retained.NativeGraphics!.Callback);
        NativeBattleSiren95Resources resources = retained.NativeGraphics.OriginalEffect!.Binding.Siren95Resources!;
        Assert.Null(resources.PhaseOneSource);
        Assert.Null(resources.PhaseOneSprites);
        using GameAssets game = new(Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR") ?? TestResources.GameDirectory);
        NativeBattleSiren95Resources authenticated = game.ReadNativeEffectCatalogForEffects([95])[94].Siren95Resources!;
        Assert.NotNull(authenticated.PhaseOneSource);
        Assert.Equal(0x16A5380u, authenticated.PhaseOneSource.Address);
        Assert.Equal(0x5A00, authenticated.PhaseOneSource.Data.Length);
        Assert.NotNull(authenticated.PhaseOneSprites);
        Assert.Equal(0x1AC, authenticated.PhaseOneSprites.Data.Length);
        Assert.Equal(JsonSerializer.Serialize(resources), JsonSerializer.Serialize(authenticated with { PhaseOneSource = null, PhaseOneSprites = null, PhaseTwoSprites = null, OverlayProgram = null, NodeSchedule = null, StaticNodeSource = null }));
        JsonNode migrated = original.DeepClone();
        JsonObject live = migrated["NativeGraphics"]!["OriginalEffect"]!["Binding"]!["Siren95Resources"]!.AsObject();
        JsonObject entry = migrated["NativeEffectMemory"]!["Entry21DFEC4"]!["Siren95Resources"]!.AsObject();
        JsonObject catalog = migrated["Definition"]!["NativeEffectCatalog"]![94]!["Siren95Resources"]!.AsObject();
        foreach (string property in new[] { "PhaseOneSource", "PhaseOneSprites" })
        {
            Assert.False(live.ContainsKey(property));
            Assert.False(catalog.ContainsKey(property));
            Assert.False(entry.ContainsKey(property));
            JsonNode value = property == "PhaseOneSource" ? JsonSerializer.SerializeToNode(authenticated.PhaseOneSource)! : JsonSerializer.SerializeToNode(authenticated.PhaseOneSprites)!;
            live[property] = value;
            catalog[property] = value.DeepClone();
            entry[property] = value.DeepClone();
        }
        JsonNode reverted = migrated.DeepClone();
        foreach (string property in new[] { "PhaseOneSource", "PhaseOneSprites" })
        {
            Assert.True(reverted["NativeGraphics"]!["OriginalEffect"]!["Binding"]!["Siren95Resources"]!.AsObject().Remove(property));
            Assert.True(reverted["Definition"]!["NativeEffectCatalog"]![94]!["Siren95Resources"]!.AsObject().Remove(property));
            Assert.True(reverted["NativeEffectMemory"]!["Entry21DFEC4"]!["Siren95Resources"]!.AsObject().Remove(property));
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
            NativeBattleModulePoolTask surface = Assert.Single(first.NativeGraphics!.OriginalEffect!.Pools.SelectMany(pool => pool.Slots), task => task.Callback == 0x73C1A0);
            Assert.Equal((byte)1, surface.ReadByte(0x29));
            Assert.Equal(1, System.Buffers.Binary.BinaryPrimitives.ReadInt32LittleEndian(surface.Payload.AsSpan().Slice(0x94 - 12 + 4)));
            Assert.Equal((ushort)4, surface.ReadWord(0x8C));
            Assert.Equal((ushort)122, surface.ReadWord(0x90));
            Assert.Contains(first.NativeEffectMemory!.Siren95!.Read(0x258E8A0, 0x188), value => value != 0);
            Assert.Equal((short)0x600, first.NativeStage!.Groups[0].ColorDepth);
            using BattleAssets assets = new(game.GameDirectory);
            byte[] pixels = new BattleScenePresentation(assets, snapshot.Definition, true).Render(first, 320, 224).ToArray();
            Assert.Equal(pixels, new BattleScenePresentation(assets, snapshot.Definition, true).Render(second, 320, 224).ToArray());
            using (FileStream stream = new(output!, FileMode.CreateNew, FileAccess.Write))
            {
                JsonSerializer.Serialize(stream, migrated);
            }
            NativeBattleRestorationPresentationTests.Publish(directory, Path.GetFileNameWithoutExtension(output!) + ".after-surface", first.Capture(), pixels);
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
