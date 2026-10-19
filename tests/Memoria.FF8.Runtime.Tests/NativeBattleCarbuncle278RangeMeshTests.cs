using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleCarbuncle278RangeMeshTests
{
    private static NativeBattleActionMatrix View => new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], 0, 0, 4096, 4096);

    [Fact]
    public void Range_mesh_preserves_three_groups_native_packet_sizes_and_ordered_window_brackets()
    {
        NativeBattleCarbuncle278RangeDraw draw = Draw(Mesh(), 0, 0, 0, 32);
        Assert.False(draw.FarColorWritten);
        Assert.Equal(204, draw.Packets.AllocationBytes);
        Assert.Equal(12, draw.Packets.Attempts.Length);
        Assert.Equal(new[] { 0, 40, 0, 52, 64, 104, 64, 116, 128, 180, 128, 192 }, draw.Packets.Attempts.Select(attempt => attempt.ByteOffsetRelative).ToArray());
        for (int group = 0; group < 3; group++)
        {
            NativeBattleGfGeometryAttempt prefix = draw.Packets.Attempts[group * 4];
            Assert.Null(prefix.Primitive);
            Assert.Equal(0xE2000000u, draw.Packets.Attempts[group * 4 + 1].Primitive!.Words[1]);
            Assert.Equal(NativeBattleEffectTextureWindows.Encode(new(0, 0, 64, 128)), draw.Packets.Attempts[group * 4 + 3].Primitive!.Words[1]);
            Assert.Equal((byte)144, prefix.Data[13]);
        }
        NativeBattleCarbuncle278RangeDraw cue = Draw(Mesh(), 0, 0, 2048, 0);
        Assert.True(cue.FarColorWritten);
        Assert.NotEqual(draw.Packets.Attempts[0].Data[4], cue.Packets.Attempts[0].Data[4]);
    }

    [Fact]
    public void Signed_source_Z_band_checks_fourth_quad_corner_before_any_prefix_or_projection()
    {
        NativeBattleCarbuncle278RangeDraw empty = Draw(Mesh(), 1, 2, 2048, 0);
        Assert.Empty(empty.Packets.Attempts);
        Assert.Equal(0, empty.Packets.AllocationBytes);
        Assert.False(empty.FarColorWritten);
        byte[] fourth = Mesh().Bytes.ToArray();
        BinaryPrimitives.WriteInt16LittleEndian(fourth.AsSpan(8 + 3 * 8 + 4), 10);
        NativeBattleCarbuncle278RangeDraw triangle = Draw(new(0, 0, fourth.ToImmutableArray()), 0, 0, 0, 0);
        Assert.Equal(64, triangle.Packets.AllocationBytes);
        Assert.Equal(4, triangle.Packets.Attempts.Length);
        Assert.Equal(40, triangle.Packets.Attempts[0].PacketLength);
        NativeBattleCarbuncle278RangeDraw clipped = NativeBattleCarbuncle278RangeMesh.Emit682760(Mesh(), View with { Z = 0 }, new(View, 288, 160, 108), 0, 0, 2048, 0, NativeBattleGteColorInterpolation.CodeConstants45F270);
        Assert.Equal(0, clipped.Packets.AllocationBytes);
        Assert.Equal(3, clipped.Packets.Attempts.Length);
        Assert.All(clipped.Packets.Attempts, attempt =>
        {
            Assert.Equal(0, attempt.ByteOffsetRelative);
            Assert.Null(attempt.Primitive);
            Assert.Equal(NativeBattleGfPacketByteState.Known, attempt.WriteMask[4]);
            Assert.Equal(NativeBattleGfPacketByteState.Untouched, attempt.WriteMask[8]);
        });
        Assert.False(clipped.FarColorWritten);
    }

    private static NativeBattleCarbuncle278RangeDraw Draw(NativeBattleStaticMeshResource mesh, short minimum, short maximum, int fade, ushort offset) => NativeBattleCarbuncle278RangeMesh.Emit682760(mesh, View, new(View, 288, 160, 108), minimum, maximum, fade, offset, NativeBattleGteColorInterpolation.CodeConstants45F270);

    private static NativeBattleStaticMeshResource Mesh()
    {
        byte[] bytes = new byte[160];
        BinaryPrimitives.WriteInt32LittleEndian(bytes, 40);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(4), 4);
        short[] coordinates = [-10, -10, 10, -10, -10, 10, 10, 10];
        for (int vertex = 0; vertex < 4; vertex++)
        {
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(8 + vertex * 8), coordinates[vertex * 2]);
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(10 + vertex * 8), coordinates[vertex * 2 + 1]);
        }
        Face(52, 3, 24);
        Face(88, 6, 28);
        Face(120, 7, 36);
        return new(0, 0, bytes.ToImmutableArray());

        void Face(int offset, int group, int length)
        {
            BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(offset), 1);
            Span<byte> face = bytes.AsSpan(offset + 4, length);
            BinaryPrimitives.WriteUInt32LittleEndian(face, group == 3 ? 0x2C123456u : group == 6 ? 0x34123456u : 0x3C123456u);
            int count = group == 6 ? 3 : 4;
            for (int corner = 0; corner < count; corner++)
            {
                BinaryPrimitives.WriteUInt16LittleEndian(face[(4 + corner * 2)..], (ushort)(corner * 2));
            }
            BinaryPrimitives.WriteUInt32LittleEndian(face[12..], 0x0020F0F0);
            BinaryPrimitives.WriteUInt32LittleEndian(face[16..], 0x0002F0FA);
            BinaryPrimitives.WriteUInt32LittleEndian(face[20..], group == 6 ? 0x00556677u : 0xFAFAF5F0u);
            if (group != 3)
            {
                BinaryPrimitives.WriteUInt32LittleEndian(face[24..], 0x00223344);
            }
            if (group == 7)
            {
                BinaryPrimitives.WriteUInt32LittleEndian(face[28..], 0x00556677);
                BinaryPrimitives.WriteUInt32LittleEndian(face[32..], 0x00778899);
            }
        }
    }
}
