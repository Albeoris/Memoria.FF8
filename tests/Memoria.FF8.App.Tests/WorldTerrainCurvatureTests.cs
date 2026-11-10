using Memoria.FF8.Rendering;

namespace Memoria.FF8.App.Tests;

public sealed class WorldTerrainCurvatureTests
{
    [Theory]
    [InlineData(-30000f)]
    [InlineData(0f)]
    [InlineData(30000f)]
    public void Curvature_starts_continuously_at_the_depth_threshold(float height)
    {
        Assert.Equal(height, WorldTerrainCurvature.PresentationHeight(height, 20863.5f));
        Assert.Equal(height, WorldTerrainCurvature.PresentationHeight(height, 20864f));
        Assert.InRange(MathF.Abs(WorldTerrainCurvature.PresentationHeight(height, 20864.5f) - height), 0, 1);
    }

    [Theory]
    [InlineData(29056f, 1024f)]
    [InlineData(32384f, 2025f)]
    public void Moving_through_old_integer_square_boundaries_has_no_height_jump(float depth, float expectedHeight)
    {
        float before = WorldTerrainCurvature.PresentationHeight(0, depth - 0.5f);
        float at = WorldTerrainCurvature.PresentationHeight(0, depth);
        float after = WorldTerrainCurvature.PresentationHeight(0, depth + 0.5f);
        Assert.Equal(expectedHeight, at);
        Assert.InRange(at - before, 0.01f, 0.2f);
        Assert.InRange(after - at, 0.01f, 0.2f);
    }

    [Theory]
    [InlineData(-1024f, 512f)]
    [InlineData(0f, 1024f)]
    [InlineData(1024f, 1536f)]
    [InlineData(30000f, 16024f)]
    public void Terrain_height_attenuation_preserves_elevated_and_lowered_geometry(float height, float expectedHeight)
    {
        Assert.Equal(expectedHeight, WorldTerrainCurvature.PresentationHeight(height, 29056));
    }

    [Theory]
    [InlineData(-16383.5f, 32767.5f)]
    [InlineData(-16384f, -32768f)]
    [InlineData(-16384.5f, -32767.5f)]
    [InlineData(-20000f, -29152f)]
    public void Signed_height_wrap_preserves_fractional_values_and_the_word_boundary(float height, float expectedHeight)
    {
        Assert.Equal(expectedHeight, WorldTerrainCurvature.PresentationHeight(height, 53632));
    }

    [Theory]
    [InlineData(-30000f)]
    [InlineData(0f)]
    [InlineData(30000f)]
    public void Camera_depth_clamps_to_the_native_unsigned_word_range(float height)
    {
        Assert.Equal(WorldTerrainCurvature.PresentationHeight(height, 0), WorldTerrainCurvature.PresentationHeight(height, -10000));
        Assert.Equal(WorldTerrainCurvature.PresentationHeight(height, 65535), WorldTerrainCurvature.PresentationHeight(height, 100000));
    }
}
