using Memoria.FF8.GameUi;
using Memoria.FF8.Runtime;

namespace Memoria.FF8;

internal enum AutoSaveKind
{
    Location,
    Interval
}

internal readonly record struct AutoSaveLocation(int Map, long Generation = 0, NativeFieldLoadContext? FieldLoad = null)
{
    public bool IsSameVisit(AutoSaveLocation other)
    {
        return Map == other.Map && Generation == other.Generation && ReferenceEquals(FieldLoad, other.FieldLoad);
    }
}

internal sealed class AutoSaveScheduler
{
    private AutoSaveLocation? location;
    private int intervalMinutes;
    private TimeSpan intervalElapsed;
    private long retryAfter;

    public AutoSaveKind? Pending { get; private set; }

    public void Reset(AutoSaveLocation? current = null)
    {
        location = current;
        intervalMinutes = 0;
        intervalElapsed = TimeSpan.Zero;
        Pending = null;
        retryAfter = 0;
    }

    public void Update(UiPreferences preferences, AutoSaveLocation? current, TimeSpan elapsed)
    {
        if (!preferences.AutoSaveEnabled)
        {
            Reset(current ?? location);
            return;
        }

        if (Pending == AutoSaveKind.Location && !preferences.AutoSaveOnLocationEntry || Pending == AutoSaveKind.Interval && preferences.AutoSaveIntervalMinutes == 0)
        {
            Pending = null;
            retryAfter = 0;
        }

        if (current is { } visit && (location is not { } previous || !visit.IsSameVisit(previous)))
        {
            location = visit;
            if (preferences.AutoSaveOnLocationEntry)
            {
                Pending ??= AutoSaveKind.Location;
            }
        }

        if (intervalMinutes != preferences.AutoSaveIntervalMinutes)
        {
            intervalMinutes = preferences.AutoSaveIntervalMinutes;
            intervalElapsed = TimeSpan.Zero;
        }

        if (intervalMinutes == 0)
        {
            return;
        }

        intervalElapsed += elapsed;
        if (intervalElapsed >= TimeSpan.FromMinutes(intervalMinutes))
        {
            Pending ??= AutoSaveKind.Interval;
            intervalElapsed = TimeSpan.Zero;
        }
    }

    public void TrySave(Action<AutoSaveKind> save)
    {
        if (Pending is not { } kind || Environment.TickCount64 < retryAfter)
        {
            return;
        }

        try
        {
            save(kind);
        }
        catch (Exception exception) when (exception is InvalidOperationException or NotSupportedException or InvalidDataException or ArgumentException)
        {
            // A live operation can still prevent capture after the initial boundary check.
            return;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or System.Text.Json.JsonException)
        {
            if (retryAfter == 0)
            {
                Console.Error.WriteLine(exception);
            }
            retryAfter = Environment.TickCount64 + 1000;
            return;
        }

        Pending = null;
        retryAfter = 0;
        if (kind == AutoSaveKind.Interval)
        {
            intervalElapsed = TimeSpan.Zero;
        }
    }
}
