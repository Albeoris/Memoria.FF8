namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleModuleAlignmentTests
{
    [Fact]
    public void Quarter_turn_maps_native_down_to_right_with_the_cross_axis_sign()
    {
        NativeBattleModuleAxisAngle alignment = NativeBattleModuleAlignment.Between571480(0, -4096, 0, 4096, 0, 0);
        Assert.Equal(1024, alignment.Angle);
        Assert.Equal(new NativeBattleVector(0, 0, 4096), alignment.Axis);
        Assert.Equal(new short[] { 0, -4096, 0, 4096, 0, 0, 0, 0, 4096 }, NativeBattleModuleAlignment.Rotation5714F0(alignment.Angle, alignment.Axis));
    }

    [Theory]
    [InlineData(0, -100, 0)]
    [InlineData(0, 0, 0)]
    public void Parallel_and_zero_velocities_keep_the_original_zero_axis_and_identity(short x, short y, short z)
    {
        Assert.Equal(new short[] { 4096, 0, 0, 0, 4096, 0, 0, 0, 4096 }, NativeBattleModuleAlignment.DownToVelocity573C40(x, y, z));
    }

    [Fact]
    public void Antiparallel_keeps_native_zero_axis_instead_of_inventing_a_perpendicular_axis()
    {
        NativeBattleModuleAxisAngle alignment = NativeBattleModuleAlignment.Between571480(0, -4096, 0, 0, 4096, 0);
        Assert.Equal(2048, alignment.Angle);
        Assert.Equal(new NativeBattleVector(0, 0, 0), alignment.Axis);
        Assert.Equal(new short[] { -4096, 0, 0, 0, -4096, 0, 0, 0, -4096 }, NativeBattleModuleAlignment.Rotation5714F0(alignment.Angle, alignment.Axis));
    }

    [Fact]
    public void Non_axis_aligned_operands_keep_quantized_square_root_angle_and_staged_Q12_products()
    {
        //Native cross=(-2896,0,2048), squaredSAR12=3071,56BED0 truncates to3546 before FPATAN.
        NativeBattleModuleAxisAngle alignment = NativeBattleModuleAlignment.Between571480(0, -4096, 0, 2048, -2048, 2896);
        Assert.Equal(683, alignment.Angle);
        Assert.Equal(new NativeBattleVector(-3344, 0, 2365), alignment.Axis);
        Assert.Equal(new short[] { 3412, -2048, -967, 2048, 2046, 2897, -967, -2897, 2729 }, NativeBattleModuleAlignment.Rotation5714F0(alignment.Angle, alignment.Axis));
    }

    [Fact]
    public void Rodrigues_WORD_stores_wrap_and_shared56BEF0_scales_columns_with_arithmetic_shifts()
    {
        Assert.Equal(new short[] { -4128, 0, 0, 0, -4096, 0, 0, 0, -4096 }, NativeBattleModuleAlignment.Rotation5714F0(2048, new(32767, 0, 0)));
        Assert.Equal(new short[] { 1, -1, 0, 0, 1, 0, 0, 0, 1 }, NativeBattlePoseMatrices.ScaleRotation56BEF0([4096, -4095, 1, 0, 4096, 0, 0, 0, 4096], new(1, 1, 1)));
    }
}
