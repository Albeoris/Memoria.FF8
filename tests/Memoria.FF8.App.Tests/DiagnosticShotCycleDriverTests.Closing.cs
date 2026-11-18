using Memoria.FF8.BattleEffects;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Tests;

public sealed partial class DiagnosticShotCycleDriverTests
{
    [Fact]
    public void Native_closing_sentinel_is_retained_when_four_UI_polls_advance_beyond_it_before_outer_observation()
    {
        DiagnosticShotCycleDriver cycle = ClosingCycle();
        NativeBattleShotWidgetState widget = cycle.Receipt.CloseRequested!.Shot!.Widget!;
        DiagnosticShotObservation? first = null;
        for (int poll = 0; poll < 4; poll++)
        {
            NativeBattleShotWidgetStep step = widget.Advance(poll < 2, false, true, 2);
            widget = step.Widget;
            DiagnosticShotObservation observation = Point(14, 2, BattleNativeShotPhase.Ending, 1);
            observation = observation with { Shot = observation.Shot! with { RemainingTimer = widget.RemainingTimer, Widget = widget } };
            if (widget.RemainingTimer == -1)
            {
                first = observation;
                cycle.ObserveClosingTransition(observation);
            }
            if (step.QueueClosingCommand)
            {
                Observe(cycle, Dispatch(3, 14, 238));
            }
        }
        Assert.Equal((short)-4, widget.RemainingTimer);
        Assert.NotNull(first);
        Assert.Same(first, cycle.Receipt.Ending);
        DiagnosticShotObservation outer = Point(14, 2, BattleNativeShotPhase.Ending, 1);
        outer = outer with { Shot = outer.Shot! with { RemainingTimer = widget.RemainingTimer, Widget = widget } };
        Assert.Equal(DiagnosticShotOperation.None, cycle.Observe(outer, false));
        Assert.Same(first, cycle.Receipt.Ending);
        cycle.Observe(Point(15, 2, null, 1), true);
        Assert.True(cycle.Completed);
        Assert.Equal(3, cycle.Receipt.Dispatches.Length);
        DiagnosticShotCycleValidation.ValidateReceipt(cycle.Receipt, Input, 188, cycle.Receipt.Dispatches, 10, 15);
    }

    [Theory]
    [InlineData(-2)]
    [InlineData(0)]
    [InlineData(100)]
    public void Closing_receipt_cannot_substitute_an_arbitrary_timer_for_the_native_sentinel(short timer)
    {
        DiagnosticShotCycleDriver cycle = ClosingCycle();
        DiagnosticShotObservation observation = Point(14, 2, BattleNativeShotPhase.Ending, 1);
        observation = observation with { Shot = observation.Shot! with { RemainingTimer = timer, Widget = observation.Shot.Widget! with { RemainingTimer = timer } } };

        Assert.Throws<InvalidDataException>(() => cycle.ObserveClosingTransition(observation));
        Assert.Null(cycle.Receipt.Ending);
    }

    private static DiagnosticShotCycleDriver ClosingCycle()
    {
        DiagnosticShotCycleDriver cycle = new(Receipt(1, 10, 3), Input);
        cycle.Opened(Point(10, 3, BattleNativeShotPhase.Opening));
        Observe(cycle, Dispatch(1, 11, 14));
        Assert.Equal(DiagnosticShotOperation.Trigger, cycle.Observe(Point(11, 3, BattleNativeShotPhase.Ready), false));
        RequestTrigger(cycle, 11, 3);
        cycle.Observe(Point(12, 2, BattleNativeShotPhase.AwaitingEffect, 1), false);
        Observe(cycle, Dispatch(2, 12, 237));
        Assert.Equal(DiagnosticShotOperation.End, cycle.Observe(Point(13, 2, BattleNativeShotPhase.Ready, 1), false));
        RequestClose(cycle, 13, 2);
        return cycle;
    }
}
