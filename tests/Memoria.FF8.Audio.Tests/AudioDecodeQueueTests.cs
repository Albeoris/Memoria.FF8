using Memoria.FF8.Audio;

namespace Memoria.FF8.Tests;

public sealed class AudioDecodeQueueTests
{
    [Fact]
    public async Task ReplacedGenerationCannotApplyEvenWhenItsDecoderIgnoresCancellation()
    {
        List<int> applied = [];
        List<Exception> errors = [];
        var first = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        var started = new TaskCompletionSource<CancellationToken>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var queue = new AudioDecodeQueue(errors.Add);
        queue.Queue("music", token =>
        {
            started.SetResult(token);
            return first.Task;
        }, applied.Add);
        CancellationToken oldToken = await started.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        queue.Queue("music", _ => Task.FromResult(2), applied.Add);
        Assert.True(oldToken.IsCancellationRequested);
        first.SetResult(1);
        await WaitUntil(() => queue.ReadyCount == 1);
        Assert.Empty(applied);
        queue.Pump();
        Assert.Equal(new[] { 2 }, applied);
        Assert.Empty(errors);
        Assert.False(queue.IsPending("music"));
    }

    [Fact]
    public async Task StopDropsAnAlreadyDecodedCompletionBeforeItCanStartPlayback()
    {
        bool applied = false;
        using var queue = new AudioDecodeQueue(exception => throw exception);
        queue.Queue("music", _ => Task.FromResult(1), _ => applied = true);
        await WaitUntil(() => queue.ReadyCount == 1);
        queue.Cancel("music");
        queue.Pump();
        Assert.False(applied);
        Assert.False(queue.IsPending("music"));
        Assert.Equal(0, queue.ReadyCount);
    }

    [Fact]
    public async Task CompletedDataRemainsBoundedUntilTheOwnerPumpsIt()
    {
        List<int> applied = [];
        using var queue = new AudioDecodeQueue(exception => throw exception, capacity: 1);
        queue.Queue(1, _ => Task.FromResult(1), applied.Add);
        await WaitUntil(() => queue.ReadyCount == 1);
        queue.Queue(2, _ => Task.FromResult(2), applied.Add);
        await WaitUntil(() => queue.ReadyCount == 2);
        Assert.Throws<InvalidOperationException>(() => queue.Queue(3, _ => Task.FromResult(3), applied.Add));
        Assert.Empty(applied);
        queue.Pump();
        Assert.Equal(new[] { 1, 2 }, applied.Order());
    }

    [Fact]
    public async Task DecodeAndApplyFailuresAreReportedByTheOwnerWithoutClaimingPlayback()
    {
        List<Exception> errors = [];
        using var queue = new AudioDecodeQueue(errors.Add);
        var failure = new InvalidDataException("invalid synthetic audio");
        queue.Queue<int>(1, _ => Task.FromException<int>(failure), _ => Assert.Fail("Invalid audio was applied."));
        await WaitUntil(() => queue.ReadyCount == 1);
        Assert.Empty(errors);
        queue.Pump();
        Assert.Same(failure, Assert.Single(errors));
        var deviceFailure = new IOException("synthetic missing device");
        queue.Queue(2, _ => Task.FromResult(1), _ => throw deviceFailure);
        await WaitUntil(() => queue.ReadyCount == 1);
        queue.Pump();
        Assert.Same(deviceFailure, errors.Last());
    }

    [Fact]
    public async Task DisposalCancelsTheActiveDecoderAndInvalidatesItsOwner()
    {
        var started = new TaskCompletionSource<CancellationToken>(TaskCreationOptions.RunContinuationsAsynchronously);
        bool applied = false;
        var queue = new AudioDecodeQueue(_ => Assert.Fail("Cancelled work was reported as playback failure."));
        queue.Queue(1, async token =>
        {
            started.SetResult(token);
            await Task.Delay(Timeout.InfiniteTimeSpan, token);
            return 1;
        }, _ => applied = true);
        CancellationToken token = await started.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        queue.Dispose();
        Assert.True(token.IsCancellationRequested);
        Assert.False(applied);
        Assert.Throws<ObjectDisposedException>(() => queue.Queue(1, _ => Task.FromResult(1), _ => Assert.Fail("Disposed work was applied.")));
    }

    private static async Task WaitUntil(Func<bool> condition)
    {
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        while (!condition())
        {
            await Task.Delay(5, deadline.Token);
        }
    }
}
