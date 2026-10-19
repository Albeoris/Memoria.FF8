using System.Buffers.Binary;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleDiablos325BodyPacketTests
{
    [Theory]
    [InlineData(64, 65, 66, 32, 0x26000000)]
    [InlineData(64, 63, 64, 40, 0x34000000)]
    public void Bitwise_shade_threshold_selects_flat_or_gradient_and_only_flat_retains_source_transparency(byte a, byte b, byte c, int length, uint code)
    {
        byte[] polygon = new byte[16];
        BinaryPrimitives.WriteUInt16LittleEndian(polygon, 0x9000);
        BinaryPrimitives.WriteUInt16LittleEndian(polygon.AsSpan(2), 0xA001);
        BinaryPrimitives.WriteUInt16LittleEndian(polygon.AsSpan(4), 0xB002);
        BinaryPrimitives.WriteUInt16LittleEndian(polygon.AsSpan(6), 0x8877);
        BinaryPrimitives.WriteUInt32LittleEndian(polygon.AsSpan(8), 0x12345678);
        BinaryPrimitives.WriteUInt32LittleEndian(polygon.AsSpan(12), 0xABCD9876);
        NativeBattleProjectedVertex[] vertices = [Vertex(0, 0, 100, a), Vertex(8, 0, 200, b), Vertex(0, 8, 300, c)];
        NativeBattleGfGeometryAttempt packet = Assert.IsType<NativeBattleGfGeometryAttempt>(NativeBattleDiablos325BodyPackets.Emit6576A0(polygon, false, index => vertices[index], 0x808080, 0x155, 12));
        Assert.Equal(length, packet.Data.Length);
        Assert.Equal(12, packet.ByteOffsetRelative);
        Assert.Equal(17 + ((600u * 0x155) >> 12), (uint)packet.Primitive!.Bucket);
        Assert.Equal(code, packet.Primitive.Words[1] & 0xFF000000);
        Assert.Equal(new[] { 0, 0, 0, 0 }, packet.Primitive.VertexPayload4);
        int pageOffset = length == 32 ? 20 : 24;
        Assert.Equal(0xA9CD9876u, BinaryPrimitives.ReadUInt32LittleEndian(packet.Data.AsSpan().Slice(pageOffset)));
        int tailOffset = length == 32 ? 28 : 36;
        Assert.Equal((ushort)0x8877, BinaryPrimitives.ReadUInt16LittleEndian(packet.Data.AsSpan().Slice(tailOffset)));
        Assert.Equal(NativeBattleGfPacketByteState.Untouched, packet.WriteMask[tailOffset + 2]);
        if (length == 40)
        {
            Assert.Equal(0x00404040u, packet.Primitive.Words[1] & 0xFFFFFF);
            Assert.Equal(0x003F3F3Fu, packet.Primitive.Words[4]);
            Assert.Equal(NativeBattleGfPacketByteState.Known, packet.WriteMask[19]);
        }
    }

    [Fact]
    public void Acceptance_uses_any_depth_any_shade_and_shared_clip_with_degenerate_front_face_allowed()
    {
        byte[] polygon = new byte[16];
        BinaryPrimitives.WriteUInt16LittleEndian(polygon.AsSpan(2), 1);
        BinaryPrimitives.WriteUInt16LittleEndian(polygon.AsSpan(4), 2);
        NativeBattleProjectedVertex[] vertices = [Vertex(0, 0, 0, 0), Vertex(0, 0, 10, 1), Vertex(0, 0, 0, 0)];
        Assert.NotNull(NativeBattleDiablos325BodyPackets.Emit6576A0(polygon, false, index => vertices[index], 0, 0x155, 0));
        vertices[1] = Vertex(0, 0, 10, 0);
        Assert.Null(NativeBattleDiablos325BodyPackets.Emit6576A0(polygon, false, index => vertices[index], 0, 0x155, 0));
        vertices[1] = Vertex(0, 0, 0, 1);
        Assert.Null(NativeBattleDiablos325BodyPackets.Emit6576A0(polygon, false, index => vertices[index], 0, 0x155, 0));
    }

    private static NativeBattleProjectedVertex Vertex(short x, short y, ushort depth, byte shade) => new(unchecked((ushort)x) | unchecked((uint)y << 16), depth | (uint)shade << 24);
}
