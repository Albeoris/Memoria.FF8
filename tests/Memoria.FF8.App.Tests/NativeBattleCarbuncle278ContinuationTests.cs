using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeBattleCarbuncle278ContinuationTests
{
    [Fact]
    public void Named_standalone_continues_mirror_and_creature_in_actual_common_packet_memory()
    {
        string? input = Environment.GetEnvironmentVariable("MEMORIA_FF8_CARBUNCLE278_SNAPSHOT");
        string? evidence = Environment.GetEnvironmentVariable("MEMORIA_FF8_CARBUNCLE278_EVIDENCE");
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        Assert.SkipUnless(input is not null && File.Exists(input) && !string.IsNullOrWhiteSpace(evidence) && directory is not null && Directory.Exists(directory), "Select installed resources, a pre-mesh standalone and a distinct evidence directory.");
        BattleSnapshot source = JsonSerializer.Deserialize<BattleSnapshot>(File.ReadAllText(input!)) ?? throw new InvalidDataException("The standalone battle is empty.");
        ushort initialCounter = Counter(source);
        Assert.True(initialCounter is 3 or 38);
        if (initialCounter == 38)
        {
            NativeBattleModulePoolTask sourceRoot = source.NativeGraphics!.OriginalEffect!.Pools.Single(pool => pool.DescriptorAddress == NativeBattleCarbuncle278Entry.MainDescriptor).Slots[0];
            uint primary = sourceRoot.ReadDword(0x10) == 0 ? 0x3ECCu : 0x13ECCu;
            NativeBattleCarbuncle278State state = source.NativeEffectMemory!.Carbuncle278!;
            Assert.Equal(state.ModelBuffer81F8 + primary, state.PacketCursor81BC);
        }
        Assert.Equal((byte)31, source.NativeGraphics!.StageResourceIndex98990);
        byte[] firstPrivate = source.NativeEffectMemory!.Arena!.Read(0x3ECC, 112);
        byte[] secondPrivate = source.NativeEffectMemory.Arena.Read(0x13ECC, 112);
        BattleSession first = BattleSession.Restore(source);
        BattleSession second = BattleSession.Restore(JsonSerializer.Deserialize<BattleSnapshot>(JsonSerializer.Serialize(source))!);
        using BattleAssets assets = new(directory!);
        BattleScenePresentation scene = new(assets, source.Definition, true);
        string output = Path.Combine(evidence!, "carbuncle-continued-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(output);
        Console.WriteLine($"Carbuncle standalone continuation evidence: {output}");
        NativeBattleRestorationPresentationTests.Publish(output, "source", source, null);
        TimeSpan callback = TimeSpan.FromTicks((TimeSpan.TicksPerSecond + 29) / 30);
        while (Counter(first.Capture()) < 88)
        {
            first.AdvanceNativeGraphicsPresentation(callback, TimeSpan.Zero);
            second.AdvanceNativeGraphicsPresentation(callback, TimeSpan.Zero);
            BattleSnapshot current = first.Capture();
            Assert.Equal(JsonSerializer.Serialize(current), JsonSerializer.Serialize(second.Capture()));
            Assert.Equal(firstPrivate, current.NativeEffectMemory!.Arena!.Read(0x3ECC, 112));
            Assert.Equal(secondPrivate, current.NativeEffectMemory.Arena.Read(0x13ECC, 112));
            NativeBattleModulePoolTask root = current.NativeGraphics!.OriginalEffect!.Pools.Single(pool => pool.DescriptorAddress == NativeBattleCarbuncle278Entry.MainDescriptor).Slots[0];
            uint primary = root.ReadDword(0x10) == 0 ? 0x3ECCu : 0x13ECCu;
            NativeBattleCarbuncle278State state = current.NativeEffectMemory.Carbuncle278!;
            Assert.Equal(state.ModelBuffer81F8 + primary, state.PacketCursor81BC);
            Assert.NotNull(current.NativeEffectMemory.PrimitiveProcess!.CommonPacketMemory);
            byte[] pixels = scene.Render(first, 320, 224).ToArray();
            Assert.Equal(pixels, scene.Render(second, 320, 224).ToArray());
            ushort counter = Counter(current);
            if (counter is 4 or 5 or 10 or 18 or 28 or 38 or 48 or 68 or 88)
            {
                NativeBattleRestorationPresentationTests.Publish(output, counter == 88 ? "before-sparkle-boundary" : "master-" + counter, current, pixels);
            }
        }
        BattleSnapshot boundary = first.Capture();
        Assert.Equal((ushort)88, Counter(boundary));
        Assert.Equal((ushort)86, boundary.NativeGraphics!.OriginalEffect!.Pools.Single(pool => pool.DescriptorAddress == 0x2508188).Slots[0].ReadWord(0xC));
        File.WriteAllText(Path.Combine(output, "result.json"), JsonSerializer.Serialize(new { Status = "creature-window-complete", boundary.Frame, NativeCallback = boundary.NativeGraphics.Callback, Counter = Counter(boundary) }));

        static ushort Counter(BattleSnapshot snapshot) => snapshot.NativeGraphics!.OriginalEffect!.Pools.Single(pool => pool.DescriptorAddress == NativeBattleCarbuncle278Entry.MainDescriptor).Slots[0].ReadWord(0xC);
    }
}
