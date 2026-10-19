using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleDuelEchoPacketsTests
{
    [Fact]
    public void NativeEchoRetainsRawPacketTailsAndClearsOnlyTheTpageBlendBit()
    {
        NativeBattleModulePoolTask context = Context(1, 1);
        byte[] faces = Faces();
        NativeBattleDuelEchoPacketsResult result = NativeBattleDuelEchoPackets.Emit68AAE0(context, 12, faces.ToImmutableArray(), Vertex, 17, 0, 0x155, 0x100);
        Assert.Equal(72, result.Batch.AllocationBytes);
        Assert.Equal(2, result.Batch.Attempts.Length);
        Assert.Equal(ushort.MaxValue, result.Context.ReadWord(20));
        Assert.Equal(ushort.MaxValue, result.Context.ReadWord(22));
        NativeBattleGfGeometryAttempt triangle = result.Batch.Attempts[0];
        NativeBattleGfGeometryAttempt quad = result.Batch.Attempts[1];
        Assert.Equal(0, triangle.ByteOffsetRelative);
        Assert.Equal(32, quad.ByteOffsetRelative);
        Assert.Equal(0x26123456u, BinaryPrimitives.ReadUInt32LittleEndian(triangle.Data.AsSpan().Slice(4)));
        Assert.Equal(0x07000000u, BinaryPrimitives.ReadUInt32LittleEndian(triangle.Data.AsSpan()));
        Assert.Equal(0xABCD1234u & ~0x02000000u, BinaryPrimitives.ReadUInt32LittleEndian(triangle.Data.AsSpan().Slice(20)));
        Assert.Equal((ushort)0x2468, BinaryPrimitives.ReadUInt16LittleEndian(triangle.Data.AsSpan().Slice(28)));
        Assert.Equal(NativeBattleGfPacketByteState.Untouched, triangle.WriteMask[30]);
        Assert.Equal(NativeBattleGfPacketByteState.Untouched, triangle.WriteMask[31]);
        Assert.Equal(NativeBattleGfPacketByteState.Untouched, quad.WriteMask[38]);
        Assert.Equal(NativeBattleGfPacketByteState.Untouched, quad.WriteMask[39]);
        Assert.Equal(17 + ((3 * 1600 * 0x155) >> 12), triangle.Primitive!.Bucket);
        Assert.Equal(17 + 400, quad.Primitive!.Bucket);
    }

    [Fact]
    public void ClipMarkerStopsVertexReadsBeforeTouchingLaterCornersOrAllocating()
    {
        List<int> reads = [];
        NativeBattleDuelEchoPacketsResult result = NativeBattleDuelEchoPackets.Emit68AAE0(Context(1, 0), 12, Faces().AsSpan(0, 16).ToArray().ToImmutableArray(), index =>
        {
            reads.Add(index);
            return new(0x00100010, 0, 16);
        }, 17, 0, 0x155, 0x100);
        Assert.Equal(new[] { 0 }, reads);
        Assert.Equal(0, result.Batch.AllocationBytes);
        Assert.Empty(result.Batch.Attempts);
        Assert.Equal(0x00100010u, result.Context.ReadDword(40));
    }

    private static NativeBattleModulePoolTask Context(ushort triangles, ushort quads) => new NativeBattleModulePoolTask(1, 0, null, 0, ImmutableArray.CreateRange(new byte[88])).WriteWord(20, triangles).WriteWord(22, quads).WriteDword(32, 0x123456);

    private static NativeBattleDuelEchoProjectedVertex Vertex(int index) => index switch
    {
        0 => new(0x00100010, 1600, 0),
        1 => new(0x00100020, 1600, 0),
        2 => new(0x00200010, 1600, 0),
        3 => new(0x00200020, 1600, 0),
        _ => throw new InvalidOperationException()
    };

    private static byte[] Faces()
    {
        byte[] faces = new byte[36];
        BinaryPrimitives.WriteUInt16LittleEndian(faces.AsSpan(2), 1);
        BinaryPrimitives.WriteUInt16LittleEndian(faces.AsSpan(4), 2);
        BinaryPrimitives.WriteUInt16LittleEndian(faces.AsSpan(6), 0x2468);
        BinaryPrimitives.WriteUInt32LittleEndian(faces.AsSpan(8), 0x12345678);
        BinaryPrimitives.WriteUInt32LittleEndian(faces.AsSpan(12), 0xABCD1234);
        BinaryPrimitives.WriteUInt16LittleEndian(faces.AsSpan(18), 1);
        BinaryPrimitives.WriteUInt16LittleEndian(faces.AsSpan(20), 2);
        BinaryPrimitives.WriteUInt16LittleEndian(faces.AsSpan(22), 3);
        BinaryPrimitives.WriteUInt32LittleEndian(faces.AsSpan(24), 0x12345678);
        BinaryPrimitives.WriteUInt32LittleEndian(faces.AsSpan(28), 0xABCD1234);
        BinaryPrimitives.WriteUInt16LittleEndian(faces.AsSpan(32), 0x2468);
        BinaryPrimitives.WriteUInt16LittleEndian(faces.AsSpan(34), 0x1357);
        return faces;
    }
}
