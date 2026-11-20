using System.Diagnostics;
using System.Text.Json;
using Memoria.AutoPlay;
using Memoria.FF7.Events;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;

internal static class NativeTransportRegionTests
{
    internal static void Run()
    {
        FieldMachine machine = Fixture();
        string before = JsonSerializer.Serialize(machine.Capture());
        FieldTransportRegionProof affordable = machine.ProveTransportRegion(0, 1, 100, [new(0, 1)]);
        Check(affordable.Complete && affordable.Cost == 10, $"The fixed-price transport certificate failed: {affordable.Reason}");
        Check(affordable.Region == machine.ProveTransportRegion(0, 1, 90, [new(0, 1)]).Region, "Same native affordability region changed with wallet balance.");
        Check(affordable.Region != machine.ProveTransportRegion(0, 1, 9, [new(0, 1)]).Region, "A below-cost wallet reused the affordable region.");
        Check(machine.ProveTransportRegion(0, 1, 10, [new(0, 1)]).Region == affordable.Region, "The exact native cost boundary was not affordable.");
        machine.Variables.WriteWord(FieldBank.Persistent0Word, 0, 1);
        Check(machine.ProveTransportRegion(0, 1, 100, [new(0, 1)]).Region != affordable.Region, "Changed non-Gil state reused a completed region.");
        machine.Variables.WriteWord(FieldBank.Persistent0Word, 0, 0);
        Check(before == JsonSerializer.Serialize(machine.Capture()), "Certificate inspection changed native state.");
        Check(!Fixture([0xA8, 0x60, 10, 0, 0, 0]).ProveTransportRegion(0, 1, 100, [new(0, 1)]).Complete, "A movement coordinate derived from Gil was certified.");
        Check(!Fixture([0x14, 0x50, 11, 0, 0, 1]).ProveTransportRegion(0, 1, 100, [new(0, 1)]).Complete, "A partial byte alias was treated as a full typed affordability word.");
        Check(!Fixture([0x3E]).ProveTransportRegion(0, 1, 100, [new(0, 1)]).Complete, "Useful healing was erased as transport novelty.");
        Check(!Fixture(overlap: true).ProveTransportRegion(0, 1, 100, [new(0, 1)]).Complete, "Overlapping low/high output aliases were certified.");
        Check(!Fixture(variableCost: true).ProveTransportRegion(0, 1, 100, [new(0, 1)]).Complete, "A variable debit was treated as fixed.");
        Check(!Fixture(postPayment: [0x3B, 0x66, 10, 12]).ProveTransportRegion(0, 1, 100, [new(0, 1)]).Complete, "A post-debit query was projected from the original wallet.");
        Check(!Fixture(differentLanding: true).ProveTransportRegion(0, 1, 100, [new(0, 1)]).Complete, "Distinct landing operands in one map were collapsed.");
        Check(!Fixture([0x80, 0x50, 20, 1], external: [0x14, 0x50, 20, 0, 0, 1, 0]).ProveTransportRegion(0, 1, 100, [new(0, 1), new(1, 1)]).Complete, "A live external progression scratch consumer was ignored.");
        Check(!Fixture(external: [0x80, 0x50, 11, 1, 0]).ProveTransportRegion(0, 1, 100, [new(0, 1), new(1, 1)]).Complete, "An external byte-alias writer to Gil output was ignored.");
        Check(!Fixture([0x80, 0x30, 55, 1]).ProveTransportRegion(0, 1, 100, [new(0, 1)]).Complete, "Persistent progress writes were certified as paid transport utility.");
        CheckPlanner();
        Console.WriteLine("PASS transport affordability/native alias/reward barriers, optional intrinsic-goal suppression and paid-route reuse");
    }

