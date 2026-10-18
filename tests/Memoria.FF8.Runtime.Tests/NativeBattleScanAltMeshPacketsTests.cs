using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleScanAltMeshPacketsTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Rejected_prefix_has_ten_word_header_without_draw_mode_or_fourth_projection(bool near)
    {
        int reads = 0;
        NativeBattleGfGeometryAttempt attempt = NativeBattleScanAltMeshPackets.EmitFace8DF200(Face(), index =>
        {
            reads++;
            Assert.True(index < 3);
            return new(Screen(index * 10, index * 10), 1000, near ? 0x20000u : 0u);
        }, Context(), 256);
        Assert.Equal(3, reads);
        Assert.Null(attempt.Primitive);
        Assert.Equal(40, attempt.Data.Length);
        Assert.Equal(0x0A000000u, BinaryPrimitives.ReadUInt32LittleEndian(attempt.Data.AsSpan()));
        Assert.All(attempt.WriteMask.AsSpan().Slice(0, 4).ToArray(), state => Assert.Equal(NativeBattleGfPacketByteState.Known, state));
    }

    [Fact]
    public void Accepted_face_contains_inline_mode_and_actual_signed_ZSF4_depth_without_gating_fourth_flags()
    {
        NativeBattleModuleProjectedVertex[] points = [new(Screen(100, 100), 1000, 0), new(Screen(200, 100), 2000, 0), new(Screen(100, 200), 3000, 0), new(Screen(200, 200), 4000, 0x60000)];
        NativeBattleGfGeometryAttempt attempt = NativeBattleScanAltMeshPackets.EmitFace8DF200(Face(), index => points[index], Context(), -256, NativeBattleGteColorInterpolation.CodeConstants45F270);
        NativeBattleGeometryPrimitive primitive = Assert.IsType<NativeBattleGeometryPrimitive>(attempt.Primitive);
        Assert.Equal(44, attempt.Data.Length);
        Assert.Equal(11, primitive.Words.Length);
        Assert.Equal(0x0A000000u, primitive.Words[0]);
        Assert.Equal(0xE1000220u, primitive.Words[10]);
        uint depth = unchecked(10000u * unchecked((uint)-256)) >> 12;
        Assert.Equal(unchecked(17 + (unchecked((int)depth) >> 2)), primitive.Bucket);
        Assert.Equal(new[] { 1000, 2000, 3000, 4000 }, primitive.VertexPayload4);
        Assert.All(attempt.WriteMask, state => Assert.Equal(NativeBattleGfPacketByteState.Known, state));
        Assert.Equal(attempt.WriteMask, primitive.PhysicalWriteMask);
    }

    [Theory]
    [InlineData(false, 88, 44)]
    [InlineData(true, 44, 0)]
    public void Dispatcher_advances_only_accepted_packets_and_keeps_two_actual_face_records(bool rejectFirst, int allocation, int secondOffset)
    {
        byte[] bytes = new byte[0x88];
        BinaryPrimitives.WriteUInt32LittleEndian(bytes, 0x38);
        short[] coordinates = [-100, -100, 0, 100, -100, 0, -100, 100, 0, 100, 100, 0];
        for (int vertex = 0; vertex < 4; vertex++)
        {
            for (int axis = 0; axis < 3; axis++)
            {
                BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(8 + vertex * 8 + axis * 2), coordinates[vertex * 3 + axis]);
            }
        }
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(0x44), 2);
        Face().CopyTo(bytes, 0x48);
        Face().CopyTo(bytes, 0x60);
        if (rejectFirst)
        {
            bytes.AsSpan(0x4C, 8).Clear();
        }
        NativeBattleActionMatrix matrix = new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], 0, 0, 1000, 4096);
        NativeBattleCameraProjection projection = new(matrix, 216, 160, 108);
        NativeBattleGfGeometryBatch batch = NativeBattleScanAltMeshPackets.Emit8DE9D0(new(0, 0x15CF674, bytes.ToImmutableArray()), matrix, projection, Context(), 256, 0, 0, NativeBattleGteColorInterpolation.CodeConstants45F270);
        Assert.Equal(allocation, batch.AllocationBytes);
        Assert.Equal(2, batch.Attempts.Length);
        Assert.Equal(secondOffset, batch.Attempts[1].ByteOffsetRelative);
    }

    [Fact]
    public void Entirely_offscreen_quad_never_appends_draw_mode()
    {
        NativeBattleModuleProjectedVertex[] points = [new(Screen(-100, 100), 1000, 0), new(Screen(-200, 100), 1000, 0), new(Screen(-100, 200), 1000, 0), new(Screen(-200, 200), 1000, 0)];
        NativeBattleGfGeometryAttempt attempt = NativeBattleScanAltMeshPackets.EmitFace8DF200(Face(), index => points[index], Context(), 256);
        Assert.Null(attempt.Primitive);
        Assert.Equal(40, attempt.Data.Length);
        Assert.All(attempt.WriteMask, state => Assert.Equal(NativeBattleGfPacketByteState.Known, state));
    }

    private static NativeBattleGfGeometryContext Context() => new(0xF0, 0, 0, null, null, 0, 17, 2);
    private static uint Screen(int x, int y) => unchecked((ushort)x | (uint)(ushort)y << 16);
    private static byte[] Face()
    {
        byte[] face = new byte[24];
        BinaryPrimitives.WriteUInt32LittleEndian(face, 0x2E808080);
        for (int index = 0; index < 4; index++)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(face.AsSpan(4 + index * 2), (ushort)(index * 2));
        }
        BinaryPrimitives.WriteUInt32LittleEndian(face.AsSpan(12), 0x11112222);
        BinaryPrimitives.WriteUInt32LittleEndian(face.AsSpan(16), 0x33334444);
        BinaryPrimitives.WriteUInt32LittleEndian(face.AsSpan(20), 0x55556666);
        return face;
    }
}
