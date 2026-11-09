namespace Memoria.AutoPlay;

/// <summary>Bounds unproductive exploration work while excluding passive checkpoint processing.</summary>
public class AutoPlayIdleDeadline(TimeProvider clock, TimeSpan limit)
{
    private readonly TimeProvider time = clock;
    private long progress = clock.GetTimestamp();
    private long suspended;
    private long pauseStarted;
    private int pauseDepth;

    public bool Expired
    {
        get
        {
            long now = time.GetTimestamp();
            long excluded = suspended + (pauseDepth == 0 ? 0 : now - pauseStarted);
            return (now - progress - excluded) / (double)time.TimestampFrequency >= limit.TotalSeconds;
        }
    }

    public void Reset()
    {
        progress = time.GetTimestamp();
        suspended = 0;
        if (pauseDepth != 0)
        {
            pauseStarted = progress;
        }
    }

    public IDisposable Suspend()
    {
        if (pauseDepth++ == 0)
        {
            pauseStarted = time.GetTimestamp();
        }
        return new Suspension(this);
    }

    private sealed class Suspension(AutoPlayIdleDeadline owner) : IDisposable
    {
        private bool disposed;

        public void Dispose()
        {
            if (disposed)
            {
                return;
            }
            disposed = true;
            if (--owner.pauseDepth == 0)
            {
                owner.suspended += owner.time.GetTimestamp() - owner.pauseStarted;
            }
        }
    }
}
