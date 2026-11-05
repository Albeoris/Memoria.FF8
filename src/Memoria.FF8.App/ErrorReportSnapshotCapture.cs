using Memoria.FF8.Runtime;

namespace Memoria.FF8;

internal sealed record ErrorReportSnapshot(GameState? State, string Kind, DateTimeOffset CapturedUtc, string UnavailableReason);

/// <summary>Runs only on the game owner thread, including resource continuation validation.</summary>
internal static class ErrorReportSnapshotCapture
{
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
                    return new(state, "earlier validated player save; age at failure " + age + " seconds; " + reason, capturedUtc, "");
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
