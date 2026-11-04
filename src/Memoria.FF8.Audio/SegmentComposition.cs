namespace Memoria.FF8.Audio;

/// <summary>Renders selected segments on one sample clock while keeping their instrument channels independent.</summary>
public sealed class SegmentComposition(DlsBank bank, int sampleRate = 44100) : IAudioSource
{
    private readonly object gate = new();
    private SegmentSynthesizer[] parts = [];

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
        }
    }
}
