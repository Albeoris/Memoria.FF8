namespace Memoria.FF8.App.Tests;

public sealed class PresentationPacerTests
{
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