    private static FieldMachine Fixture(byte[]? extra = null, bool overlap = false, bool variableCost = false, byte[]? postPayment = null, bool differentLanding = false, byte[]? external = null)
    {
        List<byte> bytes = [0, 0x16, 0x20, 0, 0, 1, 0, 0, 1, 0x80, 0x50, 14, 3, 0x48, 0x05, 0, 0, 3, 4, 14, 0x3B, 0x66, 10, (byte)(overlap ? 11 : 12)];
        bytes.AddRange(extra ?? []);
        bytes.AddRange([0x18, 0x60, 12, 0, 0, 0, 0, 10, 0x18, 0x60, 10, 0, 10, 0, 3, 2, 0, 0x3A, (byte)(variableCost ? 0x60 : 0), 10, 0, 0, 0]);
        bytes.AddRange(postPayment ?? []);
        if (differentLanding)
        {
            bytes.AddRange([0x16, 0x20, 0, 0, 1, 0, 0, 12]);
        }
        bytes.AddRange([0x60, 0x8B, 1, 0, 0, 0, 0, 0, 0, 0, 0]);
        if (differentLanding)
        {
            bytes.AddRange([0x60, 0x8B, 1, 1, 0, 0, 0, 0, 0, 0, 0]);
        }
        ushort[] entries = new ushort[32];
        entries[1] = 1;
        List<ushort[]> table = [entries];
        if (external is not null)
        {
            ushort[] peer = new ushort[32];
            peer[1] = checked((ushort)bytes.Count);
            table.Add(peer);
            bytes.AddRange(external);
        }
        FieldMachine machine = new(new FieldProgram(bytes.ToArray(), table));
        machine.Initialize();
        return machine;
    }

