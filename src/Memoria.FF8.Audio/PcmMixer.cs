using System.Buffers.Binary;

namespace Memoria.FF8.Audio;

public interface IAudioSource
{
    void RenderFrame(out double left, out double right);
}

/// <summary>One synchronized PCM clock for all sources, independent of the device's output format.</summary>
public sealed class PcmMixer(int sampleRate = 44100)
{
    private readonly object gate = new();
    private readonly List<Voice> voices = [];
    private readonly List<IAudioSource> sources = [];
    public int SampleRate { get; } = sampleRate > 0 ? sampleRate : throw new ArgumentOutOfRangeException(nameof(sampleRate));

    public void Add(IAudioSource source)
    {
        lock (gate)
        {
            sources.Add(source);
        }
    }

    public void Remove(IAudioSource source)
    {
        lock (gate)
        {
            sources.Remove(source);
        }
    }

    public Voice Create(byte[] pcm, int rate, int channels)
    {
        if (rate <= 0 || channels is not (1 or 2) || pcm.Length == 0 || pcm.Length % (channels * 2) != 0)
        {
            throw new InvalidDataException("Playback requires aligned PCM16 mono or stereo data and a positive sample rate.");
        }
        var voice = new Voice(this, pcm, rate, channels);
        lock (gate)
        {
            voices.Add(voice);
        }
        return voice;
    }

    public void Render(Span<short> stereo)
    {
        if ((stereo.Length & 1) != 0)
        {
            throw new ArgumentException("The output must contain complete stereo frames.", nameof(stereo));
        }
        lock (gate)
        {
            for (int frame = 0; frame < stereo.Length / 2; frame++)
            {
                double left = 0;
                double right = 0;
                foreach (Voice voice in voices)
                {
                    voice.Mix(ref left, ref right);
                }
                foreach (IAudioSource source in sources)
                {
                    source.RenderFrame(out double l, out double r);
                    left += l;
                    right += r;
                }
                stereo[frame * 2] = (short)Math.Clamp(left, short.MinValue, short.MaxValue);
                stereo[frame * 2 + 1] = (short)Math.Clamp(right, short.MinValue, short.MaxValue);
            }
        }
    }

    public sealed class Voice : IDisposable
    {
        private readonly PcmMixer owner;
        private readonly byte[] pcm;
        private readonly int rate;
        private readonly int channels;
        private double cursor;
        private double volume = 1;
        private double leftPan = 1;
        private double rightPan = 1;
        private bool playing;
        private bool paused;
        private bool loop;
        private bool disposed;

        internal Voice(PcmMixer owner, byte[] pcm, int rate, int channels)
        {
            this.owner = owner;
            this.pcm = pcm;
            this.rate = rate;
            this.channels = channels;
        }

        public bool IsPlaying
        {
            get
            {
                lock (owner.gate)
                {
                    return playing && !paused;
                }
            }
        }

        public int PositionBytes
        {
            get
            {
                lock (owner.gate)
                {
                    return checked((int)cursor * channels * 2);
                }
            }
        }

        public void Play(bool repeat)
        {
            lock (owner.gate)
            {
                ObjectDisposedException.ThrowIf(disposed, this);
                if (cursor >= pcm.Length / (channels * 2))
                {
                    cursor = 0;
                }
                loop = repeat;
                playing = true;
                paused = false;
            }
        }

        public void Stop()
        {
            lock (owner.gate)
            {
                playing = false;
                paused = false;
            }
        }

        public void Pause()
        {
            lock (owner.gate)
            {
                paused = playing;
            }
        }

        public void Resume()
        {
            lock (owner.gate)
            {
                paused = false;
            }
        }

        public void Volume(int hundredthDecibels)
        {
            lock (owner.gate)
            {
                volume = hundredthDecibels <= -10000 ? 0 : Math.Pow(10, Math.Clamp(hundredthDecibels, -10000, 0) / 2000d);
            }
        }

        public void Pan(int hundredthDecibels)
        {
            lock (owner.gate)
            {
                leftPan = hundredthDecibels > 0 ? Math.Pow(10, -Math.Min(hundredthDecibels, 10000) / 2000d) : 1;
                rightPan = hundredthDecibels < 0 ? Math.Pow(10, Math.Max(hundredthDecibels, -10000) / 2000d) : 1;
            }
        }

        public void Position(int pcmByteOffset)
        {
            if (pcmByteOffset < 0 || pcmByteOffset >= pcm.Length || pcmByteOffset % (channels * 2) != 0)
            {
                throw new ArgumentOutOfRangeException(nameof(pcmByteOffset));
            }
            lock (owner.gate)
            {
                cursor = pcmByteOffset / (channels * 2);
            }
        }

        internal void Mix(ref double left, ref double right)
        {
            if (!playing || paused)
            {
                return;
            }
            int frames = pcm.Length / (channels * 2);
            int first = (int)cursor;
            int second = first + 1 < frames ? first + 1 : loop ? 0 : first;
            double fraction = cursor - first;
            double l = Read(first, 0) * (1 - fraction) + Read(second, 0) * fraction;
            double r = channels == 1 ? l : Read(first, 1) * (1 - fraction) + Read(second, 1) * fraction;
            left += l * volume * leftPan;
            right += r * volume * rightPan;
            cursor += (double)rate / owner.SampleRate;
            if (cursor >= frames)
            {
                if (loop)
                {
                    cursor %= frames;
                }
                else
                {
                    cursor = frames;
                    playing = false;
                }
            }
        }

        private short Read(int frame, int channel)
        {
            return BinaryPrimitives.ReadInt16LittleEndian(pcm.AsSpan((frame * channels + channel) * 2));
        }

        public void Dispose()
        {
            lock (owner.gate)
            {
                playing = false;
                disposed = true;
                owner.voices.Remove(this);
            }
        }
    }
}
