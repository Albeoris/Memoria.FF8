using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleDiablos325ConstructionTests
{
    [Fact]
    public void Construction_clears_only_active_puff_flags_and_initializes_exact_task_targets_programme_and_table()
    {
        var fixture = NativeBattleDiablos325Tests.Fixture();
        NativeBattleDiablos325Frame frame = NativeBattleDiablos325Entry.Initialize6541E0(fixture.Frame, fixture.Packets, fixture.Models);
        frame = Resources(frame);
        frame = frame with { Memory = frame.Memory with { Arena = frame.Memory.Arena!.Write(0x4C5C, Enumerable.Repeat((byte)0xCC, 160 * 16).ToArray()) } };
        int calls = 0;
        NativeBattleDiablos325Inputs inputs = new(fixture.Packets, current =>
        {
            calls++;
            Assert.Equal((byte)0, current.Memory.Arena!.Read(0x4C6B, 1)[0]);
            return new(current, 7);
        });
        string original = JsonSerializer.Serialize(frame);
        NativeBattleDiablos325Frame result = NativeBattleDiablos325Construction.Initialize6543BA(frame, inputs);
        Assert.Equal(1, calls);
        Assert.Equal(original, JsonSerializer.Serialize(frame));
        Assert.Equal((byte)0, result.Memory.Diablos325!.Mode51F0);
        Assert.Equal(NativeBattleEffectNodeMemory.ArenaAddress + 0x4C5Cu, result.Memory.Diablos325.ParticleCursor51F4);
        for (int index = 0; index < 160; index++)
        {
            Assert.Equal((byte)0xCC, result.Memory.Arena!.Read(0x4C6A + index * 16, 1)[0]);
            Assert.Equal((byte)0, result.Memory.Arena.Read(0x4C6B + index * 16, 1)[0]);
        }
        NativeBattleModulePoolTask task = result.Effect.Pools.Single(pool => pool.DescriptorAddress == 0x25051A0).Slots[0];
        Assert.Equal(7u, task.ReadDword(0x10));
        Assert.Equal(0xFD8584u, task.ReadDword(0x264));
        Assert.Equal(0x1D972C0u, task.ReadDword(0x94));
        Assert.Equal(0x1D972C0u + 0x9Cu, task.ReadDword(0xA0));
        Assert.Equal((ushort)4096, task.ReadWord(0x9E));
        Assert.Equal((ushort)4096, task.ReadWord(0xAA));
        Assert.Equal(task.Payload.ToArray(), result.Memory.Arena!.Read(12, task.Payload.Length));
        Assert.Equal(new byte[] { 0, 0, 1, 0, 0 }, result.Memory.Arena.Read(0x4C14, 5));
        Assert.Equal(new byte[] { 0, 0, 0, 16 }, result.Memory.Arena.Read(0x994, 4));
        Assert.Equal(new byte[] { 0, 16, 0, 0 }, result.Memory.Arena.Read(0x994 + 64 * 4, 4));
    }

    [Fact]
    public void Same_creation_callback_runs_timeline_zero_and_preserves_five_ordered_IO_descriptors()
    {
        var fixture = NativeBattleDiablos325Tests.Fixture();
        NativeBattleDiablos325Frame frame = Resources(NativeBattleDiablos325Entry.Initialize6541E0(fixture.Frame, fixture.Packets, fixture.Models));
        frame = NativeBattleDiablos325Controller.Advance654350(NativeBattleDiablos325Controller.Advance654350(frame));
        List<NativeBattleEffectFileRequest> requests = [];
        List<(uint, int, int)> sounds = [];
        NativeBattleDiablos325Inputs inputs = new(fixture.Packets, current => new(current, 3))
        {
            QueueFile = (current, request) =>
            {
                requests.Add(request);
                return current;
            },
            PlaySound = (sound, flags, pan) => sounds.Add((sound, flags, pan))
        };
        NativeBattleDiablos325Frame result = NativeBattleDiablos325Controller.Advance654350(frame, inputs: inputs);
        Assert.Equal(2, result.Effect.Pools.Length);
        Assert.Equal((ushort)3, result.Effect.Pools.Single(pool => pool.DescriptorAddress == 0x25051B0).Slots[0].ReadWord(0xC));
        Assert.Equal((ushort)1, result.Effect.Pools.Single(pool => pool.DescriptorAddress == 0x25051A0).Slots[0].ReadWord(0xC));
        Assert.Equal(new[] { (240248u, 1, 128) }, sounds);
        NativeBattleEffectFileRequest request = Assert.Single(requests);
        Assert.Equal((short)727, request.FileId);
        Assert.Equal(NativeBattleEffectNodeMemory.ArenaAddress + 0x1685Cu, request.DestinationAddress);
        byte[] descriptors = result.Memory.Arena!.Read(0x4C14, 48);
        Assert.Equal((byte)5, descriptors[3]);
        Assert.Equal((byte)1, descriptors[0]);
        short[] types = [3, 2, 5, 5, 5];
        uint[] destinations = [0x1685C, 0x1685C, 0x2185C, 0x3505C, 0x1685C];
        for (int index = 0; index < 5; index++)
        {
            Assert.Equal(types[index], BinaryPrimitives.ReadInt16LittleEndian(descriptors.AsSpan(8 + index * 8)));
            Assert.Equal((short)(727 + index), BinaryPrimitives.ReadInt16LittleEndian(descriptors.AsSpan(10 + index * 8)));
            Assert.Equal(NativeBattleEffectNodeMemory.ArenaAddress + destinations[index], BinaryPrimitives.ReadUInt32LittleEndian(descriptors.AsSpan(12 + index * 8)));
        }
        List<uint> flashes = [];
        for (int counter = 1; counter <= 8; counter++)
        {
            result = NativeBattleDiablos325Controller.Advance654350(result, (current, flash) =>
            {
                flashes.Add(flash);
                return current;
            }, inputs);
            Assert.Equal((ushort)(counter + 1), result.Effect.Pools.Single(pool => pool.DescriptorAddress == 0x25051A0).Slots[0].ReadWord(0xC));
            Assert.Equal(checked(256u + (uint)(counter - 1) * 512), result.Memory.Diablos325!.PreviousFlash51EC);
        }
        Assert.Equal(new uint[] { 256, 768, 1280, 1792, 2304, 2816, 3328, 3840 }, flashes);
        Assert.Single(sounds);
        Assert.Single(requests);
        Assert.Equal((byte)5, result.Memory.Arena!.Read(0x4C14 + 3, 1)[0]);
        Assert.Contains("656B70", Assert.Throws<NotSupportedException>(() => NativeBattleDiablos325Controller.Advance654350(result, inputs: inputs)).Message);
    }

    private static NativeBattleDiablos325Frame Resources(NativeBattleDiablos325Frame frame)
    {
        byte[] programme = new byte[0x3890];
        BinaryPrimitives.WriteInt32LittleEndian(programme.AsSpan(4), 8);
        NativeBattleDiablos325Resources resources = new(new(0xFD8584, programme.ToImmutableArray()), new(new string('0', 64), [0], [0, 0, 0, 0])) { Sounds = [240248] };
        return frame with { Memory = frame.Memory with { EffectIo = new(0x534150, null, null) }, Effect = frame.Effect with { Binding = frame.Effect.Binding with { Diablos325Resources = resources } } };
    }
}
