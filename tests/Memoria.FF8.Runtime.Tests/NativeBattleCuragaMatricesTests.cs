namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleCuragaMatricesTests
{
    [Fact]
    public void Native_Z_X_Y_calls_postmultiply_the_current_GTE_matrix_instead_of_reversing_the_order()
    {
        NativeBattleActionMatrix matrix = NativeBattleCuragaMatrices.Euler8818A0(new(1024, 1024, 0));
        Assert.Equal(new short[] { 0, 0, 4096, 4096, 0, 0, 0, 4096, 0 }, matrix.Rotation.ToArray());
        Assert.Equal(new NativeBattleVector(3, 1, 2), NativeBattleCuragaMatrices.RotateVector45DF80(matrix, new(1, 2, 3)));
    }

    [Fact]
    public void Rotation_preserves_translation_and_rounds_each_product_with_the_original_GTE_IR_saturation()
    {
        NativeBattleActionMatrix left = new([30000, 30000, 30000, -30000, -30000, -30000, 1, 0, 0], 10, 20, 30, 4096);
        NativeBattleActionMatrix right = new([30000, 2048, 6144, 30000, 0, 0, 30000, 0, 0], 100, 200, 300, 4096);
        NativeBattleActionMatrix matrix = NativeBattlePoseMatrices.MultiplyGteRotation45E470(left, right);
        Assert.Equal((short)32767, matrix.Rotation[0]);
        Assert.Equal((short)-32768, matrix.Rotation[3]);
        Assert.Equal((short)0, matrix.Rotation[7]);
        Assert.Equal((short)2, matrix.Rotation[8]);
        Assert.Equal((10, 20, 30), (matrix.X, matrix.Y, matrix.Z));
    }

    [Theory]
    [InlineData(0, 0, 0, 4096, 0, 0)]
    [InlineData(0, 1024, 0, 0, 0, -4096)]
    [InlineData(0, 0, 1024, 0, 4096, 0)]
    public void Original_axis_rotations_transform_the_positive_X_direction(short x, short y, short z, short expectedX, short expectedY, short expectedZ)
    {
        NativeBattleActionMatrix matrix = NativeBattleCuragaMatrices.Euler8818A0(new(x, y, z));
        Assert.Equal(new NativeBattleVector(expectedX, expectedY, expectedZ), NativeBattleCuragaMatrices.RotateVector45DF80(matrix, new(4096, 0, 0)));
    }

    [Fact]
    public void DWORD_vector_rotation_preserves_full_Q16_components_and_ignores_translation_without_IR_saturation()
    {
        NativeBattleActionMatrix matrix = NativeBattleCuragaMatrices.Identity8DD770() with { X = 100, Y = 200, Z = 300 };
        Assert.Equal((0x12345678, -65536, int.MaxValue), NativeBattleGfMatrices.RotateLong56C600(matrix, 0x12345678, -65536, int.MaxValue));
        NativeBattleVector point = new(1, -200, 30000);
        Assert.Equal(NativeBattleGfMatrices.Rotate56C3D0(matrix, point), NativeBattleGfMatrices.RotateLong56C600(matrix, point.X, point.Y, point.Z));
    }

    [Fact]
    public void DWORD_vector_rotation_keeps_original_biased_rounding_for_positive_and_negative_half_values()
    {
        NativeBattleActionMatrix matrix = new([1, 0, 0, 3, 0, 0, -3, 0, 0], 0, 0, 0, 4096);
        Assert.Equal((0, 2, -2), NativeBattleGfMatrices.RotateLong56C600(matrix, 2048, 0, 0));
    }
}
