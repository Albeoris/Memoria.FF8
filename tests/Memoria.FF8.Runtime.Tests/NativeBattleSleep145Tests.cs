namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleSleep145Tests
{
    [Fact]
    public void Tint_preserves_the_original_quad_fourth_corner_dot_scaling_difference()
    {
        NativeBattleVector[] corners = [new(0, 0, 100), new(100, 0, 100), new(0, 100, 100), new(100, 100, 100)];
        short[] factors = new short[4];
        Assert.Equal(new NativeBattleVector(0, 0, -10000), NativeBattleSleep145.FaceNormal45E670(corners));
        Assert.True(NativeBattleSleep145.WriteTintFactors619B70(corners, factors, default));
        Assert.Equal((short)4013, factors[0]);
        Assert.Equal((short)3878, factors[3]);
    }

    [Fact]
    public void Outside_tint_corners_keep_their_actual_signed_scratch_fourth_words()
    {
        NativeBattleVector[] corners = [new(0, 0, 0), new(1000, 0, 0), new(2000, 0, 0)];
        short[] factors = [123, -4096, 1234];
        Assert.True(NativeBattleSleep145.WriteTintFactors619B70(corners, factors, default));
        Assert.Equal(new short[] { 4096, -4096, 1234 }, factors);
    }

    [Theory]
    [InlineData((short)-3000, 4096)]
    [InlineData((short)-1000, 2048)]
    [InlineData((short)1000, -2048)]
    public void Ground_copy_fade_clamps_only_the_original_lower_height_bound(short height, int expected)
    {
        Assert.Equal(expected, NativeBattleSleep145.GroundFade(height));
    }
}
