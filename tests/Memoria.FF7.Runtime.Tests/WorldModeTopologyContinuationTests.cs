using System.Diagnostics;
using System.Reflection;
using System.Text.Json;
using Memoria.FF7.Events;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;

internal static class WorldModeTopologyContinuationTests
{
    internal static void Run()
    {
        CheckChangedInertGuard();
        WorldScriptAnalysis inert = new(true, null, 0, 0, [], [], [], []) { IsInert = true };
        WorldScriptAnalysis transfer = inert with { IsInert = false, IsPureFieldTransfer = true, Transitions = [new(1, WorldOperation.FieldTransition, 1)] };
        Check(Ff7AutoPlayPolicy.RequiresWorldRiderPrerequisite(inert, transfer), "A complete inert vehicle entry lost its real rider prerequisite.");
        Check(!Ff7AutoPlayPolicy.RequiresWorldRiderPrerequisite(inert with { Complete = false }, transfer) && !Ff7AutoPlayPolicy.RequiresWorldRiderPrerequisite(transfer, transfer) && !Ff7AutoPlayPolicy.RequiresWorldRiderPrerequisite(inert, inert) && !Ff7AutoPlayPolicy.RequiresWorldRiderPrerequisite(inert, transfer with { Complete = false }), "Unknown, active vehicle or inert rider entries were suppressed.");
        Ff7AutoPlayWorldScriptObservation target = new("selected", 7, [], transfer, "predicate", "geometry", [], true, null);
        Ff7AutoPlayPolicy.WorldModeCandidate wanted = new(target, new(target.Id, new(1, 2), 7, 0));
        Ff7AutoPlayPolicy.WorldModeCandidate unrelated = new(target with { Id = "unrelated" }, new("unrelated", new(3, 4), 7, 0));
        Dictionary<string, Ff7AutoPlayPolicy.WorldModeCandidate> candidates = new() { [wanted.Target.Id] = wanted, [unrelated.Target.Id] = unrelated };
        Ff7AutoPlayActiveEvent selected = new("world", target.Id, "condition", 0, 5, 7, Ff7AutoPlayTargetKind.WorldMesh, null, 641, "witness");
        Check(ReferenceEquals(Ff7AutoPlayPolicy.SelectWorldModePrerequisite(null, candidates, 0, unrelated), unrelated), "Initial native mode discovery lost its queued candidate.");
        Check(ReferenceEquals(Ff7AutoPlayPolicy.SelectWorldModePrerequisite(selected, candidates, 1, unrelated), wanted), "One previously observed unrelated mode blocked the selected raw target's own prerequisite.");
        Check(Ff7AutoPlayPolicy.SelectWorldModePrerequisite(selected with { Accepted = true }, candidates, 0, unrelated) is null, "Speculative topology work blocked an already admitted native owner.");
        int disposed = 0;
        IEnumerable<Ff7AutoPlayWorldModeGuide?> Waiting()
        {
            try
            {
                while (true)
                {
                    yield return null;
                }
            }
            finally
            {
                disposed++;
            }
        }
        wanted.Search = new(Waiting().GetEnumerator());
        unrelated.Search = new(Waiting().GetEnumerator());
        wanted.Search.Resume(new(1, TimeSpan.MaxValue));
        unrelated.Search.Resume(new(1, TimeSpan.MaxValue));
        Ff7AutoPlayPolicy.DisposeWorldModeCandidates([wanted], [wanted, unrelated]);
        Check(disposed == 2, "Canceling topology planning leaked a restored catalog-only cursor.");
        Check(Ff7AutoPlayPolicy.IsInertWorldMovementEntry(inert) && Ff7AutoPlayPolicy.IsInertWorldMovementEntry(inert with { HostReads = [new(WorldHostDependencyKind.Special, 0, 8)] }), "A complete movement-independent current inert entry stayed directly actionable.");
        Check(!Ff7AutoPlayPolicy.IsInertWorldMovementEntry(transfer) && !Ff7AutoPlayPolicy.IsInertWorldMovementEntry(inert with { Complete = false }) && !Ff7AutoPlayPolicy.IsInertWorldMovementEntry(inert with { HostReads = [new(WorldHostDependencyKind.ModelDistance, 0, 1)] }) && !Ff7AutoPlayPolicy.IsInertWorldMovementEntry(inert with { HostReads = [new(WorldHostDependencyKind.Special, 0, 7)] }), "An active, unresolved, positional or unknown host-dependent entry was hidden.");
        int reverseVisits = 0;
        int transportVisits = 0;
        int probes = 0;
        Ff7WorldTerrainReference Face(int index) => new(0, 0, index);
        Ff7AutoPlayWorldGuideFace Read(Ff7WorldTerrainReference source) => new(source, new(source.Triangle, 0, 0), new(source.Triangle, 0, 1), new(source.Triangle + 1, 0, 0), 0);
        IEnumerable<Ff7WorldTerrainReference> Adjacent(Ff7WorldTerrainReference source, int model, int terrain, bool reverse)
        {
            if (reverse)
            {
                reverseVisits++;
                if (source.Triangle > 0)
                {
                    yield return Face(source.Triangle - 1);
                }
            }
            else
            {
                transportVisits++;
                if (source.Triangle < 5)
                {
                    yield return Face(source.Triangle + 1);
                }
            }
        }
        Ff7WorldRuntime.AutoPlayTerrainGraph graph = new(Read, _ => 0, Adjacent, () => false, (_, _, _) => true, _ => true);
        Ff7WorldTerrainReference? Landing(Ff7AutoPlayWorldGuideFace source, Ff7AutoPlayWorldGuideFace? previous)
        {
            probes++;
            Check(reverseVisits == 257, "Transport probing used a truncated rider component.");
            return source.Source.Triangle == 5 ? Face(0) : null;
        }
        using Ff7WorldModeTopologyCursor cursor = new(Ff7WorldRuntime.EnumerateSurfaceVehicleTopology(graph, new HashSet<Ff7WorldTerrainReference> { Face(256) }, Face(0), 5, 0, 0, _ => true, Landing).GetEnumerator());
        cursor.Resume(new(0, TimeSpan.MaxValue));
        Check(cursor.Operations == 0 && !cursor.Completed && probes == 0, "A zero scheduling allowance performed native topology work.");
        cursor.Resume(new(1, TimeSpan.MaxValue));
        Check(!cursor.Completed && reverseVisits > 1 && reverseVisits <= 64 && transportVisits == 0 && probes == 0, "A rider quantum escaped its retained reverse phase.");
        long before = cursor.Operations;
        while (!cursor.Completed)
        {
            int probesBefore = probes;
            cursor.Resume(new(1, TimeSpan.Zero));
            Check(probes - probesBefore <= 1, "Cheap bookkeeping batching bypassed the native landing probe operation allowance.");
            Check(cursor.Operations > before, "A pending topology queue stopped doing real work.");
            before = cursor.Operations;
        }
        Check(cursor.Result is { Limited: false, TransportGuide.Faces.Length: 6, RiderGuide.Faces.Length: 257, RiderReachableFaces.Length: 257 } && probes == 6, "The multi-quantum graph lost its transport parent route, rider next chain or finite landing probes.");
        long done = cursor.Operations;
        cursor.Resume(new(4, TimeSpan.MaxValue));
        Check(done == cursor.Operations, "A completed topology traversal manufactured work.");
        using Ff7WorldModeTopologyCursor blocked = new(Ff7WorldRuntime.EnumerateSurfaceVehicleTopology(graph, new HashSet<Ff7WorldTerrainReference> { Face(256) }, Face(0), 5, 0, 0, _ => false, (_, _) => throw new InvalidOperationException("A nonlanding terrain was probed.")).GetEnumerator());
        while (!blocked.Completed)
        {
            blocked.Resume(new(2, TimeSpan.MaxValue));
        }
        Check(blocked.Result is { Limited: false, TransportGuide.Faces.Length: 0 }, "Finite exhaustion was reported as an incomplete budget.");
        Console.WriteLine("PASS retained rider reverse, transport Dijkstra, finite landing probes, exact quantum boundaries and mode eligibility");
    }

