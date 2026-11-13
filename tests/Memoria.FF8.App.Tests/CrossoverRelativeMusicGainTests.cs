namespace Memoria.FF8.App.Tests;

public sealed class CrossoverRelativeMusicGainTests
{
    [Theory]
    [InlineData(127, null, 127)]
    [InlineData(64, null, 64)]
    [InlineData(127, 127, 127)]
    [InlineData(127, 48, 48)]
    [InlineData(64, 48, 24)]
    [InlineData(127, 0, 0)]
    [InlineData(0, 127, 0)]
    [InlineData(200, 127, 127)]
    [InlineData(-1, 127, 0)]
    public void RelativeMusicGainScalesTheExistingNativeVolumeBeforeItsOutputCurve(int nativeVolume, int? relativeGain, int expected)
    {
        Assert.Equal(expected, Ff7Scene.ApplyRelativeMusicGain(nativeVolume, relativeGain));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(128)]
    public void UnsupportedRelativeMusicGainRejectsWithoutInventingAClampedOwner(int relativeGain)
    {
        Assert.Throws<NotSupportedException>(() => Ff7Scene.ApplyRelativeMusicGain(127, relativeGain));
    }
}
