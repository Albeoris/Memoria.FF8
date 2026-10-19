using Memoria.FF8.Runtime;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleControl18BodyProjectionTests
{
    [Theory]
    [InlineData(-1, 10, 0x80060000u)]
    [InlineData(9, 10, 0x80020000u)]
    [InlineData(11, 11, 0x80020000u)]
    [InlineData(65536, 65535, 0x80440000u)]
    public void Output_depth_has_minimum10_after_unsigned_GTE_projection_and_retains_original_error_flags(int z, int expected, uint flags)
    {
        NativeBattleCameraProjection projection = Projection(z);
        NativeBattleModuleDepthCueVertex result = NativeBattleControl18BodyProjection.Project50FD40(projection.View!, new(0, 0, 0), projection, 0, 0);
        Assert.Equal((ushort)expected, result.Projected.Depth);
        Assert.Equal(flags, result.Projected.Flags);
        Assert.Equal(0u, result.Cue);
    }

    [Fact]
    public void Body_projection_saturates_GTE_screen_coordinates_without_bulk_viewport_clip_flags()
    {
        NativeBattleCameraProjection projection = Projection(4096);
        NativeBattleModuleDepthCueVertex result = NativeBattleControl18BodyProjection.Project50FD40(projection.View!, new(30000, 0, 0), projection, 0, 0);
        Assert.Equal((short)8184, unchecked((short)result.Projected.Screen));
        Assert.Equal(0x80004000u, result.Projected.Flags);
        Assert.Equal((ushort)4096, result.Projected.Depth);
    }

    private static NativeBattleCameraProjection Projection(int z) => new(new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], 0, 0, z, 4096), 216, 160, 108);
}
