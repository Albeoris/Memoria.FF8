using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeBattleTonberryCreatureMigrationTests
{
    [Fact]
    public void Migrate_only_named_Tonberry_creature_resource_then_continue_one_actual_callback()
    {
        string? source = Environment.GetEnvironmentVariable("MEMORIA_FF8_TONBERRY_MODEL_SOURCE");
        string? output = Environment.GetEnvironmentVariable("MEMORIA_FF8_TONBERRY_MODEL_OUTPUT");
        Assert.SkipUnless(source is not null && output is not null, "Select the standalone Tonberry creature boundary and a distinct migrated output.");
        Assert.False(string.Equals(Path.GetFullPath(source!), Path.GetFullPath(output!), StringComparison.OrdinalIgnoreCase));
        byte[] bytes = File.ReadAllBytes(source!);
        string sourceHash = Convert.ToHexString(SHA256.HashData(bytes));
        BattleSnapshot original = JsonSerializer.Deserialize<BattleSnapshot>(bytes)!;
        Assert.Equal(65L, original.Frame);
        Assert.Equal(145L, original.NativeGraphics!.Callback);
        NativeBattleOriginalEffectState effect = original.NativeGraphics.OriginalEffect!;
        Assert.Equal((ushort)90, effect.Binding.Effect);
        NativeBattleTonberry90Resources retained = effect.Binding.Tonberry90Resources!;
        Assert.Null(retained.CreatureModel);
        Assert.Null(retained.CreatureEntrySound);
        Assert.Null(original.NativeEffectMemory!.Tonberry90!.CreatureModelState);
        Assert.Empty(original.NativeEffectMemory.GroundCapture!.Statics.Regions);
        using GameAssets game = new(Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR") ?? TestResources.GameDirectory);
        NativeBattleTonberry90Resources authenticated = game.ReadNativeEffectCatalogForEffects([90])[89].Tonberry90Resources!;
        Assert.NotNull(authenticated.CreatureModel);
        Assert.Equal(JsonSerializer.Serialize(retained), JsonSerializer.Serialize(authenticated with { CreatureModel = null, CreatureEntrySound = null, CreatureParticleSchedules = null, CreatureProgramSource = null }));
        Assert.Equal(JsonSerializer.Serialize(retained), JsonSerializer.Serialize(original.Definition.NativeEffectCatalog[89].Tonberry90Resources));
        JsonNode originalNode = JsonNode.Parse(bytes)!;
        JsonNode migratedNode = originalNode.DeepClone();
        const string property = "CreatureModel";
        JsonObject live = migratedNode["NativeGraphics"]!["OriginalEffect"]!["Binding"]!["Tonberry90Resources"]!.AsObject();
        JsonObject catalog = migratedNode["Definition"]!["NativeEffectCatalog"]![89]!["Tonberry90Resources"]!.AsObject();
        Assert.False(live.ContainsKey(property));
        Assert.False(catalog.ContainsKey(property));
        JsonNode model = JsonSerializer.SerializeToNode(authenticated.CreatureModel)!;
        live[property] = model;
        catalog[property] = model.DeepClone();
        Assert.NotNull(authenticated.CreatureEntrySound);
        Assert.False(live.ContainsKey("CreatureEntrySound"));
        Assert.False(catalog.ContainsKey("CreatureEntrySound"));
        live["CreatureEntrySound"] = JsonSerializer.SerializeToNode(authenticated.CreatureEntrySound);
        catalog["CreatureEntrySound"] = JsonSerializer.SerializeToNode(authenticated.CreatureEntrySound);
        JsonNode reverted = migratedNode.DeepClone();
        Assert.True(reverted["NativeGraphics"]!["OriginalEffect"]!["Binding"]!["Tonberry90Resources"]!.AsObject().Remove(property));
        Assert.True(reverted["Definition"]!["NativeEffectCatalog"]![89]!["Tonberry90Resources"]!.AsObject().Remove(property));
        Assert.True(reverted["NativeGraphics"]!["OriginalEffect"]!["Binding"]!["Tonberry90Resources"]!.AsObject().Remove("CreatureEntrySound"));
        Assert.True(reverted["Definition"]!["NativeEffectCatalog"]![89]!["Tonberry90Resources"]!.AsObject().Remove("CreatureEntrySound"));
        Assert.True(JsonNode.DeepEquals(originalNode, reverted));
        BattleSnapshot migrated = migratedNode.Deserialize<BattleSnapshot>()!;
        BattleSession first = BattleSession.Restore(migrated);
        BattleSession second = BattleSession.Restore(JsonSerializer.Deserialize<BattleSnapshot>(JsonSerializer.Serialize(migrated))!);
        Assert.Equal(JsonSerializer.Serialize(migrated), JsonSerializer.Serialize(first.Capture()));
        TimeSpan callback = TimeSpan.FromTicks((TimeSpan.TicksPerSecond + 29) / 30);
        BattleSnapshot? groundProducer = null;
        BattleSnapshot? actorProducer = null;
        string directory = Path.GetDirectoryName(Path.GetFullPath(output!))!;
        Directory.CreateDirectory(directory);
        for (int tick = 0; tick < 32 && first.NativeEffectMemory!.Tonberry90!.CreatureModelState is null; tick++)
        {
            BattleSnapshot before = first.Capture();
            try
            {
                first.AdvanceNativeGraphicsPresentation(callback, TimeSpan.Zero);
                second.AdvanceNativeGraphicsPresentation(callback, TimeSpan.Zero);
            }
            catch
            {
                NativeBattleRestorationPresentationTests.Publish(directory, Path.GetFileNameWithoutExtension(output!) + ".before-boundary", before, null);
                throw;
            }
            BattleSnapshot actual = first.Capture();
            File.WriteAllText(Path.Combine(directory, Path.GetFileNameWithoutExtension(output!) + ".last-callback.battle.json"), JsonSerializer.Serialize(actual));
            Assert.Equal(JsonSerializer.Serialize(actual), JsonSerializer.Serialize(second.Capture()));
            if (groundProducer is null && before.NativeEffectMemory!.GroundCapture!.Statics.Regions.IsEmpty && !actual.NativeEffectMemory!.GroundCapture!.Statics.Regions.IsEmpty)
            {
                Assert.Contains(actual.NativeGraphics!.PrimitiveFrame!.LastCommands, command => command.Command.Packet.Bank == NativeBattleGroundCaptureMemory.PacketBank);
                Assert.DoesNotContain(actual.NativeGraphics.PrimitiveFrame.LastCommands, command => command.Command.Packet.Bank == "20DFAB8");
                groundProducer = actual;
            }
            if (actorProducer is null && actual.NativeGraphics!.OriginalEffect!.Pools[3].Slots.Any(task => task.Callback == 0x763EB0 && task.ReadDword(0x98) > 0) && actual.NativeGraphics.PrimitiveFrame!.LastCommands.Any(command => command.Command.Packet.Bank is "off_B6D070" or "off_B6D094"))
            {
                RequireCommonPackets(actual);
                actorProducer = actual;
            }
        }
        Assert.NotNull(groundProducer);
        Assert.NotNull(actorProducer);
        Assert.Equal(155L, first.NativeGraphics!.Callback);
        Assert.NotNull(first.NativeEffectMemory!.Tonberry90!.CreatureModelState);
        Assert.Contains(first.NativeGraphics.OriginalEffect!.Pools[3].Slots, task => task.Callback == 0x763EB0 && task.ReadDword(0x98) > 0);
        Assert.Equal(unchecked(0x20DFAB8u + (uint)first.NativeGraphics.OriginalEffect.DrawArenaOffset!.Value), first.NativeEffectMemory.Tonberry90.ReadDword(0x259EEA8));
        Assert.Equal(JsonSerializer.Serialize(first.Capture()), JsonSerializer.Serialize(second.Capture()));
        using BattleAssets assets = new(game.GameDirectory);
        BattleScenePresentation scene = new(assets, migrated.Definition, true);
        Assert.Equal(scene.Render(first, 320, 224).ToArray(), new BattleScenePresentation(assets, migrated.Definition, true).Render(second, 320, 224).ToArray());
        using (FileStream stream = new(output!, FileMode.CreateNew, FileAccess.Write))
        {
            JsonSerializer.Serialize(stream, migratedNode);
        }
        Assert.Equal(sourceHash, Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(source!))));
        Assert.Equal(JsonSerializer.Serialize(migrated), JsonSerializer.Serialize(BattleSession.Restore(JsonSerializer.Deserialize<BattleSnapshot>(File.ReadAllBytes(output!))!).Capture()));
        PublishProducer("ground-common", groundProducer!);
        PublishProducer("actor-common", actorProducer!);
        NativeBattleRestorationPresentationTests.Publish(directory, Path.GetFileNameWithoutExtension(output!) + ".after-constructor", first.Capture(), scene.Render(first, 320, 224).ToArray());
        Console.WriteLine($"Tonberry creature model/sound metadata-only migration source={sourceHash}; output={Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(output!)))}; frame/callback65/145 retained, independent actual constructor callback155 JSON/RGBA agree.");

        void PublishProducer(string name, BattleSnapshot snapshot)
        {
            string prefix = Path.GetFileNameWithoutExtension(output!) + "." + name;
            BattleSession restored = BattleSession.Restore(JsonSerializer.Deserialize<BattleSnapshot>(JsonSerializer.Serialize(snapshot))!);
            Assert.Equal(JsonSerializer.Serialize(snapshot), JsonSerializer.Serialize(restored.Capture()));
            NativeBattleRestorationPresentationTests.Publish(directory, prefix, snapshot, new BattleScenePresentation(assets, migrated.Definition, true).Render(restored, 320, 224).ToArray());
        }

        static void RequireCommonPackets(BattleSnapshot snapshot)
        {
            NativeBattlePrimitiveProcess process = snapshot.NativeEffectMemory!.PrimitiveProcess!;
            Assert.Equal(0, process.ByteCursor);
            Assert.Contains(snapshot.NativeGraphics!.PrimitiveFrame!.LastCommands, command => command.Command.Packet.Bank is "off_B6D070" or "off_B6D094");
            NativeBattleLogicalResolvedCommand emitted = snapshot.NativeGraphics.PrimitiveFrame.LastCommands.First(command => command.Command.Packet.Bank is "off_B6D070" or "off_B6D094");
            NativeBattlePrimitiveReference reference = emitted.Command.Packet;
            Assert.Equal(4, (process with { PacketBank = reference.Bank, PacketBuffer = reference.Buffer }).ReadCommonPacket(reference.ByteOffset, 4).Length);
            Assert.DoesNotContain(snapshot.NativeGraphics.PrimitiveFrame.LastCommands, command => command.Command.Packet.Bank == "20DFAB8");
            Assert.Equal(unchecked(0x20DFAB8u + (uint)snapshot.NativeGraphics.OriginalEffect!.DrawArenaOffset!.Value), snapshot.NativeEffectMemory.Tonberry90!.ReadDword(0x259EEA8));
        }
    }
}
