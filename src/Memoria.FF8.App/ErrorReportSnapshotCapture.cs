using Memoria.FF8.Runtime;

namespace Memoria.FF8;

internal sealed record ErrorReportSnapshot(GameState? State, string Kind, DateTimeOffset CapturedUtc, string UnavailableReason);

/// <summary>Runs only on the game owner thread, including resource continuation validation.</summary>
internal static class ErrorReportSnapshotCapture
{
    internal static IEnumerable<(GameState State, DateTimeOffset CapturedUtc)> ReadEarlierPlayerSnapshots(PlayerSaveStore store, string? configuredPath, Func<string, GameState> loadConfigured, Action<Exception> reportFailure)
    {
        HashSet<string> paths = new(StringComparer.OrdinalIgnoreCase);
        foreach (PlayerSaveEntry entry in store.ReadQuickEntries().Where(entry => entry.CanLoad).OrderByDescending(entry => entry.SavedAtUtc))
        {
            string path = store.GetPath(entry.Slot);
            if (!paths.Add(path))
            {
                continue;
            }
            GameState? state = null;
            DateTimeOffset capturedUtc = default;
            try
            {
                state = store.Load(entry.Slot);
                capturedUtc = new DateTimeOffset(File.GetLastWriteTimeUtc(path));
            }
            catch (Exception failure)
            {
                reportFailure(failure);
            }
            if (state is not null)
            {
                yield return (state, capturedUtc);
            }
        }
        if (configuredPath is null || !paths.Add(configuredPath) || !File.Exists(configuredPath))
        {
            yield break;
        }
        GameState? configured = null;
        DateTimeOffset configuredUtc = default;
        try
        {
            configured = loadConfigured(configuredPath);
            configuredUtc = new DateTimeOffset(File.GetLastWriteTimeUtc(configuredPath));
        }
        catch (Exception failure)
        {
            reportFailure(failure);
        }
        if (configured is not null)
        {
            yield return (configured, configuredUtc);
        }
    }

    internal static ErrorReportSnapshot Capture(SaveBoundaryStatus boundary, Func<GameState> captureCurrent, Func<IEnumerable<(GameState State, DateTimeOffset CapturedUtc)>> readEarlier, Action<GameState> validate, DateTimeOffset now)
    {
        string reason = "Current snapshot unavailable: " + boundary + ".";
        if (boundary == SaveBoundaryStatus.Ready)
        {
            try
            {
                GameState current = captureCurrent();
                validate(current);
                return new(current, "current validated snapshot", now, "");
            }
            catch (Exception failure)
            {
                reason = "Current snapshot capture/validation failed: " + failure.Message;
            }
        }
        try
        {
            foreach ((GameState state, DateTimeOffset capturedUtc) in readEarlier())
            {
                try
                {
                    validate(state);
                    string age = Math.Max(0, (now - capturedUtc).TotalSeconds).ToString("F0", System.Globalization.CultureInfo.InvariantCulture);
                    return new(state, "earlier validated player save; age at failure " + age + " seconds; " + reason, capturedUtc, reason + " The attached earlier save is a recovery checkpoint; reproduction of this failure has not been verified.");
                }
                catch (Exception failure)
                {
                    reason += " Earlier save continuation validation failed: " + failure.Message;
                }
            }
        }
        catch (Exception failure)
        {
            reason += " Earlier save could not be read: " + failure.Message;
        }
        return new(null, "unavailable", now, reason + " No validated reproduction save is available.");
    }
}
