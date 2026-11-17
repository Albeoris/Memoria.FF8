using System.Diagnostics;

namespace Memoria.FF8.App.Tests;

public sealed class PresentationPacerTests
{
    [Fact]
    public void Slow_update_spends_its_current_deadline_before_draw()
    {
        using PresentationPacer pacer = new();
        pacer.ObserveTiming(true);
        pacer.BeginUpdate(60, 0);
        Assert.Equal(0, pacer.TimingObservation.Calls);

        long draw = Stopwatch.Frequency / 30;
        pacer.WaitBeforeDraw(draw);
        pacer.WaitBeforeDraw(draw);
        pacer.CompletePresentedFrame();
        Assert.Equal(1, pacer.TimingObservation.Calls);
        Assert.Equal(0, pacer.TimingObservation.TimerCalls);
        Assert.Equal(60, pacer.TimingObservation.FrameLimit);
    }

    [Theory]
    [InlineData(0, 30)]
    [InlineData(40, 40)]
    public void Suppressed_ticks_throttle_and_recovery_does_not_wait_twice(int requestedLimit, int skippedLimit)
    {
        using PresentationPacer pacer = new();
        pacer.ObserveTiming(true);
        long second = Stopwatch.Frequency;
        pacer.BeginUpdate(60, 0);
        pacer.RequestFrameLimit(requestedLimit);
        pacer.BeginUpdate(requestedLimit, second);
        Assert.Equal(1, pacer.TimingObservation.Calls);
        Assert.Equal(skippedLimit, pacer.TimingObservation.FrameLimit);

        pacer.BeginUpdate(60, 2 * second);
        pacer.RequestFrameLimit(120);
        pacer.WaitBeforeDraw(2 * second);
        pacer.CompletePresentedFrame();
        Assert.Equal(2, pacer.TimingObservation.Calls);
        Assert.Equal(skippedLimit, pacer.TimingObservation.FrameLimit);

        pacer.BeginUpdate(0, 3 * second);
        Assert.Equal(2, pacer.TimingObservation.Calls);
        pacer.WaitBeforeDraw(3 * second);
        pacer.CompletePresentedFrame();
        Assert.Equal(3, pacer.TimingObservation.Calls);
        Assert.Equal(0, pacer.TimingObservation.FrameLimit);
        Assert.Equal(0, pacer.TimingObservation.TimerCalls);
    }

    [Theory]
    [InlineData(30)]
    [InlineData(60)]
    [InlineData(120)]
    public void Small_late_arrivals_do_not_accumulate_into_a_lower_frame_rate(int fps)
    {
        double period = 1000d / fps;
        double deadline = period;
        for (int frame = 1; frame <= fps * 3; frame++)
        {
            double now = frame * period + 0.75;
            deadline = PresentationPacer.AdvanceDeadline(deadline, now, period);
            Assert.Equal((frame + 1) * period, deadline, 6);
            Assert.True(deadline > now);
        }
    }

    [Fact]
    public void A_late_arrival_skips_to_the_next_future_deadline_without_adding_a_fresh_period()
    {
        const double period = 1000d / 60;
        double next = PresentationPacer.AdvanceDeadline(period, 42, period);
        Assert.Equal(50, next, 6);
        Assert.Equal(100d / 3, next - period, 6);
        Assert.True(next > 42);
    }

    [Fact]
    public void A_stall_restarts_pacing_without_replaying_missed_display_deadlines()
    {
        const double period = 1000d / 60;
        double now = 2000;
        double next = PresentationPacer.AdvanceDeadline(period, now, period);
        Assert.Equal(now + period, next, 6);
        Assert.Equal(now + period * 2, PresentationPacer.AdvanceDeadline(next, next + 0.5, period), 6);
    }

    [Theory]
    [InlineData(19.999, 20)]
    [InlineData(20, 30)]
    [InlineData(20.001, 30)]
    [InlineData(42, 50)]
    [InlineData(2007, 2010)]
    public void Overruns_skip_only_missed_deadlines_and_always_choose_a_future_phase(double now, double expected)
    {
        const double period = 10;
        double next = PresentationPacer.AdvanceDeadline(10, now, period);
        Assert.Equal(expected, next, 6);
        Assert.True(next > now);
        Assert.True(next - now <= period);
    }
}
