using System.Buffers.Binary;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleDuel274MeshPacketsTests
{
    [Theory]
    [InlineData(false, 28, 44, 208)]
    [InlineData(true, 36, 52, 209)]
    public void GouraudLeavesKeepSourceColorsAndSignedDepthBiasWithExactE1Insertion(bool quad, int polygonBytes, int allocation, int bucket)
    {
        byte[] face = Gouraud(quad);
        NativeBattleGfGeometryBatch batch = NativeBattleDuel274MeshPackets.EmitGouraud68BDB0(face, index => Corners()[index], quad, -256, true);

        Assert.Equal(allocation, batch.AllocationBytes);
        Assert.Equal(new[] { 0, polygonBytes, 0, polygonBytes + 8 }, batch.Attempts.Select(attempt => attempt.ByteOffsetRelative));
        Assert.Null(batch.Attempts[0].Primitive);
        Assert.Equal(new uint[] { 0xE1000020, quad ? 0x3A303030u : 0x32303030u, 0xE1000040 }, batch.Attempts.Skip(1).Select(attempt => attempt.Primitive!.Words[1]));
        Assert.All(batch.Attempts.Skip(1), attempt => Assert.Equal(bucket, attempt.Primitive!.Bucket));
        Assert.Equal(0x112233u, Word(batch.Attempts[0], 12));
        Assert.Equal(0x445566u, Word(batch.Attempts[0], 20));
        Assert.Equal(quad ? new int[] { 4096, 4096, 4096, 4096 } : [4096, 4096, 4096, 0], batch.Attempts[2].Primitive!.VertexPayload4);
    }

    [Fact]
    public void NegativeBiasedDepthRejectsAfterColorStoresWithoutClampingOrAllocating()
    {
        NativeBattleGfGeometryBatch batch = NativeBattleDuel274MeshPackets.EmitGouraud68BDB0(Gouraud(false), index => Corners()[index], false, -1024, true);

        Assert.Equal(0, batch.AllocationBytes);
        NativeBattleGfGeometryAttempt attempt = Assert.Single(batch.Attempts);
        Assert.Null(attempt.Primitive);
        Assert.Equal(0x112233u, Word(attempt, 12));
        Assert.Equal(0x445566u, Word(attempt, 20));
        Assert.All(attempt.WriteMask, value => Assert.Equal(NativeBattleGfPacketByteState.Known, value));
    }

    [Fact]
    public void FirstThreeFlagsRejectBeforeFourthProjectionAndLeaveOnlyNativePrefixStores()
    {
        NativeBattleModuleProjectedVertex[] corners = Corners();
        corners[0] = corners[0] with { Flags = 0x20000 };
        NativeBattleGfGeometryBatch batch = NativeBattleDuel274MeshPackets.EmitGouraud68BDB0(Gouraud(true), index => index == 3 ? throw new InvalidOperationException("Fourth projection is unreached.") : corners[index], true, 0, true);

        Assert.Equal(0, batch.AllocationBytes);
        NativeBattleGfGeometryAttempt attempt = Assert.Single(batch.Attempts);
        Assert.All(attempt.WriteMask.Take(8), value => Assert.Equal(NativeBattleGfPacketByteState.Known, value));
        Assert.All(attempt.WriteMask.Skip(8), value => Assert.Equal(NativeBattleGfPacketByteState.Untouched, value));
    }

    [Fact]
    public void TexturedModeForcesBothWindowStoresEvenAtZeroScroll()
    {
        NativeBattleGfGeometryBatch batch = NativeBattleDuel274MeshPackets.EmitFt3_68C350(Textured(), index => Corners()[index], 0);

        Assert.Equal(56, batch.AllocationBytes);
        Assert.Equal(new[] { 0, 32, 0, 44 }, batch.Attempts.Select(attempt => attempt.ByteOffsetRelative));
        Assert.Equal(0xE2000000u, batch.Attempts[1].Primitive!.Words[1]);
        Assert.Equal(0xE2000200u, batch.Attempts[3].Primitive!.Words[1]);
        Assert.Equal(0x24808080u, batch.Attempts[2].Primitive!.Words[1]);
        Assert.Equal(32, batch.Attempts[0].PacketLength);
    }

    [Fact]
    public void TexturedModeUsesAggregateVerticalOverflowAndPreservesMaterialWords()
    {
        NativeBattleGfGeometryBatch batch = NativeBattleDuel274MeshPackets.EmitFt3_68C350(Textured(), index => Corners()[index], 120);
        NativeBattleGfGeometryAttempt stores = batch.Attempts[0];

        Assert.Equal(new byte[] { 192, 242, 52 }, new[] { stores.Data[13], stores.Data[21], stores.Data[29] });
        Assert.Equal((ushort)0x1234, BinaryPrimitives.ReadUInt16LittleEndian(stores.Data.AsSpan().Slice(14)));
        Assert.Equal((ushort)0xABCD, BinaryPrimitives.ReadUInt16LittleEndian(stores.Data.AsSpan().Slice(22)));
        Assert.Equal(new byte[] { 1, 3, 5 }, new[] { stores.Data[12], stores.Data[20], stores.Data[28] });
    }

    [Fact]
    public void TexturedModeRejectsBackfacesWithoutBorrowingGouraudDoubleSidedFlag()
    {
        NativeBattleModuleProjectedVertex[] corners = Corners();
        (corners[1], corners[2]) = (corners[2], corners[1]);
        NativeBattleGfGeometryBatch batch = NativeBattleDuel274MeshPackets.EmitFt3_68C350(Textured(), index => corners[index], 0);

        Assert.Equal(0, batch.AllocationBytes);
        Assert.Null(Assert.Single(batch.Attempts).Primitive);
    }

    private static NativeBattleModuleProjectedVertex[] Corners() => [new(XY(100, 100), 4096, 0), new(XY(300, 100), 4096, 0), new(XY(100, 300), 4096, 0), new(XY(300, 300), 4096, 0)];
    private static uint XY(short x, short y) => unchecked((ushort)x) | unchecked((uint)(ushort)y << 16);
    private static uint Word(NativeBattleGfGeometryAttempt attempt, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(attempt.Data.AsSpan().Slice(offset));

    private static byte[] Gouraud(bool quad)
    {
        byte[] face = new byte[quad ? 24 : 20];
        BinaryPrimitives.WriteUInt32LittleEndian(face, quad ? 0x3A303030u : 0x32303030u);
        BinaryPrimitives.WriteUInt32LittleEndian(face.AsSpan(12), 0x112233);
        BinaryPrimitives.WriteUInt32LittleEndian(face.AsSpan(16), 0x445566);
        if (quad)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(face.AsSpan(20), 0x778899);
        }
        return face;
    }

    private static byte[] Textured()
    {
        byte[] face = new byte[20];
        BinaryPrimitives.WriteUInt32LittleEndian(face, 0x24808080);
        BinaryPrimitives.WriteUInt16LittleEndian(face.AsSpan(10), 0x3C05);
        BinaryPrimitives.WriteUInt32LittleEndian(face.AsSpan(12), 0x1234C801);
        BinaryPrimitives.WriteUInt32LittleEndian(face.AsSpan(16), 0xABCDFA03);
        return face;
    }
}
