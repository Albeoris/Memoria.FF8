using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleGuardianBoostTests
{
    [Fact]
    public void Preparation_counts_only_publication_edges_then_uses_two_UI_draws()
    {
        NativeBattleGuardianBoost widget = NativeBattleGuardianBoost.Register(1, 2, true);
        NativeBattleUiRandomState random = new(42);
        for (int callback = 0; callback < 100; callback++)
        {
            NativeBattleGuardianBoostStep idle = NativeBattleGuardianBoostRules.Advance(widget, false, true, true, random);
            widget = idle.Widget;
            Assert.Equal(random, idle.Random);
            Assert.Null(idle.RecomputePercent);
        }
        Assert.Equal((short)15, widget.WindowRemaining);
        for (int edge = 0; edge < 15; edge++)
        {
            widget = NativeBattleGuardianBoostRules.Advance(widget, true, true, false, random).Widget;
        }
        Assert.Equal((byte)3, widget.Phase);
        NativeBattleGuardianBoostStep opened = NativeBattleGuardianBoostRules.Advance(widget, false, true, false, random);
        NativeBattleUiRandomState expected = random.Draw(out ushort first).Draw(out ushort second);
        Assert.Equal(expected, opened.Random);
        Assert.Equal(15 * (first / 10922 % 3 + second / 10922 % 3 + 2), opened.Widget.WindowRemaining);
        Assert.Equal((short)30, opened.Widget.Remaining);
        Assert.Equal((sbyte)1, opened.Widget.Window);
    }

    [Fact]
    public void Forbidden_press_resets_to_seventy_five_and_display_decays_without_formula_replay()
    {
        NativeBattleGuardianBoost widget = new(4, 20, 10, 0, true, 4, 200, 200);
        NativeBattleGuardianBoostStep failed = NativeBattleGuardianBoostRules.Advance(widget, false, true, true, null);
        Assert.Equal((ushort)75, failed.Widget.Percent);
        Assert.Equal((ushort)197, failed.Widget.DisplayPercent);
        NativeBattleGuardianBoost ignored = NativeBattleGuardianBoostRules.Advance(widget, false, false, true, null).Widget;
        Assert.Equal((ushort)200, ignored.Percent);
        NativeBattleGuardianBoost success = NativeBattleGuardianBoostRules.Advance(widget with { Window = 1, Percent = 250, DisplayPercent = 250 }, false, true, true, null).Widget;
        Assert.Equal((ushort)250, success.Percent);
        Assert.Equal((ushort)250, success.DisplayPercent);
    }

    [Theory]
    [InlineData(0, 100)]
    [InlineData(75, 75)]
    [InlineData(250, 250)]
    public void Final_edge_recomputes_once_then_later_callback_unregisters(int storedPercent, int appliedPercent)
    {
        NativeBattleGuardianBoost widget = new(4, 1, 10, 1, true, 0, (ushort)storedPercent, (ushort)storedPercent);
        NativeBattleGuardianBoost restored = JsonSerializer.Deserialize<NativeBattleGuardianBoost>(JsonSerializer.Serialize(widget))!;
        NativeBattleGuardianBoostStep final = NativeBattleGuardianBoostRules.Advance(widget, true, true, true, null);
        NativeBattleGuardianBoostStep repeated = NativeBattleGuardianBoostRules.Advance(restored, true, true, true, null);
        Assert.Equal(final, repeated);
        Assert.Equal((byte)appliedPercent, final.RecomputePercent);
        Assert.False(final.Unregister);
        Assert.Equal((byte)6, final.Widget.Phase);
        Assert.False(final.Widget.Enabled);
        NativeBattleGuardianBoostStep close = NativeBattleGuardianBoostRules.Advance(final.Widget, false, true, true, null);
        Assert.True(close.Unregister);
        Assert.Null(close.RecomputePercent);
    }
}
