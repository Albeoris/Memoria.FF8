using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleDiablos325LateShapeTests
{
    [Fact]
    public void Thirteen_actual_slots_use_new_file_base_and_wrap_modulo_without_reading_previous_geometry()
    {
        var fixture = NativeBattleDiablos325Tests.Fixture();
        NativeBattleDiablos325Frame frame = NativeBattleDiablos325Entry.Initialize6541E0(fixture.Frame, fixture.Packets, fixture.Models);
        byte[] geometry = Geometry();
        NativeBattleEffectArena arena = frame.Memory.Arena!;
        for (int index = 0; index < 13; index++)
        {
            arena = arena.Write(0x3505C + index * 0x1FF0, geometry);
        }
        frame = frame with { Memory = frame.Memory with { Arena = arena, Diablos325 = frame.Memory.Diablos325! with { FarZ51DC = 4000 } } };
        string original = JsonSerializer.Serialize(frame);
        NativeBattleModulePoolTask task = new(1, 0, null, 0x6545F0, new byte[0x994 - 12].ToImmutableArray());
        NativeBattleCameraProjection projection = Projection();
        NativeBattleDiablos325ShapeDraw first = NativeBattleDiablos325Shapes.Advance654AAC(frame, task.WriteWord(0xC, 238), projection);
        Assert.Equal(Enumerable.Range(0, 13).Select(index => 0x20DFAB8u + 0x3505Cu + (uint)index * 0x1FF0), Enumerable.Range(0, 13).Select(index => first.Task.ReadDword(0x14 + index * 4)));
        NativeBattleEffectMemory memory = first.Memory with { Arena = first.Memory.Arena!.MarkUnknown(0x3505C, 4) };
        NativeBattleDiablos325Shapes.Advance654AAC(frame with { Memory = memory }, first.Task.WriteWord(0xC, 239), projection);
        Assert.Throws<NotSupportedException>(() => NativeBattleDiablos325Shapes.Advance654AAC(frame with { Memory = memory }, first.Task.WriteWord(0xC, 251), projection));
        Assert.Equal(original, JsonSerializer.Serialize(frame));
    }

    [Fact]
    public void Shape_position_uses_distinct_native_X_Y_and_far_Z_before_module_projection()
    {
        var fixture = NativeBattleDiablos325Tests.Fixture();
        NativeBattleDiablos325Frame frame = NativeBattleDiablos325Entry.Initialize6541E0(fixture.Frame, fixture.Packets, fixture.Models);
        frame = frame with { Memory = frame.Memory with { Arena = frame.Memory.Arena!.Write(0x3505C, Geometry()), Diablos325 = frame.Memory.Diablos325! with { FarZ51DC = 4000 } } };
        NativeBattleModulePoolTask task = new(1, 0, null, 0x6545F0, new byte[0x994 - 12].ToImmutableArray());
        NativeBattleDiablos325ShapeDraw drawn = NativeBattleDiablos325Shapes.Advance654AAC(frame, task.WriteWord(0xC, 238), Projection());
        NativeBattleGfGeometryAttempt attempt = Assert.Single(drawn.Packets.Attempts);
        Assert.Equal(new uint[] { 0x04000000, 0x20404040, 0x016005D0, 0x016009D0, 0x056005D0 }, attempt.Primitive!.Words);
        Assert.Equal(new int[] { 4096, 4096, 4096, 0 }, attempt.Primitive.VertexPayload4);
    }

    private static NativeBattleCameraProjection Projection() => new(new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], 0, 8792, 256, 4096), 4096, 160, 108);

    private static byte[] Geometry()
    {
        byte[] bytes = new byte[108];
        BinaryPrimitives.WriteInt32LittleEndian(bytes, 64);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(4), 3);
        Put(8, -64, -64);
        Put(16, 64, -64);
        Put(24, -64, 64);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(64), 1);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(68), 0x20404040);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(74), 2);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(76), 4);
        return bytes;

        void Put(int offset, short x, short y)
        {
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(offset), x);
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(offset + 2), y);
        }
    }
}