    private static void CheckPlanner()
    {
        string region = new('A', 64);
        AutoPlayEvent paid = new("paid", AutoPlayEventKind.Interaction, ConditionSignature: "new-wallet");
        AutoPlayEvent reward = new("reward", AutoPlayEventKind.Interaction, ConditionSignature: "fresh");
        Ff7AutoPlayState state = new();
        AutoPlayEventCoverage<Ff7AutoPlayDeferral> edge = new() { Event = paid, Available = true, Completed = true, Conditions = new() { ConsumedEntries = new(StringComparer.Ordinal) { new string('C', 64) } }, Routes = [new("B", new string('C', 64), false, [], "paid")] };
        AutoPlayEventCoverage<Ff7AutoPlayDeferral> target = new() { Event = reward, Available = true };
        state.Coverage.Visits.Add(new() { Visit = new("A", "region"), Events = [edge] });
        state.Coverage.Visits.Add(new() { Visit = new("B", "region"), Events = [target] });
        state.TransportRegions["A|paid"] = new("new-wallet", region, region, new string('C', 64), "B", "real-choice");
        AutoPlayChoiceCoverage actualChoices = new() { Key = "real-choice", FirstOption = 3, OptionCount = 2, CompletedOptions = [3], ActiveOption = 4 };
        state.Coverage.Choices.Add(actualChoices);
        Check(Ff7AutoPlayPolicy.IsIntrinsicTransportGoal(state, "A", paid), "The unpaid native outcome was suppressed before it actually completed.");
        state.PendingRestore = new("actual-choice-continuation");
        Ff7AutoPlayState restored = state.Clone();
        restored.PendingRestore = null;
        Check(restored.TransportRegions["A|paid"] == state.TransportRegions["A|paid"] && Ff7AutoPlayPolicy.IsIntrinsicTransportGoal(restored, "A", paid), "Pending observational metadata was lost or prematurely became suppressing across normal restored-state cloning.");
        actualChoices.CompletedOptions.Add(4);
        actualChoices.ActiveOption = null;
        actualChoices.Finished = true;
        Check(!Ff7AutoPlayPolicy.IsIntrinsicTransportGoal(state, "A", paid), "A learned current region remained an intrinsic frontier.");
        Check(Ff7AutoPlayPolicy.IsIntrinsicTransportGoal(state, "A", paid with { ConditionSignature = "changed" }), "A changed observed condition inherited region reuse.");
        state.TransportRegions["A|paid"] = new("new-wallet", new('B', 64), region, new string('C', 64), "B", "real-choice");
        Check(Ff7AutoPlayPolicy.IsIntrinsicTransportGoal(state, "A", paid), "A changed region inherited reuse.");
        state.TransportRegions["A|paid"] = new("new-wallet", region);
        Check(Ff7AutoPlayPolicy.IsIntrinsicTransportGoal(state, "A", paid), "An unlearned observation certified native execution.");
        state.TransportRegions["A|paid"] = new("new-wallet", region, region, new string('C', 64), "B", "real-choice");
        AutoPlayPlanner<Ff7AutoPlayDeferral> planner = Ff7AutoPlayPolicy.CreatePlanner(state);
        AutoPlayPlanStep? plan = planner.Plan("A");
        Check(plan is { IsTravel: true, Event.Id: "paid", Goal.EventId: "reward" }, "Known paid transport could not lead to a different unfinished goal.");
        state.Coverage.NavigationVisits.Add(new("A:nav:scope", "A", [paid]));
        state.Coverage.NavigationVisits.Add(new("B:nav:scope", "B", [reward]));
        state.Coverage.NavigationRoutes.Add(new("A:nav:scope", "B:nav:scope", "paid", edge.Routes[0]));
        AutoPlayPlanStep? scoped = planner.PlanNavigation("A:nav:scope");
        Check(scoped is { IsTravel: true, Event.Id: "paid", Goal.EventId: "reward", Goal.NavigationKey: "B:nav:scope" }, "Navigation projection lost the exact certified paid route or different goal.");
        Check(!Ff7AutoPlayPolicy.IsReusableTransportRoute(state, "A", paid, edge.Routes[0] with { DestinationVisitKey = "C" }), "An unrelated destination inherited paid route eligibility.");
        Check(!Ff7AutoPlayPolicy.IsReusableTransportRoute(state, "A", paid, edge.Routes[0] with { ConditionSignature = "other" }), "An unlearned historical predicate inherited paid route eligibility.");
        Ff7AutoPlayActiveEvent later = new("A", "paid", new string('D', 64), 2, 1, 1, Ff7AutoPlayTargetKind.ActorTalk, 1, 641, "witness", Accepted: true, TransitionOwned: true);
        state.Choice = new("later-unfinished", later);
        state.Coverage.Choices.Add(new() { Key = "later-unfinished", FirstOption = 3, OptionCount = 2, ActiveOption = 3 });
        Ff7AutoPlayPolicy.RecordTransportRegion(state, later, new(region, 10, 90, 1), "B");
        Check(state.TransportRegions["A|paid"].ChoiceKey == "real-choice" && !Ff7AutoPlayPolicy.IsIntrinsicTransportGoal(state, "A", paid), "A later legitimate paid replay forgot already completed equivalent outcomes.");
        AutoPlayEventRoute laterRoute = new("B", later.ConditionFingerprint, false, [], "paid");
        Check(Ff7AutoPlayPolicy.IsReusableTransportRoute(state, "A", paid, laterRoute), "A later genuine paid route was lost under current-wallet observation.");
        edge.Routes.Add(laterRoute);
        Check(planner.Plan("A") is { IsTravel: true, Goal.EventId: "reward", Route.ConditionSignature: var actualSource } && actualSource == later.ConditionFingerprint, "Current-wallet planning did not use the later actual paid-route source receipt.");
        Check(!state.Coverage.Choices.Last().Finished && state.Coverage.Choices.Last().CompletedOptions.Count == 0, "Retaining known region outcomes fabricated new native choice credit.");
        target.Completed = true;
        Check(planner.Plan("A") is null, "The transport itself remained a terminal novelty goal.");
        Check(edge.Event.ConditionSignature == "new-wallet" && !edge.Conditions!.HasExecutedEntry("new-wallet") && edge.Available && !edge.Unavailable, "Derived goal preference changed native condition/credit/availability.");
        Ff7AutoPlayState legacy = JsonSerializer.Deserialize<Ff7AutoPlayState>("{\"TransportRegions\":null}")!;
        legacy.Validate();
        Check(legacy.TransportRegions.Count == 0, "Missing legacy derived metadata did not remain unknown.");
    }

