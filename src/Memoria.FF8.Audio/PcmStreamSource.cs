using System.Buffers.Binary;
using System.Runtime.ExceptionServices;

namespace Memoria.FF8.Audio;

/// <summary>A bounded, asynchronous PCM16 stereo source at 44100 Hz. Underruns produce silence without advancing playback.</summary>
public sealed class PcmStreamSource : IAudioSource, IDisposable
{
    private const int SampleRate = 44100;
    private const int FrameBytes = 4;
    private readonly Stream pcm;
    private readonly double amplitude;
    private readonly byte[] ring = new byte[SampleRate * FrameBytes];
    private readonly CancellationTokenSource stopping = new();
    private readonly TaskCompletionSource ready = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly Task worker;
    private long writtenFrames;
    private long readFrames;
    private readonly long offsetFrames;
    private double volume = 1;
    private bool paused;
    private bool completed;
    private int disposed;
    private ExceptionDispatchInfo? failure;

    private PcmStreamSource(Stream pcm, long offsetFrames, double amplitude)
    {
        this.pcm = pcm;
        this.amplitude = amplitude;
        this.offsetFrames = offsetFrames;
        worker = Task.Run(ProduceAsync);
    }

    /// <summary>Takes ownership of the stream, skips complete frames and buffers 100 ms or through EOF before returning.</summary>
    public static async Task<PcmStreamSource> CreateAsync(Stream pcm, double offsetSeconds, CancellationToken cancellationToken = default, double amplitude = 1)
    {
        ArgumentNullException.ThrowIfNull(pcm);
        if (!double.IsFinite(offsetSeconds) || offsetSeconds < 0 || offsetSeconds >= (double)(long.MaxValue / FrameBytes) / SampleRate)
        {
            throw new ArgumentOutOfRangeException(nameof(offsetSeconds));
        }
        if (!double.IsFinite(amplitude) || amplitude < 0 || amplitude > 16)
        {
            throw new ArgumentOutOfRangeException(nameof(amplitude));
        }
        var source = new PcmStreamSource(pcm, (long)(offsetSeconds * SampleRate), amplitude);
        try
        {
            await source.ready.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            source.ThrowIfFailed();
            return source;
        }
        catch
        {
            source.Dispose();
            throw;
        }
    }

    public long PositionFrames => offsetFrames + Volatile.Read(ref readFrames);
    public int BufferedFrames
    {
        get
        {
            long read = Volatile.Read(ref readFrames);
            return (int)(Volatile.Read(ref writtenFrames) - read);
        }
    }
    public bool IsCompleted => Volatile.Read(ref completed) && BufferedFrames == 0;

    public void Pause(bool pause)
    {
        Volatile.Write(ref paused, pause);
    }

    public void Volume(int hundredthDecibels)
    {
        Volatile.Write(ref volume, hundredthDecibels <= -10000 ? 0 : Math.Pow(10, Math.Clamp(hundredthDecibels, -10000, 0) / 2000d));
    }

    public void ThrowIfFailed()
    {
        Volatile.Read(ref failure)?.Throw();
    }

    public bool HasBufferedFrame => Volatile.Read(ref readFrames) < Volatile.Read(ref writtenFrames);

    // Called by one audio render thread. The producer publishes only complete frames.
    public void RenderFrame(out double left, out double right)
    {
        left = 0;
        right = 0;
        long read = Volatile.Read(ref readFrames);
        if (Volatile.Read(ref disposed) != 0 || Volatile.Read(ref paused) || read == Volatile.Read(ref writtenFrames))
        {
            return;
        }
        int index = (int)(read % SampleRate) * FrameBytes;
        double gain = amplitude * Volatile.Read(ref volume);
        left = BinaryPrimitives.ReadInt16LittleEndian(ring.AsSpan(index, 2)) * gain;
        right = BinaryPrimitives.ReadInt16LittleEndian(ring.AsSpan(index + 2, 2)) * gain;
        Volatile.Write(ref readFrames, read + 1);
    }

    private async Task ProduceAsync()
    {
        byte[] buffer = new byte[4096];
        int partial = 0;
        long skipBytes = offsetFrames * FrameBytes;
        try
        {
            // Offset reads are bounded too, including streams that return a byte at a time.
            while (skipBytes > 0)
            {
                int count = await pcm.ReadAsync(buffer.AsMemory(0, (int)Math.Min(buffer.Length, skipBytes)), stopping.Token).ConfigureAwait(false);
                if (count == 0)
                {
                    throw new EndOfStreamException("The PCM stream ended before the requested playback offset.");
                }
                skipBytes -= count;
            }
            while (true)
            {
                stopping.Token.ThrowIfCancellationRequested();
                long written = Volatile.Read(ref writtenFrames);
                int freeFrames = SampleRate - (int)(written - Volatile.Read(ref readFrames));
                if (freeFrames == 0)
                {
                    await Task.Delay(5, stopping.Token).ConfigureAwait(false);
                    continue;
                }
                int requested = Math.Min(buffer.Length, freeFrames * FrameBytes) - partial;
                int count = await pcm.ReadAsync(buffer.AsMemory(partial, requested), stopping.Token).ConfigureAwait(false);
                if (count == 0)
                {
                    if (partial != 0)
                    {
                        throw new InvalidDataException("The PCM stream ended with an incomplete stereo frame.");
                    }
                    Volatile.Write(ref completed, true);
                    ready.TrySetResult();
                    return;
                }
                int available = partial + count;
                int frames = available / FrameBytes;
                for (int frame = 0; frame < frames; frame++)
                {
                    buffer.AsSpan(frame * FrameBytes, FrameBytes).CopyTo(ring.AsSpan((int)((written + frame) % SampleRate) * FrameBytes, FrameBytes));
                }
                Volatile.Write(ref writtenFrames, written + frames);
                partial = available % FrameBytes;
                buffer.AsSpan(frames * FrameBytes, partial).CopyTo(buffer);
                if (written + frames >= SampleRate / 10)
                {
                    ready.TrySetResult();
                }
            }
        }
        catch (Exception exception) when (stopping.IsCancellationRequested)
        {
            _ = exception;
            ready.TrySetCanceled(stopping.Token);
        }
        catch (Exception exception)
        {
            Volatile.Write(ref failure, ExceptionDispatchInfo.Capture(exception));
            ready.TrySetResult();
        }
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref disposed, 1) != 0)
        {
            return;
        }
        stopping.Cancel();
        try
        {
            pcm.Dispose();
        }
        finally
        {
            // Disposing the owned backend stream interrupts outstanding pipe reads and terminates its process.
            worker.Wait(TimeSpan.FromSeconds(2));
            _ = worker.ContinueWith(task =>
            {
                _ = task.Exception;
                stopping.Dispose();
            }, CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
        }
    }
}
