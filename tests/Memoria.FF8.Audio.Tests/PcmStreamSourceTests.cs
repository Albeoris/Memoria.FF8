using System.Buffers.Binary;
using Memoria.FF8.Audio;

namespace Memoria.FF8.Tests;

public sealed class PcmStreamSourceTests
{
    [Fact]
    public async Task AmplitudeAndAttenuationAreAppliedBeforeFinalMixerClipping()
    {
        byte[] bytes = new byte[8];
        for (int sample = 0; sample < 4; sample++)
        {
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(sample * 2, 2), 20000);
        }
        using var source = await PcmStreamSource.CreateAsync(new SyntheticStream(bytes), 0, TestContext.Current.CancellationToken, amplitude: 3.2);
        var mixer = new PcmMixer();
        mixer.Add(source);
        source.Volume(-2000);
        short[] stereo = new short[2];
        mixer.Render(stereo);
        Assert.Equal(new short[] { 6400, 6400 }, stereo);
        source.Volume(0);
        mixer.Render(stereo);
        Assert.Equal(new short[] { short.MaxValue, short.MaxValue }, stereo);
    }

    [Fact]
    public async Task PartialReadsPreserveStereoAlignmentAndOffset()
    {
        using var stream = new SyntheticStream(Frames(8), maximumRead: 3);
        using var source = await PcmStreamSource.CreateAsync(stream, 2d / 44100, TestContext.Current.CancellationToken);
        Assert.Equal(2, source.PositionFrames);
        for (int frame = 2; frame < 8; frame++)
        {
            source.RenderFrame(out double left, out double right);
            Assert.Equal(frame + 1, left);
            Assert.Equal(-frame - 1, right);
        }
        Assert.True(source.IsCompleted);
        source.RenderFrame(out double silence, out _);
        Assert.Equal(0, silence);
        Assert.Equal(8, source.PositionFrames);
    }

    [Fact]
    public async Task PauseAndGainPreservePlaybackPosition()
    {
        using var source = await PcmStreamSource.CreateAsync(new SyntheticStream(Frames(2)), 0, TestContext.Current.CancellationToken);
        source.Pause(true);
        source.RenderFrame(out double paused, out _);
        Assert.Equal(0, paused);
        Assert.Equal(0, source.PositionFrames);
        source.Pause(false);
        source.Volume(-2000);
        source.RenderFrame(out double left, out double right);
        Assert.Equal(0.1, left, 8);
        Assert.Equal(-0.1, right, 8);
        source.Volume(-10000);
        source.RenderFrame(out double muted, out _);
        Assert.Equal(0, muted);
        Assert.Equal(2, source.PositionFrames);
    }

    [Fact]
    public async Task UnderrunsDoNotConsumeFutureDataAndCreationTokenDoesNotOwnPlayback()
    {
        using var token = new CancellationTokenSource();
        using var stream = new SyntheticStream(Frames(4410), blockTail: true);
        using var source = await PcmStreamSource.CreateAsync(stream, 0, token.Token);
        token.Cancel();
        for (int frame = 0; frame < 4410; frame++)
        {
            source.RenderFrame(out _, out _);
        }
        source.RenderFrame(out double silence, out _);
        Assert.Equal(0, silence);
        Assert.Equal(4410, source.PositionFrames);
        stream.Tail.SetResult(Frames(1));
        await WaitUntil(() => source.BufferedFrames == 1);
        source.RenderFrame(out double next, out _);
        Assert.Equal(1, next);
        source.ThrowIfFailed();
    }

    [Fact]
    public async Task ProducerIsBoundedWhilePausedAndDisposalClosesTheStream()
    {
        var stream = new SyntheticStream(Frames(100000));
        var source = await PcmStreamSource.CreateAsync(stream, 0, TestContext.Current.CancellationToken);
        source.Pause(true);
        await WaitUntil(() => source.BufferedFrames == 44100);
        Assert.Equal(44100 * 4, stream.BytesRead);
        source.Dispose();
        source.Dispose();
        Assert.Equal(1, stream.Disposals);
        source.RenderFrame(out double silence, out _);
        Assert.Equal(0, silence);
    }

    [Fact]
    public async Task CancelledPrebufferClosesTheOwnedStream()
    {
        var stream = new SyntheticStream([], blockTail: true);
        using var cancellation = new CancellationTokenSource();
        Task<PcmStreamSource> creation = PcmStreamSource.CreateAsync(stream, 0, cancellation.Token);
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => creation);
        Assert.Equal(1, stream.Disposals);
    }

    [Fact]
    public async Task IncompleteFramesAndOffsetsBeyondEofFailCreation()
    {
        var truncated = new SyntheticStream([1, 2, 3]);
        await Assert.ThrowsAsync<InvalidDataException>(() => PcmStreamSource.CreateAsync(truncated, 0, TestContext.Current.CancellationToken));
        Assert.Equal(1, truncated.Disposals);
        var shortStream = new SyntheticStream(Frames(1));
        await Assert.ThrowsAsync<EndOfStreamException>(() => PcmStreamSource.CreateAsync(shortStream, 2d / 44100, TestContext.Current.CancellationToken));
        Assert.Equal(1, shortStream.Disposals);
    }

    [Fact]
    public async Task BackgroundFailuresAreReportedOnTheOwner()
    {
        using var stream = new SyntheticStream(Frames(4410), blockTail: true);
        using var source = await PcmStreamSource.CreateAsync(stream, 0, TestContext.Current.CancellationToken);
        var failure = new IOException("synthetic decoder failure");
        stream.Tail.SetException(failure);
        await WaitUntil(() =>
        {
            try
            {
                source.ThrowIfFailed();
                return false;
            }
            catch (IOException)
            {
                return true;
            }
        });
        Assert.Same(failure, Assert.Throws<IOException>(source.ThrowIfFailed));
    }

    private static byte[] Frames(int count)
    {
        byte[] bytes = new byte[count * 4];
        for (int frame = 0; frame < count; frame++)
        {
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(frame * 4, 2), (short)(frame + 1));
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(frame * 4 + 2, 2), (short)(-frame - 1));
        }
        return bytes;
    }

    private static async Task WaitUntil(Func<bool> condition)
    {
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        while (!condition())
        {
            await Task.Delay(5, deadline.Token);
        }
    }

    private sealed class SyntheticStream(byte[] initial, int maximumRead = int.MaxValue, bool blockTail = false) : Stream
    {
        private byte[] data = initial;
        private int position;
        private bool tailRead;
        private int bytesRead;
        public TaskCompletionSource<byte[]> Tail { get; } = CreateTail(blockTail);
        public int BytesRead => Volatile.Read(ref bytesRead);
        public int Disposals { get; private set; }
        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }

        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            if (position == data.Length && !tailRead)
            {
                data = await Tail.Task.WaitAsync(cancellationToken);
                position = 0;
                tailRead = true;
            }
            int count = Math.Min(Math.Min(buffer.Length, maximumRead), data.Length - position);
            data.AsMemory(position, count).CopyTo(buffer);
            position += count;
            Interlocked.Add(ref bytesRead, count);
            return count;
        }

        protected override void Dispose(bool disposing)
        {
            Disposals++;
            base.Dispose(disposing);
        }

        private static TaskCompletionSource<byte[]> CreateTail(bool block)
        {
            var tail = new TaskCompletionSource<byte[]>(TaskCreationOptions.RunContinuationsAsynchronously);
            if (!block)
            {
                tail.SetResult([]);
            }
            return tail;
        }

        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override void Flush() => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
