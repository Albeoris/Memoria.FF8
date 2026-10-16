using Memoria.FF8.Assets;

namespace Memoria.FF8.Assets.Tests;

public sealed class WorldFieldInputContextTests
{
    [Theory]
    [InlineData(64, 0, null, null, false)]
    [InlineData(64, 64, 0, false, true)]
    [InlineData(64, 64, 64, false, false)]
    [InlineData(64, 64, 0, true, false)]
    [InlineData(64, 64, 0, null, null)]
    [InlineData(64, 64, null, false, null)]
    [InlineData(64, 128, null, null, false)]
    [InlineData(0, 65535, null, null, false)]
    [InlineData(32768, 32768, 0, false, true)]
    [InlineData(65535, 65536, 0, false, false)]
    public void Masked_operand_uses_exact_rising_low_word_and_short_circuits_unknown_activity(ushort operand, uint current, int? previous, bool? activity, bool? expected)
    {
        WorldFieldInputContext input = new(current, previous is int prior ? checked((uint)prior) : null, activity, -1, -1, -1, -1);
        Assert.Equal(expected, input.Matches(operand));
        Assert.True((input with { PreviewOverride = true }).Matches(operand));
    }

    [Theory]
    [InlineData(-1, false)]
    [InlineData(128, false)]
    [InlineData(173, false)]
    [InlineData(174, true)]
    [InlineData(83, false)]
    [InlineData(82, true)]
    [InlineData(0, true)]
    [InlineData(255, true)]
    [InlineData(384, false)]
    public void All_input_operand_centers_each_axis_as_signed_byte_and_uses_strict45_threshold(int axis, bool expected)
    {
        for (int index = 0; index < 4; index++)
        {
            int[] axes = [-1, -1, -1, -1];
            axes[index] = axis;
            WorldFieldInputContext input = new(0, null, false, axes[0], axes[1], axes[2], axes[3]);
            Assert.Equal(expected, input.Matches(65535));
            Assert.False((input with { Activity = true }).Matches(65535));
        }
    }

    [Fact]
    public void Unknown_analog_and_activity_require_only_the_reached_authority()
    {
        WorldFieldInputContext input = new(0, null, false, null, -1, -1, -1);
        Assert.Null(input.Matches(65535));
        Assert.True((input with { Axis3 = 0 }).Matches(65535));
        Assert.Null((input with { Axis3 = 0, Activity = null }).Matches(65535));
        Assert.False((input with { Activity = true }).Matches(65535));
        Assert.Null((input with { CurrentButtons = 64, PreviousButtons = 0, Activity = null }).Matches(64));
        Assert.True((input with { CurrentButtons = 64, PreviousButtons = 0 }).Matches(65535));
    }
}
