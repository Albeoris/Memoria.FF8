namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleEffectSoundWatchdogTests
{
    [Theory]
    [InlineData(0, 1, false)]
    [InlineData(898, 899, false)]
    [InlineData(899, 900, true)]
    [InlineData(900, 901, true)]
    [InlineData(32766, 32767, true)]
    [InlineData(32767, 32768, false)]
    [InlineData(65535, 0, false)]
    public void Native_word_increment_uses_signed_comparison_and_wraps_without_releasing_the_effect(int current, int expected, bool stop)
    {
        Assert.Equal(((ushort)expected, stop), NativeBattleEffectSoundWatchdog.Advance((ushort)current));
    }
}
