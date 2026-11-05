namespace Memoria.FF8.Audio;

/// <summary>Renders selected segments on one sample clock while keeping their instrument channels independent.</summary>
public sealed class SegmentComposition(DlsBank bank, int sampleRate = 44100) : IAudioSource
{
    private readonly object gate = new();
    private SegmentSynthesizer[] parts = [];
    private readonly AudioPlaybackTimeline timeline = new();
    private long renderedFrames;
    private int initialStartPoint;
    private bool paused;

    public bool IsPlaying
    {
        get
        {
            lock (gate)
            {
                return parts.Any(part => part.IsPlaying);
            }
        }
    }

    public void Play(IReadOnlyList<MusicSegment> segments, int volume = 0, int startPoint = 0)
    {
        SegmentSynthesizer[] prepared = segments.Select(segment =>
        {
            SegmentSynthesizer part = new(bank, sampleRate);
            part.Play(segment, volume, startPoint);
            return part;
        }).ToArray();
        lock (gate)
        {
            parts = prepared;
            renderedFrames = 0;
            initialStartPoint = startPoint;
            paused = false;
            timeline.Reset(0);
        }
    }

    public SegmentPlaybackPosition? CapturePosition(int queuedFrames = 0)
    {
        lock (gate)
        {
            return parts.Length == 0 ? null : new(timeline.PositionBeforeQueuedFrames(queuedFrames), sampleRate, initialStartPoint, 1, paused);
        }
    }

    public void PlayAtPosition(IReadOnlyList<MusicSegment> segments, SegmentPlaybackPosition position, int volume = 0, CancellationToken cancellationToken = default)
    {
        position.Validate();
        if (position.SampleRate != sampleRate || position.InitialSystemGain != 1)
        {
            throw new NotSupportedException("Composition playback requires its shared sample rate and fresh independent part controls.");
        }
        SegmentSynthesizer[] prepared = segments.Select(segment =>
        {
            SegmentSynthesizer part = new(bank, sampleRate);
            part.PlayAtPosition(segment, position, volume, cancellationToken);
            return part;
        }).ToArray();
        lock (gate)
        {
            parts = prepared;
            renderedFrames = position.Frames;
            initialStartPoint = position.StartPoint;
            paused = position.Paused;
            timeline.Reset(renderedFrames);
        }
    }

    public void Volume(int volume)
    {
        lock (gate)
        {
            foreach (SegmentSynthesizer part in parts)
            {
                part.Volume(volume);
            }
        }
    }

    public void Pause()
    {
        lock (gate)
        {
            paused = true;
            foreach (SegmentSynthesizer part in parts)
            {
                part.Pause();
            }
        }
    }

    public void Resume()
    {
        lock (gate)
        {
            paused = false;
            foreach (SegmentSynthesizer part in parts)
            {
                part.Resume();
            }
        }
    }

    public void Stop()
    {
        lock (gate)
        {
            parts = [];
        }
    }

    public void RenderFrame(out double left, out double right)
    {
        lock (gate)
        {
            left = 0;
            right = 0;
            foreach (SegmentSynthesizer part in parts)
            {
                part.RenderFrame(out double l, out double r);
                left += l;
                right += r;
            }
            if (!paused && parts.Length != 0)
            {
                renderedFrames++;
            }
            timeline.Record(renderedFrames);
        }
    }
}
