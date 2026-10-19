using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeBattleCarbuncle278RecipientContinuationTests
{
    [Fact]
    public void Named_standalone_migrates_only_original_resources_and_continues_recipient_programmes_and_timed_texture_io()
    {
        string? input = Environment.GetEnvironmentVariable("MEMORIA_FF8_CARBUNCLE278_SNAPSHOT");
        string? evidence = Environment.GetEnvironmentVariable("MEMORIA_FF8_CARBUNCLE278_EVIDENCE");
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        Assert.SkipUnless(input is not null && File.Exists(input) && !string.IsNullOrWhiteSpace(evidence) && directory is not null && Directory.Exists(directory), "Select installed resources, a recipient boundary standalone and a distinct evidence directory.");
        string original = File.ReadAllText(input!);
        JsonNode node = JsonNode.Parse(original)!;
        BattleSnapshot source = JsonSerializer.Deserialize<BattleSnapshot>(original)!;
        Assert.Equal((ushort)145, Creature(source).ReadWord(0xC));
        string output = Path.Combine(evidence!, "carbuncle-continued-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(output);
        Console.WriteLine($"Carbuncle standalone continuation evidence: {output}");
        using GameAssets game = new(directory!);
        NativeBattleCarbuncle278Resources installed = game.ReadNativeCarbuncle278Resources();
        JsonNode resources = JsonSerializer.SerializeToNode(installed)!;
        List<object> changes = [];
        int owners = 0;
        AddResources(node, "");
        Assert.Equal(3, owners);
        Assert.Equal(9, changes.Count);
        string migratedPath = Path.Combine(output, "recipient-resources.battle.json");
        string migrated = node.ToJsonString();
        File.WriteAllText(migratedPath, migrated);
        File.WriteAllText(Path.Combine(output, "resource-migration.json"), JsonSerializer.Serialize(new { OriginalSha256 = Hash(original), MigratedSha256 = Hash(migrated), Changes = changes }));
        source = JsonSerializer.Deserialize<BattleSnapshot>(migrated)!;
        BattleSession first = BattleSession.Restore(source);
        BattleSession second = BattleSession.Restore(JsonSerializer.Deserialize<BattleSnapshot>(migrated)!);
        using BattleAssets assets = new(directory!);
        BattleScenePresentation scene = new(assets, source.Definition, true);
        NativeBattleRestorationPresentationTests.Publish(output, "source", source, null);
        TimeSpan callback = TimeSpan.FromTicks((TimeSpan.TicksPerSecond + 29) / 30);
        string? hiddenPose = null;
        int callbacks = 0;
        while (Creature(first.Capture()).ReadWord(0xC) < 160)
        {
            Assert.True(callbacks++ < 128, "The bounded recipient window did not progress.");
            first.AdvanceNativeGraphicsPresentation(callback, TimeSpan.Zero);
            second.AdvanceNativeGraphicsPresentation(callback, TimeSpan.Zero);
            BattleSnapshot current = first.Capture();
            Assert.Equal(JsonSerializer.Serialize(current), JsonSerializer.Serialize(second.Capture()));
            ushort counter = Creature(current).ReadWord(0xC);
            NativeBattleRestorationPresentationTests.Publish(output, "before-check-" + callbacks, current, null);
            Assert.All(current.NativeGraphics!.Models.Where(model => model.Index < 3), model => Assert.Equal(0, model.Flags0 & 4));
            if (counter == 146)
            {
                hiddenPose = JsonSerializer.Serialize(current.NativeEffectMemory!.CarbuncleExecutableMemory);
            }
            if (counter > 146)
            {
                Assert.Equal(hiddenPose, JsonSerializer.Serialize(current.NativeEffectMemory!.CarbuncleExecutableMemory));
            }
            Assert.Equal(0x10C5C8Cu, Creature(current).ReadDword(0xCC));
            Assert.Equal((uint)(counter - 145), Creature(current).ReadDword(0xD0));
            NativeBattleEffectTaskPool recipients = current.NativeGraphics.OriginalEffect!.Pools.Single(pool => pool.DescriptorAddress == 0x2508158);
            Assert.InRange(recipients.Slots.Count(task => (task.Flags & 1) != 0), 1, 3);
            foreach (var indexed in recipients.Slots.Select((task, index) => (Task: task, Index: index)).Where(item => (item.Task.Flags & 1) != 0 && item.Task.ReadWord(0xC) != 0))
            {
                int offset = checked((int)(recipients.BankAddress - 0x20DFAB8) + indexed.Index * recipients.Stride);
                Assert.Throws<NotSupportedException>(() => current.NativeEffectMemory!.Arena!.Read(offset + 0x46, 2));
                Assert.Equal(indexed.Task.Payload.AsSpan().Slice(0x48 - 12, 12).ToArray(), current.NativeEffectMemory!.Arena!.Read(offset + 0x48, 12));
            }
            NativeBattlePrimitiveFrame primitive = current.NativeGraphics.PrimitiveFrame!;
            uint primary = current.NativeGraphics.OriginalEffect.Pools.Single(pool => pool.DescriptorAddress == NativeBattleCarbuncle278Entry.MainDescriptor).Slots[0].ReadDword(0x10) == 0 ? 0x3ECCu : 0x13ECCu;
            int cursor = checked((int)primary);
            foreach (int address in primitive.LastCommands.Select(command => command.Command.Packet).Where(packet => packet.Bank == "20DFAB8").Distinct().Select(packet => packet.ByteOffset).Order())
            {
                Assert.Equal(cursor, address);
                byte[] tag = current.NativeEffectMemory!.Arena!.Read(address, 4);
                Assert.True(tag[3] > 0);
                cursor += 4 + 4 * tag[3];
            }
            NativeBattleCarbuncle278State state = current.NativeEffectMemory!.Carbuncle278!;
            Assert.Equal(state.ModelBuffer81F8 + (uint)cursor, state.PacketCursor81BC);
            byte[] pixels = scene.Render(first, 320, 224).ToArray();
            Assert.Equal(pixels, scene.Render(second, 320, 224).ToArray());
            if (counter is 146 or 150 or 151 or 155 or 160)
            {
                NativeBattleRestorationPresentationTests.Publish(output, counter == 160 ? "before-recipient-child-boundary" : "creature-" + counter + "-callback-" + callbacks, current, pixels);
            }
        }
        BattleSnapshot boundary = first.Capture();
        int controller = boundary.NativeEffectMemory!.EffectIo!.BaseOffset!.Value;
        byte[] header = boundary.NativeEffectMemory.Arena!.Read(controller, 5);
        Assert.Equal((byte)0, header[0]);
        Assert.Equal((byte)2, header[3]);
        Assert.Equal((byte)2, header[4]);
        NativeBattleEffectFileAsset last = installed.Files.Single(file => file.FileId == 718);
        Assert.Equal(last.Data.ToArray(), boundary.NativeEffectMemory.Arena.Read(0x23ECC, last.Data.Length));
        File.WriteAllText(Path.Combine(output, "result.json"), JsonSerializer.Serialize(new { Status = "recipient-programme-and-timed-io", boundary.Frame, NativeCallback = boundary.NativeGraphics!.Callback, Creature = Creature(boundary).ReadWord(0xC), Callbacks = callbacks }));
        Assert.Equal(original, File.ReadAllText(input!));

        void AddResources(JsonNode current, string path)
        {
            if (current is JsonObject obj)
            {
                foreach (var entry in obj.ToArray())
                {
                    if (entry.Key == "Carbuncle278Resources" && entry.Value is JsonObject retained)
                    {
                        Assert.Equal(resources["MirrorBoundsMesh"]!.ToJsonString(), retained["MirrorBoundsMesh"]!.ToJsonString());
                        if (retained["CreatureProgramme"] is JsonNode firstProgramme)
                        {
                            Assert.Equal(resources["CreatureProgramme"]!.ToJsonString(), firstProgramme.ToJsonString());
                        }
                        owners++;
                        foreach (string name in new[] { "SecondCreatureProgramme", "RecipientProgramme", "Files" })
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

    private static NativeBattleModulePoolTask Creature(BattleSnapshot snapshot) => snapshot.NativeGraphics!.OriginalEffect!.Pools.Single(pool => pool.DescriptorAddress == 0x2508188).Slots[0];
    private static string Hash(string value) => Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(value)));
}
