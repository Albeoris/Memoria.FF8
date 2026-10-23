using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleBlizzagaModelBandTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ProjectionStoresQuarterDepthBeforePacketOrderingAndProxyExpansion(bool quad)
    {
        NativeBattleRawMesh mesh = new([new(new(0, 0, 0), 0), new(new(10000, 0, 0), 0), new(new(0, 10000, 0), 0), new(new(10000, 10000, 0), 0)], [new(quad, Polygon(quad, [3, 4, 5, 6]))]);
        NativeBattleBlizzagaModelBandResult batch = NativeBattleBlizzagaModelBand.Emit6D8D80([mesh], [Matrix(0, 65535)], Projection, 0, 1, -1, 1);
        Assert.All(batch.VertexScratchWrites, write => Assert.Equal((ushort)16383, BinaryPrimitives.ReadUInt16LittleEndian(write.Data.AsSpan().Slice(4, 2))));
        NativeBattleGeometryPrimitive packet = Assert.Single(batch.Primitives);
        Assert.InRange(packet.Bucket, 0, NativeBattlePrimitiveOrdering.BattleTableLength - 1);
        Assert.Equal(65532, packet.VertexPayload4[0]);
        Assert.Single(NativeBattleLogicalOrdering.Clear().Prepend(packet.Bucket, new("COMMON", 0, 0), packet.Words, 0, packet.VertexPayload4).Packets);
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(3, 0)]
    [InlineData(4, 1)]
    [InlineData(17500, 4375)]
    [InlineData(65535, 16383)]
    public void DepthRead45E250PreservesTheRawProjectionRegister(ushort raw, ushort stored)
    {
        NativeBattleModuleProjectedVertex vertex = new(0, raw, 0);
        Assert.Equal(stored, vertex.ReadDepth45E250());
        Assert.Equal(raw, vertex.Depth);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void BandOverridePreservesVAndSourceSemiflagAndUsesExplicitDepthProxies(bool quad)
    {
        ImmutableArray<byte> polygon = Polygon(quad, [64, 127, 63, 128]);
        NativeBattleProjectedVertex[] vertices = Vertices();
        vertices[1] = vertices[1] with { DepthAndClip = 200u | 0x1000000u };
        NativeBattleGeometryPrimitive result = NativeBattleBlizzagaModelBand.EmitPolygon(polygon, quad, vertices, 0x00123456)!;
        Assert.Equal(quad ? 0x2E123456u : 0x26123456u, result.Words[1]);
        Assert.Equal(0x3D141000u, result.Words[3]);
        Assert.Equal(0x00BA203Fu, result.Words[5]);
        Assert.Equal(0x3000u, result.Words[7]);
        Assert.Equal(quad ? new[] { 400, 800, 1200, 1600 } : new[] { 400, 800, 1200, 0 }, result.VertexPayload4);
        Assert.Equal(17 + ((quad ? 1000 * 0x100 : 600 * 0x155) >> 12), result.Bucket);
        Assert.Equal(NativeBattleGfPacketByteState.Untouched, result.PhysicalWriteMask!.Value[30]);
        if (quad)
        {
            Assert.Equal(0x4000u, result.Words[9]);
            Assert.Equal(NativeBattleGfPacketByteState.Untouched, result.PhysicalWriteMask.Value[38]);
        }
        Assert.Equal((byte)64, polygon[8]);
    }

    [Fact]
    public void OutsideBandRetainsClutClearsTpageBit200AndDoesNotUseModelDepthBias()
    {
        ImmutableArray<byte> polygon = Polygon(false, [3, 4, 5, 6]);
        byte[] biased = polygon.ToArray();
        BinaryPrimitives.WriteUInt16LittleEndian(biased, 0xF000);
        NativeBattleGeometryPrimitive result = NativeBattleBlizzagaModelBand.EmitPolygon(biased.ToImmutableArray(), false, Vertices(), 0xABCDEF)!;
        Assert.Equal(0x11111003u, result.Words[3]);
        Assert.Equal(0x01AA2004u, result.Words[5]);
        Assert.Equal(new[] { 400, 800, 1200, 0 }, result.VertexPayload4);
        Assert.Equal(0x26ABCDEFu, result.Words[1]);
    }

    [Fact]
    public void LowClipFlagsRejectDepthZeroAndSharedOutsideEdgesButBandFlagsDoNotClip()
    {
        ImmutableArray<byte> source = Polygon(false, [3, 4, 5, 6]);
        NativeBattleProjectedVertex[] vertices = Vertices();
        vertices[0] = vertices[0] with { DepthAndClip = 0x100000 };
        Assert.Null(NativeBattleBlizzagaModelBand.EmitPolygon(source, false, vertices, 0));
        vertices = Vertices().Select(vertex => vertex with { DepthAndClip = vertex.DepthAndClip | 0x10000 }).ToArray();
        Assert.Null(NativeBattleBlizzagaModelBand.EmitPolygon(source, false, vertices, 0));
        vertices = Vertices();
        (vertices[1], vertices[2]) = (vertices[2], vertices[1]);
        Assert.Null(NativeBattleBlizzagaModelBand.EmitPolygon(source, false, vertices, 0));
        vertices = Vertices().Select(vertex => vertex with { DepthAndClip = vertex.DepthAndClip | 0x1000000 }).ToArray();
        Assert.NotNull(NativeBattleBlizzagaModelBand.EmitPolygon(source, false, vertices, 0));
    }

    [Fact]
    public void ProjectionUsesSaturatedIr2InclusiveBandAndNativeElseIfBounds()
    {
        NativeBattleRawMesh mesh = new([new(new(0, 30, 0), 0), new(new(0, 20, 0), 0), new(new(0, 10, 0), 0)], []);
        NativeBattleActionMatrix matrix = Matrix(0, 4096);
        NativeBattleBlizzagaModelBandResult decreasing = NativeBattleBlizzagaModelBand.Emit6D8D80([mesh], [matrix], Projection, 0, 1, 10, 20);
        Assert.Equal((short)10, decreasing.Minimum);
        Assert.Equal((short)-32767, decreasing.Maximum);
        Assert.Equal(new ushort[] { 0, 0x100, 0x100 }, decreasing.VertexScratchWrites.Select(write => BinaryPrimitives.ReadUInt16LittleEndian(write.Data.AsSpan().Slice(6, 2))));
        NativeBattleBlizzagaModelBandResult saturated = NativeBattleBlizzagaModelBand.Emit6D8D80([mesh], [Matrix(40000, 4096)], Projection, 0, 1, 32767, 32767);
        Assert.Equal(short.MaxValue, saturated.Minimum);
        Assert.Equal(short.MaxValue, saturated.Maximum);
        Assert.All(saturated.VertexScratchWrites, write => Assert.NotEqual(0, BinaryPrimitives.ReadUInt16LittleEndian(write.Data.AsSpan().Slice(6, 2)) & 0x100));
        NativeBattleBlizzagaModelBandResult depthZero = NativeBattleBlizzagaModelBand.Emit6D8D80([mesh], [Matrix(0, -1)], Projection, 0, 1, short.MinValue, short.MaxValue);
        Assert.Equal((short)32767, depthZero.Minimum);
        Assert.Equal((short)-32767, depthZero.Maximum);
        Assert.All(depthZero.VertexScratchWrites, write => Assert.Equal((ushort)0x10, BinaryPrimitives.ReadUInt16LittleEndian(write.Data.AsSpan().Slice(6, 2))));
    }

    [Fact]
    public void ActualObjectMaskAndBoneOwnersAreRequired()
    {
        NativeBattleRawMesh mesh = new([new(new(0, 0, 0), 7)], []);
        Assert.Empty(NativeBattleBlizzagaModelBand.Emit6D8D80([mesh], [], Projection, 0, 0, -1, 1).VertexScratchWrites);
        Assert.Throws<InvalidDataException>(() => NativeBattleBlizzagaModelBand.Emit6D8D80([mesh], [], Projection, 0, 1, -1, 1));
    }

    private static NativeBattleCameraProjection Projection => new(null, 216, 160, 108);
    private static NativeBattleActionMatrix Matrix(int y, int z) => new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], 0, y, z, 4096);
    private static uint XY(short x, short y) => unchecked((ushort)x) | unchecked((uint)y << 16);
    private static NativeBattleProjectedVertex[] Vertices() => [new(XY(0, 0), 100), new(XY(128, 0), 200), new(XY(0, 128), 300), new(XY(128, 128), 400)];

    private static ImmutableArray<byte> Polygon(bool quad, byte[] u)
    {
        byte[] result = new byte[quad ? 20 : 16];
        BinaryPrimitives.WriteUInt16LittleEndian(result, 0);
        BinaryPrimitives.WriteUInt16LittleEndian(result.AsSpan(2), 1);
        BinaryPrimitives.WriteUInt16LittleEndian(result.AsSpan(4), 2);
        if (quad)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(result.AsSpan(6), 3);
        }
        BinaryPrimitives.WriteUInt16LittleEndian(result.AsSpan(8), (ushort)(0x1000 | u[0]));
        BinaryPrimitives.WriteUInt16LittleEndian(result.AsSpan(10), 0x1111);
        BinaryPrimitives.WriteUInt16LittleEndian(result.AsSpan(12), (ushort)(0x2000 | u[1]));
        BinaryPrimitives.WriteUInt16LittleEndian(result.AsSpan(14), 0x3AA);
        BinaryPrimitives.WriteUInt16LittleEndian(result.AsSpan(quad ? 16 : 6), (ushort)(0x3000 | u[2]));
        if (quad)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(result.AsSpan(18), (ushort)(0x4000 | u[3]));
        }
        return result.ToImmutableArray();
    }
}
