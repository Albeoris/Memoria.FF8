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
    public void A_stall_restarts_pacing_without_replaying_missed_display_deadlines()
    {
        const double period = 1000d / 60;
        double now = 2000;
        double next = PresentationPacer.AdvanceDeadline(period, now, period);
        Assert.Equal(now + period, next);
        Assert.Equal(now + period * 2, PresentationPacer.AdvanceDeadline(next, next + 0.5, period), 6);
    }
}
