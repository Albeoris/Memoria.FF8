namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleAxisMatrixTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(65536)]
    [InlineData(-65536)]
    public void Original_zero_WORD_angles_produce_identity_on_both_axes(int angle)
    {
        short[] identity = [4096, 0, 0, 0, 4096, 0, 0, 0, 4096];
        Assert.Equal(identity, NativeBattlePandemona291Restoration.Rotation701220(angle).Rotation);
        Assert.Equal(identity, NativeBattlePandemona291Restoration.Rotation701270(angle).Rotation);
    }

    [Theory]
    [InlineData(1024, 20, -10)]
    [InlineData(-1024, -20, 10)]
    [InlineData(66560, 20, -10)]
    public void X_axis_uses_cosine_on_the_diagonal_and_original_sine_signs(int angle, short y, short z)
    {
        NativeBattleVector rotated = NativeBattleGfMatrices.RotateShort56C4F0(NativeBattlePandemona291Restoration.Rotation701220(angle), new(0, 10, 20));
        Assert.Equal(new NativeBattleVector(0, y, z), rotated);
    }

    [Theory]
    [InlineData(1024, 20, -10)]
    [InlineData(-1024, -20, 10)]
    [InlineData(66560, 20, -10)]
    public void Y_axis_negates_the_signed_WORD_argument_before_calling_trigonometry(int angle, short x, short z)
    {
        NativeBattleVector rotated = NativeBattleGfMatrices.RotateShort56C4F0(NativeBattlePandemona291Restoration.Rotation701270(angle), new(10, 0, 20));
        Assert.Equal(new NativeBattleVector(x, 0, z), rotated);
    }
}
