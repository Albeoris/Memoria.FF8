using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeBattleDiablos325WingsContinuationTests
{
    [Fact]
    public void Named_paired_wings_morph_live_vertices_and_colors_and_queue_the_next_original_file()
    {
        string? input = Environment.GetEnvironmentVariable("MEMORIA_FF8_DIABLOS325_SNAPSHOT");
        string? evidence = Environment.GetEnvironmentVariable("MEMORIA_FF8_DIABLOS325_EVIDENCE");
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        Assert.SkipUnless(input is not null && File.Exists(input) && !string.IsNullOrWhiteSpace(evidence) && directory is not null && Directory.Exists(directory), "Select installed resources, a before-paired-wings Diablos standalone and a distinct evidence directory.");
        string original = File.ReadAllText(input!);
        BattleSnapshot source = JsonSerializer.Deserialize<BattleSnapshot>(original)!;
        Assert.Equal((ushort)93, Root(source).ReadWord(0xC));
        Assert.Equal((byte)1, Root(source).ReadByte(0xF));
        string output = Path.Combine(evidence!, "diablos-wings-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(output);
        Console.WriteLine($"Diablos wings continuation evidence: {output}");
        using GameAssets game = new(directory!);
        JsonNode resources = JsonSerializer.SerializeToNode(game.ReadNativeDiablos325Resources())!;
        JsonNode node = JsonNode.Parse(original)!;
        List<object> changes = [];
        int owners = 0;
        AddResources(node, "");
        Assert.Equal(3, owners);
        Assert.InRange(changes.Count, 0, 3);
        string migrated = node.ToJsonString();
        File.WriteAllText(Path.Combine(output, "programme-resources.battle.json"), migrated);
        File.WriteAllText(Path.Combine(output, "resource-migration.json"), JsonSerializer.Serialize(new { OriginalSha256 = Hash(original), MigratedSha256 = Hash(migrated), Changes = changes }));
        source = JsonSerializer.Deserialize<BattleSnapshot>(migrated)!;
        BattleSession first = BattleSession.Restore(source);
        BattleSession second = BattleSession.Restore(JsonSerializer.Deserialize<BattleSnapshot>(migrated)!);
        using BattleAssets assets = new(directory!);
        BattleScenePresentation scene = new(assets, source.Definition, true);
        TimeSpan callback = TimeSpan.FromTicks((TimeSpan.TicksPerSecond + 29) / 30);
        NativeBattleRestorationPresentationTests.Publish(output, "source", source, null);
        List<object> rows = [];
        BattleSnapshot before = source;
        try
        {
            for (ushort counter = 91; counter <= 91; counter++)
            {
                before = first.Capture();
                first.AdvanceNativeGraphicsPresentation(callback, TimeSpan.Zero);
                second.AdvanceNativeGraphicsPresentation(callback, TimeSpan.Zero);
                BattleSnapshot current = first.Capture();
                Assert.Equal(JsonSerializer.Serialize(current), JsonSerializer.Serialize(second.Capture()));
                byte[] pixels = scene.Render(first, 320, 224).ToArray();
                Assert.Equal(pixels, scene.Render(second, 320, 224).ToArray());
                NativeBattleModulePoolTask task = current.NativeGraphics!.OriginalEffect!.Pools.Single(pool => pool.DescriptorAddress == 0x25051A0).Slots[0];
                NativeBattleRestorationPresentationTests.Publish(output, counter == 91 ? "before-body-model" : "timeline-" + counter, current, pixels);
                Assert.Equal((ushort)(counter + 1), task.ReadWord(0xC));
                Assert.Equal((uint)(counter - 8), task.ReadDword(0x268));
                Assert.Equal(4096u, current.NativeEffectMemory!.Diablos325!.Flash51E8);
                Assert.Equal(4096u, current.NativeEffectMemory.Diablos325.PreviousFlash51EC);
                Assert.All(current.NativeStage!.Groups, group =>
                {
                    Assert.Equal((short)4096, group.ColorDepth);
                    Assert.Equal(0, group.Flags & 2);
                });
                Assert.Equal(source.NativeCrtRandom, current.NativeCrtRandom);
                Assert.Equal(690162, current.NativeEffectMemory.SummonStream!.Number);
                Assert.Equal((byte)0, current.NativeEffectMemory.Diablos325!.Mode51F0);
                Assert.Equal(8, System.Buffers.Binary.BinaryPrimitives.ReadInt32LittleEndian(current.NativeEffectMemory.Arena!.Read(0x4C244, 4)));
                Assert.Equal(257, System.Buffers.Binary.BinaryPrimitives.ReadInt32LittleEndian(current.NativeEffectMemory.Arena.Read(0x4C248, 4)));
                foreach (int offset in new[] { 0x14, 0x54 })
                {
                    Assert.Equal(0x20DFAB8u + 0x3505C, task.ReadDword(offset));
                    Assert.Equal(0x20DFAB8u + 0x3704C, task.ReadDword(offset + 4));
                    Assert.Equal(0x20DFAB8u + 0x3505C, task.ReadDword(offset + 8));
                }
                if (counter == 91)
                {
                    NativeBattleModuleSound[] sounds = current.Events.Skip(before.Events.Length).Where(item => item.NativeModuleSound is not null).Select(item => item.NativeModuleSound!).ToArray();
                    Assert.Equal(2, sounds.Length);
                    NativeBattleModuleSound sound = sounds[0];
                    Assert.Equal(240251u, sounds[1].Sound);
                    Assert.Equal(1u, sounds[1].Flags);
                    Assert.Equal<byte?>(128, sounds[1].Pan);
                    Assert.Equal(690162u, sound.Sound);
                    Assert.Equal<uint?>(128u, sound.RawPan);
                    Assert.Equal(127u, sound.Volume);
                    byte[] descriptor = current.NativeEffectMemory.Arena.Read(0x4C1C, 8);
                    Assert.Equal((short)5, System.Buffers.Binary.BinaryPrimitives.ReadInt16LittleEndian(descriptor));
                    Assert.Equal((short)732, System.Buffers.Binary.BinaryPrimitives.ReadInt16LittleEndian(descriptor.AsSpan(2)));
                    Assert.Equal(0x20DFAB8u + 0x1685C, System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(descriptor.AsSpan(4)));
                }
                rows.Add(new { current.Frame, NativeCallback = current.NativeGraphics.Callback, Timeline = counter + 1, JsonSha256 = Hash(JsonSerializer.Serialize(current)), PixelsSha256 = Convert.ToHexString(SHA256.HashData(pixels)), CommonCursor = current.NativeEffectMemory.PrimitiveProcess!.ByteCursor });
            }
            BattleSnapshot boundary = first.Capture();
            File.WriteAllText(Path.Combine(output, "callback-proofs.json"), JsonSerializer.Serialize(rows));
            File.WriteAllText(Path.Combine(output, "result.json"), JsonSerializer.Serialize(new { Status = "paired-wings-validated", boundary.Frame, NativeCallback = boundary.NativeGraphics!.Callback }));
        }
        catch
        {
            NativeBattleRestorationPresentationTests.Publish(output, "before-boundary", before, null);
            NativeBattleRestorationPresentationTests.Publish(output, "failed-live", first.Capture(), null);
            throw;
        }
        Assert.Equal(original, File.ReadAllText(input!));

        void AddResources(JsonNode current, string path)
        {
            if (current is JsonObject obj)
            {
                foreach (var entry in obj.ToArray())
                {
                    if (entry.Key == "Diablos325Resources" && entry.Value is JsonObject retained)
                    {
                        Assert.Equal(resources["OpeningProgramme"]!.ToJsonString(), retained["OpeningProgramme"]!.ToJsonString());
                        Assert.Equal(resources["Vibration"]!.ToJsonString(), retained["Vibration"]!.ToJsonString());
                        Assert.Equal(resources["Sounds"]!.ToJsonString(), retained["Sounds"]!.ToJsonString());
                        owners++;
                        JsonArray files = retained["Files"]!.AsArray();
                        Assert.InRange(files.Count, 5, 6);
                        for (int index = 0; index < files.Count; index++)
                        {
                            Assert.Equal(resources["Files"]![index]!.ToJsonString(), files[index]!.ToJsonString());
                        }
                        if (files.Count == 5)
                        {
                            JsonNode added = resources["Files"]![5]!.DeepClone();
                            Assert.Equal(732, added["FileId"]!.GetValue<int>());
                            files.Add(added);
                            changes.Add(new { Path = path + "/" + entry.Key + "/Files/5", Operation = "add", ValueSha256 = Hash(added.ToJsonString()) });
                        }
                    }
                    else if (entry.Value is not null)
                    {
                        AddResources(entry.Value, path + "/" + entry.Key);
                    }
                }
            }
            else if (current is JsonArray array)
            {
                for (int index = 0; index < array.Count; index++)
                {
                    if (array[index] is JsonNode value)
                    {
                        AddResources(value, path + "/" + index);
                    }
                }
            }
        }
    }

    private static NativeBattleModulePoolTask Root(BattleSnapshot snapshot) => snapshot.NativeGraphics!.OriginalEffect!.Pools.Single(pool => pool.DescriptorAddress == 0x25051B0).Slots[0];
    private static string Hash(string value) => Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(value)));
}
