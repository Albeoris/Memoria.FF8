using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8;

internal sealed record AutoPlayRecentAction(long Step, int Field, string Scene, string Action);

internal sealed partial class AutoPlayRunState
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? InterruptedFieldCallbackEntry { get; set; }
}

internal sealed partial class AutoPlayController
{
    internal string? DiagnosticFailurePath { get; private set; }

    private bool captureDirty;
    private bool pendingLocationCheckpoint;
    private const long MaximumCaptureBytes = 128L * 1024 * 1024;
    private sealed record CaptureRootSet(string[] Dependencies, string? Snapshot);
    private CaptureRootSet? bundleCaptureRoots;
    private (string Name, GameState State)? stagedAttempt;

    private static CaptureRootSet FreezeCaptureRoots(AutoPlayRunState state) => new(CaptureDependencies(state).ToArray(), state.Snapshot);

    private void ResetCaptureRoots()
    {
        bundleCaptureRoots = null;
        stagedAttempt = null;
        pendingLocationCheckpoint = false;
        DiagnosticFailurePath = null;
    }

    internal static bool EnteredLocation(GameState before, GameState after)
    {
        return after.Scene is SceneKind.Field or SceneKind.WorldMap
            && (before.FieldId != after.FieldId
                || (before.Scene == SceneKind.WorldMap) != (after.Scene == SceneKind.WorldMap)
                || after.Scene == SceneKind.Field && after.EventState is null && !ReferenceEquals(before.FieldLoadContext, after.FieldLoadContext));
    }

    private void SaveLocationEntry()
    {
        if (pendingLocationCheckpoint)
        {
            SaveBundle($"entry-{run.Steps:D10}", CaptureState());
        }
    }

    internal string StageAttempt(string name, GameState state)
    {
        // The rejected variant no longer owns the active attempt. A search that
        // still references its source keeps it through the regular dependencies.
        run.ActiveBeforeCheckpoint = null;
        MaterializeAttempt();
        stagedAttempt = (name, state);
        return name;
    }

    private void MaterializeAttempt()
    {
        if (stagedAttempt is { } staged && CaptureDependencies(run).Contains(staged.Name, StringComparer.OrdinalIgnoreCase))
        {
            string path = Path.Combine(runDirectory!, staged.Name);
            if (!File.Exists(path))
            {
                SaveGame(path, staged.State);
            }
        }
    }

    private void RecordRecentAction(string action)
    {
        Memoria.AutoPlay.AutoPlayRecording.AppendRecent(run.RecentActions, new AutoPlayRecentAction(run.Steps, session.State.FieldId, session.State.Scene.ToString(), Memoria.AutoPlay.AutoPlayRecording.LimitActionText(action)));
    }

    private void PreserveFailureCapture(Exception exception)
    {
        if (runDirectory is not string directory)
        {
            return;
        }

        using IDisposable suspended = idleDeadline.Suspend();
        try
        {
            (string manifest, bool interruptedCallback) = SaveFailureBundle(directory, exception);
            if (interruptedCallback)
            {
                DiagnosticFailurePath = manifest;
                Status += " The exact snapshot records an interrupted field callback. Resume from the location checkpoint.";
            }
            else
            {
                ReproductionPath = manifest;
            }
        }
        catch (Exception captureError)
        {
            Status += $" Exact failure snapshot unavailable. Location checkpoint: {ReproductionPath ?? "unavailable"}. Capture error: {captureError.Message}";
        }

        try
        {
            const int maximumDiagnosticText = 8192;
            string text = Status + Environment.NewLine + Error;
            string details = string.Join(Environment.NewLine, text[..Math.Min(text.Length, maximumDiagnosticText)], JsonSerializer.Serialize((exception as EventScriptException)?.FaultContext, BeforeSnapshotJsonOptions), JsonSerializer.Serialize(run.RecentActions, BeforeSnapshotJsonOptions));
            if (Encoding.UTF8.GetByteCount(details) <= RemainingCaptureBytes(directory))
            {
                AtomicWrite(Path.Combine(directory, "failure.txt"), details);
            }
        }
        catch (Exception saveError) when (saveError is IOException or UnauthorizedAccessException)
        {
            // The reproduction path still points to the last successfully published bundle.
        }
    }

