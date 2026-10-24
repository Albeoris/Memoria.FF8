using System.Buffers.Binary;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleGeometryPacketsTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void RawPacketsRetainExactDepthPayloadAndPhysicalTailMaskWithIndependentOwnedWords(bool quad)
    {
        byte[] polygon = Polygon(quad);
        NativeBattleProjectedVertex[] vertices = Vertices();
        BattleSceneScreenPoint[] coordinates = [new(10, 10), new(30, 10), new(10, 30), new(30, 30)];
        NativeBattleGeometryPrimitive first = NativeBattleGeometryPackets.EmitRaw50FDF0(polygon, vertices, quad, 0x123456, 17, 0, projectedCoordinates: coordinates)!;
        NativeBattleGeometryPrimitive second = NativeBattleGeometryPackets.EmitRaw50FDF0(polygon, vertices, quad, 0x654321, 17, 0, projectedCoordinates: coordinates)!;
        Assert.Equal(new[] { 102, 202, 302, quad ? 402 : 0 }, first.VertexPayload4);
        Assert.Equal(117, first.Bucket);
        Assert.Equal(quad ? 10 : 8, first.Words.Length);
        Assert.Equal(0x123456u | (quad ? 0x2C000000u : 0x24000000u), first.Words[1]);
        Assert.True(first.PhysicalWriteMask!.Value.Equals(second.PhysicalWriteMask!.Value));
        Assert.Equal(NativeBattleGfPacketByteState.Untouched, first.PhysicalWriteMask.Value[30]);
        Assert.Equal(NativeBattleGfPacketByteState.Untouched, first.PhysicalWriteMask.Value[31]);
        if (quad)
        {
            Assert.Equal(NativeBattleGfPacketByteState.Untouched, first.PhysicalWriteMask.Value[38]);
            Assert.Equal(NativeBattleGfPacketByteState.Untouched, first.PhysicalWriteMask.Value[39]);
        }
        coordinates[0] = new(999, 999);
        vertices[0] = new(0, 0);
        polygon[8] = 0;
        Assert.Equal(new BattleSceneScreenPoint(10, 10), first.ProjectedCoordinates!.Value[0]);
        Assert.Equal(0x000A000Au, first.Words[2]);
        Assert.NotEqual(first.Words[1], second.Words[1]);
    }

    [Fact]
    public void RawTriangleEmissionAllocatesOnlyTheRetainedPacketBuffers()
    {
        byte[] polygon = Polygon(false);
        NativeBattleProjectedVertex[] vertices = Vertices();
        _ = NativeBattleGeometryPackets.EmitRaw50FDF0(polygon, vertices, false, 0, 17, 0);
        long before = GC.GetAllocatedBytesForCurrentThread();
        uint total = 0;
        for (int index = 0; index < 1000; index++)
        {
            total += NativeBattleGeometryPackets.EmitRaw50FDF0(polygon, vertices, false, 0, 17, 0)!.Words[0];
        }
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.NotEqual(0u, total);
        Assert.True(allocated < 256000, $"Triangle packet emission allocated {allocated} bytes.");
    }

    private static NativeBattleProjectedVertex[] Vertices() => [new(0x000A000A, 100), new(0x000A001E, 200), new(0x001E000A, 300), new(0x001E001E, 400)];

    private static byte[] Polygon(bool quad)
    {
        byte[] polygon = new byte[quad ? 20 : 16];
        BinaryPrimitives.WriteUInt16LittleEndian(polygon, 0xA000);
        BinaryPrimitives.WriteUInt16LittleEndian(polygon.AsSpan(2), 1);
        BinaryPrimitives.WriteUInt16LittleEndian(polygon.AsSpan(4), 2);
        BinaryPrimitives.WriteUInt16LittleEndian(polygon.AsSpan(6), 3);
        BinaryPrimitives.WriteUInt32LittleEndian(polygon.AsSpan(8), 0x12345678);
        BinaryPrimitives.WriteUInt32LittleEndian(polygon.AsSpan(12), 0xABCD);
        return polygon;
    }
}
