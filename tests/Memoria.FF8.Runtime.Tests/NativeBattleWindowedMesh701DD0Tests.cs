using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleWindowedMesh701DD0Tests
{
    [Fact]
    public void Ft3PublishesExactCursorFlagsNclipBiasedDepthAndWindowPackets()
    {
        NativeBattleFileStaging staging = NativeBattleFileStaging.Unknown().Write(0x1A77238, Enumerable.Repeat((byte)0xA5, 88).ToArray());
        NativeBattleWindowedMesh701DD0Result result = Draw(staging, Context() with { UOffset = 10, VOffset = 10 });
        Assert.Equal(56, result.Batch.AllocationBytes);
        Assert.Equal(new[] { 0, 32, 0, 44 }, result.Batch.Attempts.Select(attempt => attempt.ByteOffsetRelative).ToArray());
        NativeBattleGeometryPrimitive triangle = result.Batch.Attempts[2].Primitive!;
        Assert.Equal(261, triangle.Bucket);
        Assert.Equal(0x26112233u, triangle.Words[1]);
        Assert.Equal(0x12349484u, triangle.Words[3]);
        Assert.Equal(0x56788494u, triangle.Words[5]);
        Assert.Equal(0x0000949Eu, triangle.Words[7]);
        Assert.Equal(0x1150054u, U32(result.Staging, 52));
        Assert.Equal(10404u, U32(result.Staging, 56));
        Assert.Equal(979u, U32(result.Staging, 64));
        Assert.Equal(0u, U32(result.Staging, 68));
        Assert.Equal(Enumerable.Repeat((byte)0xA5, 52).ToArray(), result.Staging.Read(0x1A77238, 52));
        Assert.Equal(0xA5A5A5A5u, U32(result.Staging, 60));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void RejectedFt3KeepsOriginalColorBeforeDeferredSemiFlags(bool gteRejected)
    {
        NativeBattleFileStaging staging = NativeBattleFileStaging.Unknown().Write(0x1A77238, Enumerable.Repeat((byte)0xA5, 88).ToArray());
        NativeBattleWindowedMesh701DD0Result result = Draw(staging, Context() with { Flags = 0x2001 }, gteRejected, !gteRejected);
        NativeBattleGfGeometryAttempt attempt = Assert.Single(result.Batch.Attempts);
        Assert.Null(attempt.Primitive);
        Assert.Equal(0, result.Batch.AllocationBytes);
        Assert.Equal(0x24112233u, BinaryPrimitives.ReadUInt32LittleEndian(attempt.Data.AsSpan().Slice(4)));
        Assert.Equal(NativeBattleGfPacketByteState.Untouched, attempt.WriteMask[8]);
        Assert.Equal(NativeBattleGfPacketByteState.Known, attempt.WriteMask[12]);
        Assert.Equal(0xA5A5A5A5u, U32(result.Staging, 64));
        Assert.Equal(gteRejected ? 0xA5A5A5A5u : unchecked((uint)-10404), U32(result.Staging, 56));
    }

    [Fact]
    public void ZeroNclipIsAcceptedAndNegativeBiasedDepthClampsToZero()
    {
        NativeBattleFileStaging staging = NativeBattleFileStaging.Unknown().Write(0x1A77238, new byte[88]);
        NativeBattleWindowedMesh701DD0Result result = Draw(staging, Context() with { DepthBias = -2000 }, zeroArea: true);
        Assert.Equal(17, Assert.Single(result.Batch.Attempts).Primitive!.Bucket);
        Assert.Equal(0u, U32(result.Staging, 56));
        Assert.Equal(0u, U32(result.Staging, 64));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Gt3RetainsRawRejectedSourceAndPublishesAcceptedGouraudWindowStores(bool rejected)
    {
        NativeBattleFileStaging staging = NativeBattleFileStaging.Unknown().Write(0x1A77238, Enumerable.Repeat((byte)0xA5, 88).ToArray());
        NativeBattleWindowedMesh701DD0Result result = Draw(staging, Context() with { Flags = 0x2002, UOffset = 10, VOffset = 10 }, gteRejected: rejected, gouraud: true);
        if (rejected)
        {
            NativeBattleGfGeometryAttempt attempt = Assert.Single(result.Batch.Attempts);
            Assert.Null(attempt.Primitive);
            Assert.Equal(0, result.Batch.AllocationBytes);
            Assert.Equal(0x34112233u, BinaryPrimitives.ReadUInt32LittleEndian(attempt.Data.AsSpan().Slice(4)));
            Assert.Equal(NativeBattleGfPacketByteState.Untouched, attempt.WriteMask[16]);
            Assert.Equal(NativeBattleGfPacketByteState.Known, attempt.WriteMask[36]);
        }
        else
        {
            Assert.Equal(64, result.Batch.AllocationBytes);
            NativeBattleGeometryPrimitive primitive = result.Batch.Attempts[2].Primitive!;
            Assert.Equal(0x36112233u, primitive.Words[1]);
            Assert.Equal(0x00445566u, primitive.Words[4]);
            Assert.Equal(0x00778899u, primitive.Words[7]);
            Assert.Equal(0x12349484u, primitive.Words[3]);
            Assert.Equal(0x56788494u, primitive.Words[6]);
            Assert.Equal(0x0000949Eu, primitive.Words[9]);
            Assert.Equal(0x115005Cu, U32(result.Staging, 52));
        }
    }

    private static NativeBattleWindowedMesh701DD0Result Draw(NativeBattleFileStaging staging, NativeBattleEsunaMeshContext context, bool gteRejected = false, bool reversed = false, bool zeroArea = false, bool gouraud = false)
    {
        ImmutableArray<byte> mesh = Mesh(gouraud);
        NativeBattleVector Vertex(int offset)
        {
            int corner = offset / 8;
            if (reversed)
            {
                corner = corner == 0 ? 1 : corner == 1 ? 0 : 2;
            }
            return new(corner == 1 ? (short)100 : (short)-100, zeroArea || corner != 2 ? (short)-100 : (short)100, gteRejected ? (short)0 : (short)4000);
        }
        return NativeBattleWindowedMesh701DD0.Emit(staging, 0x1A77238, 0x1150000, mesh, NativeBattleGfMatrices.Euler56CE30(new(0, 0, 0)), new(null, 256, 160, 108), context, NativeBattleGteColorInterpolation.CodeConstants45F270, Vertex);
    }

    private static NativeBattleEsunaMeshContext Context() => new(0x2001, null, null, -20, 0, 0, new(0, 0, 256, 256), new(0, 0, 128, 128), 17, 2);
    private static uint U32(NativeBattleFileStaging staging, uint offset) => BinaryPrimitives.ReadUInt32LittleEndian(staging.Read(0x1A77238 + offset, 4));
    private static ImmutableArray<byte> Mesh(bool gouraud)
    {
        if (gouraud)
        {
            byte[] triangle = new byte[92];
            BinaryPrimitives.WriteUInt32LittleEndian(triangle, 32);
            BinaryPrimitives.WriteUInt32LittleEndian(triangle.AsSpan(4), 3);
            BinaryPrimitives.WriteUInt32LittleEndian(triangle.AsSpan(56), 1);
            BinaryPrimitives.WriteUInt32LittleEndian(triangle.AsSpan(60), 0x34112233);
            BinaryPrimitives.WriteUInt16LittleEndian(triangle.AsSpan(64), 0);
            BinaryPrimitives.WriteUInt16LittleEndian(triangle.AsSpan(66), 2);
            BinaryPrimitives.WriteUInt16LittleEndian(triangle.AsSpan(68), 4);
            BinaryPrimitives.WriteUInt16LittleEndian(triangle.AsSpan(70), 0x0A14);
            BinaryPrimitives.WriteUInt32LittleEndian(triangle.AsSpan(72), 0x12340AFA);
            BinaryPrimitives.WriteUInt32LittleEndian(triangle.AsSpan(76), 0x5678FA0A);
            BinaryPrimitives.WriteUInt32LittleEndian(triangle.AsSpan(80), 0x00445566);
            BinaryPrimitives.WriteUInt32LittleEndian(triangle.AsSpan(84), 0x00778899);
            return triangle.ToImmutableArray();
        }
        byte[] mesh = new byte[84];
        BinaryPrimitives.WriteUInt32LittleEndian(mesh, 32);
        BinaryPrimitives.WriteUInt32LittleEndian(mesh.AsSpan(4), 3);
        BinaryPrimitives.WriteUInt32LittleEndian(mesh.AsSpan(40), 1);
        BinaryPrimitives.WriteUInt32LittleEndian(mesh.AsSpan(44), 0x24112233);
        BinaryPrimitives.WriteUInt16LittleEndian(mesh.AsSpan(48), 0);
        BinaryPrimitives.WriteUInt16LittleEndian(mesh.AsSpan(50), 2);
        BinaryPrimitives.WriteUInt16LittleEndian(mesh.AsSpan(52), 4);
        BinaryPrimitives.WriteUInt16LittleEndian(mesh.AsSpan(54), 0x0A14);
        BinaryPrimitives.WriteUInt32LittleEndian(mesh.AsSpan(56), 0x12340AFA);
        BinaryPrimitives.WriteUInt32LittleEndian(mesh.AsSpan(60), 0x5678FA0A);
        return mesh.ToImmutableArray();
    }
}
