using System.Buffers.Binary;
using System.Collections.Immutable;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleCinematicMonsterPacketsTests
{
    [Fact]
    public void GlowTriangleUsesActualRawIndexUvClutAndPageLayoutWithAverageDepth()
    {
        byte[] record = new byte[16];
        ushort[] values = [0, 1, 2, 0x1122, 0x3344, 0x5566, 0x7788, 0x99AA];
        for (int index = 0; index < values.Length; index++)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(record.AsSpan(index * 2), values[index]);
        }
        NativeBattleRawMesh mesh = new([], [new(false, record.ToImmutableArray())]);
        NativeBattleProjectedVertex[] projected = [new(0, 16), new(8, 32), new(8u << 16, 48)];
        NativeBattleGfGeometryBatch batch = NativeBattleCinematicMonsterPackets.Glow(mesh, projected, new uint[] { 0x00010203, 0x00040506, 0x00070809 }, 17);
        NativeBattleGfGeometryAttempt attempt = Assert.Single(batch.Attempts);
        Assert.Equal(40, batch.AllocationBytes);
        Assert.Equal(19, attempt.Primitive!.Bucket);
        Assert.Equal(0x34010203u, attempt.Primitive.Words[1]);
        Assert.Equal(0x55663344u, attempt.Primitive.Words[3]);
        Assert.Equal(0x99AA7788u, attempt.Primitive.Words[6]);
        Assert.Equal((ushort)0x1122, BinaryPrimitives.ReadUInt16LittleEndian(attempt.Data.AsSpan().Slice(36)));
        Assert.Equal(new[] { 16, 32, 48, 0 }, attempt.Primitive.VertexPayload4);
        Assert.Equal(NativeBattleGfPacketByteState.Untouched, attempt.WriteMask[38]);
        Assert.Equal(NativeBattleGfPacketByteState.Untouched, attempt.WriteMask[39]);
    }

    [Fact]
    public void GlowCopiesUnknownColorHighBytesButKeepsTheFirstCommandByteKnown()
    {
        byte[] record = new byte[16];
        BinaryPrimitives.WriteUInt16LittleEndian(record.AsSpan(2), 1);
        BinaryPrimitives.WriteUInt16LittleEndian(record.AsSpan(4), 2);
        NativeBattleRawMesh mesh = new([], [new(false, record.ToImmutableArray())]);
        NativeBattleProjectedVertex[] vertices = [new(0, 16), new(8, 32), new(8u << 16, 48)];
        NativeBattleGfGeometryAttempt result = Assert.Single(NativeBattleCinematicMonsterPackets.Glow(mesh, vertices, new uint[3], 0, new[] { false, false, false }).Attempts);
        Assert.Equal(NativeBattleGfPacketByteState.Known, result.WriteMask[7]);
        Assert.Equal(NativeBattleGfPacketByteState.Unknown, result.WriteMask[19]);
        Assert.Equal(NativeBattleGfPacketByteState.Unknown, result.WriteMask[31]);
        Assert.Equal((byte)0x34, result.Data[7]);
    }

    [Fact]
    public void GlowUsesTheOriginalNonnegativeBackfaceGateAndRetainsZeroArea()
    {
        byte[] record = new byte[16];
        BinaryPrimitives.WriteUInt16LittleEndian(record.AsSpan(2), 1);
        BinaryPrimitives.WriteUInt16LittleEndian(record.AsSpan(4), 2);
        NativeBattleRawMesh mesh = new([], [new(false, record.ToImmutableArray())]);
        NativeBattleProjectedVertex[] back = [new(0, 16), new(8u << 16, 16), new(8, 16)];
        Assert.Empty(NativeBattleCinematicMonsterPackets.Glow(mesh, back, new uint[3], 0).Attempts);
        Assert.Single(NativeBattleCinematicMonsterPackets.Glow(mesh, new NativeBattleProjectedVertex[] { new(0, 16), new(0, 16), new(0, 16) }, new uint[3], 0).Attempts);
    }
}

