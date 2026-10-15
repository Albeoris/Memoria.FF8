using Memoria.FF8.Runtime;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeWorldEntryControlTests
{
    [Theory]
    [InlineData(0, 24)]
    [InlineData(1, 23)]
    [InlineData(23, 1)]
    [InlineData(24, 0)]
    [InlineData(25, 0)]
    public void FieldOriginUsesTheDecrementedCounterForBothMovementAndEncounterEligibility(long callbackAge, int expectedDelay)
    {
        Assert.Equal(expectedDelay, NativeWorldEntryControl.DelayAfterCallback(callbackAge, 0));
    }

    [Theory]
    [InlineData(0, 8)]
    [InlineData(1, 7)]
    [InlineData(7, 1)]
    [InlineData(8, 0)]
    public void OriginalLoaderModeFourUsesItsSeparateEightCallbackCount(long callbackAge, int expectedDelay)
    {
        Assert.Equal(expectedDelay, NativeWorldEntryControl.DelayAfterCallback(callbackAge, 4));
    }

    [Fact]
    public void MissingModeAndNegativeAgeDoNotAcquireInferredControl()
    {
        Assert.Throws<NotSupportedException>(() => NativeWorldEntryControl.DelayAfterCallback(0, 5));
        Assert.Throws<ArgumentOutOfRangeException>(() => NativeWorldEntryControl.DelayAfterCallback(-1, 0));
    }
}
