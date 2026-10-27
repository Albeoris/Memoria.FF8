using System.Threading.Channels;

namespace Memoria.FF8.Audio;

/// <summary>Bounded background work. Queue, cancellation and Pump belong to the presentation owner.</summary>
public sealed class AudioDecodeQueue : IDisposable
{
    private readonly object gate = new();
    private readonly Channel<Job> requests;
    private readonly Dictionary<object, Completion> completions = [];
    private readonly Dictionary<object, Job> pending = [];
    private readonly CancellationTokenSource stopping = new();
    private readonly Action<Exception> report;
    private readonly Task worker;
    private readonly int maximumPending;
    private bool disposed;

    public AudioDecodeQueue(Action<Exception> report, int capacity = 16)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(capacity, 1);
        this.report = report;
        maximumPending = capacity + 1;
        requests = Channel.CreateBounded<Job>(new BoundedChannelOptions(capacity) { SingleReader = true, FullMode = BoundedChannelFullMode.Wait });
        worker = Task.Run(RunAsync);
    }

    public int ReadyCount
    {
        get
        {
            lock (gate)
            {
                return completions.Count;
            }
        }
    }

    public bool IsPending(object owner)
    {
        lock (gate)
        {
            return pending.ContainsKey(owner);
        }
    }

    public void Queue<T>(object owner, Func<CancellationToken, Task<T>> decode, Action<T> apply, Action<Exception>? failed = null)
    {
        lock (gate)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            Cancel(owner);
            if (pending.Count >= maximumPending)
            {
                throw Busy();
            }
            Job job = new(owner, CancellationTokenSource.CreateLinkedTokenSource(stopping.Token), async token =>
            {
                T result = await decode(token).ConfigureAwait(false);
                token.ThrowIfCancellationRequested();
                return () => apply(result);
            }, failed ?? report);
            pending.Add(owner, job);
            if (!requests.Writer.TryWrite(job))
            {
                pending.Remove(owner);
                job.Cancel();
                job.Cancellation.Dispose();
                throw Busy();
            }
        }
    }

    private static InvalidOperationException Busy()
    {
        return new InvalidOperationException("The audio decoder queue is full. Stop unused audio requests and try again.");
    }

    public void Cancel(object owner)
    {
        lock (gate)
        {
            completions.Remove(owner);
            if (pending.Remove(owner, out Job? job))
            {
                job.Cancel();
            }
        }
    }

    public void CancelAll()
    {
        lock (gate)
        {
            foreach (Job job in pending.Values)
            {
                job.Cancel();
            }
            pending.Clear();
            completions.Clear();
        }
    }

    public void Pump()
    {
        while (true)
        {
            Completion completion;
            lock (gate)
            {
                ObjectDisposedException.ThrowIf(disposed, this);
                if (completions.Count == 0)
                {
                    return;
                }
                completion = completions.Values.First();
                completions.Remove(completion.Job.Owner);
                pending.Remove(completion.Job.Owner);
            }
            if (completion.Error is Exception failure)
            {
                completion.Job.Failed(failure);
                continue;
            }
            try
            {
                completion.Apply!();
            }
            catch (Exception exception)
            {
                completion.Job.Failed(exception);
            }
        }
    }

    private async Task RunAsync()
    {
        try
        {
            await foreach (Job job in requests.Reader.ReadAllAsync(stopping.Token).ConfigureAwait(false))
            {
                try
                {
                    job.Token.ThrowIfCancellationRequested();
                    Action apply = await job.Decode(job.Token).ConfigureAwait(false);
                    Complete(new Completion(job, apply, null));
                }
                catch (Exception exception)
                {
                    Complete(new Completion(job, null, exception));
                }
                finally
                {
                    job.Cancellation.Dispose();
                }
            }
        }
        catch (OperationCanceledException) when (stopping.IsCancellationRequested)
        {
        }
        finally
        {
            while (requests.Reader.TryRead(out Job? job))
            {
                job.Cancellation.Dispose();
            }
        }
    }

    private void Complete(Completion completion)
    {
        lock (gate)
        {
            if (!disposed && pending.TryGetValue(completion.Job.Owner, out Job? current) && ReferenceEquals(current, completion.Job))
            {
                completions[completion.Job.Owner] = completion;
            }
        }
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
            CancelAll();
        }
        requests.Writer.TryComplete();
        stopping.Cancel();
        worker.Wait(TimeSpan.FromSeconds(2));
        _ = worker.ContinueWith(_ => stopping.Dispose(), CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
    }

    private sealed class Job(object owner, CancellationTokenSource cancellation, Func<CancellationToken, Task<Action>> decode, Action<Exception> failed)
    {
        public object Owner { get; } = owner;
        public CancellationTokenSource Cancellation { get; } = cancellation;
        public CancellationToken Token { get; } = cancellation.Token;
        public Func<CancellationToken, Task<Action>> Decode { get; } = decode;
        public Action<Exception> Failed { get; } = failed;
        public void Cancel()
        {
            try
            {
                _ = Cancellation.CancelAsync().ContinueWith(task =>
                {
                    _ = task.Exception;
                }, CancellationToken.None, TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
            }
            catch (ObjectDisposedException)
            {
                // Completion may already be waiting for the owner to pump it.
            }
        }
    }

    private sealed record Completion(Job Job, Action? Apply, Exception? Error);
}
