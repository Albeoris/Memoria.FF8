using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleGilgameshSwordPacketsTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void FT_emitters_preserve_high_UV_padding_and_actual_common_bytes_and_use_FIFO_depths(bool quad)
    {
        ImmutableArray<NativeBattleGilgameshSwordClipVertex> points = Points().Take(quad ? 4 : 3).ToImmutableArray();
        NativeBattleGfGeometryAttempt attempt = NativeBattleGilgameshSwordPackets.EmitChosen(points, 0xAABB1234, 0xCCDD5678, 0x808080, View(), Projection(), Registers());
        Assert.Equal(quad ? 40 : 32, attempt.PacketLength);
        Assert.Equal(quad ? 0x2C808080u : 0x24808080u, attempt.Primitive!.Words[1]);
        Assert.Equal(0xAABB1001u, attempt.Primitive.Words[3]);
        Assert.Equal(0xCCDD2002u, attempt.Primitive.Words[5]);
        Assert.Equal(quad ? new[] { 3000, 3000, 3000, 3000 } : new[] { 3000, 3000, 3000, 0 }, attempt.Primitive.VertexPayload4.ToArray());
        Assert.Equal(NativeBattleGfPacketByteState.Untouched, attempt.WriteMask[30]);
        Assert.Equal(NativeBattleGfPacketByteState.Untouched, attempt.WriteMask[31]);
        if (quad)
        {
            Assert.Equal(NativeBattleGfPacketByteState.Untouched, attempt.WriteMask[38]);
            Assert.Equal(NativeBattleGfPacketByteState.Untouched, attempt.WriteMask[39]);
        }
        NativeBattlePrimitiveProcess process = NativeBattlePrimitiveProcess.FromColdProcess().Initialize500400() with { PacketBank = "off_B6D070", ByteCursor = 32 };
        process = process.ApplyCommonWrite(32, Enumerable.Repeat((byte)0xA5, attempt.PacketLength).ToImmutableArray(), Enumerable.Repeat(NativeBattleGfPacketByteState.Known, attempt.PacketLength).ToImmutableArray());
        NativeBattleCommonPacketWrite written = NativeBattleCommonPacketArena.Append(process, NativeBattleLogicalOrdering.Clear(), new([attempt], attempt.PacketLength));
        Assert.Equal(new byte[] { 0xA5, 0xA5 }, written.Process.ReadCommonPacket(62, 2));
        if (quad)
        {
            Assert.Equal(new byte[] { 0xA5, 0xA5 }, written.Process.ReadCommonPacket(70, 2));
        }
    }

    [Fact]
    public void Quad_second_triple_has_no_NCLIP_or_flags_gate_and_keeps_first_material_high_words()
    {
        NativeBattleEffectArena arena = Arena();
        NativeBattleGfGeometryBatch batch = NativeBattleGilgameshSwordPackets.Emit595550(Mesh(true), arena, 0x100, View(), Projection(), 0x808080, Registers());
        Assert.Equal(64, batch.AllocationBytes);
        Assert.Equal(2, batch.Attempts.Length);
        Assert.Equal(new[] { 0, 32 }, batch.Attempts.Select(attempt => attempt.ByteOffsetRelative).ToArray());
        Assert.Equal(0xAABB4004u, batch.Attempts[1].Primitive!.Words[3]);
        Assert.Equal(0xCCDD2002u, batch.Attempts[1].Primitive!.Words[5]);
        arena = Point(arena, 3, new(100, -100, -4000));
        Assert.Equal(2, NativeBattleGilgameshSwordPackets.Emit595550(Mesh(true), arena, 0x100, View(), Projection(), 0x808080, Registers()).Attempts.Length);
    }

    [Fact]
    public void First_triple_rejection_skips_both_quad_halves_before_fourth_vertex_read()
    {
        NativeBattleRawMesh mesh = Mesh(true);
        byte[] face = mesh.Polygons[0].Bytes.ToArray();
        BinaryPrimitives.WriteUInt16LittleEndian(face.AsSpan(2), 2);
        BinaryPrimitives.WriteUInt16LittleEndian(face.AsSpan(4), 1);
        BinaryPrimitives.WriteUInt16LittleEndian(face.AsSpan(6), 0xFFFF);
        mesh = mesh with { Polygons = [new(true, face.ToImmutableArray())] };
        NativeBattleEffectArena arena = Arena().MarkUnknown(0x100 + 0xFFF * 8, 8);
        Assert.Empty(NativeBattleGilgameshSwordPackets.Emit595550(mesh, arena, 0x100, View(), Projection(), 0x808080, null).Attempts);
    }

    [Fact]
    public void Mask_world_Y_is_used_before_view_and_FT_emission_does_not_reject_selected_near_clip_points()
    {
        NativeBattleEffectArena arena = Arena();
        NativeBattleActionMatrix translated = View() with { Y = 1000 };
        Assert.Single(NativeBattleGilgameshSwordPackets.Emit595550(Mesh(false), arena, 0x100, translated, Projection(), 0x808080, Registers()).Attempts);
        NativeBattleGfGeometryAttempt near = NativeBattleGilgameshSwordPackets.EmitChosen(Points().Take(3).ToImmutableArray(), 0, 0, 0x808080, View() with { Z = -1000 }, Projection(), Registers());
        Assert.NotNull(near.Primitive);
        Assert.Equal(new[] { 0, 0, 0, 0 }, near.Primitive!.VertexPayload4.ToArray());
    }

    [Fact]
    public void Actual_command_classification_and_unsigned_low32_AVSZ_use_current_registers_and_masked_shift()
    {
        ImmutableArray<NativeBattleGilgameshSwordClipVertex> points = Points().Take(3).ToImmutableArray();
        NativeBattleGfGeometryAttempt high = NativeBattleGilgameshSwordPackets.EmitChosen(points, 0, 0, 0x80808080, View(), Projection(), Registers());
        Assert.Equal(0xA4808080u, high.Primitive!.Words[1]);
        Assert.Equal(new[] { 0, 0, 0, 0 }, high.Primitive.VertexPayload4.ToArray());
        Assert.Throws<NotSupportedException>(() => NativeBattleGilgameshSwordPackets.EmitChosen(points, 0, 0, 0x08808080, View(), Projection(), Registers()));
        NativeBattleGfGeometryAttempt logical = NativeBattleGilgameshSwordPackets.EmitChosen(points, 0, 0, 0x808080, View(), Projection(), Registers() with { Zsf3 = -1 }, 34);
        Assert.Equal(17 + (int)((unchecked(9000u * uint.MaxValue) >> 12) >> 2), logical.Primitive!.Bucket);
    }

    [Fact]
    public void Sword_skinning_writes_saturated_XYZ_but_retains_unknown_fourth_WORD_and_tail()
    {
        NativeBattleRawMesh mesh = new([new(new(100, -100, 20), 0)], []);
        NativeBattleEffectArena arena = NativeBattleEffectArena.Clear().Write(0x100, Enumerable.Repeat((byte)0xA5, 16).ToArray()).MarkUnknown(0x106, 2);
        NativeBattleEffectArena written = NativeBattleGilgameshSwords.SkinWorld595250(mesh, [View() with { X = 40000, Y = -40000, Z = 50 }], arena, 0x100);
        Assert.Equal(short.MaxValue, BinaryPrimitives.ReadInt16LittleEndian(written.Read(0x100, 2)));
        Assert.Equal(short.MinValue, BinaryPrimitives.ReadInt16LittleEndian(written.Read(0x102, 2)));
        Assert.Equal((short)70, BinaryPrimitives.ReadInt16LittleEndian(written.Read(0x104, 2)));
        Assert.Throws<NotSupportedException>(() => written.Read(0x106, 2));
        Assert.Equal(Enumerable.Repeat((byte)0xA5, 8).ToArray(), written.Read(0x108, 8));
    }

    private static NativeBattleGilgameshSwordClipVertex[] Points() => [new(new(-100, -200, 0), 0x1001), new(new(100, -200, 0), 0x2002), new(new(-100, -100, 0), 0x3003), new(new(100, -100, 0), 0x4004)];
    private static NativeBattleActionMatrix View() => new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], 0, 0, 3000, 4096);
    private static NativeBattleCameraProjection Projection() => new(null, 288, 160, 108);
    private static NativeBattleScriptedGraphicsMemory Registers() => NativeBattleScriptedGraphicsMemory.FromColdProcess().InitializeGeom56BAA0();

    private static NativeBattleEffectArena Arena()
    {
        NativeBattleEffectArena arena = NativeBattleEffectArena.Clear();
        for (int index = 0; index < 4; index++)
        {
            arena = Point(arena, index, Points()[index].Position).MarkUnknown(0x100 + index * 8 + 6, 2);
        }
        return arena;
    }

    private static NativeBattleEffectArena Point(NativeBattleEffectArena arena, int index, NativeBattleVector point)
    {
        byte[] bytes = new byte[6];
        BinaryPrimitives.WriteInt16LittleEndian(bytes, point.X);
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(2), point.Y);
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(4), point.Z);
        return arena.Write(0x100 + index * 8, bytes);
    }

    private static NativeBattleRawMesh Mesh(bool quad)
    {
        byte[] bytes = new byte[quad ? 20 : 16];
        BinaryPrimitives.WriteUInt16LittleEndian(bytes, 0xF000);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(2), 1);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(4), 2);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(6), quad ? (ushort)3 : (ushort)0x3003);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(8), 0xAABB1001);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(12), 0xCCDD2002);
        if (quad)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(16), 0x40043003);
        }
        return new([], [new(quad, bytes.ToImmutableArray())]);
    }
}
