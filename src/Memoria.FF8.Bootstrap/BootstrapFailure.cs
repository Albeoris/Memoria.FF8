using Memoria.FF8.Platform;

namespace Memoria.FF8.Bootstrap;

public sealed record GraphicsAttemptEvidence(string Backend, bool Ready, int ExitCode, bool InitializationFailure, string? Failure, ErrorException? Exception, string? ReadinessReceipt, string? FailureReceipt, IReadOnlyList<string> LogPaths);

public sealed record BootstrapFailure(string LaunchId, string RequestedBackend, string Summary, Exception? Exception, IReadOnlyList<GraphicsAttemptEvidence> Attempts)
{
    public ErrorIncident Capture()
    {
        return ErrorIncident.Capture(Exception ?? new InvalidOperationException(Summary), "Graphics host", runtime: new { requestedBackend = RequestedBackend, attempts = Attempts, nativeStack = "Unavailable when the host terminates without a managed incident." }, logPaths: Attempts.SelectMany(attempt => attempt.LogPaths), saveUnavailableReason: "The host did not supply a validated reproduction save.", launchId: LaunchId);
    }

    public string Persist(string? reportsDirectory = null)
    {
        ErrorIncident? existing = ErrorIncident.DiscoverLatest(reportsDirectory, LaunchId);
        ErrorIncident incident = existing is null ? Capture() : existing.WithLogPaths(Attempts.SelectMany(attempt => attempt.LogPaths));
        return incident.Persist(reportsDirectory);
    }

    internal static void TryPersist(BootstrapFailure failure)
    {
        failure.Persist();
    }
}
