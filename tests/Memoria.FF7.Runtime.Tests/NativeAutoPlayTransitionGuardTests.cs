using Memoria.FF7.Runtime;

internal static class NativeAutoPlayTransitionGuardTests
{
    public static void Run()
    {
        ExternalPublicationsDoNotConcealTransitionCycles();
        OriginalPublicationWitnesses();
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

    private static void ExternalPublicationsDoNotConcealTransitionCycles()
    {
        Ff7Progress progress = new() { InitialKernelData = [], Characters = [], Party = [], Inventory = [], Materia = [] };
        byte[] persistent = new byte[1280];
        string baseline = Ff7Session.CaptureTransitionProgressWitness(persistent, progress);
        Ff7AutoPlayTransitionGuard guard = new();
        for (int generation = 0; generation <= 6; generation++)
        {
            persistent[16] = (byte)generation;
            persistent[75] = (byte)generation;
            persistent[122] = (byte)generation;
            persistent[774] = (byte)(generation * 33);
            persistent[775] = (byte)generation;
            persistent[798] = (byte)generation;
            string witness = Ff7Session.CaptureTransitionProgressWitness(persistent, progress);
            Check(witness == baseline && persistent[774] == (byte)(generation * 33), "Transition observation counted an external native publication as progress or changed its source.");
            Check(guard.Observe(generation % 2 == 0 ? "field-a" : "field-b", generation, witness) == (generation == 6), "Encounter counters concealed an unchanged native transition cycle.");
        }
        persistent[200] = 1;
        Check(Ff7Session.CaptureTransitionProgressWitness(persistent, progress) != baseline, "A native quest flag outside the chapter counter was masked as presentation.");
        persistent[200] = 0;
        Check(Ff7Session.CaptureTransitionProgressWitness(persistent, progress with { Inventory = [new(1, 1)] }) != baseline, "An actual inventory reward failed to renew transition progress.");
    }
    private static void OriginalPublicationWitnesses()
    {
        string? game = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR");
        string? firstPath = Environment.GetEnvironmentVariable("MEMORIA_FF7_TRANSITION_PUBLICATION_SAVE");
        string? secondPath = Environment.GetEnvironmentVariable("MEMORIA_FF7_TRANSITION_COMPARISON_SAVE");
        if (game is null || firstPath is null || secondPath is null)
        {
            return;
        }
        byte[] firstBytes = File.ReadAllBytes(firstPath);
        byte[] secondBytes = File.ReadAllBytes(secondPath);
        using MemoryStream firstStream = new(firstBytes, writable: false);
        using MemoryStream secondStream = new(secondBytes, writable: false);
        var installation = Memoria.FF7.Resources.Ff7Installation.Discover(game);
        using Ff7Session first = Ff7Session.Restore(installation, CombinedSessionCodec.Read(firstStream).Ff7!);
        using Ff7Session second = Ff7Session.Restore(installation, CombinedSessionCodec.Read(secondStream).Ff7!);
        string firstBefore = System.Text.Json.JsonSerializer.Serialize(first.Capture());
        string secondBefore = System.Text.Json.JsonSerializer.Serialize(second.Capture());
        Check(first.CaptureEventProgressWitness() != second.CaptureEventProgressWitness(), "The original general event witness lost native external-publication changes.");
        Check(first.CaptureTransitionProgressWitness() == second.CaptureTransitionProgressWitness(), "Ordinary native publications concealed unchanged material transition progress.");
        Check(firstBefore == System.Text.Json.JsonSerializer.Serialize(first.Capture()) && secondBefore == System.Text.Json.JsonSerializer.Serialize(second.Capture()), "Transition progress observation changed a native session.");
        Check(firstBytes.SequenceEqual(File.ReadAllBytes(firstPath)) && secondBytes.SequenceEqual(File.ReadAllBytes(secondPath)), "Transition witness checks changed their external standalone sources.");
        Console.WriteLine("PASS original material transition witnesses match while general native event witnesses remain distinct; native/source unchanged.");
    }
    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
