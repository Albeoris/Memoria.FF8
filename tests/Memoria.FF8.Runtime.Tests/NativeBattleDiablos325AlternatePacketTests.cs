using System.Buffers.Binary;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleDiablos325AlternatePacketTests
{
    [Theory]
    [InlineData(1, 24, 40)]
    [InlineData(5, 36, 88)]
    public void Accepted_quad_keeps_separate_native_link_order_and_untouched_physical_tail(int group, int bodyLength, int allocation)
    {
        NativeBattleGfGeometryBatch batch = NativeBattleDiablos325AlternatePackets.Emit657030(Face(group), Vertices(), group, new(0x2030, 0, 0, null, null, 512, 17, 2));
        Assert.Equal(allocation, batch.AllocationBytes);
        Assert.Equal(3, batch.Attempts.Length);
        NativeBattleGfGeometryAttempt before = batch.Attempts[0];
        NativeBattleGfGeometryAttempt face = batch.Attempts[1];
        NativeBattleGfGeometryAttempt after = batch.Attempts[2];
        Assert.Equal(bodyLength, before.ByteOffsetRelative);
        Assert.Equal(0xE1000220u, before.Primitive!.Words[1]);
        Assert.Equal(0, face.ByteOffsetRelative);
        Assert.Equal(bodyLength, face.PacketLength);
        Assert.Equal(bodyLength + 8, after.ByteOffsetRelative);
        Assert.Equal(0xE1000240u, after.Primitive!.Words[1]);
        Assert.Equal(2, after.Primitive.Words.Length);
        Assert.Equal(allocation, after.ByteOffsetRelative + after.PacketLength);
        Assert.All(after.WriteMask.Take(8), value => Assert.Equal(NativeBattleGfPacketByteState.Known, value));
        Assert.All(after.WriteMask.Skip(8), value => Assert.Equal(NativeBattleGfPacketByteState.Untouched, value));
        Assert.Equal(17 + ((25 + 512) >> 2), face.Primitive!.Bucket);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(5)]
    public void First_RTPT_rejection_retains_prefix_stores_without_modes_or_allocation(int group)
    {
        NativeBattleModuleProjectedVertex[] vertices = Vertices();
        vertices[0] = vertices[0] with { Flags = 0x60000 };
        NativeBattleGfGeometryBatch batch = NativeBattleDiablos325AlternatePackets.Emit657030(Face(group), vertices, group, new(0x2030, 0, 0, null, null, 512, 17, 2));
        Assert.Equal(0, batch.AllocationBytes);
        NativeBattleGfGeometryAttempt attempt = Assert.Single(batch.Attempts);
        Assert.Null(attempt.Primitive);
        Assert.Equal(group == 1 ? 0x5000000u : 0x8000000u, BinaryPrimitives.ReadUInt32LittleEndian(attempt.Data.AsSpan()));
    }

    private static byte[] Face(int group)
    {
        byte[] face = new byte[group == 1 ? 12 : 24];
        BinaryPrimitives.WriteUInt32LittleEndian(face, group == 1 ? 0x28808080u : 0x38808080u);
        if (group == 5)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(face.AsSpan(12), 0x112233);
            BinaryPrimitives.WriteUInt32LittleEndian(face.AsSpan(16), 0x445566);
            BinaryPrimitives.WriteUInt32LittleEndian(face.AsSpan(20), 0x778899);
        }
        return face;
    }

    private static NativeBattleModuleProjectedVertex[] Vertices() => [new(0x000A000A, 100, 0), new(0x000A0014, 100, 0), new(0x0014000A, 100, 0), new(0x00140014, 100, 0)];
}
