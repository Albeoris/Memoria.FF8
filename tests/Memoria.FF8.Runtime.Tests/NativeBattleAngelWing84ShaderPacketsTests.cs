using System.Buffers.Binary;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleAngelWing84ShaderPacketsTests
{
    [Theory]
    [InlineData(0, 0, 0, 0, 128, 85)]
    [InlineData(0, 0, 0, 0, 0, 0)]
    [InlineData(3, 4, 0, 0, 69, 42)]
    [InlineData(3, 14, 0, 10, 69, 42)]
    [InlineData(200, 0, 0, 0, 256, 37)]
    [InlineData(200, 0, 0, 0, 200, 0)]
    public void Radial_mask_uses_signed_center_native_sqrt_and_two_thirds_byte_scale(short x, short y, short z, short center, short radius, byte expected)
    {
        Assert.Equal(expected, NativeBattleAngelWing84ShaderPackets.Shade(new(x, y, z), center, radius));
    }

    [Theory]
    [InlineData(false, 40, 272, 0x36)]
    [InlineData(true, 52, 273, 0x3E)]
    public void Native_Gouraud_packets_keep_separate_material_words_depth_and_unwritten_color_padding(bool quad, int length, int bucket, byte command)
    {
        NativeBattleGfGeometryBatch batch = NativeBattleAngelWing84ShaderPackets.Emit(Polygon(quad), quad, Corners(), new byte[] { 11, 22, 33, 44 });
        NativeBattleGfGeometryAttempt attempt = Assert.Single(batch.Attempts);
        Assert.Equal(length, batch.AllocationBytes);
        Assert.Equal(bucket, attempt.Primitive!.Bucket);
        Assert.Equal(command, attempt.Data[7]);
        Assert.Equal(11, attempt.Data[4]);
        Assert.Equal(22, attempt.Data[16]);
        Assert.Equal(33, attempt.Data[28]);
        Assert.Equal(0x12340011u, BinaryPrimitives.ReadUInt32LittleEndian(attempt.Data.AsSpan().Slice(12)));
        Assert.Equal(0x56780022u, BinaryPrimitives.ReadUInt32LittleEndian(attempt.Data.AsSpan().Slice(24)));
        Assert.Equal(NativeBattleGfPacketByteState.Untouched, attempt.WriteMask[19]);
        Assert.Equal(NativeBattleGfPacketByteState.Untouched, attempt.WriteMask[31]);
        Assert.Equal(NativeBattleGfPacketByteState.Untouched, attempt.WriteMask[38]);
        Assert.Equal(NativeBattleGfPacketByteState.Untouched, attempt.WriteMask[39]);
        if (quad)
        {
            Assert.Equal(44, attempt.Data[40]);
            Assert.Equal(NativeBattleGfPacketByteState.Untouched, attempt.WriteMask[43]);
            Assert.Equal(NativeBattleGfPacketByteState.Untouched, attempt.WriteMask[50]);
            Assert.Equal(NativeBattleGfPacketByteState.Untouched, attempt.WriteMask[51]);
        }
    }

    [Fact]
    public void Zero_facing_allocates_nothing_and_does_not_consume_a_fourth_vertex_or_shade()
    {
        NativeBattleGfGeometryBatch batch = NativeBattleAngelWing84ShaderPackets.Emit(Polygon(true), true, new NoFourthVertex(), new NoFourthShade());
        Assert.Empty(batch.Attempts);
        Assert.Equal(0, batch.AllocationBytes);
    }

    private static NativeBattleModuleProjectedVertex[] Corners() => [new(100u | 100u << 16, 4096, 0x60000), new(200u | 100u << 16, 4096, 0), new(100u | 200u << 16, 4096, 0), new(200u | 200u << 16, 4096, 0)];
    private static byte[] Polygon(bool quad)
    {
        byte[] bytes = new byte[quad ? 20 : 16];
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(8), 0x12340011);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(12), 0x56780022);
        return bytes;
    }
    private sealed class NoFourthVertex : IReadOnlyList<NativeBattleModuleProjectedVertex>
    {
        public int Count => 4;
        public NativeBattleModuleProjectedVertex this[int index] => index == 3 ? throw new InvalidOperationException("Rejected fourth vertex must remain unread.") : new(0, 4096, 0);
        public IEnumerator<NativeBattleModuleProjectedVertex> GetEnumerator() => throw new NotSupportedException();
        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
    }
    private sealed class NoFourthShade : IReadOnlyList<byte>
    {
        public int Count => 4;
        public byte this[int index] => throw new InvalidOperationException("Rejected shades must remain unread.");
        public IEnumerator<byte> GetEnumerator() => throw new NotSupportedException();
        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
