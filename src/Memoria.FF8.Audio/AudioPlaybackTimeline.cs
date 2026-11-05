namespace Memoria.FF8.Audio;

/// <summary>Maps queued output frames back to source positions, including pauses, underruns and loop boundaries.</summary>
public sealed class AudioPlaybackTimeline
{
    private readonly long[] positions = new long[16384];
    private long count;
    private long initial;

    public long RecordedFrames => count;

    public void Reset(long position)
    {
        initial = position;
        count = 0;
    }

    public void Record(long position)
    {
        positions[count % positions.Length] = position;
        count++;
    }

    public long PositionBeforeQueuedFrames(int queuedFrames)
    {
        if (queuedFrames < 0 || queuedFrames >= positions.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(queuedFrames));
        }
        long index = count - queuedFrames - 1;
        return index < 0 ? initial : positions[index % positions.Length];
    }
}
