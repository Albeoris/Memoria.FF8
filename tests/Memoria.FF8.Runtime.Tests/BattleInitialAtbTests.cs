using Memoria.FF8.Runtime;

namespace Memoria.FF8.Runtime.Tests;

public sealed class BattleInitialAtbTests
{
    [Theory]
    [InlineData(0, 0, 0)]
    [InlineData(0, 34, 0)]
    [InlineData(4, 34, 100)]
    [InlineData(40, 64, 4000)]
    [InlineData(255, 127, 10000)]
    [InlineData(40, 192, 4000)]
    public void Shared_initial_gauge_uses_current_speed_and_seven_bit_draw_with_endpoint_clamps(int speed, int roll, int expected)
    {
        Assert.Equal(expected, BattleInitialAtb.Calculate(speed, checked((byte)roll)));
    }

    [Fact]
    public void Speed_changes_gauge_without_consuming_or_owning_random_state()
    {
        Assert.Equal(1000, BattleInitialAtb.Calculate(0, 44));
        Assert.Equal(3500, BattleInitialAtb.Calculate(100, 44));
    }
}
