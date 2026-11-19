namespace Memoria.FF8;

internal enum PcmMusicRepeat
{
    None,
    TaggedRange,
    SeparateLoop
}

/// <summary>Preserves the original PCM coordinates while preparing the intro and repeat buffers.</summary>
internal sealed class PreparedPcmMusic
{
    private readonly PcmMusicRepeat repeat;
    private readonly byte[]? loopPcm;
    private readonly int alignment;

    public PreparedPcmMusic(PcmAudio pcm, ExternalMusicLoop loop, PcmMusicRepeat repeat)
    {
        alignment = checked(pcm.Channels * sizeof(short));
        Pcm = pcm.Bytes.Length == loop.EndByte ? pcm : new(pcm.Bytes.AsSpan(0, loop.EndByte).ToArray(), pcm.SampleRate, pcm.Channels);
        Loop = loop;
        this.repeat = repeat;
        if (repeat == PcmMusicRepeat.SeparateLoop || repeat == PcmMusicRepeat.TaggedRange && loop.StartByte > 0)
        {
            loopPcm = loop.StartByte == 0 ? Pcm.Bytes : Pcm.Bytes.AsSpan(loop.StartByte).ToArray();
        }
    }

    public PcmAudio Pcm { get; }
    public ExternalMusicLoop Loop { get; }
    public int LoopStartFrames => Loop.StartByte / alignment;
    public int EndFrames => Loop.EndByte / alignment;

    public static PreparedPcmMusic FromVorbis(PcmAudio pcm, byte[] encoded, PcmMusicRepeat repeat)
    {
        return new(pcm, ExternalMusicLoop.Read(encoded, pcm), repeat);
    }

    public PcmLoopPlayback Start(PcmAudioOutput output, double positionFrames, bool loopStarted, int gain, int volume, bool paused)
    {
        PcmAudioOutput.Buffer primary = output.Create(Pcm.Bytes, Pcm.SampleRate, Pcm.Channels, output.Music);
        PcmAudioOutput.Buffer? loop = null;
        try
        {
            if (loopPcm is not null)
            {
                loop = output.Create(loopPcm, Pcm.SampleRate, Pcm.Channels, output.Music);
            }
            bool startInLoop = loop is not null && (loopStarted || positionFrames >= EndFrames);
            double remainingSeconds = Math.Max(0, EndFrames - positionFrames) / Pcm.SampleRate;
            int bytesPerSecond = checked(Pcm.SampleRate * alignment);
            PcmLoopPlayback playback = new(primary, loop, volume, remainingSeconds, bytesPerSecond, Loop.EndByte - Loop.StartByte, startInLoop, alignment);
            playback.SetGain(gain);
            if (startInLoop)
            {
                loop!.PositionFrames((positionFrames - LoopStartFrames) % (EndFrames - LoopStartFrames));
                loop.Play(true);
            }
            else if (positionFrames < EndFrames)
            {
                primary.PositionFrames(positionFrames);
                primary.Play(repeat != PcmMusicRepeat.None && loop is null);
            }
            else
            {
                primary.PositionAtEnd();
            }
            playback.Pause(paused);
            return playback;
        }
        catch
        {
            primary.Dispose();
            loop?.Dispose();
            throw;
        }
    }
}
