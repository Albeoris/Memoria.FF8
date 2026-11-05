namespace Memoria.FF8.Audio;

/// <summary>The advancing sample clock since Play, independent of wrapped musical time and queued device frames.</summary>
public sealed record SegmentPlaybackPosition(long Frames, int SampleRate, int StartPoint, double InitialSystemGain, bool Paused)
{
    public const string DecoderRevision = "sgt-dls-sample-clock-v1";

    public void Validate()
    {
        if (Frames < 0 || SampleRate <= 0 || StartPoint < 0 || !double.IsFinite(InitialSystemGain) || InitialSystemGain is < 0 or > 1)
        {
            throw new InvalidDataException("Segment playback requires a valid sample clock and initial musical controls.");
        }
    }
}

/// <summary>Content identity binds a worker position to its ordered segments, DLS bank and decoder contract.</summary>
public sealed record MusicPlaybackPosition(string ContentSha256, SegmentPlaybackPosition Position);
