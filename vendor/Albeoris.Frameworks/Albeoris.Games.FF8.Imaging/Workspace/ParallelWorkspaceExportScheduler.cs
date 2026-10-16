namespace Albeoris.Games.FF8.Imaging.Workspace;

/// <summary>Runs export operations on the thread pool with a shared concurrency limit.</summary>
public sealed class ParallelWorkspaceExportScheduler : IWorkspaceExportScheduler
{
    private readonly SemaphoreSlim _slots;

    public ParallelWorkspaceExportScheduler(Int32 maxDegreeOfParallelism = 0)
    {
        MaxDegreeOfParallelism = maxDegreeOfParallelism == 0 ? Environment.ProcessorCount : maxDegreeOfParallelism;
        ArgumentOutOfRangeException.ThrowIfLessThan(MaxDegreeOfParallelism, 1);
        _slots = new SemaphoreSlim(MaxDegreeOfParallelism, MaxDegreeOfParallelism);
    }

    public Int32 MaxDegreeOfParallelism { get; }

    public async Task<T> Schedule<T>(Func<T> operation)
    {
        ArgumentNullException.ThrowIfNull(operation);
        await _slots.WaitAsync().ConfigureAwait(false);
        try
        {
            return await Task.Run(operation).ConfigureAwait(false);
        }
        finally
        {
            _slots.Release();
        }
    }
}
