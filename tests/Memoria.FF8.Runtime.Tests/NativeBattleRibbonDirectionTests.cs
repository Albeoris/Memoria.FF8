namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleRibbonDirectionTests
{
    [Fact]
    public void First_degenerate_segment_is_rejected_without_inventing_previous_components()
    {
        NativeBattleRibbonDirectionStep actual = NativeBattleRibbonDirection.Resolve(0, 0, 0, false, null, null);
        Assert.False(actual.Valid);
        Assert.Null(actual.PreviousX);
        Assert.Null(actual.PreviousY);
    }

    [Fact]
    public void Later_degenerate_segment_reuses_actual_previous_word_components()
    {
        NativeBattleRibbonDirectionStep actual = NativeBattleRibbonDirection.Resolve(0, 0, 0, true, -123, 456);
        Assert.True(actual.Valid);
        Assert.Equal(-123, actual.X);
        Assert.Equal(456, actual.Y);
        Assert.Throws<NotSupportedException>(() => NativeBattleRibbonDirection.Resolve(0, 0, 0, true, null, 456));
    }

    [Theory]
    [InlineData(16, 0, 0, 4096)]
    [InlineData(0, 16, 4096, 0)]
    [InlineData(-16, 0, 0, -4096)]
    public void First_segment_normalizes_perpendicular_and_publishes_word_history(int dx, int dy, int x, int y)
    {
        NativeBattleRibbonDirectionStep actual = NativeBattleRibbonDirection.Resolve(dx, dy, 16, false, null, null);
        Assert.True(actual.Valid);
        Assert.Equal(x, actual.X);
        Assert.Equal(y, actual.Y);
        Assert.Equal((short)x, actual.PreviousX);
        Assert.Equal((short)y, actual.PreviousY);
    }

    [Fact]
    public void Opposite_segments_use_native_doubled_current_direction_before_miter_factor()
    {
        NativeBattleRibbonDirectionStep actual = NativeBattleRibbonDirection.Resolve(-16, 0, 16, true, 0, 4096);
        Assert.Equal(0, actual.X);
        Assert.Equal(-8192, actual.Y);
        Assert.Equal((short)-4096, actual.PreviousY);
    }
}
