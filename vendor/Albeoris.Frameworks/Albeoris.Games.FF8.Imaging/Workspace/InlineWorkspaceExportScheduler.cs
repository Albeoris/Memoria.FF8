namespace Albeoris.Games.FF8.Imaging.Workspace;

/// <summary>Executes each export operation immediately on the calling thread.</summary>
public sealed class InlineWorkspaceExportScheduler : IWorkspaceExportScheduler
{
    public static InlineWorkspaceExportScheduler Instance { get; } = new();

    private InlineWorkspaceExportScheduler()
    {
    }

    public Task<T> Schedule<T>(Func<T> operation)
    {
        ArgumentNullException.ThrowIfNull(operation);
        try
        {
            return Task.FromResult(operation());
        }
        catch (Exception exception)
        {
            return Task.FromException<T>(exception);
        }
    }
}
