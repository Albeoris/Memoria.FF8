namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleCuragaRandomTests
{
    [Theory]
    [InlineData(10, 20, 11)]
    [InlineData(20, 10, 19)]
    public void Signed_range_uses_one_actual_CRT_draw_and_original_reversed_range_direction(int first, int second, int expected)
    {
        NativeBattleCuragaRandomValue result = NativeBattleCuragaRandom.Range881F20(NativeCrtRandomState.FromColdProcess(), first, second);
        Assert.Equal(expected, result.Value);
        Assert.Equal(2745024u, result.Random.Seed);
    }

    [Fact]
    public void Equal_range_bounds_consume_no_random_draw_for_either_original_range_helper()
    {
        NativeCrtRandomState random = new(1234);
        Assert.Equal(new NativeBattleCuragaRandomValue(random, -7), NativeBattleCuragaRandom.Range881F20(random, -7, -7));
        Assert.Equal(new NativeBattleCuragaRandomValue(random, -7), NativeBattleCuragaRandom.ProductRange881F50(random, -7, -7));
    }

    [Theory]
    [InlineData(10, 20, 17)]
    [InlineData(20, 10, 13)]
    public void Product_range_consumes_two_ordered_draws_before_the_signed_remainder(int first, int second, int expected)
    {
        NativeBattleCuragaRandomValue result = NativeBattleCuragaRandom.ProductRange881F50(NativeCrtRandomState.FromColdProcess(), first, second);
        Assert.Equal(expected, result.Value);
        Assert.Equal(3357800067u, result.Random.Seed);
    }

    [Theory]
    [InlineData(-4096, 4096)]
    [InlineData(-8192, 4096)]
    [InlineData(-1, 4095)]
    [InlineData(4096, 0)]
    [InlineData(32767, 4095)]
    public void Angle_normalization_preserves_the_original_negative_multiple_case(short angle, int expected)
    {
        NativeBattleCuragaRandomValue result = NativeBattleCuragaRandom.Angle882030(new(99), angle, 0);
        Assert.Equal(expected, result.Value);
        Assert.Equal(99u, result.Random.Seed);
    }

    [Fact]
    public void Euler_draws_X_then_Y_then_Z_and_signed_span_halves_truncate_toward_zero()
    {
        NativeBattleCuragaRandomVector result = NativeBattleCuragaRandom.Euler881FE0(NativeCrtRandomState.FromColdProcess(), new(0, 0, 0), new(8, 8, 8));
        Assert.Equal(new NativeBattleVector(4093, 4095, 2), result.Value);
        Assert.Equal(415139642u, result.Random.Seed);
        NativeBattleCuragaRandomValue reversed = NativeBattleCuragaRandom.Angle882030(NativeCrtRandomState.FromColdProcess(), 0, -9);
        Assert.Equal(3, reversed.Value);
        Assert.Equal(2745024u, reversed.Random.Seed);
        Assert.Equal(77u, NativeBattleCuragaRandom.Angle882030(new(77), 20, -1).Random.Seed);
    }
}
