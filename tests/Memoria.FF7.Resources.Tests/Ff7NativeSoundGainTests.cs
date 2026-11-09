using Memoria.FF7.Resources;

namespace Memoria.FF7.Resources.Tests;

public sealed class Ff7NativeSoundGainTests
{
    [Fact]
    public void Original_script_gain_uses_the_authenticated_native_curve()
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR");
        Assert.SkipWhen(string.IsNullOrWhiteSpace(directory), "Set MEMORIA_FF7_GAME_DIR for original native volume data.");
        Ff7PcExecutableData native = Ff7PcExecutableData.Load(Ff7Installation.Discover(directory));
        Assert.Equal(128, native.NativeSoundVolume.Count);
        Assert.Equal(-10000, native.NativeSoundGain(0));
        Assert.Equal(-9205, native.NativeSoundGain(1));
        Assert.Equal(-9800, native.NativeSoundGain(2));
        Assert.Equal(-5300, native.NativeSoundGain(16));
        Assert.Equal(-2842, native.NativeSoundGain(32));
        Assert.Equal(-890, native.NativeSoundGain(63));
        Assert.Equal(-870, native.NativeSoundGain(64));
        Assert.Equal(-850, native.NativeSoundGain(65));
        Assert.Equal(-216, native.NativeSoundGain(96));
        Assert.Equal(-7, native.NativeSoundGain(126));
        Assert.Equal(0, native.NativeSoundGain(127));
        Assert.All(native.NativeSoundVolume, gain => Assert.InRange(gain, -10000, 0));
        Assert.True(native.NativeSoundVolume.Skip(2).Zip(native.NativeSoundVolume.Skip(3)).All(pair => pair.First <= pair.Second));
        Assert.Throws<ArgumentOutOfRangeException>(() => native.NativeSoundGain(-1));
        Assert.Throws<ArgumentOutOfRangeException>(() => native.NativeSoundGain(128));
    }
}
