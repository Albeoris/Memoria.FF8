using System.Buffers.Binary;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleEffect83SpiralTests
{
    [Fact]
    public void First_face_retains_leading_row_and_waits_one_callback_after_threshold()
    {
        NativeBattleEffectArena faces = NativeBattleEffectArena.Clear().Write(0, [31]);
        NativeBattleEffectArena temporary = Corners().Write(0x90, BitConverter.GetBytes((short)100));
        NativeBattleEffect83SpiralStep first = NativeBattleEffect83Spiral.AdvanceFace(faces, 32, temporary, false);
        Assert.Equal((byte)31, first.Faces.Read(0, 1)[0]);
        Assert.Equal((short)1, Word(first.Faces, 38));
        Assert.Equal((short)100, Word(first.Faces, 34));
        Assert.Equal((short)-20, Word(first.Faces, 32));
        Assert.Equal((short)1024, Word(first.Faces, 36));
        Assert.Equal(temporary.Read(0x54, 24), first.Temporary148.Read(0x54, 24));
        NativeBattleEffect83SpiralStep second = NativeBattleEffect83Spiral.AdvanceFace(first.Faces, 32, first.Temporary148, false);
        Assert.Equal((short)2, Word(second.Faces, 38));
        Assert.Equal((short)0, Word(second.Faces, 40));
        Assert.Equal((short)-20, Word(second.Faces, 32));
        NativeBattleEffect83SpiralStep third = NativeBattleEffect83Spiral.AdvanceFace(second.Faces, 32, second.Temporary148, false);
        Assert.Equal((short)1, Word(third.Faces, 40));
        Assert.Equal((short)-68, Word(third.Faces, 32));
        Assert.Equal((short)16, Word(third.Faces, 62));
    }

    [Fact]
    public void Active_face_scales_current_deltas_with_truncation_and_preserves_unused_triangle_lane()
    {
        NativeBattleEffectArena faces = NativeBattleEffectArena.Clear().Write(38, BitConverter.GetBytes((short)2)).Write(40, BitConverter.GetBytes((short)14)).Write(60, BitConverter.GetBytes((short)4096)).Write(42, BitConverter.GetBytes((short)-129)).Write(46, BitConverter.GetBytes((short)123));
        NativeBattleEffect83SpiralStep step = NativeBattleEffect83Spiral.AdvanceFace(faces, 32, Corners(), false);
        Assert.Equal((short)4064, Word(step.Faces, 60));
        Assert.Equal((short)-127, Word(step.Faces, 42));
        Assert.Equal((short)123, Word(step.Faces, 46));
        Assert.Equal((short)4064, Word(step.Temporary148, 0x8E));
        step = NativeBattleEffect83Spiral.AdvanceFace(step.Faces, 32, step.Temporary148, false);
        Assert.Equal((short)-125, Word(step.Faces, 42));
    }

    [Fact]
    public void Snapshot_continuation_preserves_quad_deltas_and_word_wrap()
    {
        NativeBattleEffectArena faces = NativeBattleEffectArena.Clear().Write(38, BitConverter.GetBytes((short)2)).Write(32, BitConverter.GetBytes(short.MinValue)).Write(62, BitConverter.GetBytes((short)255));
        NativeBattleEffect83SpiralStep first = NativeBattleEffect83Spiral.AdvanceFace(faces, 32, Corners(), true);
        Assert.Equal((short)256, Word(first.Faces, 62));
        Assert.Equal(unchecked((short)(short.MinValue - 48)), Word(first.Faces, 32));
        NativeBattleEffect83SpiralStep restored = NativeBattleStorageSnapshots.Restore(JsonSerializer.Deserialize<NativeBattleEffect83SpiralStep>(JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(first)))!);
        NativeBattleEffect83SpiralStep expected = NativeBattleEffect83Spiral.AdvanceFace(first.Faces, 32, first.Temporary148, true);
        NativeBattleEffect83SpiralStep actual = NativeBattleEffect83Spiral.AdvanceFace(restored.Faces, 32, restored.Temporary148, true);
        Assert.Equal(expected.Faces.Read(0, 64), actual.Faces.Read(0, 64));
        Assert.Equal(expected.Temporary148.Read(0, 148), actual.Temporary148.Read(0, 148));
        Assert.Throws<InvalidDataException>(() => NativeBattleEffect83Spiral.AdvanceFace(faces, 0, Corners(), true));
        Assert.Throws<InvalidDataException>(() => NativeBattleEffect83Spiral.AdvanceFace(faces, 0x7D00, Corners(), true));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Raster_preserves_strided_unknown_W_and_native_packet_holes(bool quad)
    {
        byte[] source = Geometry(quad);
        NativeBattleEffect83SpiralDrawingStep result = Raster(source, quad);
        NativeBattleGfGeometryAttempt packet = Assert.Single(result.Batch.Attempts);
        Assert.Equal(quad ? 40 : 32, result.Batch.AllocationBytes);
        Assert.Equal(quad ? 0x2Eu : 0x26u, packet.Primitive!.Words[1] >> 24);
        Assert.Equal(quad ? 517 : 516, packet.Primitive.Bucket);
        Assert.Equal(NativeBattleGfPacketByteState.Untouched, packet.WriteMask[30]);
        Assert.Equal(NativeBattleGfPacketByteState.Untouched, packet.WriteMask[31]);
        Assert.Equal(new byte[8], result.Transformed.Read(8, 8));
        Assert.Throws<NotSupportedException>(() => result.Transformed.Read(6, 2));
        Assert.Equal((short)1, Word(result.Faces, 38));
        if (quad)
        {
            Assert.Equal(3u, BinaryPrimitives.ReadUInt32LittleEndian(result.Temporary148.Read(0x44, 4)));
        }
    }

    [Fact]
    public void Zero_area_rejects_packet_after_transform_and_persistent_face_initialization()
    {
        byte[] source = Geometry(false);
        source.AsSpan(14, 18).Clear();
        NativeBattleEffect83SpiralDrawingStep result = Raster(source, false);
        Assert.Empty(result.Batch.Attempts);
        Assert.Equal(0, result.Batch.AllocationBytes);
        Assert.Equal((short)1, Word(result.Faces, 38));
        Assert.Equal(0u, result.Memory.GeometryRegisters!.Mac0);
    }

    private static NativeBattleEffect83SpiralDrawingStep Raster(byte[] source, bool quad)
    {
        NativeBattleActionMatrix identity = new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], 0, 0, 0, 4096);
        NativeBattlePackedMatrix32 bone = NativeBattlePackedMatrix32.FromMatrix(identity, null);
        NativeBattlePackedMatrix32 view = NativeBattlePackedMatrix32.FromMatrix(identity with { Z = 8000 }, null);
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess() with { GeometryRegisters = NativeBattleGeometry.LoadMatrix(NativeBattleGeometryRegisters.Unknown(), identity, true, true, null), ScriptedColorRegisters = NativeBattleScriptedColorRegisters.Unknown() };
        NativeBattleEffectArena temporary = NativeBattleEffectArena.Clear().MarkUnknown(0, 148).Write(0x48, BitConverter.GetBytes(0x24123456u)).Write(0x4C, BitConverter.GetBytes(0x2C123456u)).Write(0x50, BitConverter.GetBytes(1u)).Write(0x90, new byte[2]);
        return NativeBattleEffect83SpiralDrawing.Draw(memory, temporary, (address, length) => NativeBattleEffectArena.Clear().Write(0, source.AsSpan(checked((int)(address - 0x1000)), length)), 0x1000, [bone], view, new(null, 100, 160, 108), NativeBattleScriptedGraphicsMemory.FromColdProcess().InitializeGeom56BAA0(), NativeBattleEffectArena.Clear(), (quad ? 4 : 3) * 16, NativeBattleEffectArena.Clear());
    }

    private static byte[] Geometry(bool quad)
    {
        int vertices = quad ? 4 : 3;
        int counts = (14 + vertices * 6 + 3) & ~3;
        byte[] source = new byte[counts + 12 + (quad ? 20 : 16)];
        BinaryPrimitives.WriteInt32LittleEndian(source, 1);
        BinaryPrimitives.WriteInt32LittleEndian(source.AsSpan(4), 8);
        Put(8, 1);
        Put(12, vertices);
        Put(20, 100);
        Put(28, 100);
        if (quad)
        {
            Put(32, 100);
            Put(34, 100);
        }
        Put(counts + (quad ? 2 : 0), 1);
        int face = counts + 12;
        Put(face + 2, 1);
        Put(face + 4, 2);
        Put(face + 6, quad ? 3 : 0x8078);
        BinaryPrimitives.WriteUInt32LittleEndian(source.AsSpan(face + 8), 0x3D948048);
        BinaryPrimitives.WriteUInt32LittleEndian(source.AsSpan(face + 12), 0x02B68078);
        if (quad)
        {
            Put(face + 16, 0x8078);
            Put(face + 18, 0x8048);
        }
        return source;

        void Put(int offset, int value)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(source.AsSpan(offset), checked((ushort)value));
        }
    }

    private static NativeBattleEffectArena Corners()
    {
        NativeBattleEffectArena result = NativeBattleEffectArena.Clear();
        short[] values = [100, -20, 0, 110, -30, 5, 120, -40, 10, 130, -50, 15];
        for (int corner = 0; corner < 4; corner++)
        {
            for (int axis = 0; axis < 3; axis++)
            {
                result = result.Write(0x54 + corner * 8 + axis * 2, BitConverter.GetBytes(values[corner * 3 + axis]));
            }
        }
        return result;
    }

    private static short Word(NativeBattleEffectArena memory, int offset) => BinaryPrimitives.ReadInt16LittleEndian(memory.Read(offset, 2));
}
