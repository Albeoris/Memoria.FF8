namespace Memoria.AutoPlay;

/// <summary>Authenticates the next native choice against a recorded travel route and its executed prefix.</summary>
public static class AutoPlayTravelChoice
{
    public static void RequireComplete(AutoPlayEventRoute route, IReadOnlyList<AutoPlayChoiceSelection>? executed)
    {
        if (route.Selections is { } expected && (executed is null || !expected.SequenceEqual(executed)))
        {
            throw new NotSupportedException("The native event returned control before every recorded travel choice was encountered.");
        }
    }

    public static int SelectRecordedSelection(AutoPlayEventRoute route, IReadOnlyList<AutoPlayChoiceSelection>? executed, string key, int first, int last)
    {
        if (route.Selections is null || executed is null || executed.Count >= route.Selections.Count || !route.Selections.Take(executed.Count).SequenceEqual(executed))
        {
            throw new NotSupportedException("The recorded travel route has no authenticated next choice selection.");
        }
        AutoPlayChoiceSelection next = route.Selections[executed.Count];
        if (next.Key != key || next.Selection < first || next.Selection > last)
        {
            throw new NotSupportedException("The actual native choice does not match the recorded travel route key and selection range.");
        }
        return next.Selection;
    }
}
