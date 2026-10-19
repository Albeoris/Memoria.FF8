using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleWendigoBallGeometryTests
{
    [Theory]
    [InlineData(0, -3, 4)]
    [InlineData(2048, -32, 42)]
    [InlineData(4096, -60, 80)]
    public void Signed_sphere_division_and_separate_gte_products_preserve_native_rounding(short morph, short x, short z)
    {
        NativeBattleActionMatrix identity = NativeBattleCuragaMatrices.Identity8DD770();
        NativeBattleRawVertex vertex = new(new(-3, 0, 4), 0);
        Assert.Equal(new NativeBattleVector(x, 0, z), NativeBattleWendigoBallGeometry.Morph(vertex, identity, identity, default, morph, new(100, 100, 100)));
    }

    [Fact]
    public void Ball_packets_keep_native_average_depth_alpha_and_unwritten_uv_padding_without_projection_flag_gate()
    {
        byte[] polygon = new byte[16];
        BinaryPrimitives.WriteUInt16LittleEndian(polygon.AsSpan(2), 1);
        BinaryPrimitives.WriteUInt16LittleEndian(polygon.AsSpan(4), 2);
        BinaryPrimitives.WriteUInt16LittleEndian(polygon.AsSpan(6), 0xABCD);
        BinaryPrimitives.WriteUInt32LittleEndian(polygon.AsSpan(8), 0x12345678);
        BinaryPrimitives.WriteUInt32LittleEndian(polygon.AsSpan(12), 0x02BA0022);
        ImmutableArray<NativeBattleModuleProjectedVertex> projected = [new(Screen(-10, -10), 64, 0x60000), new(Screen(10, -10), 80, 0), new(Screen(-10, 10), 112, 0)];
        NativeBattleGfGeometryBatch batch = NativeBattleWendigoBallGeometry.Packets([new(false, polygon.ToImmutableArray())], projected, 0xAABBCCDD);
        NativeBattleGfGeometryAttempt attempt = Assert.Single(batch.Attempts);
        NativeBattleGeometryPrimitive packet = Assert.IsType<NativeBattleGeometryPrimitive>(attempt.Primitive);
        Assert.Equal(32, batch.AllocationBytes);
        Assert.Equal(22, packet.Bucket);
        Assert.Equal(0x26BBCCDDu, packet.Words[1]);
        Assert.Equal(0x12345678u, packet.Words[3]);
        Assert.Equal(0x02BA0022u, packet.Words[5]);
        Assert.Equal(0xABCDu, packet.Words[7]);
        Assert.Equal(NativeBattleGfPacketByteState.Untouched, attempt.WriteMask[30]);
        Assert.Equal(NativeBattleGfPacketByteState.Untouched, attempt.WriteMask[31]);
        NativeBattleGfGeometryBatch backface = NativeBattleWendigoBallGeometry.Packets([new(false, polygon.ToImmutableArray())], [projected[0], projected[2], projected[1]], 0);
        Assert.Empty(backface.Attempts);
        Assert.Equal(0, backface.AllocationBytes);
    }

    private static uint Screen(short x, short y) => unchecked((ushort)x) | unchecked((uint)y << 16);
}
