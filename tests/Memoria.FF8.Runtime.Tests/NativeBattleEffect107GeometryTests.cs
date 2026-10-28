using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleEffect107GeometryTests
{
    private static NativeBattleActionMatrix Matrix => new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], 0, 0, 4096, 4096);
    private static NativeBattleCameraProjection Projection => new(Matrix, 216, 160, 108);
    private static NativeBattleGfGeometryContext Context => new(0, 0, 0, null, null, 0, 17, 0);

    [Fact]
    public void DeformationUsesOriginalDistanceAndUnsignedScaleTablesAndReturnsPreDeformationExtent()
    {
        List<(uint Address, int Length)> reads = [];
        short extent = 3;
        NativeBattleProjectedVertex result = NativeBattleEffect107Geometry.Deform6D1C30(new(XY(160, 320), 1000, 0x80020000), 10, 20, 64, ref extent, 2, Read);
        Assert.Equal(new NativeBattleProjectedVertex(XY(120, 240), 1000), result);
        Assert.Equal(20, extent);
        Assert.Equal(new (uint, int)[] { (0x12F6382, 1), (0x12F3BB8, 2) }, reads);

        ImmutableArray<byte> Read(uint address, int length)
        {
            reads.Add((address, length));
            return length == 1 ? [16] : [0, 8];
        }
    }

    [Fact]
    public void Inclusive128DistanceLookupAndPixelBoundaryFlagsArePreserved()
    {
        short extent = 0;
        uint address = 0;
        NativeBattleProjectedVertex result = NativeBattleEffect107Geometry.Deform6D1C30(new(XY(320 * 8 + 3, 216 * 8 + 5), 10, 0), 0, 0, 0, ref extent, 0, Read);
        Assert.Equal(128, extent);
        Assert.Equal(0x12F99F8u, address);
        Assert.Equal(XY(320 * 8 + 3, 216 * 8 + 5), result.Screen);
        Assert.Equal(0xA000Au, result.DepthAndClip);

        ImmutableArray<byte> Read(uint current, int length)
        {
            address = current;
            Assert.Equal(1, length);
            return [128];
        }
    }

    [Fact]
    public void ZeroDepthRejectsWithoutReadingLookupTablesOrChangingExtent()
    {
        short extent = 91;
        NativeBattleProjectedVertex result = NativeBattleEffect107Geometry.Deform6D1C30(new(XY(100, 100), 0, 0), 0, 0, 100, ref extent, 0, (_, _) => throw new InvalidOperationException());
        Assert.Equal(91, extent);
        Assert.Equal(0x100000u, result.DepthAndClip);
    }

    [Fact]
    public void DeformationUsesUnsignedFactorAndArithmeticShiftBeforeLowWordStores()
    {
        short extent = 0;
        NativeBattleProjectedVertex result = NativeBattleEffect107Geometry.Deform6D1C30(new(XY(79, 79), 1000, 0), 10, 10, 64, ref extent, 0, (_, length) => length == 1 ? [1] : [0, 240]);
        Assert.Equal(XY(-40, -40), result.Screen);
        Assert.Equal(0x50000u | 1000, result.DepthAndClip);
        Assert.Equal(1, extent);
    }

    [Fact]
    public void NativeBodyPacketsMaskEveryIndexClearPageBitAndPreservePadding()
    {
        NativeBattleRawPolygon polygon = Body(false);
        NativeBattleProjectedVertex[] vertices = [new(XY(80, 80), 4096), new(XY(160, 80), 4096), new(XY(80, 160), 4096)];
        NativeBattleGfGeometryBatch batch = NativeBattleEffect107Geometry.Emit6BCB50([polygon], vertices, 0x123456, null);
        NativeBattleGfGeometryAttempt attempt = Assert.Single(batch.Attempts);
        NativeBattleGeometryPrimitive primitive = Assert.IsType<NativeBattleGeometryPrimitive>(attempt.Primitive);
        Assert.Equal(32, batch.AllocationBytes);
        Assert.Equal(1040, primitive.Bucket);
        Assert.Equal(new uint[] { 0x7000000, 0x26123456, XY(80, 80), 0x12340201, XY(160, 80), 0x00040302, XY(80, 160), 0x0605 }, primitive.Words);
        Assert.Equal(new int[] { 4096, 4096, 4096, 0 }, primitive.VertexPayload4);
        Assert.Equal(NativeBattleGfPacketByteState.Untouched, attempt.WriteMask[30]);
        Assert.Equal(NativeBattleGfPacketByteState.Untouched, attempt.WriteMask[31]);
    }

    [Fact]
    public void BodyCullUsesNativeClipIntersectionAndAcceptsZeroArea()
    {
        NativeBattleRawPolygon polygon = Body(false);
        NativeBattleProjectedVertex first = new(XY(80, 80), 1);
        Assert.Single(NativeBattleEffect107Geometry.Emit6BCB50([polygon], [first, first, first], 0, null).Attempts);
        Assert.Empty(NativeBattleEffect107Geometry.Emit6BCB50([polygon], [first with { DepthAndClip = 0x100001 }, first, first], 0, null).Attempts);
        NativeBattleProjectedVertex outside = first with { DepthAndClip = 0x10001 };
        Assert.Empty(NativeBattleEffect107Geometry.Emit6BCB50([polygon], [outside, outside, outside], 0, null).Attempts);
        NativeBattleRawPolygon colored = polygon with { Bytes = polygon.Bytes.AddRange(new byte[4]) };
        Assert.Empty(NativeBattleEffect107Geometry.Emit6BCB50([colored], [], 0, null).Attempts);
    }

    [Theory]
    [InlineData(false, 28, 44)]
    [InlineData(true, 36, 52)]
    public void HierarchyPacketsPreserveNativePhysicalLengthsAndModeInsertionOrder(bool quad, int polygonLength, int allocationLength)
    {
        NativeBattleGfGeometryBatch batch = NativeBattleEffect107Geometry.EmitBatch6D23D0(Hierarchy(quad), 0, Vertices(), Matrix, Projection, Context);
        Assert.Equal(allocationLength, batch.AllocationBytes);
        Assert.Equal(3, batch.Attempts.Length);
        Assert.Equal(new int[] { polygonLength, 0, polygonLength + 8 }, batch.Attempts.Select(attempt => attempt.ByteOffsetRelative));
        Assert.Equal(0xE1000220u, batch.Attempts[0].Primitive!.Words[1]);
        Assert.Equal(quad ? 0x8000000u : 0x6000000u, batch.Attempts[1].Primitive!.Words[0]);
        Assert.Equal(polygonLength, batch.Attempts[1].Data.Length);
        Assert.Equal(0xE1000240u, batch.Attempts[2].Primitive!.Words[1]);
        Assert.Equal(quad ? new int[] { 4096, 4096, 4096, 4096 } : new int[] { 4096, 4096, 4096, 0 }, batch.Attempts[1].Primitive!.VertexPayload4);
    }

    [Fact]
    public void HierarchyRejectionRetainsPrefixStoresAndDoesNotReadFourthVertex()
    {
        List<int> offsets = [];
        NativeBattleGfGeometryBatch batch = NativeBattleEffect107Geometry.EmitBatch6D23D0(Hierarchy(true), 0, default, Matrix with { Z = 1 }, Projection, Context, offset =>
        {
            offsets.Add(offset);
            Assert.NotEqual(24, offset);
            return new(0, 0, 0);
        });
        NativeBattleGfGeometryAttempt attempt = Assert.Single(batch.Attempts);
        Assert.Equal(new int[] { 0, 8, 16 }, offsets);
        Assert.Null(attempt.Primitive);
        Assert.Equal(0, batch.AllocationBytes);
        Assert.All(attempt.WriteMask.Take(8), state => Assert.Equal(NativeBattleGfPacketByteState.Known, state));
        Assert.All(attempt.WriteMask.Skip(8), state => Assert.Equal(NativeBattleGfPacketByteState.Untouched, state));
    }

    [Fact]
    public void HierarchyNegativeBiasedDepthRejectsAfterColorStores()
    {
        NativeBattleGfGeometryBatch batch = NativeBattleEffect107Geometry.EmitBatch6D23D0(Hierarchy(false), 0, Vertices(), Matrix, Projection, Context with { DepthCue = -2000 });
        NativeBattleGfGeometryAttempt attempt = Assert.Single(batch.Attempts);
        Assert.Null(attempt.Primitive);
        Assert.Equal(0, batch.AllocationBytes);
        Assert.All(attempt.WriteMask, state => Assert.Equal(NativeBattleGfPacketByteState.Known, state));
    }

    [Theory]
    [InlineData(0u, false)]
    [InlineData(0x10u, false)]
    [InlineData(0x20u, true)]
    public void HierarchyBackfaceOverrideUsesGouraudFlag20(uint flags, bool accepted)
    {
        NativeBattleGfGeometryBatch batch = NativeBattleEffect107Geometry.EmitBatch6D23D0(Hierarchy(false), 0, default, Matrix, Projection, Context with { Flags = flags }, offset => offset switch
        {
            0 => new(0, 0, 0),
            8 => new(0, 100, 0),
            16 => new(100, 0, 0),
            _ => throw new InvalidDataException()
        });
        Assert.Equal(accepted ? 44 : 0, batch.AllocationBytes);
    }

    [Fact]
    public void Native_body_depth_4387_writes_the_adjacent_retained_OT_root_instead_of_clamping_or_drawing_in_the_active_table()
    {
        NativeBattleProjectedVertex[] vertices = [new(XY(80, 80), 17498), new(XY(160, 80), 17498), new(XY(80, 160), 17498)];
        NativeBattleGfGeometryBatch batch = NativeBattleEffect107Geometry.Emit6BCB50([Body(false)], vertices, 0x123456, null);
        Assert.Equal(4387, Assert.Single(batch.Attempts).Primitive!.Bucket);
        NativeBattlePrimitiveProcess process = NativeBattlePrimitiveProcess.FromColdProcess().Initialize500400();
        Assert.Equal((byte)0, process.Buffer80);
        NativeBattleLogicalOrdering ordering = NativeBattleLogicalOrdering.Clear();
        NativeBattleCommonPacketWrite written = NativeBattleCommonPacketArena.Append(process, ordering, batch);
        Assert.Equal(32, written.Process.ByteCursor);
        Assert.Equal(new NativeBattlePrimitiveLink(false, 0), written.Ordering.Table[1]);
        Assert.Equal(new NativeBattlePrimitiveLink(true, 0), written.Ordering.OtherTable!.Value[1]);
        NativeBattleLogicalPacket packet = Assert.Single(written.Ordering.Packets);
        Assert.Equal((byte)1, packet.NextTableBuffer);
        Assert.Equal((byte)1, written.Ordering.Proxies[0].NextHigh22);
        Assert.Equal(new byte[] { 0xE0, 0x24, 0xD9 }, written.Process.ReadCommonPacket(0, 3));
        byte[] lengths = new byte[256];
        lengths[0x26] = 7;
        Assert.Empty(written.Ordering.Traverse(lengths.ToImmutableArray()));
        written.Ordering.SelectTable(1);
        Assert.Equal(1, Assert.Single(written.Ordering.Traverse(lengths.ToImmutableArray())).OrderingBucket);
    }

    private static NativeBattleRawPolygon Body(bool quad)
    {
        byte[] bytes = new byte[quad ? 20 : 16];
        BinaryPrimitives.WriteUInt16LittleEndian(bytes, 0xF000);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(2), 0xE001);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(4), 0xD002);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(6), quad ? (ushort)3 : (ushort)0x0605);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(8), 0x12340201);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(12), 0x02040302);
        return new(quad, bytes.ToImmutableArray());
    }

    private static ImmutableArray<byte> Hierarchy(bool quad)
    {
        byte[] bytes = new byte[64];
        BinaryPrimitives.WriteInt32LittleEndian(bytes, 16);
        int group = quad ? 36 : 32;
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(group), 1);
        int face = group + 4;
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(face), quad ? 0x38202020u : 0x30202020u);
        for (int corner = 0; corner < (quad ? 4 : 3); corner++)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(face + 4 + corner * 2), (ushort)(corner * 2));
        }
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(face + 12), 0x12345678);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(face + 16), 0x23456789);
        if (quad)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(face + 20), 0x34567890);
        }
        return bytes.ToImmutableArray();
    }

    private static ImmutableArray<byte> Vertices()
    {
        byte[] bytes = new byte[32];
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(8), 100);
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(18), 100);
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(24), 100);
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(26), 100);
        return bytes.ToImmutableArray();
    }

    private static uint XY(int x, int y) => unchecked((ushort)x) | unchecked((uint)y << 16);
}
