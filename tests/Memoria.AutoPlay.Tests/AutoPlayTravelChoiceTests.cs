using Memoria.AutoPlay;

internal static class AutoPlayTravelChoiceTests
{
    public static void Run()
    {
        AutoPlayEventRoute route = new("destination", "entry", false, [new("outer", 2), new("inner", 3)]);
        Check(AutoPlayTravelChoice.SelectRecordedSelection(route, [], "outer", 2, 3) == 2, "The first authenticated travel selection was changed.");
        Check(AutoPlayTravelChoice.SelectRecordedSelection(route, [new("outer", 2)], "inner", 2, 3) == 3, "The next authenticated travel selection was changed.");
        Reject(() => AutoPlayTravelChoice.SelectRecordedSelection(route, [new("outer", 1)], "inner", 2, 3), "A different executed selection authenticated a travel prefix.");
        Reject(() => AutoPlayTravelChoice.SelectRecordedSelection(route, [new("other", 2)], "inner", 2, 3), "A different executed key authenticated a travel prefix.");
        Reject(() => AutoPlayTravelChoice.SelectRecordedSelection(route, [], "other", 2, 3), "A different native choice key authenticated a travel selection.");
        Reject(() => AutoPlayTravelChoice.SelectRecordedSelection(route, [], "outer", 3, 4), "A selection below the native range was accepted.");
        Reject(() => AutoPlayTravelChoice.SelectRecordedSelection(route, [], "outer", 0, 1), "A selection above the native range was accepted.");
        Reject(() => AutoPlayTravelChoice.SelectRecordedSelection(route, route.Selections, "after", 0, 1), "An exhausted route invented another selection.");
        Reject(() => AutoPlayTravelChoice.SelectRecordedSelection(route with { Selections = [new("outer", 2)] }, [new("outer", 2), new("inner", 3)], "after", 0, 1), "A truncated route authenticated an overlong executed prefix.");
        Reject(() => AutoPlayTravelChoice.SelectRecordedSelection(route with { Selections = null }, [], "outer", 2, 3), "A missing recorded selection list authenticated travel.");
        Reject(() => AutoPlayTravelChoice.SelectRecordedSelection(route with { Selections = [] }, [], "outer", 2, 3), "An empty recorded selection list authenticated travel.");
        Reject(() => AutoPlayTravelChoice.SelectRecordedSelection(route, null, "outer", 2, 3), "A missing executed prefix authenticated travel.");
        AutoPlayEventRoute repeated = route with { Selections = [new("prompt", 2), new("prompt", 3)] };
        Check(AutoPlayTravelChoice.SelectRecordedSelection(repeated, [new("prompt", 2)], "prompt", 2, 3) == 3, "A repeated native key did not consume the next positional selection.");
        AutoPlayTravelChoice.RequireComplete(route, [new("outer", 2), new("inner", 3)]);
        AutoPlayTravelChoice.RequireComplete(route with { Selections = null }, null);
        AutoPlayTravelChoice.RequireComplete(route with { Selections = null }, [new("unrecorded", 1)]);
        AutoPlayTravelChoice.RequireComplete(route with { Selections = [] }, []);
        Reject(() => AutoPlayTravelChoice.RequireComplete(route, null), "A missing executed sequence completed recorded travel choices.");
        Reject(() => AutoPlayTravelChoice.RequireComplete(route, [new("outer", 2)]), "A truncated executed sequence completed recorded travel choices.");
        Reject(() => AutoPlayTravelChoice.RequireComplete(route, [new("outer", 1), new("inner", 3)]), "A different executed selection completed recorded travel choices.");
        Reject(() => AutoPlayTravelChoice.RequireComplete(route, [new("other", 2), new("inner", 3)]), "A different executed key completed recorded travel choices.");
        Reject(() => AutoPlayTravelChoice.RequireComplete(route, [new("outer", 2), new("inner", 3), new("extra", 0)]), "An overlong executed sequence completed recorded travel choices.");
        Reject(() => AutoPlayTravelChoice.RequireComplete(route with { Selections = [] }, null), "A missing executed sequence completed an explicitly empty recorded sequence.");
    }

    private static void Reject(Action select, string message)
    {
        try
        {
            select();
        }
        catch (NotSupportedException)
        {
            return;
        }
        throw new InvalidOperationException(message);
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
