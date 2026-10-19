using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeBattleDiablos325ConstructionContinuationTests
{
    [Fact]
    public void Named_before_creation_standalone_initializes_native_timeline_and_queues_original_files_and_sound()
    {
        string? input = Environment.GetEnvironmentVariable("MEMORIA_FF8_DIABLOS325_SNAPSHOT");
        string? evidence = Environment.GetEnvironmentVariable("MEMORIA_FF8_DIABLOS325_EVIDENCE");
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        Assert.SkipUnless(input is not null && File.Exists(input) && !string.IsNullOrWhiteSpace(evidence) && directory is not null && Directory.Exists(directory), "Select installed resources, a before-creation Diablos standalone and a distinct evidence directory.");
        string original = File.ReadAllText(input!);
        BattleSnapshot source = JsonSerializer.Deserialize<BattleSnapshot>(original)!;
        Assert.Equal((ushort)2, Root(source).ReadWord(0xC));
        Assert.Equal((byte)0, Root(source).ReadByte(0xF));
        string output = Path.Combine(evidence!, "diablos-construction-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(output);
        Console.WriteLine($"Diablos construction continuation evidence: {output}");
        using GameAssets game = new(directory!);
        JsonNode resources = JsonSerializer.SerializeToNode(game.ReadNativeDiablos325Resources())!;
        JsonNode node = JsonNode.Parse(original)!;
        List<object> changes = [];
        int owners = 0;
        AddResources(node, "");
        Assert.Equal(3, owners);
        Assert.Equal(6, changes.Count);
        string migrated = node.ToJsonString();
        File.WriteAllText(Path.Combine(output, "timeline-resources.battle.json"), migrated);
        File.WriteAllText(Path.Combine(output, "resource-migration.json"), JsonSerializer.Serialize(new { OriginalSha256 = Hash(original), MigratedSha256 = Hash(migrated), Changes = changes }));
        source = JsonSerializer.Deserialize<BattleSnapshot>(migrated)!;
        BattleSession first = BattleSession.Restore(source);
        BattleSession second = BattleSession.Restore(JsonSerializer.Deserialize<BattleSnapshot>(migrated)!);
        using BattleAssets assets = new(directory!);
        BattleScenePresentation scene = new(assets, source.Definition, true);
        TimeSpan callback = TimeSpan.FromTicks((TimeSpan.TicksPerSecond + 29) / 30);
        NativeBattleRestorationPresentationTests.Publish(output, "source", source, null);
        try
        {
            first.AdvanceNativeGraphicsPresentation(callback, TimeSpan.Zero);
            second.AdvanceNativeGraphicsPresentation(callback, TimeSpan.Zero);
            BattleSnapshot current = first.Capture();
            Assert.Equal(JsonSerializer.Serialize(current), JsonSerializer.Serialize(second.Capture()));
            byte[] pixels = scene.Render(first, 320, 224).ToArray();
            Assert.Equal(pixels, scene.Render(second, 320, 224).ToArray());
            NativeBattleRestorationPresentationTests.Publish(output, "timeline-created", current, pixels);
            Assert.Equal(new NativeBattleCameraPose(new(0, -1796, 5667), new(0, -3166, -3315), 288, 0), current.NativeCamera!.CurrentPose);
            Assert.Equal((ushort)3, Root(current).ReadWord(0xC));
            Assert.Equal((byte)1, Root(current).ReadByte(0xF));
            NativeBattleModulePoolTask timeline = current.NativeGraphics!.OriginalEffect!.Pools.Single(pool => pool.DescriptorAddress == 0x25051A0).Slots[0];
            Assert.Equal((ushort)1, timeline.ReadWord(0xC));
            Assert.Equal(0xFD8584u, timeline.ReadDword(0x264));
            NativeBattleModuleSound sound = current.Events.Last(item => item.NativeModuleSound?.Sound == 240248).NativeModuleSound!;
            Assert.Equal(1u, sound.Flags);
            Assert.Equal((byte?)128, sound.Pan);
            for (ushort counter = 1; counter <= 8; counter++)
            {
                first.AdvanceNativeGraphicsPresentation(callback, TimeSpan.Zero);
                second.AdvanceNativeGraphicsPresentation(callback, TimeSpan.Zero);
                current = first.Capture();
                Assert.Equal(JsonSerializer.Serialize(current), JsonSerializer.Serialize(second.Capture()));
                pixels = scene.Render(first, 320, 224).ToArray();
                Assert.Equal(pixels, scene.Render(second, 320, 224).ToArray());
                NativeBattleModulePoolTask flashed = current.NativeGraphics!.OriginalEffect!.Pools.Single(pool => pool.DescriptorAddress == 0x25051A0).Slots[0];
                Assert.Equal((ushort)(counter + 1), flashed.ReadWord(0xC));
                uint flash = checked(256u + (uint)(counter - 1) * 512);
                Assert.Equal(flash, current.NativeEffectMemory!.Diablos325!.Flash51E8);
                Assert.Equal(flash, current.NativeEffectMemory.Diablos325.PreviousFlash51EC);
                Assert.All(current.NativeStage!.Groups, group => Assert.Equal(unchecked((short)flash), group.ColorDepth));
                NativeBattleRestorationPresentationTests.Publish(output, counter == 8 ? "before-opening-programme" : "flash-" + counter, current, pixels);
            }
            File.WriteAllText(Path.Combine(output, "result.json"), JsonSerializer.Serialize(new { Status = "construction-and-eight-flash-callbacks-validated", current.Frame, NativeCallback = current.NativeGraphics!.Callback }));
        }
        catch
        {
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
                        owners++;
                        foreach (string name in new[] { "Files", "Sounds" })
                        {
                            Assert.False(retained.ContainsKey(name));
                            JsonNode added = resources[name]!.DeepClone();
                            retained[name] = added;
                            changes.Add(new { Path = path + "/" + entry.Key + "/" + name, Operation = "add", ValueSha256 = Hash(added.ToJsonString()) });
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
