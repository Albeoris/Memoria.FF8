using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleFullLifeGouraudTexturedTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Packed_uv_carries_precede_material_addition_and_command_clear_takes_precedence(bool quad)
    {
        NativeBattleFullLifeMeshContext context = new(0x1F2A, null, null, 2, 3, 0x10002, 17, 2);
        NativeBattleGfGeometryBatch batch = Emit(quad, Corners(), context);
        NativeBattleGfGeometryAttempt attempt = Assert.Single(batch.Attempts);
        Assert.Equal(quad ? 52 : 40, batch.AllocationBytes);
        Assert.Equal(0x12390001u, Word(attempt, 12));
        Assert.Equal(0xABD00405u, Word(attempt, 24));
        Assert.Equal(quad ? 0x080A0607u : 0x00010607u, Word(attempt, 36));
        if (quad)
        {
            Assert.Equal(0x0000080Au, Word(attempt, 48));
        }
        NativeBattleGeometryPrimitive primitive = attempt.Primitive!;
        Assert.Equal(quad ? 0x3Cu : 0x34u, primitive.Words[1] >> 24);
        Assert.Equal(quad ? new[] { 4096, 4096, 4096, 4096 } : new[] { 4096, 4096, 4096, 0 }, primitive.VertexPayload4);
        NativeBattleGfGeometryAttempt replaced = Assert.Single(Emit(quad, Corners(), context with { Flags = 0x1320 }).Attempts);
        Assert.Equal((ushort)3, BinaryPrimitives.ReadUInt16LittleEndian(replaced.Data.AsSpan().Slice(14)));
        Assert.Equal((ushort)2, BinaryPrimitives.ReadUInt16LittleEndian(replaced.Data.AsSpan().Slice(26)));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Near_rejection_keeps_full_uv_prefix_without_material_or_screen_or_unknown_fade_reads(bool quad)
    {
        NativeBattleModuleProjectedVertex[] corners = Corners();
        corners[0] = corners[0] with { Flags = 0x20000 };
        NativeBattleGfGeometryBatch batch = Emit(quad, corners, new(0x1F80, null, null, 2, 3, 0x10002, 17, 2), true);
        NativeBattleGfGeometryAttempt attempt = Assert.Single(batch.Attempts);
        Assert.Equal(0, batch.AllocationBytes);
        Assert.Null(attempt.Primitive);
        Assert.Equal(0x12360001u, Word(attempt, 12));
        Assert.Equal(0xABCE0405u, Word(attempt, 24));
        Assert.Equal(quad ? 0x080A0607u : 0x00010607u, Word(attempt, 36));
        Assert.All(attempt.WriteMask.Skip(12).Take(4), value => Assert.Equal(NativeBattleGfPacketByteState.Known, value));
        Assert.All(attempt.WriteMask.Skip(8).Take(4), value => Assert.Equal(NativeBattleGfPacketByteState.Untouched, value));
        Assert.All(attempt.WriteMask.Skip(16).Take(4), value => Assert.Equal(NativeBattleGfPacketByteState.Untouched, value));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Negative_winding_rejects_before_fourth_projection_but_after_material_stores(bool quad)
    {
        NativeBattleModuleProjectedVertex[] corners = Corners();
        (corners[1], corners[2]) = (corners[2], corners[1]);
        NativeBattleGfGeometryBatch batch = Emit(quad, corners, new(0x1F00, null, null, 2, 3, 0x10002, 17, 2), true);
        NativeBattleGfGeometryAttempt attempt = Assert.Single(batch.Attempts);
        Assert.Null(attempt.Primitive);
        Assert.Equal(0, batch.AllocationBytes);
        Assert.Equal(0x12390001u, Word(attempt, 12));
        Assert.Equal(0xABD00405u, Word(attempt, 24));
        Assert.Equal(NativeBattleGfPacketByteState.Untouched, attempt.WriteMask[8]);
        Assert.NotNull(Assert.Single(Emit(quad, corners, new(0x20, null, null, null, null, null, 17, 2)).Attempts).Primitive);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Accepted_gouraud_fade_uses_all_source_colors_and_missing_operands_fail_only_after_admission(bool quad)
    {
        NativeBattleFullLifeMeshContext context = new(0x80, 0x112233, 4096, null, null, null, 17, 2);
        NativeBattleGfGeometryAttempt attempt = Assert.Single(Emit(quad, Corners(), context).Attempts);
        foreach (int offset in quad ? new[] { 4, 16, 28, 40 } : new[] { 4, 16, 28 })
        {
            Assert.Equal(0x112233u, Word(attempt, offset) & 0xFFFFFF);
        }
        Assert.Throws<NotSupportedException>(() => Emit(quad, Corners(), context with { Fade = null }));
    }

    private static NativeBattleGfGeometryBatch Emit(bool quad, NativeBattleModuleProjectedVertex[] corners, NativeBattleFullLifeMeshContext context, bool forbidFourth = false)
    {
        NativeBattleModuleProjectedVertex Read(int index) => index == 3 && forbidFourth ? throw new InvalidOperationException("Fourth vertex was consumed before admission.") : corners[index];
        return quad ? NativeBattleRestorationMeshPackets.EmitGt4_885570(Face(true), Read, context, NativeBattleGteColorInterpolation.CodeConstants45F270) : NativeBattleRestorationMeshPackets.EmitGt3_885310(Face(false), Read, context, NativeBattleGteColorInterpolation.CodeConstants45F270);
    }

    private static byte[] Face(bool quad)
    {
        byte[] face = new byte[quad ? 36 : 28];
        BinaryPrimitives.WriteUInt32LittleEndian(face, quad ? 0x3E808080u : 0x36808080u);
        BinaryPrimitives.WriteUInt32LittleEndian(face.AsSpan(12), 0x1234FFFF);
        BinaryPrimitives.WriteUInt32LittleEndian(face.AsSpan(16), 0xABCD0403);
        if (quad)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(face.AsSpan(20), 0x08070605);
            BinaryPrimitives.WriteUInt32LittleEndian(face.AsSpan(24), 0x11223344);
            BinaryPrimitives.WriteUInt32LittleEndian(face.AsSpan(28), 0x55667788);
            BinaryPrimitives.WriteUInt32LittleEndian(face.AsSpan(32), 0x99AABBCC);
        }
        else
        {
            BinaryPrimitives.WriteUInt16LittleEndian(face.AsSpan(10), 0x0605);
            BinaryPrimitives.WriteUInt32LittleEndian(face.AsSpan(20), 0x11223344);
            BinaryPrimitives.WriteUInt32LittleEndian(face.AsSpan(24), 0x55667788);
        }
        return face;
    }

    private static NativeBattleModuleProjectedVertex[] Corners() => [new(XY(100, 100), 4096, 0), new(XY(300, 100), 4096, 0), new(XY(100, 300), 4096, 0), new(XY(300, 300), 4096, 0)];
    private static uint XY(short x, short y) => unchecked((ushort)x) | (uint)unchecked((ushort)y) << 16;
    private static uint Word(NativeBattleGfGeometryAttempt attempt, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(attempt.Data.AsSpan().Slice(offset));
}
