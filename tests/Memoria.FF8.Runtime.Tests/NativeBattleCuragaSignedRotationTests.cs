namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleCuragaSignedRotationTests
{
    [Theory]
    [InlineData(-32390, 3426, 3427)]
    [InlineData(-30342, -3426, -3427)]
    [InlineData(5766, -3426, -3427)]
    [InlineData(7814, 3426, 3427)]
    public void Native_axis_cosine_uses_wrapped_shifted_sine_instead_of_the_independent_cosine_producer(short angle, int expected, int independentCosine)
    {
        Assert.Equal(expected, NativeBattleCuragaMatrices.Cosine8DDRotation(angle));
        Assert.Equal(independentCosine, NativeBattleCameraMath.Cosine(angle));
        NativeBattleActionMatrix identity = NativeBattleCuragaMatrices.Identity8DD770();
        short sine = unchecked((short)NativeBattleCameraMath.Sine(angle));
        NativeBattleActionMatrix x = NativeBattleCuragaMatrices.RotateX8DD7E0(identity, angle);
        NativeBattleActionMatrix y = NativeBattleCuragaMatrices.RotateY8DD8A0(identity, angle);
        NativeBattleActionMatrix z = NativeBattleCuragaMatrices.RotateZ8DD960(identity, angle);
        Assert.Equal(new short[] { 4096, 0, 0, 0, (short)expected, unchecked((short)-sine), 0, sine, (short)expected }, x.Rotation);
        Assert.Equal(new short[] { (short)expected, 0, sine, 0, 4096, 0, unchecked((short)-sine), 0, (short)expected }, y.Rotation);
        Assert.Equal(new short[] { (short)expected, unchecked((short)-sine), 0, sine, (short)expected, 0, 0, 0, 4096 }, z.Rotation);
    }

    [Theory]
    [InlineData(-32390)]
    [InlineData(7814)]
    public void Signed_axis_rotation_keeps_original_postmultiply_saturation_translation_and_input_sine(short angle)
    {
        NativeBattleActionMatrix input = new([20000, -31000, 30000, -20000, 31000, -30000, 32767, -32768, 32767], 123456, -234567, 345678, 4096);
        short cosine = unchecked((short)NativeBattleCameraMath.Sine((angle + 1024) & 4095));
        short sine = unchecked((short)NativeBattleCameraMath.Sine(angle));
        NativeBattleActionMatrix expected = NativeBattlePoseMatrices.MultiplyGteRotation45E470(input, new([cosine, unchecked((short)-sine), 0, sine, cosine, 0, 0, 0, 4096], 0, 0, 0, 4096));
        NativeBattleActionMatrix actual = NativeBattleCuragaMatrices.RotateZ8DD960(input, angle);
        Assert.Equal(expected.Rotation.ToArray(), actual.Rotation.ToArray());
        Assert.Equal(input.X, actual.X);
        Assert.Equal(input.Y, actual.Y);
        Assert.Equal(input.Z, actual.Z);
        Assert.Contains(actual.Rotation, value => value is short.MaxValue or short.MinValue);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1024)]
    [InlineData(2048)]
    [InlineData(3072)]
    [InlineData(4095)]
    public void Masked_axis_angles_preserve_the_current_fixed_point_values(short angle)
    {
        Assert.Equal(NativeBattleCameraMath.Cosine(angle), NativeBattleCuragaMatrices.Cosine8DDRotation(angle));
    }
}
