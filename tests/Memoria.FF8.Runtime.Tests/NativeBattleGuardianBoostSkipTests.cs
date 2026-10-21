using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleGuardianBoostSkipTests
{
    [Fact]
    public void No_input_keeps_native_hundred_percent_and_ignores_animated_display()
    {
        NativeBattleGuardianBoost widget = new(4, 100, 20, 1, true, 4, 0, 90);
        NativeBattleGuardianBoostStep result = NativeBattleGuardianBoostSkip.Complete(widget, true, new(1));
        Assert.Equal((byte)100, result.RecomputePercent);
        Assert.Equal((ushort)0, result.Widget.Percent);
        Assert.False(result.Widget.Enabled);
        Assert.Equal((byte)6, result.Widget.Phase);
    }

    [Fact]
    public void Pending_valid_press_is_earned_once_before_input_is_closed_and_survives_JSON()
    {
        NativeBattleGuardianBoost widget = new(4, 100, 20, 1, true, 4, 124, 140) { PendingPress = true };
        NativeBattleGuardianBoostStep result = NativeBattleGuardianBoostSkip.Complete(widget, true, new(1));
        Assert.Equal((byte)125, result.RecomputePercent);
        Assert.False(result.Widget.PendingPress);
        NativeBattleGuardianBoost restored = JsonSerializer.Deserialize<NativeBattleGuardianBoost>(JsonSerializer.Serialize(result.Widget))!;
        NativeBattleGuardianBoostStep again = NativeBattleGuardianBoostSkip.Complete(restored, true, result.Random);
        Assert.Null(again.RecomputePercent);
        Assert.Equal(result.Widget, again.Widget);
        Assert.Equal(result.Random, again.Random);
    }
}
