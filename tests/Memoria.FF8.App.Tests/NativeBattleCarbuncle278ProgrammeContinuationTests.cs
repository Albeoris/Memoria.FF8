using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeBattleCarbuncle278ProgrammeContinuationTests
{
    [Fact]
    public void Named_standalone_continues_original_programme_workspace_private_packets_and_pose_camera()
    {
        string? input = Environment.GetEnvironmentVariable("MEMORIA_FF8_CARBUNCLE278_SNAPSHOT");
        string? evidence = Environment.GetEnvironmentVariable("MEMORIA_FF8_CARBUNCLE278_EVIDENCE");
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        Assert.SkipUnless(input is not null && File.Exists(input) && !string.IsNullOrWhiteSpace(evidence) && directory is not null && Directory.Exists(directory), "Select installed resources, a pre-mesh standalone and a distinct evidence directory.");
        BattleSnapshot source = JsonSerializer.Deserialize<BattleSnapshot>(File.ReadAllText(input!)) ?? throw new InvalidDataException("The standalone battle is empty.");
        ushort initialCounter = Counter(source);
        Assert.True(initialCounter is 112 or 124);
        Assert.NotNull(source.NativeGraphics!.OriginalEffect!.Binding.Carbuncle278Resources!.CreatureProgramme);
        Assert.Equal((byte)31, source.NativeGraphics.StageResourceIndex98990);
        BattleSession first = BattleSession.Restore(source);
        BattleSession second = BattleSession.Restore(JsonSerializer.Deserialize<BattleSnapshot>(JsonSerializer.Serialize(source))!);
        using BattleAssets assets = new(directory!);
        BattleScenePresentation scene = new(assets, source.Definition, true);
        string output = Path.Combine(evidence!, "carbuncle-continued-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(output);
        Console.WriteLine($"Carbuncle standalone continuation evidence: {output}");
        NativeBattleRestorationPresentationTests.Publish(output, "source", source, null);
        TimeSpan callback = TimeSpan.FromTicks((TimeSpan.TicksPerSecond + 29) / 30);
        while (Counter(first.Capture()) < 147)
        {
            first.AdvanceNativeGraphicsPresentation(callback, TimeSpan.Zero);
            second.AdvanceNativeGraphicsPresentation(callback, TimeSpan.Zero);
            BattleSnapshot current = first.Capture();
            Assert.Equal(JsonSerializer.Serialize(current), JsonSerializer.Serialize(second.Capture()));
            NativeBattleModulePoolTask root = current.NativeGraphics!.OriginalEffect!.Pools.Single(pool => pool.DescriptorAddress == NativeBattleCarbuncle278Entry.MainDescriptor).Slots[0];
            uint primary = root.ReadDword(0x10) == 0 ? 0x3ECCu : 0x13ECCu;
            NativeBattleCarbuncle278State state = current.NativeEffectMemory!.Carbuncle278!;
            NativeBattlePrimitiveFrame primitive = current.NativeGraphics.PrimitiveFrame!;
            int[] privatePackets = primitive.LastCommands.Select(command => command.Command.Packet).Where(packet => packet.Bank == "20DFAB8").Distinct().Select(packet => packet.ByteOffset).Order().ToArray();
            int cursor = checked((int)primary);
            foreach (int address in privatePackets)
            {
                Assert.Equal(cursor, address);
                byte[] tag = current.NativeEffectMemory.Arena!.Read(address, 4);
                Assert.True(tag[3] > 0);
                cursor += 4 + 4 * tag[3];
            }
            Assert.Equal(state.ModelBuffer81F8 + (uint)cursor, state.PacketCursor81BC);
            NativeBattleModulePoolTask creature = current.NativeGraphics.OriginalEffect.Pools.Single(pool => pool.DescriptorAddress == 0x2508188).Slots[0];
            Assert.Equal(0x10C2608u, creature.ReadDword(0xCC));
            Assert.Equal((uint)(Counter(current) - 112), creature.ReadDword(0xD0));
            if (Counter(current) == 113)
            {
                Assert.Equal((byte)1, creature.ReadByte(0x8C));
            }
            Assert.Equal((ushort)(Counter(current) - 2), creature.ReadWord(0xC));
            if (Counter(current) is 138 or 139)
            {
                NativeBattleRestorationPresentationTests.Publish(output, "pose-selector-" + Counter(current), current, null);
                Assert.Equal(Counter(current) == 138 ? (byte)1 : (byte)2, creature.ReadByte(0x8C));
            }
            NativeBattleEffectTaskPool sparkles = current.NativeGraphics.OriginalEffect.Pools.Single(pool => pool.DescriptorAddress == 0x2508178);
            Assert.InRange(sparkles.Slots.Count(task => (task.Flags & 1) != 0), 0, 40);
            Assert.All(sparkles.Slots.Where(task => (task.Flags & 1) != 0), task => Assert.InRange(task.ReadWord(0x12), (ushort)1, (ushort)10));
            Assert.NotNull(current.NativeEffectMemory.PrimitiveProcess!.CommonPacketMemory);
            byte[] pixels = scene.Render(first, 320, 224).ToArray();
            Assert.Equal(pixels, scene.Render(second, 320, 224).ToArray());
            ushort counter = Counter(current);
            if (counter is 113 or 122 or 124 or 138 or 139 or 147)
            {
                NativeBattleRestorationPresentationTests.Publish(output, counter == 147 ? "before-recipient-programme-boundary" : "master-" + counter, current, pixels);
            }
        }
        BattleSnapshot boundary = first.Capture();
        Exception? error = Record.Exception(() => first.AdvanceNativeGraphicsPresentation(callback, TimeSpan.Zero));
        Assert.NotNull(error);
        Assert.Contains("6822F0", error.ToString());
        File.WriteAllText(Path.Combine(output, "result.json"), JsonSerializer.Serialize(new { Status = "programme-before-recipient-beams", boundary.Frame, NativeCallback = boundary.NativeGraphics!.Callback, Counter = Counter(boundary), Error = error.ToString() }));

        static ushort Counter(BattleSnapshot snapshot) => snapshot.NativeGraphics!.OriginalEffect!.Pools.Single(pool => pool.DescriptorAddress == NativeBattleCarbuncle278Entry.MainDescriptor).Slots[0].ReadWord(0xC);
    }
}
