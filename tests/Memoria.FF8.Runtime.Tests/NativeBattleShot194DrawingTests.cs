using System.Buffers.Binary;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleShot194DrawingTests
{
    [Fact]
    public void Original_quad_uses_three_projected_corners_and_SZ3_without_NCLIP_rejection()
    {
        NativeBattleActionMatrix view = new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], 0, 0, 4096, 4096);
        NativeBattleGfGeometryBatch draw = NativeBattleShot194Drawing.DrawQuad5B5FB0(new(0, 0, 0), 1024, 0x2E202080, view, new(view, 256, 160, 112));
        NativeBattleGeometryPrimitive primitive = Assert.IsType<NativeBattleGeometryPrimitive>(Assert.Single(draw.Attempts).Primitive);
        Assert.Equal(40, draw.AllocationBytes);
        Assert.Equal(17 + 256, primitive.Bucket);
        Assert.Equal(0x09000000u, primitive.Words[0]);
        Assert.Equal(0x2E202080u, primitive.Words[1]);
        Assert.Equal(0x3E144080u, primitive.Words[3]);
        Assert.Equal(0x003840A0u, primitive.Words[5]);
        Assert.Equal((primitive.Words[4] & 0xFFFF) | (primitive.Words[6] & 0xFFFF0000), primitive.Words[8]);
        NativeBattleGfGeometryBatch zeroArea = NativeBattleShot194Drawing.DrawQuad5B5FB0(new(0, 0, 0), 0, 0x2E202080, view, new(view, 256, 160, 112));
        Assert.NotNull(Assert.Single(zeroArea.Attempts).Primitive);
    }

    [Fact]
    public void Near_rejected_quad_keeps_written_color_and_UVs_at_unconsumed_packet_cursor()
    {
        NativeBattleActionMatrix view = new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], 0, 0, 1, 4096);
        NativeBattleGfGeometryBatch draw = NativeBattleShot194Drawing.DrawQuad5B5FB0(new(0, 0, 0), 1024, 0x2E202080, view, new(view, 256, 160, 112));
        NativeBattleGfGeometryAttempt attempt = Assert.Single(draw.Attempts);
        Assert.Null(attempt.Primitive);
        Assert.Equal(0, draw.AllocationBytes);
        Assert.Equal(0x2E202080u, BinaryPrimitives.ReadUInt32LittleEndian(attempt.Data.AsSpan().Slice(4)));
        Assert.All(attempt.WriteMask.Skip(8).Take(4), state => Assert.Equal(NativeBattleGfPacketByteState.Untouched, state));
        Assert.All(attempt.WriteMask.Skip(12).Take(4), state => Assert.Equal(NativeBattleGfPacketByteState.Known, state));
    }
}