    internal static void RunOriginal()
    {
        string path = Environment.GetEnvironmentVariable("MEMORIA_FF7_TRANSPORT_REGION_SAVE") ?? throw new InvalidOperationException("Select a current native paid-transfer fixture.");
        string game = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR") ?? throw new InvalidOperationException("Select resources.");
        byte[] bytes = File.ReadAllBytes(path);
        using MemoryStream input = new(bytes, false);
        CombinedSessionSave combined = CombinedSessionCodec.Read(input);
        Ff7SessionSave saved = combined.Ff7!;
        Ff7AutoPlayActiveEvent source = saved.AutoPlay!.Active!;
        string key = source.VisitId + "|" + source.EventId;
        string choiceKey = saved.AutoPlay.Choice!.Key;
        AutoPlayChoiceCoverage prior = saved.AutoPlay.Coverage.Choices.Single(item => item.Key == choiceKey);
        string directory = Path.Combine(Path.GetTempPath(), "memoria-transport-region-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            using Ff7Session session = Ff7Session.Restore(Ff7Installation.Discover(game), saved);
            using Ff7AutoPlayPolicy policy = new(session, (id, snapshot) =>
            {
                string file = Path.Combine(directory, "continuation.ffsave");
                using FileStream output = File.Create(file);
                CombinedSessionCodec.Write(output, combined with { Ff7 = snapshot });
                return file;
            });
            Ff7AutoPlayController controller = new(session, policy.ChooseInput, chooseDialogueSelection: policy.ChooseDialogueSelection, advanceTick: () => session.AdvanceTick(advanceClock: false));
            controller.Start();
            Stopwatch elapsed = Stopwatch.StartNew();
            while (controller.Running && controller.Steps < 4 && !policy.State.TransportRegions.ContainsKey(key) && elapsed.Elapsed < TimeSpan.FromSeconds(5))
            {
                controller.Advance(1);
            }
            Check(controller.Error is null && policy.State.TransportRegions.TryGetValue(key, out Ff7AutoPlayTransportRegion? region) && region.CompletedRegion == region.Region && region.Region.Length == 64, $"Actual accepted paid transfer failed to learn its native region: {controller.Error}");
            AutoPlayChoiceCoverage current = policy.State.Coverage.Choices.Single(item => item.Key == choiceKey);
            Check(Ff7AutoPlayPolicy.IsIntrinsicTransportGoal(policy.State, source.VisitId, new(source.EventId, AutoPlayEventKind.Interaction, ConditionSignature: source.ConditionFingerprint)), "The unplayed native decline branch was suppressed by one paid receipt.");
            Check(!current.Finished && current.ActiveOption == prior.ActiveOption && current.CompletedOptions.SequenceEqual(prior.CompletedOptions), "Unplayed choice exploration was skipped or synthetically completed.");
            Check(policy.State.Choice?.Key == choiceKey && policy.State.Active is { Accepted: true, TransitionOwned: true } && policy.WorkProgress.EndsWith(":native-planning:0", StringComparison.Ordinal), "Learning displaced actual movie/setup ownership or started native graph work.");
            Check(session.Progress.Gil == saved.Progress.Gil && bytes.AsSpan().SequenceEqual(File.ReadAllBytes(path)), "Learning changed money or its original source.");
            Console.WriteLine($"PASS current original actual paid-transfer completion learns optional region: {controller.Steps} callbacks/{elapsed.Elapsed.TotalSeconds:F3}s, Gil{session.Progress.Gil} unchanged; real remaining branch/movie ownership preserved; source unchanged.");
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    private static void Check(bool value, string message)
    {
        if (!value)
        {
            throw new InvalidOperationException(message);
        }
    }
}

