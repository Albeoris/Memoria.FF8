using Memoria.FF7.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class CrossoverGuestSpeedTests
{
    [Fact]
    public void SpeedCurveRetainsOriginalOpeningAnchorAndNativeEndpoints()
    {
        byte[] values = Enumerable.Range(0, 256).Select(CrossoverGuestHostActions.ProjectSpeed).ToArray();
        Assert.Equal((0, 6, 255), ((int)values[0], (int)values[21], (int)values[255]));
        Assert.Equal(values.Order(), values);
        int initialDenominator = Ff7NativeAtb.SpeedDenominator([values[21], 5]);
        Assert.Equal(56, initialDenominator);
        Assert.Equal(182, Ff7NativeAtb.Increment(0, values[21], 0, 0, initialDenominator, 128));
        Assert.True(Ff7NativeAtb.Increment(0, values[40], 0, 0, initialDenominator, 128) > 182);
    }
}
