using Memoria.FF7.Runtime;
using Memoria.AutoPlay;
using Memoria.FF7.Resources;

internal static class NativeAutoPlayTransitionGuardTests
{
    public static void Run()
    {
        ExternalPublicationsDoNotConcealTransitionCycles();
        OriginalPublicationWitnesses();
        AuthenticatedStaticSourceExploration();
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

    private static void AuthenticatedStaticSourceExploration()
    {
        string visit = "ff7:385:" + new string('A', 64);
        AutoPlayEventCoverage<Ff7AutoPlayDeferral> Source(string id, string condition)
        {
            AutoPlayConditionCoverage conditions = new();
            conditions.ConsumedEntries.Add(condition);
            return new() { Event = new(id, AutoPlayEventKind.Transition, ConditionSignature: condition), Completed = true, Conditions = conditions, Routes = [new("destination", condition, false, null, TargetId: id)] };
        }
        Ff7AutoPlayState State(params AutoPlayEventCoverage<Ff7AutoPlayDeferral>[] entries)
        {
            Ff7AutoPlayState state = new();
            state.Coverage.Visits.Add(new() { Visit = new(visit, "program"), Events = entries.ToList() });
            return state;
        }
        Ff7AutoPlayTransitionGuard finite = new();
        List<AutoPlayEventCoverage<Ff7AutoPlayDeferral>> completed = [];
        for (int generation = 0; generation <= 12; generation++)
        {
            if (generation is 2 or 4 or 6)
            {
                completed.Add(Source($"gateway:{generation / 2 - 1}", "executed"));
            }
            Check(finite.Observe(generation % 2 == 0 ? "a" : "b", generation, "material", State(completed.ToArray())) == (generation == 12), "Finite distinct native gateway exploration or its later unchanged cycle was misclassified.");
        }
        foreach (string invalid in new[] { "legacy", "offered", "attempt", "wrong-condition", "wrong-target", "unknown", "duplicate", "new-condition", "restored-ledger" })
        {
            Ff7AutoPlayTransitionGuard guard = new();
            for (int generation = 0; generation <= 6; generation++)
            {
                var source = Source(invalid == "unknown" ? $"dynamic:{generation}" : "gateway:0", "condition:" + (invalid == "new-condition" ? generation : 0));
                if (invalid == "legacy")
                {
                    source.Conditions = null;
                }
                if (invalid == "offered")
                {
                    source.Completed = false;
                }
                if (invalid == "attempt")
                {
                    source.Routes.Clear();
                    source.Unavailable = true;
                }
                if (invalid == "wrong-condition")
                {
                    source.Conditions!.ConsumedEntries.Clear();
                }
                if (invalid == "wrong-target")
                {
                    source.Routes = [new("destination", "condition:0", false, null, TargetId: "gateway:1")];
                }
                var state = invalid == "restored-ledger" && generation > 0 ? State() : invalid == "duplicate" ? State(source, source) : State(source);
                Check(guard.Observe(generation % 2 == 0 ? "a" : "b", generation, "material", state) == (generation == 6), $"{invalid} source publications concealed a true unchanged cycle.");
            }
        }
        Console.WriteLine("PASS finite authenticated static source exploration; duplicate, condition, offered, attempt, legacy and restored-ledger cycle negatives");
    }

    internal static void RunExplorationOriginal()
    {
        string path = Environment.GetEnvironmentVariable("MEMORIA_FF7_TRANSITION_EXPLORATION_SAVE")!;
        byte[] bytes = File.ReadAllBytes(path);
        using MemoryStream stream = new(bytes, false);
        using Ff7Session session = Ff7Session.Restore(Ff7Installation.Discover(Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR")!), CombinedSessionCodec.Read(stream).Ff7!);
        using Ff7AutoPlayPolicy policy = new(session, (_, _) => throw new InvalidOperationException("Unexpected choice boundary."));
        bool admitted = false;
        session.NativeInteractionScriptRequested += request => admitted |= request.Entity == 14 && request.Script == 1 && request.Accepted;
        Ff7AutoPlayController controller = new(session, policy.ChooseInput, advanceTick: () => session.AdvanceTick(advanceClock: false), chooseDialogueSelection: policy.ChooseDialogueSelection, readPlanningProgress: () => policy.WorkProgress, readFrontierProgress: () => policy.FrontierProgress);
        controller.Start();
        var elapsed = System.Diagnostics.Stopwatch.StartNew();
        int polls = 0;
        while (controller.Running && !admitted && polls++ < 2000 && elapsed.Elapsed.TotalSeconds < 30)
        {
            controller.Advance(1);
        }
        var aliases = policy.State.Coverage.Visits.Single(visit => visit.Visit.Key.StartsWith("ff7:385:", StringComparison.Ordinal)).Events;
        Check(aliases.Count(item => item.Completed && item.Conditions?.ConsumedEntries.Count > 0) == 3, "The finite distinct gateway sources did not complete.");
        Console.WriteLine($"Original finite gateway exploration: callbacks={controller.Steps}, tick={session.Machine.Tick}, actor14Admitted={admitted}, active={policy.State.Active?.EventId}, physical={controller.PlanningProgress.PhysicalNodes}, detached={controller.PlanningProgress.DetachedNativeCallbacks}, error={controller.Error}");
        Check(controller.Error is null && admitted, "Normal policy did not progress from finite gateway exploration to the next native source.");
        Check(bytes.AsSpan().SequenceEqual(File.ReadAllBytes(path)), "The standalone source changed.");
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
