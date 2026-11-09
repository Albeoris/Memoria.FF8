using System.Buffers.Binary;
using System.Runtime.CompilerServices;

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
                    voice.RecordPosition();
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
        private static readonly ConditionalWeakTable<byte[], CompressedSource> compressedSources = new();
        private readonly PcmMixer owner;
        private readonly byte[] pcm;
        private readonly int rate;
        private readonly int channels;
        private readonly object tempoGate = new();
        private byte[] playbackPcm;
        private double cursor;
        internal readonly AudioPlaybackTimeline timeline = new();
        private double volume = 1;
        private double frequencyRatio = 1;
        private double leftPan = 1;
        private double rightPan = 1;
        private readonly AudioPlaybackTimeline startedTimeline = new();
        private bool hasStarted;
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
            playbackPcm = pcm;
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

        public bool HasStartedBeforeQueuedFrames(int queuedFrames)
        {
            lock (owner.gate)
            {
                return startedTimeline.PositionBeforeQueuedFrames(queuedFrames) != 0;
            }
        }

        public int PositionBytesBeforeQueuedFrames(int queuedFrames)
        {
            lock (owner.gate)
            {
                return checked((int)timeline.PositionBeforeQueuedFrames(queuedFrames) * channels * 2);
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
                hasStarted = true;
                if (startedTimeline.RecordedFrames == 0)
                {
                    startedTimeline.Reset(1);
                }
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

        public void PositionAtEnd()
        {
            lock (owner.gate)
            {
                ObjectDisposedException.ThrowIf(disposed, this);
                cursor = pcm.Length / (channels * 2);
                timeline.Reset((long)cursor);
                playing = false;
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
                timeline.Reset((long)cursor);
            }
        }

        /// <summary>Changes playback frequency and pitch without resetting the original PCM cursor.</summary>
        public void FrequencyRatio(double ratio)
        {
            if (!double.IsFinite(ratio) || ratio <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(ratio));
            }
            lock (owner.gate)
            {
                ObjectDisposedException.ThrowIf(disposed, this);
                frequencyRatio = ratio;
            }
        }

        /// <summary>Changes duration while retaining pitch and the original PCM position.</summary>
        public void Tempo(double speed)
        {
            if (!double.IsFinite(speed) || speed is < 0.5 or > 30)
            {
                throw new ArgumentOutOfRangeException(nameof(speed));
            }
            // Compression can be expensive. Never hold the render lock while preparing it.
            lock (tempoGate)
            {
                lock (owner.gate)
                {
                    ObjectDisposedException.ThrowIf(disposed, this);
                }
                byte[] prepared = speed == 1 ? pcm : compressedSources.GetValue(pcm, static _ => new CompressedSource()).Get(pcm, rate, channels, speed);
                lock (owner.gate)
                {
                    ObjectDisposedException.ThrowIf(disposed, this);
                    playbackPcm = prepared;
                }
            }
        }

        internal void RecordPosition()
        {
            timeline.Record((long)cursor);
            startedTimeline.Record(hasStarted ? 1 : 0);
        }

        internal void Mix(ref double left, ref double right)
        {
            if (!playing || paused)
            {
                return;
            }
            int originalFrames = pcm.Length / (channels * 2);
            int frames = playbackPcm.Length / (channels * 2);
            if (frames == 0)
            {
                cursor = originalFrames;
                playing = false;
                return;
            }
            double playbackCursor = cursor * frames / originalFrames;
            int first = Math.Min((int)playbackCursor, frames - 1);
            int second = first + 1 < frames ? first + 1 : loop ? 0 : first;
            double fraction = playbackCursor - first;
            double l = Read(first, 0) * (1 - fraction) + Read(second, 0) * fraction;
            double r = channels == 1 ? l : Read(first, 1) * (1 - fraction) + Read(second, 1) * fraction;
            left += l * volume * leftPan;
            right += r * volume * rightPan;
            cursor += (double)rate / owner.SampleRate * originalFrames / frames * frequencyRatio;
            if (cursor >= originalFrames)
            {
                if (loop)
                {
                    cursor %= originalFrames;
                }
                else
                {
                    cursor = originalFrames;
                    playing = false;
                }
            }
        }

        private short Read(int frame, int channel)
        {
            return BinaryPrimitives.ReadInt16LittleEndian(playbackPcm.AsSpan((frame * channels + channel) * 2));
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

        private sealed class CompressedSource
        {
            private readonly Dictionary<(int Rate, int Channels, double Speed), byte[]> formats = [];

            public byte[] Get(byte[] pcm, int rate, int channels, double speed)
            {
                lock (formats)
                {
                    if (!formats.TryGetValue((rate, channels, speed), out byte[]? result))
                    {
                        result = PcmTimeStretch.Compress(pcm, rate, channels, speed);
                        if (formats.Count >= 8)
                        {
                            formats.Remove(formats.Keys.First());
                        }
                        formats.Add((rate, channels, speed), result);
                    }
                    return result;
                }
            }
        }
    }
}
