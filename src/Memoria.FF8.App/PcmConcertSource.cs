using System.Buffers.Binary;
using Memoria.FF8.Audio;
using Memoria.FF8.Runtime;

namespace Memoria.FF8;

/// <summary>Renders concert stems together while retaining independent loop and playback positions.</summary>
internal sealed class PcmConcertSource : IAudioSource, IDisposable
{
    internal sealed record Part(PcmAudio Audio, int LoopStartByte, int LoopEndByte, int StartByte, string Fingerprint, int Volume = 100);

    private readonly object gate = new();
    private readonly PartState[] parts;
    private bool paused;
    private bool disposed;
    private double gain = 1;

    public PcmConcertSource(IReadOnlyList<Part> parts)
    {
        ArgumentNullException.ThrowIfNull(parts);
        if (parts.Count == 0)
        {
            throw new ArgumentException("A concert requires at least one PCM stem.", nameof(parts));
        }
        this.parts = parts.Select(part => new PartState(part)).ToArray();
    }

    public void Pause(bool value)
    {
        lock (gate)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            paused = value;
        }
    }

    public void SetGain(int hundredthDecibels)
    {
        if (hundredthDecibels is < -10000 or > 0)
        {
            throw new ArgumentOutOfRangeException(nameof(hundredthDecibels));
        }
        lock (gate)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            gain = hundredthDecibels == -10000 ? 0 : Math.Pow(10, hundredthDecibels / 2000d);
        }
    }

    public GameplaySaveAudioPosition[] CapturePositions(int queuedFrames)
    {
        lock (gate)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            return parts.Select(part => new GameplaySaveAudioPosition("pcm16-v1", part.Part.Fingerprint, "source-frames", part.Timeline.PositionBeforeQueuedFrames(queuedFrames), 44100, 2, part.Part.LoopStartByte / 4, part.Part.LoopEndByte / 4)).ToArray();
        }
    }

    public void RenderFrame(out double left, out double right)
    {
        lock (gate)
        {
            left = 0;
            right = 0;
            if (disposed)
            {
                return;
            }
            foreach (PartState part in parts)
            {
                if (!paused)
                {
                    ReadOnlySpan<byte> frame = part.Part.Audio.Bytes.AsSpan(part.PositionByte, 4);
                    double amplitude = gain * part.Part.Volume / 100d;
                    left += BinaryPrimitives.ReadInt16LittleEndian(frame) * amplitude;
                    right += BinaryPrimitives.ReadInt16LittleEndian(frame[2..]) * amplitude;
                    part.PositionByte += 4;
                    if (part.PositionByte == part.Part.LoopEndByte)
                    {
                        part.PositionByte = part.Part.LoopStartByte;
                    }
                }
                part.Timeline.Record(part.PositionByte / 4);
            }
        }
    }

    public void Dispose()
    {
        lock (gate)
        {
            disposed = true;
        }
    }

    private sealed class PartState
    {
        public PartState(Part part)
        {
            ArgumentNullException.ThrowIfNull(part);
            ArgumentNullException.ThrowIfNull(part.Audio);
            if (part.Audio.SampleRate != 44100 || part.Audio.Channels != 2 || part.Audio.Bytes.Length % 4 != 0 || part.LoopStartByte < 0 || part.LoopEndByte > part.Audio.Bytes.Length || part.LoopStartByte >= part.LoopEndByte || part.StartByte < 0 || part.StartByte >= part.LoopEndByte || part.LoopStartByte % 4 != 0 || part.LoopEndByte % 4 != 0 || part.StartByte % 4 != 0 || part.Volume is < 0 or > 100 || string.IsNullOrEmpty(part.Fingerprint))
            {
                throw new ArgumentException("Concert stems require aligned 44.1 kHz stereo PCM, a nonempty loop, an available start frame, a fingerprint and a percentage between zero and one hundred.", nameof(part));
            }
            Part = part;
            PositionByte = part.StartByte;
            Timeline.Reset(PositionByte / 4);
        }

        public Part Part { get; }
        public int PositionByte { get; set; }
        public AudioPlaybackTimeline Timeline { get; } = new();
    }
}
