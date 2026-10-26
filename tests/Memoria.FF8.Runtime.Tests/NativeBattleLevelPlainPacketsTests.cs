using System.Buffers.Binary;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleLevelPlainPacketsTests
{
    [Theory]
    [InlineData(false, 48, 3)]
    [InlineData(true, 52, 4)]
    public void Special_quads_keep_raw_UVs_and_colors_and_exact_E1_allocation_and_link_order(bool gouraud, int allocation, int attempts)
    {
        byte[] face = Face(gouraud);
        NativeBattleGfGeometryBatch batch = NativeBattleLevelPlainPackets.EmitFace(face, gouraud, Corner, Context(-10000), null!, 17, 2, 100);
        Assert.Equal(allocation, batch.AllocationBytes);
        Assert.Equal(attempts, batch.Attempts.Length);
        Assert.Equal(0xE1000220u, BinaryPrimitives.ReadUInt32LittleEndian(batch.Attempts[1].Data.AsSpan().Slice(4, 4)));
        Assert.Equal(100, batch.Attempts[2].ByteOffsetRelative);
        Assert.Equal(17, batch.Attempts[2].Primitive!.Bucket);
        if (gouraud)
        {
            Assert.Equal(0xE1000240u, BinaryPrimitives.ReadUInt32LittleEndian(batch.Attempts[3].Data.AsSpan().Slice(4, 4)));
            Assert.Equal(0x00112233u, BinaryPrimitives.ReadUInt32LittleEndian(batch.Attempts[0].Data.AsSpan().Slice(12, 4)));
        }
        else
        {
            Assert.Equal(250, batch.Attempts[0].Data[13]);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Initial_projection_rejection_writes_no_mode_packet_and_does_not_read_the_fourth_vertex(bool gouraud)
    {
        NativeBattleGfGeometryBatch batch = NativeBattleLevelPlainPackets.EmitFace(Face(gouraud), gouraud, index =>
        {
            Assert.InRange(index, 0, 2);
            return Corner(index) with { Flags = 0x60000 };
        }, Context(0), null!, 17, 2);
        Assert.Equal(0, batch.AllocationBytes);
        Assert.Null(Assert.Single(batch.Attempts).Primitive);
    }

    private static NativeBattleEffectArena Context(int bias) => NativeBattleEffectArena.Clear().Write(12, new byte[4]).Write(24, BitConverter.GetBytes(bias));

    private static NativeBattleModuleProjectedVertex Corner(int index)
    {
        uint[] points = [100u | 100u << 16, 100u | 200u << 16, 200u | 100u << 16, 200u | 200u << 16];
        return new(points[index], 1000, 0);
    }

    private static byte[] Face(bool gouraud)
    {
        byte[] bytes = new byte[24];
        BinaryPrimitives.WriteUInt32LittleEndian(bytes, gouraud ? 0x3A808080u : 0x2E808080u);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(12), gouraud ? 0x00112233u : 0x0055FA66u);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(16), 0x00445566);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(20), 0x00778899);
        return bytes;
    }
}
