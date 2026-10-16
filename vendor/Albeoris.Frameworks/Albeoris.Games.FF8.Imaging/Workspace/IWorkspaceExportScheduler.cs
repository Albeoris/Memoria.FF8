namespace Albeoris.Games.FF8.Imaging.Workspace;

/// <summary>Schedules independent CPU-bound parts of a workspace export.</summary>
public interface IWorkspaceExportScheduler
{
    Task<T> Schedule<T>(Func<T> operation);
}
