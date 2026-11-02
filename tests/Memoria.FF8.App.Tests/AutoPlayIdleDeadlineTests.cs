namespace Memoria.FF8.App.Tests;

public sealed class AutoPlayIdleDeadlineTests
{
    [Fact]
    public void NestedPersistenceExcludesTimeOnceWithoutForgivingEarlierWork()
    {
        Clock clock = new();
        AutoPlayIdleDeadline deadline = new(clock, TimeSpan.FromSeconds(10));
        clock.Advance(4);
        using (deadline.Suspend())
        {
            clock.Advance(30);
            using (deadline.Suspend())
            {
                clock.Advance(40);
            }
            clock.Advance(30);
            Assert.False(deadline.Expired);
        }
        clock.Advance(5);
        Assert.False(deadline.Expired);
        clock.Advance(1);
        Assert.True(deadline.Expired);
        using (deadline.Suspend())
        {
            clock.Advance(60);
            Assert.True(deadline.Expired);
        }
    }

    [Fact]
    public void ResetWithinSuspensionAndExceptionalExitKeepTheResumedBudgetExact()
    {
        Clock clock = new();
        AutoPlayIdleDeadline deadline = new(clock, TimeSpan.FromSeconds(10));
        clock.Advance(9);
        Assert.Throws<InvalidOperationException>((Action)(() =>
        {
            using (deadline.Suspend())
            {
                clock.Advance(40);
                deadline.Reset();
                clock.Advance(50);
                throw new InvalidOperationException("Checkpoint failed.");
            }
        }));
        clock.Advance(9);
        Assert.False(deadline.Expired);
        clock.Advance(1);
        Assert.True(deadline.Expired);
    }

    private sealed class Clock : TimeProvider
    {
        private long timestamp;
        public override long TimestampFrequency => 1000;
        public override long GetTimestamp() => timestamp;
        internal void Advance(int seconds) => timestamp += seconds * TimestampFrequency;
    }
}
