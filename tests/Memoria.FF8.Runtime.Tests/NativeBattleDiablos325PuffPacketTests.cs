using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleDiablos325PuffPacketTests
{
    private static NativeBattleActionMatrix Matrix => new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], 0, 0, 1000, 4096);
    private static NativeBattleCameraProjection Projection => new(null, 1000, 0, 0);

    [Fact]
    public void Frame_selection_fixed_bucket_and_E1_first_link_keep_actual_depths_and_untouched_UV_padding()
    {
        NativeBattleGfGeometryBatch batch = NativeBattleDiablos325PuffPackets.Emit6FC9E0(Resource(), 2, Matrix, Projection, 19);
        Assert.Equal(48, batch.AllocationBytes);
        Assert.Equal(3, batch.Attempts.Length);
        Assert.Null(batch.Attempts[0].Primitive);
        Assert.Equal(40, batch.Attempts[1].ByteOffsetRelative);
        Assert.Equal(new uint[] { 0x01000000, 0xE1000220 }, batch.Attempts[1].Primitive!.Words);
        Assert.Equal(new int[] { 0, 0, 0, 0 }, batch.Attempts[1].Primitive!.VertexPayload4);
        NativeBattleGeometryPrimitive face = batch.Attempts[2].Primitive!;
        Assert.Equal(19, face.Bucket);
        Assert.Equal(0, batch.Attempts[2].ByteOffsetRelative);
        Assert.Equal(new int[] { 1000, 1000, 1000, 1000 }, face.VertexPayload4);
        Assert.Equal(0x2E808080u, face.Words[1]);
        Assert.Equal((byte)2, batch.Attempts[0].Data[12]);
        Assert.All(batch.Attempts[0].WriteMask.AsSpan().Slice(30, 2).ToArray(), value => Assert.Equal(NativeBattleGfPacketByteState.Untouched, value));
        Assert.All(batch.Attempts[0].WriteMask.AsSpan().Slice(38, 2).ToArray(), value => Assert.Equal(NativeBattleGfPacketByteState.Untouched, value));
    }

    [Fact]
    public void Near_rejection_preserves_native_prefix_without_allocating_or_linking()
    {
        NativeBattleGfGeometryBatch batch = NativeBattleDiablos325PuffPackets.Emit6FC9E0(Resource(), 1, Matrix with { Z = -1000 }, Projection, 19);
        Assert.Equal(0, batch.AllocationBytes);
        NativeBattleGfGeometryAttempt attempt = Assert.Single(batch.Attempts);
        Assert.Null(attempt.Primitive);
        Assert.Equal(NativeBattleGfPacketByteState.Known, attempt.WriteMask[20]);
        Assert.Equal(NativeBattleGfPacketByteState.Untouched, attempt.WriteMask[22]);
        Assert.Equal(NativeBattleGfPacketByteState.Untouched, attempt.WriteMask[29]);
        Assert.Equal(NativeBattleGfPacketByteState.Untouched, attempt.WriteMask[8]);
    }

    [Fact]
    public void Record_angle_is_ignored_but_unconditional_axis_scales_are_used()
    {
        ImmutableArray<byte> original = Resource();
        byte[] angled = original.ToArray();
        BinaryPrimitives.WriteInt16LittleEndian(angled.AsSpan(20 + 4 + 12), 1024);
        ImmutableArray<uint> expected = NativeBattleDiablos325PuffPackets.Emit6FC9E0(original, 4, Matrix, Projection, 19).Attempts[2].Primitive!.Words;
        Assert.Equal(expected, NativeBattleDiablos325PuffPackets.Emit6FC9E0(angled.ToImmutableArray(), 4, Matrix, Projection, 19).Attempts[2].Primitive!.Words);
        BinaryPrimitives.WriteInt16LittleEndian(angled.AsSpan(20 + 4 + 16), 2048);
        Assert.NotEqual(expected[2], NativeBattleDiablos325PuffPackets.Emit6FC9E0(angled.ToImmutableArray(), 4, Matrix, Projection, 19).Attempts[2].Primitive!.Words[2]);
    }

    private static ImmutableArray<byte> Resource()
    {
        byte[] data = new byte[116];
        BinaryPrimitives.WriteInt16LittleEndian(data.AsSpan(8), 4);
        for (int index = 0; index < 4; index++)
        {
            int frame = 20 + index * 24;
            BinaryPrimitives.WriteInt16LittleEndian(data.AsSpan(10 + index * 2), (short)frame);
            BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(frame), 0x80000001);
            int quad = frame + 4;
            data[quad] = 2;
            data[quad + 1] = 128;
            data[quad + 2] = 2;
            data[quad + 3] = 0x2E;
            data[quad + 4] = (byte)index;
            BinaryPrimitives.WriteInt16LittleEndian(data.AsSpan(quad + 8), -1);
            BinaryPrimitives.WriteInt16LittleEndian(data.AsSpan(quad + 10), -1);
            BinaryPrimitives.WriteInt16LittleEndian(data.AsSpan(quad + 16), 4096);
            BinaryPrimitives.WriteInt16LittleEndian(data.AsSpan(quad + 18), 4096);
        }
        return data.ToImmutableArray();
    }
}
