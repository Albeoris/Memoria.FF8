using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleDiablos325WingsTests
{
    [Fact]
    public void Initializer_publishes_both_three_pointer_tables_and_second_half_uses_actual_alternate_then_first()
    {
        var fixture = NativeBattleDiablos325Tests.Fixture();
        NativeBattleDiablos325Frame frame = NativeBattleDiablos325Entry.Initialize6541E0(fixture.Frame, fixture.Packets, fixture.Models);
        NativeBattleEffectArena arena = frame.Memory.Arena!.Write(0x3505C, Shape(10)).Write(0x3704C, Shape(110)).MarkUnknown(0x3506A, 2).MarkUnknown(0x3705A, 2).MarkUnknown(0xD9A, 2);
        NativeBattlePrimitiveProcess process = frame.Memory.PrimitiveProcess! with { ModelRendererRegisters = new(1, 2, 3, 0x123, 0x456) };
        frame = frame with { Memory = frame.Memory with { Arena = arena, PrimitiveProcess = process } };
        NativeBattleModulePoolTask task = new(1, 0, null, 0x6545F0, new byte[0x994 - 12].ToImmutableArray());
        task = task.WriteWord(0xC, 91);
        NativeBattleCameraProjection projection = new(new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], 0, 0, 0, 4096), 288, 160, 108);
        var preserved = NativeBattleStorageSnapshots.Capture(frame);
        string original = NativeBattleTestSnapshots.Json(preserved);
        NativeBattleDiablos325WingsDraw initial = NativeBattleDiablos325Wings.Advance654811(frame, task, projection);
        Assert.Equal(new short[] { 10, 20, 30 }, Coordinates(initial.Memory));
        Assert.Equal(0, initial.Packets.AllocationBytes);
        foreach (int offset in new[] { 0x14, 0x54 })
        {
            Assert.Equal(0x20DFAB8u + 0x3505C, initial.Task.ReadDword(offset));
            Assert.Equal(0x20DFAB8u + 0x3704C, initial.Task.ReadDword(offset + 4));
            Assert.Equal(0x20DFAB8u + 0x3505C, initial.Task.ReadDword(offset + 8));
        }
        NativeBattleDiablos325WingsDraw alternate = NativeBattleDiablos325Wings.Advance654811(frame with { Memory = initial.Memory }, initial.Task.WriteWord(0xC, 97), projection);
        Assert.Equal(new short[] { 110, 120, 130 }, Coordinates(alternate.Memory));
        NativeBattleDiablos325WingsDraw late = NativeBattleDiablos325Wings.Advance654811(frame with { Memory = alternate.Memory }, alternate.Task.WriteWord(0xC, 102), projection);
        Assert.Equal(new short[] { 27, 37, 47 }, Coordinates(late.Memory));
        Assert.Throws<NotSupportedException>(() => late.Memory.Arena!.Read(0xD9A, 1));
        NativeBattleModelRendererRegisters registers = late.Memory.PrimitiveProcess!.ModelRendererRegisters!;
        Assert.Null(registers.FarRed);
        Assert.Null(registers.FarGreen);
        Assert.Null(registers.FarBlue);
        Assert.Equal<uint?>(0x123, registers.PacketCursor);
        Assert.Equal<uint?>(0x456, registers.SourceCursor);
        Assert.Equal(original, NativeBattleTestSnapshots.Json(preserved));
    }

    private static short[] Coordinates(NativeBattleEffectMemory memory)
    {
        byte[] bytes = memory.Arena!.Read(0xD94, 6);
        return [BinaryPrimitives.ReadInt16LittleEndian(bytes), BinaryPrimitives.ReadInt16LittleEndian(bytes.AsSpan(2)), BinaryPrimitives.ReadInt16LittleEndian(bytes.AsSpan(4))];
    }

    private static byte[] Shape(short x)
    {
        byte[] bytes = new byte[48];
        BinaryPrimitives.WriteInt32LittleEndian(bytes, 16);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(4), 1);
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(8), x);
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(10), checked((short)(x + 10)));
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(12), checked((short)(x + 20)));
        return bytes;
    }
}
