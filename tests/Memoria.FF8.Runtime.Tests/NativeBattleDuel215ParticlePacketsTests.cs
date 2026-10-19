using System.Buffers.Binary;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleDuel215ParticlePacketsTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void AcceptedG3KeepsItsColorsZeroPayloadAndNativeTriangleAverage(bool zeroArea)
    {
        NativeBattleDuel215ParticleObservation? observed = null;
        NativeBattleGfGeometryBatch batch = NativeBattleDuel215ParticlePackets.Emit69CE00(Face(), corner => Vertex(corner, zeroArea), 17, 2, observe: value => observed = value);
        NativeBattleGfGeometryAttempt attempt = Assert.Single(batch.Attempts);
        NativeBattleGeometryPrimitive primitive = attempt.Primitive!;
        Assert.Equal(28, batch.AllocationBytes);
        Assert.Equal(66, primitive.Bucket);
        Assert.Equal(0x06000000u, primitive.Words[0]);
        Assert.Equal(0x30010203u, primitive.Words[1]);
        Assert.Equal(0x00040506u, primitive.Words[3]);
        Assert.Equal(0x00070809u, primitive.Words[5]);
        Assert.Equal(new[] { 0, 0, 0, 0 }, primitive.VertexPayload4);
        Assert.Equal(new NativeBattleDuel215ParticleObservation(0, zeroArea ? 0 : 8100, 199), observed);
        Assert.All(attempt.WriteMask, state => Assert.Equal(NativeBattleGfPacketByteState.Known, state));
    }

    [Fact]
    public void NegativeAreaRejectsAfterAllScreenAndColorStoresWithoutWritingAverageDepth()
    {
        NativeBattleDuel215ParticleObservation? observed = null;
        NativeBattleGfGeometryBatch batch = NativeBattleDuel215ParticlePackets.Emit69CE00(Face(), corner => Vertex(corner == 0 ? 1 : corner == 1 ? 0 : 2, false), 17, 2, observe: value => observed = value);
        NativeBattleGfGeometryAttempt attempt = Assert.Single(batch.Attempts);
        Assert.Null(attempt.Primitive);
        Assert.Equal(0, batch.AllocationBytes);
        Assert.All(attempt.WriteMask, state => Assert.Equal(NativeBattleGfPacketByteState.Known, state));
        Assert.Equal(new NativeBattleDuel215ParticleObservation(0, -8100, null), observed);
    }

    [Fact]
    public void GteRejectionRetainsOnlyTagAndFirstColor()
    {
        NativeBattleDuel215ParticleObservation? observed = null;
        NativeBattleGfGeometryBatch batch = NativeBattleDuel215ParticlePackets.Emit69CE00(Face(), corner => Vertex(corner, false) with { Flags = 0x80020000 }, 17, 2, observe: value => observed = value);
        NativeBattleGfGeometryAttempt attempt = Assert.Single(batch.Attempts);
        Assert.Null(attempt.Primitive);
        Assert.All(attempt.WriteMask.Take(8), state => Assert.Equal(NativeBattleGfPacketByteState.Known, state));
        Assert.All(attempt.WriteMask.Skip(8), state => Assert.Equal(NativeBattleGfPacketByteState.Untouched, state));
        Assert.Equal(new NativeBattleDuel215ParticleObservation(0x80020000, null, null), observed);
    }

    private static NativeBattleModuleProjectedVertex Vertex(int corner, bool zeroArea) => new((uint)(corner == 1 ? 100 : 10) | (uint)(zeroArea || corner != 2 ? 10 : 100) << 16, 800, 0);
    private static byte[] Face()
    {
        byte[] face = new byte[20];
        BinaryPrimitives.WriteUInt32LittleEndian(face, 0x30010203);
        BinaryPrimitives.WriteUInt32LittleEndian(face.AsSpan(12), 0x00040506);
        BinaryPrimitives.WriteUInt32LittleEndian(face.AsSpan(16), 0x00070809);
        return face;
    }
}
