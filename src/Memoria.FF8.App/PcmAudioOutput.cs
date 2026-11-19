using Memoria.FF8.Audio;

namespace Memoria.FF8;

/// <summary>Owns the game's shared PCM mixer and opens the SDL3 device on first playback.</summary>
internal sealed class PcmAudioOutput : IDisposable
{
    private readonly object gate = new();
    private readonly PcmMixer? offlineMixer;
    private SdlAudioOutput? output;
    private bool disposed;

    public PcmAudioOutput()
    {
    }

    internal PcmAudioOutput(PcmMixer mixer)
    {
        offlineMixer = mixer;
    }

    internal T CapturePlaybackPosition<T>(Func<int, T> capture)
    {
        lock (gate)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            return output is null ? capture(0) : output.CapturePlaybackPosition(capture);
        }
    }

    public PcmMixBus Music { get; } = new();
    public PcmMixBus Effects { get; } = new();
    public PcmMixBus Voice { get; } = new();
    public PcmMixBus Movie { get; } = new();

    public Exception? Error => Volatile.Read(ref output)?.Error;

    public Buffer Create(byte[] pcm, int rate, int channels, PcmMixBus? bus = null)
    {
        lock (gate)
        {
            return new Buffer(PlaybackMixer().Create(pcm, rate, channels, bus ?? Effects));
        }
    }

    internal void AddSource(IAudioSource source, PcmMixBus bus)
    {
        lock (gate)
        {
            PlaybackMixer().Add(source, bus);
        }
    }

    internal void RemoveSource(IAudioSource source)
    {
        lock (gate)
        {
            (offlineMixer ?? output?.Mixer)?.Remove(source);
        }
    }

    public void ThrowIfFailed()
    {
        Volatile.Read(ref output)?.ThrowIfFailed();
    }

    private PcmMixer PlaybackMixer()
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (offlineMixer is not null)
        {
            return offlineMixer;
        }
        output ??= new SdlAudioOutput();
        output.ThrowIfFailed();
        return output.Mixer;
    }

    public void Dispose()
    {
        lock (gate)
        {
            if (disposed)
            {
                return;
            }
            disposed = true;
        }
        output?.Dispose();
    }

    internal sealed class Buffer(PcmMixer.Voice voice) : IDisposable
    {
        public bool IsPlaying => voice.IsPlaying;
        internal bool HasStartedBeforeQueuedFrames(int queuedFrames) => voice.HasStartedBeforeQueuedFrames(queuedFrames);
        internal int PositionBytesBeforeQueuedFrames(int queuedFrames) => voice.PositionBytesBeforeQueuedFrames(queuedFrames);
        internal double PositionFramesBeforeQueuedFrames(int queuedFrames) => voice.PositionFramesBeforeQueuedFrames(queuedFrames);
        internal void PositionFrames(double sourceFrame) => voice.PositionFrames(sourceFrame);
        internal int PositionBytes => voice.PositionBytes;
        internal int PanValue { get; private set; }
        internal int GainValue { get; private set; }
        internal bool IsDisposed { get; private set; }
        public void Play(bool loop)
        {
            voice.Play(loop);
        }
        public void FrequencyRatio(double ratio)
        {
            voice.FrequencyRatio(ratio);
        }
        public void Tempo(double speed)
        {
            voice.Tempo(speed);
        }
        public void Stop()
        {
            voice.Stop();
        }
        public void Pause()
        {
            voice.Pause();
        }
        public void Resume()
        {
            voice.Resume();
        }
        public void Volume(int hundredthDecibels)
        {
            voice.Volume(hundredthDecibels);
            GainValue = hundredthDecibels;
        }
        public void PositionAtEnd()
        {
            voice.PositionAtEnd();
        }
        public void Position(int pcmByteOffset)
        {
            voice.Position(pcmByteOffset);
        }
        public void Pan(int hundredthDecibels)
        {
            voice.Pan(hundredthDecibels);
            PanValue = hundredthDecibels;
        }
        public void Dispose()
        {
            voice.Dispose();
            IsDisposed = true;
        }
    }
}
