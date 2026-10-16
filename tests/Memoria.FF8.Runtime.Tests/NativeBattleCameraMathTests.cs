using Memoria.FF8.Runtime;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleCameraMathTests
{
    [Theory]
    [InlineData(0, 0, 4096)]
    [InlineData(512, 2896, 2896)]
    [InlineData(1024, 4096, 0)]
    [InlineData(2048, 0, -4096)]
    public void NativeFixedTrigUsesTheExecutableConstants(int angle, int sine, int cosine)
    {
        Assert.Equal(sine, NativeBattleCameraMath.Sine(angle));
        Assert.Equal(cosine, NativeBattleCameraMath.Cosine(angle));
    }

    [Fact]
    public void EasingUsesTheSignedFiveBitExponentAndPreservesUnselectedModes()
    {
        Assert.Equal(2048, NativeBattleCameraMath.Ease(2048, 0));
        Assert.Equal(2896, NativeBattleCameraMath.Ease(2048, 2));
        Assert.Equal(1200, NativeBattleCameraMath.Ease(2048, 0x3E));
        Assert.Equal(2048, NativeBattleCameraMath.Ease(2048, 1));
    }

    [Fact]
    public void NaturalSplineUsesSignedCoordinatesAndTruncatesTheCubicResult()
    {
        short[] times = [0, 16, 32];
        short[] values = [0, 10, 0];
        var coefficients = NativeBattleCameraMath.SplineCoefficients(times, values);
        Assert.Equal(new float[] { 0, -0.01953125f, 0 }, coefficients);
        Assert.Equal((short)0, NativeBattleCameraMath.SampleSpline(0, times, values, coefficients));
        Assert.Equal((short)6, NativeBattleCameraMath.SampleSpline(8, times, values, coefficients));
        Assert.Equal((short)10, NativeBattleCameraMath.SampleSpline(16, times, values, coefficients));
        Assert.Equal((short)6, NativeBattleCameraMath.SampleSpline(24, times, values, coefficients));
        values = [0, -10, 0];
        coefficients = NativeBattleCameraMath.SplineCoefficients(times, values);
        Assert.Equal((short)-6, NativeBattleCameraMath.SampleSpline(8, times, values, coefficients));
    }
}
