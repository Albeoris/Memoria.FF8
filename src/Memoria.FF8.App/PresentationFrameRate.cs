namespace Memoria.FF8;

/// <summary>Recent completed presentation intervals, retaining the full interval crossing the rolling one-second boundary.</summary>
internal sealed class PresentationFrameRate
{
    private readonly Queue<TimeSpan> presentations = new();
    private TimeSpan? started;
    public double? FramesPerSecond { get; private set; }

    public void Presented(TimeSpan timestamp)
    {
        if (started is null)
        {
            started = timestamp;
            return;
        }
        presentations.Enqueue(timestamp);
        Trim(timestamp);
        if (timestamp > started.Value)
        {
            FramesPerSecond = presentations.Count / (timestamp - started.Value).TotalSeconds;
        }
    }

    private void Trim(TimeSpan timestamp)
    {
        TimeSpan cutoff = timestamp - TimeSpan.FromSeconds(1);
        while (presentations.TryPeek(out TimeSpan oldest) && oldest <= cutoff)
        {
            started = presentations.Dequeue();
        }
    }
}
