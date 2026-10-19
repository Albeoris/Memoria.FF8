using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleDiablos325ShapeTests
{
    [Fact]
    public void Nine_actual_pointers_replace_the_pair_and_follow_modulo_source_order_without_consuming_other_headers()
    {
        var fixture = NativeBattleDiablos325Tests.Fixture();
        NativeBattleDiablos325Frame frame = NativeBattleDiablos325Entry.Initialize6541E0(fixture.Frame, fixture.Packets, fixture.Models);
        NativeBattleEffectArena arena = frame.Memory.Arena!;
        uint[] offsets = [0x3903C, 0x3B02C, 0x3D01C, 0x3F00C, 0x40FFC, 0x42FEC, 0x44FDC, 0x46FCC, 0x48FBC];
        byte[] shape = new byte[48];
        BinaryPrimitives.WriteInt32LittleEndian(shape, 16);
        BinaryPrimitives.WriteInt32LittleEndian(shape.AsSpan(4), 1);
        foreach (uint offset in offsets)
        {
            arena = arena.Write((int)offset, shape);
        }
        frame = frame with { Memory = frame.Memory with { Arena = arena, PrimitiveProcess = frame.Memory.PrimitiveProcess! with { ModelRendererRegisters = new(1, 2, 3, 0x123, 0x456) } } };
        string original = JsonSerializer.Serialize(frame);
        NativeBattleModulePoolTask task = new(1, 0, null, 0x6545F0, new byte[0x994 - 12].ToImmutableArray());
        task = task.WriteWord(0xC, 103);
        NativeBattleCameraProjection projection = new(new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], 0, 0, 0, 4096), 288, 160, 108);
        NativeBattleDiablos325ShapeDraw first = NativeBattleDiablos325Shapes.Advance654899(frame, task, projection);
        Assert.Equal(offsets.Select(offset => 0x20DFAB8u + offset), Enumerable.Range(0, 9).Select(index => first.Task.ReadDword(0x14 + index * 4)));
        Assert.Equal(0, first.Packets.AllocationBytes);
        NativeBattleEffectMemory nextMemory = first.Memory with { Arena = first.Memory.Arena!.MarkUnknown(0x3903C, 4) };
        NativeBattleDiablos325ShapeDraw second = NativeBattleDiablos325Shapes.Advance654899(frame with { Memory = nextMemory }, first.Task.WriteWord(0xC, 104), projection);
        Assert.Equal(0, second.Packets.AllocationBytes);
        NativeBattleEffectMemory invalid = second.Memory with { Arena = second.Memory.Arena!.MarkUnknown(0x3D01C, 4) };
        Assert.Throws<NotSupportedException>(() => NativeBattleDiablos325Shapes.Advance654899(frame with { Memory = invalid }, first.Task.WriteWord(0xC, 105), projection));
        NativeBattleModelRendererRegisters registers = second.Memory.PrimitiveProcess!.ModelRendererRegisters!;
        Assert.Null(registers.FarRed);
        Assert.Null(registers.FarGreen);
        Assert.Null(registers.FarBlue);
        Assert.Equal<uint?>(0x123, registers.PacketCursor);
        Assert.Equal<uint?>(0x456, registers.SourceCursor);
        Assert.Equal(original, JsonSerializer.Serialize(frame));
    }

    [Fact]
    public void Zero_scale_node_does_not_read_missing_selector_source_or_camera_owners()
    {
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess();
        NativeBattleGfAnimationNode node = new(0, 0, new byte[44].ToImmutableArray());
        NativeBattleDiablos325NodeDraw draw = NativeBattleDiablos325Programme.Emit656B70(memory, new(0, default), node, new(null, null, null, null), null!, new(0, 0, 0), -128, 0, null, null);
        Assert.Equal(0, draw.Packets.AllocationBytes);
        Assert.Same(memory, draw.Memory);
    }
}
