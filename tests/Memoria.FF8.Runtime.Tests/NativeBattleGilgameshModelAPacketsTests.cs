using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleGilgameshModelAPacketsTests
{
    [Fact]
    public void Accepted_FT3_and_FT4_write_every_byte_sign_extend_UV_and_publish_actual_FIFO_depths()
    {
        NativeBattleRawMesh mesh = Mesh(Quad(), Triangle());
        NativeBattleGilgameshModelAReveal result = Emit(mesh, Arena(2), 1, 0x80808080);
        Assert.Equal(72, result.Packets.AllocationBytes);
        Assert.Equal(0x1004, result.LabelEndOffset);
        Assert.Equal(new[] { 0, 32 }, result.Packets.Attempts.Select(attempt => attempt.ByteOffsetRelative).ToArray());
        NativeBattleGeometryPrimitive triangle = result.Packets.Attempts[0].Primitive!;
        NativeBattleGeometryPrimitive quad = result.Packets.Attempts[1].Primitive!;
        Assert.Equal(0x07000000u, triangle.Words[0]);
        Assert.Equal(0x09000000u, quad.Words[0]);
        Assert.Equal(0x80808080u | 0x02000000u | 0x24000000u, triangle.Words[1]);
        Assert.Equal(0x11223344u, triangle.Words[3]);
        Assert.Equal(0x00345678u, triangle.Words[5]);
        Assert.Equal(0xFFFFFEDCu, triangle.Words[7]);
        Assert.Equal(0xFEDC1234u, quad.Words[7]);
        Assert.Equal(0xFFFFFEDCu, quad.Words[9]);
        Assert.Equal(new[] { 3000, 3000, 3000, 0 }, triangle.VertexPayload4.ToArray());
        Assert.Equal(new[] { 3000, 3000, 3000, 3000 }, quad.VertexPayload4.ToArray());
        Assert.All(result.Packets.Attempts, attempt => Assert.All(attempt.WriteMask, state => Assert.Equal(NativeBattleGfPacketByteState.Known, state)));
        Assert.Equal(17 + (int)((unchecked(9000u * 0x155) >> 12) >> 2), triangle.Bucket);
        Assert.Equal(17 + (int)((unchecked(12000u * 0x100) >> 12) >> 2), quad.Bucket);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Rejected_candidates_keep_the_four_prefix_DWORD_stores_and_all_other_heap_bytes(bool nearClip)
    {
        NativeBattleRawMesh mesh = Mesh(nearClip ? Triangle() : Triangle(true));
        NativeBattleGilgameshModelAReveal result = NativeBattleGilgameshModelAPackets.Emit596220(mesh, Arena(1), World, Labels, 1, View() with { Z = nearClip ? 100 : 3000 }, Projection(), 0x808080, null);
        NativeBattleGfGeometryAttempt rejected = Assert.Single(result.Packets.Attempts);
        Assert.Null(rejected.Primitive);
        Assert.Equal(0, result.Packets.AllocationBytes);
        int[] known = [0, 1, 2, 3, 4, 5, 6, 7, 12, 13, 14, 15, 20, 21, 22, 23];
        for (int index = 0; index < 32; index++)
        {
            Assert.Equal(known.Contains(index) ? NativeBattleGfPacketByteState.Known : NativeBattleGfPacketByteState.Untouched, rejected.WriteMask[index]);
        }
        NativeBattlePrimitiveProcess process = NativeBattlePrimitiveProcess.FromColdProcess().Initialize500400() with { PacketBank = "off_B6D070", ByteCursor = 32 };
        process = process.ApplyCommonWrite(32, Enumerable.Repeat((byte)0xA5, 32).ToImmutableArray(), Enumerable.Repeat(NativeBattleGfPacketByteState.Known, 32).ToImmutableArray());
        NativeBattleCommonPacketWrite written = NativeBattleCommonPacketArena.Append(process, NativeBattleLogicalOrdering.Clear(), result.Packets);
        Assert.Equal(32, written.Process.ByteCursor);
        Assert.Empty(written.Ordering.Proxies);
        byte[] actual = written.Process.ReadCommonPacket(32, 32);
        for (int index = 0; index < 32; index++)
        {
            Assert.Equal(known.Contains(index) ? rejected.Data[index] : (byte)0xA5, actual[index]);
        }
    }

    [Fact]
    public void Rejected_attempt_reuses_the_same_common_cursor_and_label_skip_has_no_projection_or_writes()
    {
        NativeBattleGilgameshModelAReveal result = Emit(Mesh(Triangle(true), Triangle()), Arena(2));
        Assert.Equal(new[] { 0, 0 }, result.Packets.Attempts.Select(attempt => attempt.ByteOffsetRelative).ToArray());
        Assert.Null(result.Packets.Attempts[0].Primitive);
        Assert.NotNull(result.Packets.Attempts[1].Primitive);
        Assert.Equal(32, result.Packets.AllocationBytes);
        NativeBattleEffectArena unknown = Arena(1).MarkUnknown(World, 32);
        unknown = PutWord(unknown, Labels, 0x4002);
        NativeBattleGilgameshModelAReveal skipped = NativeBattleGilgameshModelAPackets.Emit596220(Mesh(Triangle()), unknown, World, Labels, 1, null!, null!, 0, null);
        Assert.Empty(skipped.Packets.Attempts);
        Assert.Equal(Labels + 2, skipped.LabelEndOffset);
    }

    [Fact]
    public void Quad_fourth_projection_is_after_first_triple_admission_and_its_flags_do_not_reject()
    {
        NativeBattleEffectArena arena = Arena(1);
        arena = Point(arena, 3, new(100, 100, -4000));
        NativeBattleGilgameshModelAReveal result = Emit(Mesh(Quad()), arena);
        NativeBattleGeometryPrimitive primitive = Assert.Single(result.Packets.Attempts).Primitive!;
        Assert.NotNull(primitive);
        Assert.Equal(0, primitive.VertexPayload4[3]);
        byte[] bad = Quad().Bytes.ToArray();
        BinaryPrimitives.WriteUInt16LittleEndian(bad.AsSpan(6), 0xFFFF);
        BinaryPrimitives.WriteUInt16LittleEndian(bad.AsSpan(2), 2);
        BinaryPrimitives.WriteUInt16LittleEndian(bad.AsSpan(4), 1);
        NativeBattleEffectArena unresolved = Arena(1).MarkUnknown(World + 0xFFF * 8, 6);
        NativeBattleGilgameshModelAReveal rejected = Emit(Mesh(new NativeBattleRawPolygon(true, bad.ToImmutableArray())), unresolved);
        Assert.Null(Assert.Single(rejected.Packets.Attempts).Primitive);
    }

    [Fact]
    public void Reveal_uses_logical_unsigned_low32_AVSZ_with_actual_signed_ZSF_and_requires_only_consumed_registers()
    {
        NativeBattleScriptedGraphicsMemory negative = Registers() with { Zsf3 = -1 };
        NativeBattleGilgameshModelAReveal result = NativeBattleGilgameshModelAPackets.Emit596220(Mesh(Triangle()), Arena(1), World, Labels, 1, View(), Projection(), 0x808080, negative);
        Assert.Equal(17 + (int)((unchecked(9000u * uint.MaxValue) >> 12) >> 2), result.Packets.Attempts[0].Primitive!.Bucket);
        Assert.Throws<NotSupportedException>(() => NativeBattleGilgameshModelAPackets.Emit596220(Mesh(Triangle()), Arena(1), World, Labels, 1, View(), Projection(), 0, Registers() with { Zsf3 = null }));
        Assert.NotNull(NativeBattleGilgameshModelAPackets.Emit596220(Mesh(Quad()), Arena(1), World, Labels, 1, View(), Projection(), 0, Registers() with { Zsf3 = null }).Packets.Attempts[0].Primitive);
    }

    [Fact]
    public void Reveal_does_not_apply_an_extra_viewport_rejection()
    {
        NativeBattleEffectArena arena = Arena(1);
        arena = Point(arena, 0, new(2000, -100, 0));
        arena = Point(arena, 1, new(2200, -100, 0));
        arena = Point(arena, 2, new(2000, 100, 0));
        Assert.NotNull(Emit(Mesh(Triangle()), arena).Packets.Attempts[0].Primitive);
    }

    [Fact]
    public void Label_setup_truncates_negative_centers_divides_by3000_ORs_existing_high_bits_and_keeps_unused_tail()
    {
        NativeBattleEffectArena arena = Arena(2);
        arena = Point(arena, 0, new(-150, 0, 0));
        arena = Point(arena, 1, new(-150, 0, 0));
        arena = Point(arena, 2, new(-149, 0, 0));
        for (int index = 3; index < 7; index++)
        {
            arena = Point(arena, index, new(150, 0, 0));
        }
        byte[] quad = Quad().Bytes.ToArray();
        for (int corner = 0; corner < 4; corner++)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(quad.AsSpan(corner * 2), (ushort)(0xF000 | corner + 3));
        }
        arena = PutWord(arena, Labels + 2, 0x4004);
        arena = PutWord(arena, Labels + 4, 0x4567);
        NativeBattleGilgameshModelALabels result = NativeBattleGilgameshModelAPackets.SetupLabels5964B0(Mesh(new(true, quad.ToImmutableArray()), Triangle()), arena, World, Labels);
        Assert.Equal((ushort)0x4000, Word(result.Arena, Labels));
        Assert.Equal((ushort)0x4005, Word(result.Arena, Labels + 2));
        Assert.Equal((ushort)0x4567, Word(result.Arena, Labels + 4));
        Assert.Equal(Labels + 4, result.LabelEndOffset);
    }

    [Fact]
    public void Negative_squared_radius_and_colored_polygon_contracts_fail_fast()
    {
        NativeBattleEffectArena arena = Arena(1);
        for (int index = 0; index < 3; index++)
        {
            arena = Point(arena, index, new(short.MinValue, 0, short.MinValue));
        }
        Assert.Throws<NotSupportedException>(() => NativeBattleGilgameshModelAPackets.SetupLabels5964B0(Mesh(Triangle()), arena, World, Labels));
        Assert.Throws<NotSupportedException>(() => Emit(Mesh(new NativeBattleRawPolygon(false, new byte[20].ToImmutableArray())), Arena(1)));
    }

    private const int World = 0x100;
    private const int Labels = 0x1000;
    private static NativeBattleActionMatrix View() => new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], 0, 0, 3000, 4096);
    private static NativeBattleCameraProjection Projection() => new(null, 288, 160, 108);
    private static NativeBattleScriptedGraphicsMemory Registers() => NativeBattleScriptedGraphicsMemory.FromColdProcess().InitializeGeom56BAA0();
    private static NativeBattleGilgameshModelAReveal Emit(NativeBattleRawMesh mesh, NativeBattleEffectArena arena, int phase = 1, uint color = 0x808080) => NativeBattleGilgameshModelAPackets.Emit596220(mesh, arena, World, Labels, phase, View(), Projection(), color, Registers());
    private static NativeBattleRawMesh Mesh(params NativeBattleRawPolygon[] polygons) => new([], polygons.ToImmutableArray());
    private static ushort Word(NativeBattleEffectArena arena, int offset) => BinaryPrimitives.ReadUInt16LittleEndian(arena.Read(offset, 2));

    private static NativeBattleEffectArena Arena(int labels)
    {
        NativeBattleEffectArena arena = NativeBattleEffectArena.Clear();
        arena = Point(arena, 0, new(-100, -100, 0));
        arena = Point(arena, 1, new(100, -100, 0));
        arena = Point(arena, 2, new(-100, 100, 0));
        arena = Point(arena, 3, new(100, 100, 0));
        for (int index = 0; index < labels; index++)
        {
            arena = PutWord(arena, Labels + index * 2, 0x4000);
        }
        return arena;
    }

    private static NativeBattleEffectArena Point(NativeBattleEffectArena arena, int index, NativeBattleVector value)
    {
        byte[] bytes = new byte[8];
        BinaryPrimitives.WriteInt16LittleEndian(bytes, value.X);
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(2), value.Y);
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(4), value.Z);
        return arena.Write(World + index * 8, bytes);
    }

    private static NativeBattleEffectArena PutWord(NativeBattleEffectArena arena, int offset, ushort value)
    {
        byte[] bytes = new byte[2];
        BinaryPrimitives.WriteUInt16LittleEndian(bytes, value);
        return arena.Write(offset, bytes);
    }

    private static NativeBattleRawPolygon Triangle(bool reversed = false)
    {
        byte[] bytes = new byte[16];
        BinaryPrimitives.WriteUInt16LittleEndian(bytes, 0xF000);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(2), reversed ? (ushort)2 : (ushort)1);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(4), reversed ? (ushort)1 : (ushort)2);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(6), 0xFEDC);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(8), 0x11223344);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(12), 0xF2345678);
        return new(false, bytes.ToImmutableArray());
    }

    private static NativeBattleRawPolygon Quad()
    {
        byte[] bytes = new byte[20];
        Triangle().Bytes.AsSpan().CopyTo(bytes);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(6), 3);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(16), 0xFEDC1234);
        return new(true, bytes.ToImmutableArray());
    }
}