    internal static void RunOriginal()
    {
        string path = Environment.GetEnvironmentVariable("MEMORIA_FF7_WORLD_MODE_TOPOLOGY_SAVE") ?? throw new InvalidOperationException("Select a current native mode topology fixture.");
        string game = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR") ?? throw new InvalidOperationException("Select original resources.");
        byte[] bytes = File.ReadAllBytes(path);
        using MemoryStream stream = new(bytes, false);
        using Ff7Session session = Ff7Session.Restore(Ff7Installation.Discover(game), CombinedSessionCodec.Read(stream).Ff7!);
        Ff7WorldRuntime world = session.World ?? throw new InvalidDataException("No world allocation.");
        Check(world.ControlledModel == 5, "The fixture is not the current native surface vehicle boundary.");
        Ff7SessionSave initial = session.Capture();
        string native = JsonSerializer.Serialize(initial with { AutoPlay = null });
        using Ff7AutoPlayPolicy policy = new(session, (_, _) => throw new InvalidOperationException("Unexpected choice boundary."));
        string goal = JsonSerializer.Serialize(policy.State.NavigationGoal);
        int idle = policy.State.FrontierIdleSteps;
        Ff7AutoPlayController controller = new(session, policy.ChooseInput, advanceTick: () => session.AdvanceTick(advanceClock: false), chooseDialogueSelection: policy.ChooseDialogueSelection, readPlanningProgress: () => policy.WorkProgress, readFrontierProgress: () => policy.FrontierProgress);
        Ff7AutoplayDriver driver = (Ff7AutoplayDriver)typeof(Ff7AutoPlayController).GetField("driver", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(controller)!;
        controller.Start();
        Stopwatch elapsed = Stopwatch.StartNew();
        TimeSpan previous = TimeSpan.Zero;
        for (int poll = 0; poll < 64 && controller.Running && elapsed.Elapsed < TimeSpan.FromSeconds(15); poll++)
        {
            TimeSpan now = elapsed.Elapsed;
            session.AdvanceClock(now - previous);
            previous = now;
            controller.Advance(1);
        }
        var candidates = (System.Collections.IDictionary)typeof(Ff7AutoPlayPolicy).GetField("worldModePrerequisites", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(policy)!;
        object candidate = candidates["mesh:11:15:script:7"] ?? throw new InvalidOperationException("The selected raw goal has no proved rider prerequisite.");
        Ff7WorldModeTopologyCursor? search = (Ff7WorldModeTopologyCursor?)candidate.GetType().GetProperty("Search")!.GetValue(candidate);
        Check(controller.Error is null && driver.PlanningPending && search is { Completed: false, Operations: > 8 } && driver.PhysicalPlanningWork == 0 && controller.Steps == 0, $"Current native topology continuation failed: {controller.Error}; status={policy.Status}; work={search?.Operations}");
        Check(idle == policy.State.FrontierIdleSteps && goal == JsonSerializer.Serialize(policy.State.NavigationGoal), "Topology scheduling charged input or replaced the owned downstream goal.");
        Ff7SessionSave after = session.Capture();
        Ff7SessionSave normalized = after with
        {
            AutoPlay = null,
            Progress = after.Progress with { Clock = initial.Progress.Clock },
            Process = after.Process with { SoundVolumeClockSubmillisecondTicks = initial.Process.SoundVolumeClockSubmillisecondTicks }
        };
        Check(JsonSerializer.Serialize(normalized) == native && bytes.AsSpan().SequenceEqual(File.ReadAllBytes(path)), "Topology advice changed native state beyond the private clock/audio bridges or its source file.");
        Console.WriteLine($"PASS current native vehicle/rider topology: callbacks={controller.Steps}, worldTick={world.Process.Tick}, model={world.ControlledModel}, operations={search!.Operations}, physical={driver.PhysicalPlanningWork}, idle={idle}, elapsed={elapsed.Elapsed.TotalSeconds:F3}s; goal/source/native state unchanged; disembarking unproved");
    }

    private static void CheckChangedInertGuard()
    {
        byte[] persistent = new byte[1280];
        persistent[100] = 4;
        ushort[] code = [0, 0x100, 0x114, 802, 0x17, 0x201, 13, 0x100, 0x110, 1, 0x110, 0, 0x318, 0x203];
        WorldMachine machine = new(new(new([new(0x8000, 1)], code)), persistent, new RejectHost());
        WorldScriptAnalysis blocked = machine.AnalyzeMeshDispatch(0, 0, 3, currentEntryOnly: true);
        string blockedSource = machine.ReadPredicateFingerprint(blocked.Reads);
        Check(Ff7AutoPlayPolicy.IsInertWorldMovementEntry(blocked) && machine.AnalyzeMeshDispatch(0, 0, 3).MayEnterField, "Current inactivity erased its future native transfer branch.");
        persistent[100] = 0;
        WorldScriptAnalysis opened = machine.AnalyzeMeshDispatch(0, 0, 3, currentEntryOnly: true);
        Check(blockedSource != machine.ReadPredicateFingerprint(blocked.Reads) && opened.Complete && opened.MayEnterField && !Ff7AutoPlayPolicy.IsInertWorldMovementEntry(opened), "A changed native predicate reused the old inert eligibility proof.");
    }

    private sealed class RejectHost : IWorldHost
    {
        public WorldHostReply Execute(WorldHostRequest request) => throw new InvalidOperationException("Read-only eligibility dispatched a native effect.");
        public int ReadSpecial(int selector, int activeModel) => throw new InvalidOperationException("Read-only eligibility sampled the live native host.");
    }

    internal static void RunCurrentInertEntry(bool terrainMode = false)
    {
        string path = Environment.GetEnvironmentVariable(terrainMode ? "MEMORIA_FF7_WORLD_TERRAIN_SAVE" : "MEMORIA_FF7_WORLD_INERT_SAVE") ?? throw new InvalidOperationException("Select a current native inert-entry fixture.");
        string game = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR") ?? throw new InvalidOperationException("Select original resources.");
        byte[] bytes = File.ReadAllBytes(path);
        using MemoryStream source = new(bytes, false);
        using Ff7Session session = Ff7Session.Restore(Ff7Installation.Discover(game), CombinedSessionCodec.Read(source).Ff7!);
        Ff7WorldRuntime world = session.World ?? throw new InvalidDataException("No current native world allocation.");
        using Ff7AutoPlayPolicy policy = new(session, (_, _) => throw new InvalidOperationException("Unexpected choice boundary."));
        string initial = policy.State.Active?.EventId ?? throw new InvalidDataException("No current raw target.");
        Ff7AutoPlayWorldScriptObservation target = world.ReadAutoPlayFrontierCatalog(26, 18).Scripts.Single(item => item.Id == initial);
        var current = world.ReadAutoPlayMeshEntryForModel(target, world.ControlledModel);
        Check(terrainMode ? world.ReadAutoPlayCurrentModeTerrainEligibility(target) == false : Ff7AutoPlayPolicy.IsInertWorldMovementEntry(current), "The current fixture no longer supplies the proved current-mode exclusion.");
        var before = policy.State.Coverage.Visits.SelectMany(visit => visit.Events).Where(item => item.Event.Id == initial).Select(item => JsonSerializer.Serialize(new { item.Completed, item.Conditions, item.Routes, item.UnavailableAttempts })).ToArray();
        Ff7AutoPlayController controller = new(session, policy.ChooseInput, advanceTick: () => session.AdvanceTick(advanceClock: false), chooseDialogueSelection: policy.ChooseDialogueSelection, readPlanningProgress: () => policy.WorkProgress, readFrontierProgress: () => policy.FrontierProgress);
        Ff7AutoplayDriver driver = (Ff7AutoplayDriver)typeof(Ff7AutoPlayController).GetField("driver", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(controller)!;
        controller.Start();
        Stopwatch elapsed = Stopwatch.StartNew();
        TimeSpan previous = TimeSpan.Zero;
        for (int poll = 0; poll < 64 && controller.Running && session.World == world && session.Battle is null && elapsed.Elapsed < TimeSpan.FromSeconds(10); poll++)
        {
            TimeSpan now = elapsed.Elapsed;
            session.AdvanceClock(now - previous);
            previous = now;
            controller.Advance(1);
            Check(policy.State.Active is not { EventId: var selected, Accepted: false, Kind: Ff7AutoPlayTargetKind.WorldMesh } || selected != initial, "The policy resumed physical search toward a currently proved inert raw target.");
        }
        var after = policy.State.Coverage.Visits.SelectMany(visit => visit.Events).Where(item => item.Event.Id == initial).Select(item => JsonSerializer.Serialize(new { item.Completed, item.Conditions, item.Routes, item.UnavailableAttempts })).ToArray();
        Check(controller.Error is null && before.SequenceEqual(after) && bytes.AsSpan().SequenceEqual(File.ReadAllBytes(path)), $"Inert eligibility changed native history/source or failed: {controller.Error}");
        Console.WriteLine($"PASS current native mode-excluded entry: callbacks={controller.Steps}, worldTick={world.Process.Tick}, controlled={world.ControlledModel}, physical={driver.PhysicalPlanningWork}, active={policy.State.Active?.EventId}, pending={driver.PlanningPending}, elapsed={elapsed.Elapsed.TotalSeconds:F3}s; inert history/source unchanged");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
