using Memoria.FF7.Runtime;

internal static class NativeAutoPlayTransitionGuardTests
{
    public static void Run()
    {
        Ff7AutoPlayTransitionGuard guard = new();
        for (int generation = 0; generation < 6; generation++)
        {
            Check(!guard.Observe(generation % 2 == 0 ? "field-a" : "field-b", generation, "unchanged-native-progress"), "The transition guard stopped before three complete roundtrips.");
        }
        Check(guard.Observe("field-a", 6, "unchanged-native-progress"), "A genuine unchanged two-field cycle was not detected.");
        guard.Reset();
        for (int generation = 0; generation < 18; generation++)
        {
            Check(!guard.Observe(generation % 2 == 0 ? "field-a" : "field-b", generation, $"native-quest-flags:{generation / 2}"), "Native quest progress without a chapter-counter change was mistaken for a closed transition cycle.");
        }
        guard.Reset();
        for (int attempt = 0; attempt < 12; attempt++)
        {
            Check(!guard.Observe("field-a", 1, "unchanged-native-progress"), "Repeated observation of one native generation advanced transition history.");
        }
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
