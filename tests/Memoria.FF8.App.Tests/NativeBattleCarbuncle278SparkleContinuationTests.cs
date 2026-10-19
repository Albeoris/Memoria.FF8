using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeBattleCarbuncle278SparkleContinuationTests
{
    [Fact]
    public void Named_standalone_continues_private_sparkles_and_posed_vertex_camera_to_original_programme()
    {
        string? input = Environment.GetEnvironmentVariable("MEMORIA_FF8_CARBUNCLE278_SNAPSHOT");
        string? evidence = Environment.GetEnvironmentVariable("MEMORIA_FF8_CARBUNCLE278_EVIDENCE");
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        Assert.SkipUnless(input is not null && File.Exists(input) && !string.IsNullOrWhiteSpace(evidence) && directory is not null && Directory.Exists(directory), "Select installed resources, a pre-mesh standalone and a distinct evidence directory.");
        BattleSnapshot source = JsonSerializer.Deserialize<BattleSnapshot>(File.ReadAllText(input!)) ?? throw new InvalidDataException("The standalone battle is empty.");
        ushort initialCounter = Counter(source);
        Assert.Equal((ushort)88, initialCounter);
        Assert.NotNull(source.NativeGraphics!.OriginalEffect!.Binding.Carbuncle278Resources!.SparkleSprite);
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
        while (Counter(first.Capture()) < 112)
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
            uint expectedCursor = state.ModelBuffer81F8 + primary + (uint)(40 * privatePackets.Length);
            if (expectedCursor != state.PacketCursor81BC)
            {
                NativeBattleRestorationPresentationTests.Publish(output, "cursor-assertion-" + Counter(current), current, null);
            }
            Assert.Equal(Enumerable.Range(0, privatePackets.Length).Select(index => checked((int)primary + index * 40)), privatePackets);
            Assert.Equal(expectedCursor, state.PacketCursor81BC);
            foreach (int address in privatePackets)
            {
                byte[] tag = current.NativeEffectMemory.Arena!.Read(address, 4);
                Assert.Equal((byte)9, tag[3]);
            }
            NativeBattleEffectTaskPool sparkles = current.NativeGraphics.OriginalEffect.Pools.Single(pool => pool.DescriptorAddress == 0x2508178);
            Assert.InRange(sparkles.Slots.Count(task => (task.Flags & 1) != 0), 4, 44);
            Assert.All(sparkles.Slots.Where(task => (task.Flags & 1) != 0), task => Assert.InRange(task.ReadWord(0x12), (ushort)1, (ushort)10));
            Assert.NotNull(current.NativeEffectMemory.PrimitiveProcess!.CommonPacketMemory);
            byte[] pixels = scene.Render(first, 320, 224).ToArray();
            Assert.Equal(pixels, scene.Render(second, 320, 224).ToArray());
            ushort counter = Counter(current);
            if (counter is 89 or 93 or 99 or 109 or 112)
            {
                NativeBattleRestorationPresentationTests.Publish(output, counter == 112 ? "before-programme-boundary" : "master-" + counter, current, pixels);
            }
        }
        BattleSnapshot boundary = first.Capture();
        Exception? error = Record.Exception(() => first.AdvanceNativeGraphicsPresentation(callback, TimeSpan.Zero));
        Assert.NotNull(error);
        Assert.Contains("7016B0", error.ToString());
        File.WriteAllText(Path.Combine(output, "result.json"), JsonSerializer.Serialize(new { Status = "sparkle-and-camera-before-programme", boundary.Frame, NativeCallback = boundary.NativeGraphics!.Callback, Counter = Counter(boundary), Error = error.ToString() }));

        static ushort Counter(BattleSnapshot snapshot) => snapshot.NativeGraphics!.OriginalEffect!.Pools.Single(pool => pool.DescriptorAddress == NativeBattleCarbuncle278Entry.MainDescriptor).Slots[0].ReadWord(0xC);
    }
}
