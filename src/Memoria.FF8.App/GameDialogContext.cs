using System.Collections.Concurrent;

namespace Memoria.FF8;

/// <summary>Returns asynchronous dialog continuations to the game thread while its host keeps drawing.</summary>
internal sealed class GameDialogContext : SynchronizationContext, IDisposable
{
    private readonly ConcurrentQueue<(SendOrPostCallback Callback, object? State)> pending = new();
    private int operations;
    private volatile bool disposed;

    internal bool IsBusy => !disposed && Volatile.Read(ref operations) != 0;
    public override void OperationStarted() => Interlocked.Increment(ref operations);
    public override void OperationCompleted() => Interlocked.Decrement(ref operations);
    public override void Post(SendOrPostCallback callback, object? state)
    {
        if (!disposed)
        {
            pending.Enqueue((callback, state));
        }
    }

    public void Dispose()
    {
        disposed = true;
        pending.Clear();
        operations = 0;
    }

    internal void Drain()
    {
        while (pending.TryDequeue(out var work))
        {
            work.Callback(work.State);
        }
    }
}
