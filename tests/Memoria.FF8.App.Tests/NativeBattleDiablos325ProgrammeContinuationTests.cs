using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeBattleDiablos325ProgrammeContinuationTests
{
    [Fact]
    public void Named_opening_programme_standalone_preserves_native_packets_camera_and_ordered_IO_through_thirty()
    {
        string? input = Environment.GetEnvironmentVariable("MEMORIA_FF8_DIABLOS325_SNAPSHOT");
        string? evidence = Environment.GetEnvironmentVariable("MEMORIA_FF8_DIABLOS325_EVIDENCE");
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        Assert.SkipUnless(input is not null && File.Exists(input) && !string.IsNullOrWhiteSpace(evidence) && directory is not null && Directory.Exists(directory), "Select installed resources, a before-programme Diablos standalone and a distinct evidence directory.");
        string original = File.ReadAllText(input!);
        BattleSnapshot source = JsonSerializer.Deserialize<BattleSnapshot>(original)!;
        Assert.Equal((ushort)11, Root(source).ReadWord(0xC));
        Assert.Equal((byte)1, Root(source).ReadByte(0xF));
        string output = Path.Combine(evidence!, "diablos-programme-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(output);
        Console.WriteLine($"Diablos programme continuation evidence: {output}");
        using GameAssets game = new(directory!);
        JsonNode resources = JsonSerializer.SerializeToNode(game.ReadNativeDiablos325Resources())!;
        JsonNode node = JsonNode.Parse(original)!;
        List<object> changes = [];
        int owners = 0;
        AddResources(node, "");
        Assert.Equal(3, owners);
        Assert.InRange(changes.Count, 0, 9);
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
            for (ushort counter = 9; counter <= 30; counter++)
            {
                before = first.Capture();
                first.AdvanceNativeGraphicsPresentation(callback, TimeSpan.Zero);
                second.AdvanceNativeGraphicsPresentation(callback, TimeSpan.Zero);
                BattleSnapshot current = first.Capture();
                Assert.Equal(JsonSerializer.Serialize(current), JsonSerializer.Serialize(second.Capture()));
                byte[] pixels = scene.Render(first, 320, 224).ToArray();
                Assert.Equal(pixels, scene.Render(second, 320, 224).ToArray());
                NativeBattleModulePoolTask task = current.NativeGraphics!.OriginalEffect!.Pools.Single(pool => pool.DescriptorAddress == 0x25051A0).Slots[0];
                NativeBattleRestorationPresentationTests.Publish(output, counter == 30 ? "before-well-model" : "timeline-" + counter, current, pixels);
                Assert.Equal((ushort)(counter + 1), task.ReadWord(0xC));
                Assert.Equal((uint)(counter - 8), task.ReadDword(0x268));
                Assert.Equal(4096u, current.NativeEffectMemory!.Diablos325!.Flash51E8);
                Assert.Equal(4096u, current.NativeEffectMemory.Diablos325.PreviousFlash51EC);
                Assert.All(current.NativeStage!.Groups, group =>
                {
                    Assert.Equal((short)4096, group.ColorDepth);
                    Assert.Equal(0, group.Flags & 2);
                });
                rows.Add(new { current.Frame, NativeCallback = current.NativeGraphics.Callback, Timeline = counter + 1, JsonSha256 = Hash(JsonSerializer.Serialize(current)), PixelsSha256 = Convert.ToHexString(SHA256.HashData(pixels)), CommonCursor = current.NativeEffectMemory.PrimitiveProcess!.ByteCursor });
            }
            BattleSnapshot boundary = first.Capture();
            File.WriteAllText(Path.Combine(output, "callback-proofs.json"), JsonSerializer.Serialize(rows));
            NotSupportedException error = Assert.Throws<NotSupportedException>(() => first.AdvanceNativeGraphicsPresentation(callback, TimeSpan.Zero));
            NotSupportedException independent = Assert.Throws<NotSupportedException>(() => second.AdvanceNativeGraphicsPresentation(callback, TimeSpan.Zero));
            Assert.Contains("65473A", error.Message);
            Assert.Equal(error.Message, independent.Message);
            Assert.Equal(JsonSerializer.Serialize(first.Capture()), JsonSerializer.Serialize(second.Capture()));
            File.WriteAllText(Path.Combine(output, "result.json"), JsonSerializer.Serialize(new { Status = "opening-programme-validated", boundary.Frame, NativeCallback = boundary.NativeGraphics!.Callback, Boundary = error.Message }));
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
                        Assert.Equal(resources["Files"]!.ToJsonString(), retained["Files"]!.ToJsonString());
                        Assert.Equal(resources["Sounds"]!.ToJsonString(), retained["Sounds"]!.ToJsonString());
                        owners++;
                        foreach (string name in new[] { "PrimitiveKinds", "TextureKinds", "PuffSprite" })
                        {
                            JsonNode? previous = retained[name];
                            if (previous is not null && previous.ToJsonString() != "\"\"" && previous.ToJsonString() != "[]")
                            {
                                Assert.Equal(resources[name]!.ToJsonString(), previous.ToJsonString());
                                continue;
                            }
                            Assert.True(previous is null || previous.ToJsonString() == "\"\"" || previous.ToJsonString() == "[]");
                            JsonNode added = resources[name]!.DeepClone();
                            retained[name] = added;
                            changes.Add(new { Path = path + "/" + entry.Key + "/" + name, Operation = previous is null ? "add" : "replace", Previous = previous?.ToJsonString(), ValueSha256 = Hash(added.ToJsonString()) });
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
