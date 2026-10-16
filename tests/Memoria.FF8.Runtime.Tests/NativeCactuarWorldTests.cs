using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeCactuarWorldTests
{
    [Fact]
    public void NativePhaseFallthroughPreservesBothThirtyBurstEdgesAndCooldown()
    {
        NativeCactuarWorldState state = NativeCactuarWorldState.CreateAfterLoad();
        Assert.Equal(state, state.Advance(false, out bool failedEmit));
        Assert.False(failedEmit);
        int emissions = 0;
        for (int callback = 1; callback <= 147; callback++)
        {
            state = state.Advance(callback == 1, out bool emit);
            emissions += emit ? 1 : 0;
            if (callback == 30)
            {
                Assert.Equal(new NativeCactuarWorldState(2, 1), state);
                Assert.True(emit);
            }
            if (callback == 59)
            {
                Assert.Equal(new NativeCactuarWorldState(3, 1), state);
                Assert.True(emit);
            }
            if (callback == 88)
            {
                Assert.Equal(new NativeCactuarWorldState(4, 1), state);
                Assert.True(emit);
            }
        }
        Assert.Equal(60, emissions);
        Assert.Equal(new NativeCactuarWorldState(0, 0), state);
    }

    [Fact]
    public void ExactRestoreKeepsUnobservedInitialCounterDistinctFromExecutedCooldown()
    {
        NativeCactuarWorldState initial = NativeCactuarWorldState.CreateAfterLoad();
        Assert.Null(JsonSerializer.Deserialize<NativeCactuarWorldState>(JsonSerializer.Serialize(initial))!.Counter);
        NativeCactuarWorldState active = new(3, 17);
        NativeCactuarWorldState restored = JsonSerializer.Deserialize<NativeCactuarWorldState>(JsonSerializer.Serialize(active))!;
        Assert.Equal(active.Advance(false, out bool emit), restored.Advance(false, out bool restoredEmit));
        Assert.Equal(emit, restoredEmit);
        Assert.Throws<InvalidDataException>(() => new NativeCactuarWorldState(2, null).Validate());
    }
}
