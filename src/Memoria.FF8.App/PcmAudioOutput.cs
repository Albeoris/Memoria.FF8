using Memoria.FF8.Audio;

namespace Memoria.FF8;

/// <summary>Compatibility facade over the common portable PCM mixer and SDL3 device.</summary>
internal sealed class PcmAudioOutput : IDisposable
{
    private readonly SdlAudioOutput? output;
    private readonly PcmMixer mixer;

    public PcmAudioOutput()
    {
        output = new SdlAudioOutput();
        mixer = output.Mixer;
    }

    internal PcmAudioOutput(PcmMixer mixer)
    {
        this.mixer = mixer;
    }

    public Exception? Error => output?.Error;

    public Buffer Create(byte[] pcm, int rate, int channels)
    {
        output?.ThrowIfFailed();
        return new Buffer(mixer.Create(pcm, rate, channels));
    }

    internal void AddSource(IAudioSource source)
    {
        output?.ThrowIfFailed();
        mixer.Add(source);
    }

    internal void RemoveSource(IAudioSource source)
    {
        mixer.Remove(source);
    }

    public void ThrowIfFailed()
    {
        output?.ThrowIfFailed();
    }

    public void Dispose()
    {
        output?.Dispose();
    }

    internal sealed class Buffer(PcmMixer.Voice voice) : IDisposable
    {
        public bool IsPlaying => voice.IsPlaying;
        internal int PositionBytes => voice.PositionBytes;
        internal int PanValue { get; private set; }
        internal int GainValue { get; private set; }
        internal bool IsDisposed { get; private set; }
        public void Play(bool loop)
        {
            voice.Play(loop);
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
