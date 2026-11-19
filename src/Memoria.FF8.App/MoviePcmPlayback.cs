using System.Buffers.Binary;
using Memoria.FF8.Audio;

namespace Memoria.FF8;

/// <summary>Consumes decoded movie PCM on the shared mixer's timeline or the movie clock when muted.</summary>
internal sealed class MoviePcmPlayback(Func<byte[]?> readBuffer) : IAudioSource
{
    private const int FrameBytes = 4;
    private const int SampleRate = 44100;
    private readonly AudioPlaybackTimeline timeline = new();
    private byte[] buffer = [];
    private int offset;
    private long consumedFrames;
    private bool paused;

    public void Pause(bool pause)
    {
        Volatile.Write(ref paused, pause);
    }

    // The SDL device gate makes this check atomic with rendering and queueing.
    public bool HasPendingAudio(int queuedFrames)
    {
        return offset < buffer.Length || queuedFrames > 0 && timeline.PositionBeforeQueuedFrames(queuedFrames) < consumedFrames;
    }

    public void RenderFrame(out double left, out double right)
    {
        left = 0;
        right = 0;
        if (!Volatile.Read(ref paused) && ReadNextBuffer())
        {
            left = BinaryPrimitives.ReadInt16LittleEndian(buffer.AsSpan(offset, 2));
            right = BinaryPrimitives.ReadInt16LittleEndian(buffer.AsSpan(offset + 2, 2));
            offset += FrameBytes;
            consumedFrames++;
        }
        timeline.Record(consumedFrames);
    }

    public void AdvanceSilently(double seconds)
    {
        long target = (long)(seconds * SampleRate);
        while (consumedFrames < target && !Volatile.Read(ref paused) && ReadNextBuffer())
        {
            int frames = (int)Math.Min(target - consumedFrames, (buffer.Length - offset) / FrameBytes);
            offset += frames * FrameBytes;
            consumedFrames += frames;
        }
    }

    private bool ReadNextBuffer()
    {
        while (offset == buffer.Length)
        {
            if (readBuffer() is not byte[] next)
            {
                return false;
            }
            if (next.Length % FrameBytes != 0)
            {
                throw new InvalidDataException("Movie audio must contain complete PCM16 stereo frames.");
            }
            buffer = next;
            offset = 0;
        }
        return true;
    }
}