    private (string Manifest, bool InterruptedCallback) SaveFailureBundle(string directory, Exception exception)
    {
        long remainingBytes = RemainingCaptureBytes(directory);
        if (remainingBytes <= 0)
        {
            throw new IOException("Automatic exploration capture reached its 128 MiB storage budget");
        }

        GameState failure = CaptureFailureState(exception);
        bool interrupted = HasInterruptedFieldCallback(failure.EventState) || HasInterruptedFieldCallback(failure.DepartingFieldState);
        string snapshot = $"failure-{run.Steps:D10}.json";
        string manifest = Path.ChangeExtension(snapshot, ".run.json");
        string? previousSnapshot = run.Snapshot;
        string staging = Path.Combine(directory, ".failure-" + Guid.NewGuid().ToString("N"));
        List<string> files = [];
        int published = 0;
        void StageFile(string filename, Action<string> write)
        {
            string temporary = Path.Combine(staging, filename);
            write(temporary);
            remainingBytes -= new FileInfo(temporary).Length;
            if (remainingBytes < 0)
            {
                throw new IOException("The failure bundle exceeds the remaining 128 MiB capture budget");
            }
            files.Add(filename);
        }

        Directory.CreateDirectory(staging);
        try
        {
            foreach (string reference in CaptureDependencies(run))
            {
                if (reference != Path.GetFileName(reference))
                {
                    throw new InvalidDataException("An automatic exploration dependency is outside its run directory");
                }
                if (File.Exists(Path.Combine(directory, reference)))
                {
                    continue;
                }
                if (stagedAttempt is not { } staged || reference != staged.Name)
                {
                    throw new InvalidDataException($"Automatic exploration checkpoint dependency '{reference}' is missing");
                }
                StageFile(reference, path => new GameSession(staged.State).Save(path, compact: true));
            }

            run.Snapshot = snapshot;
            if (interrupted)
            {
                run.InterruptedFieldCallbackEntry = Path.GetFileName(latestBundleManifest) ?? "latest.run.json";
            }
            StageFile(snapshot, path => new GameSession(failure).Save(path, compact: true));
            StageFile(manifest, path => File.WriteAllText(path, JsonSerializer.Serialize(run, BeforeSnapshotJsonOptions)));
            foreach (string file in files)
            {
                File.Move(Path.Combine(staging, file), Path.Combine(directory, file));
                published++;
            }
            return (Path.Combine(directory, manifest), interrupted);
        }
        catch
        {
            foreach (string file in files.Take(published))
            {
                File.Delete(Path.Combine(directory, file));
            }
            throw;
        }
        finally
        {
            run.Snapshot = previousSnapshot;
            run.InterruptedFieldCallbackEntry = null;
            try
            {
                Directory.Delete(staging, recursive: true);
            }
            catch (Exception cleanupError) when (cleanupError is IOException or UnauthorizedAccessException)
            {
                // A staging cleanup failure cannot invalidate a published manifest.
            }
        }
    }

    private GameState CaptureFailureState(Exception exception)
    {
        bool wasAdvancing = advancing;
        advancing = false;
        try
        {
            GameWorld world = getWorld();
            if (world.Events.LastFault is not null && world.Field?.Id != world.Events.FieldId)
            {
                throw new InvalidOperationException("The fault interrupted native field allocation before its VM could be saved");
            }
            GameState state = CaptureState(world);
            if (exception is EventScriptException { FaultContext: { } fault }
                && !HasFaultedVirtualMachine(state.EventState, fault) && !HasFaultedVirtualMachine(state.DepartingFieldState, fault))
            {
                throw new InvalidOperationException($"The captured state lacks the faulted field {fault.FieldId} VM");
            }
            return state;
        }
        finally
        {
            advancing = wasAdvancing;
        }
    }

    private static bool HasFaultedVirtualMachine(JsonElement? state, EventFaultContext fault) =>
        state is { } vm && vm.GetProperty("FieldId").GetInt32() == fault.FieldId && vm.GetProperty("Frame").GetInt64() == fault.Frame;

    private static bool HasInterruptedFieldCallback(JsonElement? state) =>
        state is { } vm && vm.TryGetProperty("FieldCallbackInterrupted", out JsonElement interrupted) && interrupted.ValueKind == JsonValueKind.True;

    private static void RejectDiagnosticFailureManifest(AutoPlayRunState saved, string manifest)
    {
        if (saved.InterruptedFieldCallbackEntry is string entry)
        {
            string fallback = Path.Combine(Path.GetDirectoryName(manifest)!, Path.GetFileName(entry));
            throw new NotSupportedException($"This failure bundle records an interrupted field callback. Resume from the location checkpoint '{fallback}'.");
        }
    }

    private static long RemainingCaptureBytes(string directory) =>
        MaximumCaptureBytes - Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories).Sum(path => new FileInfo(path).Length);

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
        IEnumerable<string?> references = new[] { state.Snapshot, state.PrimaryContinuation, state.ActiveBeforeCheckpoint, state.DirectCodeEntry.TrainRetryCheckpoint, state.DirectCodeEntry.TrainRetryContext?.BeforeCheckpoint };
        foreach (AutoPlayChoiceCoverage choice in state.Coverage.Choices)
        {
            if (!choice.Finished)
            {
                references = references.Concat(new[] { choice.BeforeCheckpoint, choice.ContinuationCheckpoint, choice.BeforeContext?.BeforeCheckpoint });
            }
            references = references.Concat(choice.PolicyCheckpoints.Values.SelectMany(item => new[] { item.BeforeCheckpoint, item.ContinuationCheckpoint, item.BeforeContext?.BeforeCheckpoint }));
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
        }
        if (run.Snapshot is not null)
        {
            standalone.Add(run.Snapshot);
        }
        KeepStandalone(bundleCaptureRoots);
        HashSet<string> manifests = new(StringComparer.OrdinalIgnoreCase) { "latest.run.json" };
        if (latestBundleManifest is not null)
        {
            manifests.Add(Path.GetFileName(latestBundleManifest));
        }
        if (stagedAttempt is { } staged && !File.Exists(Path.Combine(runDirectory, staged.Name)))
        {
            retained.Remove(staged.Name);
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
        if (RemainingCaptureBytes(runDirectory) < 0)
        {
            throw new IOException("Automatic exploration capture reached its 128 MiB storage budget. The last committed checkpoint remains available; stop and inspect the active search before continuing.");
        }
        captureDirty = false;
    }
}
