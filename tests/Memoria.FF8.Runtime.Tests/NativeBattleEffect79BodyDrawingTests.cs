using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleEffect79BodyDrawingTests
{
    private static readonly NativeBattleActionMatrix Identity = NativeBattleCuragaMatrices.Identity8DD770();

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Raw_faces_build_FT_packets_with_explicit_depth_without_AVSZ(bool quad)
    {
        NativeBattleEffect79BodyDrawingStep result = Draw(Geometry(quad), Context(depth: -7), Temporary(), quad ? 4 : 3);
        NativeBattleGfGeometryAttempt packet = Assert.Single(result.Batch.Attempts);
        Assert.Equal(quad ? 40 : 32, result.Batch.AllocationBytes);
        Assert.Equal(quad ? 0x2E123456u : 0x26123456u, packet.Primitive!.Words[1]);
        Assert.Equal(510, packet.Primitive.Bucket);
        Assert.Equal(new[] { 8000, 8000, 8000, quad ? 8000 : 0 }, packet.Primitive.VertexPayload4);
        Assert.Equal(0x1357u, result.Memory.GeometryRegisters!.Otz);
        Assert.Equal(0x3D948048u, packet.Primitive.Words[3]);
        Assert.Equal(0x02B68078u, packet.Primitive.Words[5]);
        Assert.Equal(0x8078u, packet.Primitive.Words[7]);
        Assert.Equal(NativeBattleGfPacketByteState.Untouched, packet.WriteMask[30]);
        Assert.Equal(NativeBattleGfPacketByteState.Untouched, packet.WriteMask[31]);
        if (quad)
        {
            Assert.Equal(0x8048u, packet.Primitive.Words[9]);
            Assert.Equal(NativeBattleGfPacketByteState.Untouched, packet.WriteMask[38]);
            Assert.Equal(NativeBattleGfPacketByteState.Untouched, packet.WriteMask[39]);
        }
    }

    [Theory]
    [InlineData(0, 100)]
    [InlineData(2048, 200)]
    [InlineData(4096, 300)]
    public void Actual_bone_position_blends_with_normalized_anisotropic_sphere(short weight, short expected)
    {
        NativeBattleEffectArena context = Context(weight: weight).Write(0xA2, BitConverter.GetBytes((short)300)).Write(0xA4, BitConverter.GetBytes((short)-500)).Write(0xA6, BitConverter.GetBytes((short)700));
        NativeBattleEffect79BodyDrawingStep result = Draw(Points([new(100, 0, 0)]), context, Temporary(), 1);
        Assert.Equal(expected, S(result.Rows, 0));
        Assert.Equal((short)0, S(result.Rows, 2));
        Assert.Equal((short)0, S(result.Rows, 4));
        Assert.Equal((short)100, S(result.Temporary168, 0x54));
        Assert.Equal((short)300, S(result.Temporary168, 0xA0));
        Assert.Equal((short)-500, S(result.Temporary168, 0xA2));
        Assert.Equal((short)700, S(result.Temporary168, 0xA4));
        Assert.Equal(8000u, U(result.Rows, 12));
        Assert.Empty(result.Batch.Attempts);
    }

    [Fact]
    public void Relative_WORD_wrap_retains_caller_W_padding_and_exact_snapshot_knownness()
    {
        NativeBattleEffectArena context = Context().Write(0x70, BitConverter.GetBytes((short)-20));
        NativeBattleEffectArena temporary = Temporary().Write(0x52, BitConverter.GetBytes((ushort)0xCAFE));
        NativeBattleEffect79BodyDrawingStep first = Draw(Points([new(32760, 0, 0)]), context, temporary, 1);
        Assert.Equal((short)-32756, S(first.Temporary168, 0x54));
        Assert.Equal((short)32760, S(first.Rows, 0));
        Assert.Equal((ushort)0xCAFE, unchecked((ushort)S(first.Rows, 6)));
        Assert.False(first.Memory.ScriptedLighting!.ColorMatrix!.Known[18]);
        Assert.False(first.Memory.ScriptedLighting.ColorMatrix.Known[19]);
        Assert.Throws<NotSupportedException>(() => first.Rows.Read(16, 16));
        NativeBattleEffect79BodyDrawingStep restored = NativeBattleStorageSnapshots.Restore(JsonSerializer.Deserialize<NativeBattleEffect79BodyDrawingStep>(JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(first)))!);
        NativeBattleEffect79BodyDrawingStep expected = Draw(Points([new(32760, 0, 0)]), context, first.Temporary168, 1, first.Memory, first.Rows);
        NativeBattleEffect79BodyDrawingStep actual = Draw(Points([new(32760, 0, 0)]), context, restored.Temporary168, 1, restored.Memory, restored.Rows);
        Assert.Equal(JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(expected)), JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(actual)));
        NativeBattleEffect79BodyDrawingStep unknownW = Draw(Points([new(100, 0, 0)]), Context(), Temporary(), 1);
        Assert.Throws<NotSupportedException>(() => unknownW.Rows.Read(6, 2));
    }

    [Fact]
    public void Current_geometry_requires_bounded_bones_rows_and_masked_face_indices()
    {
        byte[] source = Geometry(false);
        byte[] invalidBone = (byte[])source.Clone();
        Put(invalidBone, 10, 1);
        Assert.Throws<InvalidDataException>(() => Draw(invalidBone, Context(), Temporary(), 3));
        Assert.Throws<InvalidDataException>(() => Draw(source, Context(), Temporary(), 2));
        byte[] invalidFace = (byte[])source.Clone();
        int face = ((14 + 3 * 6 + 3) & ~3) + 12;
        Put(invalidFace, face, 0x8003);
        Assert.Throws<InvalidDataException>(() => Draw(invalidFace, Context(), Temporary(), 3));
        Put(source, face, 0x8000);
        Assert.Single(Draw(source, Context(), Temporary(), 3).Batch.Attempts);
    }

    [Fact]
    public void Zero_area_still_updates_CPU_rows_and_NCLIP_but_allocates_no_face_packet()
    {
        byte[] source = Geometry(false);
        source.AsSpan(14, 18).Clear();
        NativeBattleEffect79BodyDrawingStep result = Draw(source, Context(), Temporary(), 3);
        Assert.Empty(result.Batch.Attempts);
        Assert.Equal(0, result.Batch.AllocationBytes);
        Assert.Equal(0u, result.Memory.GeometryRegisters!.Mac0);
        Assert.Equal(0x1357u, result.Memory.GeometryRegisters.Otz);
        Assert.Equal(8000u, U(result.Rows, 12));
        Assert.Equal(8000u, U(result.Rows, 44));
    }

    private static NativeBattleEffect79BodyDrawingStep Draw(byte[] source, NativeBattleEffectArena context, NativeBattleEffectArena temporary, int vertices, NativeBattleEffectMemory? memory = null, NativeBattleEffectArena? rows = null)
    {
        memory ??= NativeBattleEffectMemory.FromColdProcess() with
        {
            GeometryRegisters = NativeBattleGeometry.LoadMatrix(NativeBattleGeometryRegisters.Unknown(), Identity, true, true, null) with { Otz = 0x1357 },
            ScriptedColorRegisters = NativeBattleScriptedColorRegisters.Unknown()
        };
        return NativeBattleEffect79BodyDrawing.Draw(memory, context, temporary, (address, length) => NativeBattleEffectArena.Clear().Write(0, source.AsSpan(checked((int)(address - 0x1000)), length)), 0x1000, [NativeBattlePackedMatrix32.FromMatrix(Identity, null)], 0x7F123456, 1, new(null, 100, 160, 108), NativeBattleScriptedGraphicsMemory.FromColdProcess().InitializeGeom56BAA0(), rows ?? NativeBattleEffectArena.Clear().MarkUnknown(0, 128), vertices * 16, NativeBattleGteColorInterpolation.CodeConstants45F270);
    }

    private static NativeBattleEffectArena Context(short weight = 0, short depth = 0)
    {
        NativeBattlePackedMatrix32 basis = NativeBattlePackedMatrix32.FromMatrix(Identity, null);
        NativeBattlePackedMatrix32 view = NativeBattlePackedMatrix32.FromMatrix(Identity with { Z = 8000 }, null);
        NativeBattleEffectArena context = NativeBattleEffectArena.Clear().Write(0, basis.Bytes.AsSpan()).MarkUnknown(18, 2).Write(0x40, view.Bytes.AsSpan()).MarkUnknown(0x52, 2);
        return context.Write(0xA0, BitConverter.GetBytes(weight)).Write(0xD0, BitConverter.GetBytes(depth));
    }

    private static NativeBattleEffectArena Temporary() => NativeBattleEffectArena.Clear().MarkUnknown(0, 168);

    private static byte[] Points(NativeBattleVector[] vertices)
    {
        int counts = (14 + vertices.Length * 6 + 3) & ~3;
        byte[] source = new byte[counts + 12];
        BinaryPrimitives.WriteInt32LittleEndian(source, 1);
        BinaryPrimitives.WriteInt32LittleEndian(source.AsSpan(4), 8);
        Put(source, 8, 1);
        Put(source, 12, vertices.Length);
        for (int index = 0; index < vertices.Length; index++)
        {
            Put(source, 14 + index * 6, vertices[index].X);
            Put(source, 16 + index * 6, vertices[index].Y);
            Put(source, 18 + index * 6, vertices[index].Z);
        }
        return source;
    }

    private static byte[] Geometry(bool quad)
    {
        NativeBattleVector[] vertices = quad ? [new(0, 0, 0), new(100, 0, 0), new(0, 100, 0), new(100, 100, 0)] : [new(0, 0, 0), new(100, 0, 0), new(0, 100, 0)];
        byte[] points = Points(vertices);
        int counts = points.Length - 12;
        byte[] source = new byte[points.Length + (quad ? 20 : 16)];
        points.CopyTo(source, 0);
        Put(source, counts + (quad ? 2 : 0), 1);
        int face = counts + 12;
        Put(source, face + 2, 1);
        Put(source, face + 4, 2);
        Put(source, face + 6, quad ? 3 : 0x8078);
        BinaryPrimitives.WriteUInt32LittleEndian(source.AsSpan(face + 8), 0x3D948048);
        BinaryPrimitives.WriteUInt32LittleEndian(source.AsSpan(face + 12), 0x02B68078);
        if (quad)
        {
            Put(source, face + 16, 0x8078);
            Put(source, face + 18, 0x8048);
        }
        return source;
    }

    private static void Put(byte[] source, int offset, int value) => BinaryPrimitives.WriteUInt16LittleEndian(source.AsSpan(offset), unchecked((ushort)value));
    private static short S(NativeBattleEffectArena source, int offset) => BinaryPrimitives.ReadInt16LittleEndian(source.Read(offset, 2));
    private static uint U(NativeBattleEffectArena source, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(source.Read(offset, 4));
}
