using System.Buffers.Binary;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleWindowedTrianglePacketsTests
{
    [Theory]
    [InlineData(false, 40, 0x26112233u)]
    [InlineData(true, 48, 0x36112233u)]
    public void TriangleRetainsNativeBracketLengthUvMaterialAndColorMultiplication(bool gouraud, int length, uint color)
    {
        NativeBattleGfGeometryBatch batch = NativeBattleHoly175WindowedMeshPackets.EmitTriangle(Face(gouraud), Vertex, gouraud, Context());
        NativeBattleGfGeometryAttempt attempt = Assert.Single(batch.Attempts);
        NativeBattleGeometryPrimitive primitive = attempt.Primitive!;
        Assert.Equal(length, batch.AllocationBytes);
        Assert.Equal((uint)(length / 4 - 1) << 24, primitive.Words[0]);
        Assert.Equal(color, primitive.Words[2]);
        Assert.Equal(0x1234903Fu, primitive.Words[4]);
        Assert.Equal(0u, primitive.Words[gouraud ? 10 : 8] >> 16);
        Assert.Equal(0xE2000000u, primitive.Words[^1]);
        Assert.Equal(66, primitive.Bucket);
        Assert.All(attempt.WriteMask, state => Assert.Equal(NativeBattleGfPacketByteState.Known, state));
        if (gouraud)
        {
            Assert.Equal(0x00112233u, primitive.Words[5]);
            Assert.Equal(0x00112233u, primitive.Words[8]);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void GteRejectionPreservesOnlyPrefixBeforeUvAdjustment(bool gouraud)
    {
        NativeBattleGfGeometryBatch batch = NativeBattleHoly175WindowedMeshPackets.EmitTriangle(Face(gouraud), corner => Vertex(corner) with { Flags = 0x60000 }, gouraud, Context() with { Flags = 0xF3, Fade = null });
        NativeBattleGfGeometryAttempt attempt = Assert.Single(batch.Attempts);
        Assert.Equal(0, batch.AllocationBytes);
        Assert.Null(attempt.Primitive);
        Assert.Equal(0, attempt.Data[16]);
        Assert.Equal(NativeBattleGfPacketByteState.Known, attempt.WriteMask[16]);
        Assert.Equal(NativeBattleGfPacketByteState.Untouched, attempt.WriteMask[4]);
        Assert.Equal(NativeBattleGfPacketByteState.Untouched, attempt.WriteMask[12]);
        Assert.Equal(gouraud ? NativeBattleGfPacketByteState.Untouched : NativeBattleGfPacketByteState.Known, attempt.WriteMask[8]);
    }

    private static NativeBattleHoly175WindowedMeshContext Context() => new(0x33, 0, null, 0x00112233, -65, 160, new(0, 128, 64, 128), 17, 2);
    private static NativeBattleModuleProjectedVertex Vertex(int corner) => new(corner == 0 ? 0x000A000Au : corner == 1 ? 0x000A0064u : 0x0064000Au, 800, 0);

    private static byte[] Face(bool gouraud)
    {
        byte[] face = new byte[gouraud ? 28 : 20];
        BinaryPrimitives.WriteUInt32LittleEndian(face, 0x34808080);
        BinaryPrimitives.WriteUInt16LittleEndian(face.AsSpan(10), 0xF014);
        BinaryPrimitives.WriteUInt32LittleEndian(face.AsSpan(12), 0x1234F000);
        BinaryPrimitives.WriteUInt32LittleEndian(face.AsSpan(16), 0x5678F0FA);
        if (gouraud)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(face.AsSpan(20), 0x00808080);
            BinaryPrimitives.WriteUInt32LittleEndian(face.AsSpan(24), 0x00808080);
        }
        return face;
    }
}
