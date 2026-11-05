namespace Memoria.FF8;

internal sealed record AutoPlayRecentAction(long Step, int Field, string Scene, string Action);

internal sealed partial class AutoPlayController
{
    private bool captureDirty;
    private const long MaximumCaptureBytes = 128L * 1024 * 1024;
    private sealed record CaptureRootSet(string[] Dependencies, string? Snapshot, string? ExcursionCheckpoint);
    private CaptureRootSet? rollbackCaptureRoots;
    private CaptureRootSet? bundleCaptureRoots;
    private CaptureRootSet? failureCaptureRoots;

    private static CaptureRootSet FreezeCaptureRoots(AutoPlayRunState state) => new(CaptureDependencies(state).ToArray(), state.Snapshot, state.NavigationExcursion?.Checkpoint);

    private void ResetCaptureRoots()
    {
        rollbackCaptureRoots = null;
        bundleCaptureRoots = null;
        failureCaptureRoots = null;
    }

    private void RecordRecentAction(string action)
    {
        run.RecentActions.Add(new(run.Steps, session.State.FieldId, session.State.Scene.ToString(), action.Length > 512 ? action[..512] : action));
        if (run.RecentActions.Count > 32)
        {
            run.RecentActions.RemoveRange(0, run.RecentActions.Count - 32);
        }
    }

    internal static void RetireClosedSearchSnapshots(AutoPlayRunState state)
    {
        IEnumerable<AutoPlayInputSearch> searches = state.InputSearch is { } active ? state.InputSearchHistory.Append(active) : state.InputSearchHistory;
        foreach (AutoPlayInputSearch search in searches.Where(item => item.Closed))
        {
            search.SnapshotsRetired = true;
            foreach (AutoPlayInputSearchFrame frame in search.Frames)
            {
                frame.Checkpoint = string.Empty;
                frame.Context.BeforeCheckpoint = null;
            }
        }
    }

    internal static IEnumerable<string> CaptureDependencies(AutoPlayRunState state)
    {
        IEnumerable<string?> references = new[] { state.Snapshot, state.PrimaryContinuation, state.ActiveBeforeCheckpoint, state.DirectCodeEntry.TrainRetryCheckpoint, state.DirectCodeEntry.TrainRetryContext?.BeforeCheckpoint, state.NavigationExcursion?.Checkpoint };
        foreach (AutoPlayChoiceCoverage choice in state.Coverage.Choices)
        {
            if (!choice.Finished)
            {
                references = references.Concat(new[] { choice.BeforeCheckpoint, choice.ContinuationCheckpoint });
            }
            references = references.Concat(choice.PolicyCheckpoints.Values.SelectMany(item => new[] { item.BeforeCheckpoint, item.ContinuationCheckpoint }));
        }
        IEnumerable<AutoPlayInputSearch> searches = state.InputSearch is { } active ? state.InputSearchHistory.Append(active) : state.InputSearchHistory;
        references = references.Concat(searches.Where(item => !item.Closed).SelectMany(item => item.Frames).SelectMany(frame => new[] { frame.Checkpoint, frame.Context.BeforeCheckpoint }));
        return references.Where(item => !string.IsNullOrEmpty(item)).Select(item => item!).Distinct(StringComparer.OrdinalIgnoreCase);
    }

    internal void MaintainCapture()
    {
        if (!captureDirty || runDirectory is null)
        {
            return;
        }
        using IDisposable suspended = idleDeadline.Suspend();
        using AutoPlayPerformanceProfile.Scope measurement = performance.Measure("Capture.Maintain");
        RetireClosedSearchSnapshots(run);
        HashSet<string> retained = new(CaptureDependencies(run), StringComparer.OrdinalIgnoreCase);
        HashSet<string> standalone = new(StringComparer.OrdinalIgnoreCase);
        void KeepStandalone(CaptureRootSet? roots)
        {
            if (roots is null)
            {
                return;
            }
            retained.UnionWith(roots.Dependencies);
            if (roots.Snapshot is not null)
            {
                standalone.Add(roots.Snapshot);
            }
            if (roots.ExcursionCheckpoint is not null)
            {
                standalone.Add(roots.ExcursionCheckpoint);
            }
        }
        if (run.Snapshot is not null)
        {
            standalone.Add(run.Snapshot);
        }
        if (run.NavigationExcursion is { } excursion)
        {
            standalone.Add(excursion.Checkpoint);
        }
        KeepStandalone(rollbackCaptureRoots);
        KeepStandalone(bundleCaptureRoots);
        KeepStandalone(failureCaptureRoots);
        HashSet<string> manifests = new(StringComparer.OrdinalIgnoreCase) { "latest.run.json" };
        foreach (string? manifest in new[] { latestBundleManifest, failureBeforeManifest })
        {
            if (manifest is not null)
            {
                manifests.Add(Path.GetFileName(manifest));
            }
        }
        foreach (string reference in retained)
        {
            if (reference != Path.GetFileName(reference) || !File.Exists(Path.Combine(runDirectory, reference)))
            {
                throw new InvalidDataException("Automatic exploration checkpoint dependency is missing or outside its run directory.");
            }
        }
        foreach (string reference in retained.Except(standalone, StringComparer.OrdinalIgnoreCase))
        {
            string path = Path.Combine(runDirectory, reference);
            if (!AutoPlayCheckpointStore.IsStored(path))
            {
                AutoPlayCheckpointStore.Write(path, path);
            }
        }
        AutoPlayCheckpointStore.Collect(runDirectory, retained);
        foreach (string path in Directory.EnumerateFiles(runDirectory, "*.json"))
        {
            string name = Path.GetFileName(path);
            if (!manifests.Contains(name) && !retained.Contains(name))
            {
                File.Delete(path);
            }
        }
        long bytes = Directory.EnumerateFiles(runDirectory, "*", SearchOption.AllDirectories).Sum(path => new FileInfo(path).Length);
        if (bytes > MaximumCaptureBytes)
        {
            throw new IOException("Automatic exploration capture reached its 128 MiB storage budget. The last committed checkpoint remains available; stop and inspect the active search before continuing.");
        }
        captureDirty = false;
    }
}
