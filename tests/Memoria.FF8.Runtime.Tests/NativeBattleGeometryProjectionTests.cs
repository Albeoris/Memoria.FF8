using System.Collections.Immutable;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleGeometryProjectionTests
{
    private static readonly NativeBattleActionMatrix Identity = new(ImmutableArray.Create<short>(4096, 0, 0, 0, 4096, 0, 0, 0, 4096), 0, 0, 0, 4096);

    [Fact]
    public void Original_projector_publishes_eighth_pixel_coordinates_and_unsigned_depth()
    {
        NativeBattleProjectedVertex actual = NativeBattleGeometryProjection.Project50F900(Identity, new(100, -50, 1024), new(Identity, 512, 160, 108), new(0, 0, 320, 216));
        Assert.Equal(1680, unchecked((short)actual.Screen));
        Assert.Equal(664, unchecked((int)actual.Screen) >> 16);
        Assert.Equal(1024u, actual.DepthAndClip);
    }

    [Theory]
    [InlineData(255, 0x20000u)]
    [InlineData(256, 0x20000u)]
    [InlineData(257, 0u)]
    [InlineData(-1, 0x20000u)]
    public void Near_test_uses_signed_rounded_depth_against_half_H(short z, uint expectedClip)
    {
        NativeBattleProjectedVertex actual = NativeBattleGeometryProjection.Project50F900(Identity, new(0, 0, z), new(Identity, 512, 160, 108), new(0, 0, 320, 216));
        Assert.Equal(expectedClip, actual.DepthAndClip & 0xFFFF0000);
        Assert.Equal(unchecked((ushort)z), actual.Depth);
    }

    [Fact]
    public void Original_zero_origin_and_offset_viewports_have_distinct_clip_bits()
    {
        NativeBattleCameraProjection projection = new(Identity, 512, 0, 0);
        NativeBattleProjectedVertex zero = NativeBattleGeometryProjection.Project50F900(Identity, new(-100, -100, 1024), projection, new(0, 0, 320, 216));
        NativeBattleProjectedVertex offset = NativeBattleGeometryProjection.Project50F900(Identity, new(-100, -100, 1024), projection, new(10, 10, 320, 216));
        Assert.Equal(0x3000000u, zero.DepthAndClip & 0xFFFF0000);
        Assert.Equal(0x3500000u, offset.DepthAndClip & 0xFFFF0000);
    }

    [Fact]
    public void Unknown_projection_offsets_are_not_supplied_by_the_renderer()
    {
        Assert.Throws<NotSupportedException>(() => NativeBattleGeometryProjection.Project50F900(Identity, new(0, 0, 1024), new(Identity, 512, null, 108), new(0, 0, 320, 216)));
    }
}
