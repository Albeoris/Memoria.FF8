namespace Memoria.FF8;

/// <summary>Visible frame changes per elapsed second, retaining the full presentation interval crossing the rolling one-second boundary.</summary>
internal sealed class PresentationFrameRate
{
    private readonly Queue<(TimeSpan Timestamp, bool Changed)> presentations = new();
    private TimeSpan? started;
    private int changedFrames;
    public double? FramesPerSecond { get; private set; }
    public double? PresentationsPerSecond { get; private set; }

    public void Presented(TimeSpan timestamp, bool changed)
    {
        if (started is null)
        {
            started = timestamp;
            return;
        }
        presentations.Enqueue((timestamp, changed));
        if (changed)
        {
            changedFrames++;
        }
        Trim(timestamp);
        if (timestamp > started.Value)
        {
            double seconds = (timestamp - started.Value).TotalSeconds;
            FramesPerSecond = changedFrames / seconds;
            PresentationsPerSecond = presentations.Count / seconds;
        }
    }

    public void Reset()
    {
        presentations.Clear();
        started = null;
        changedFrames = 0;
        FramesPerSecond = null;
        PresentationsPerSecond = null;
    }

    private void Trim(TimeSpan timestamp)
    {
        TimeSpan cutoff = timestamp - TimeSpan.FromSeconds(1);
        while (presentations.TryPeek(out var oldest) && oldest.Timestamp <= cutoff)
        {
            presentations.Dequeue();
            started = oldest.Timestamp;
            if (oldest.Changed)
            {
                changedFrames--;
            }
        }
    }
}
