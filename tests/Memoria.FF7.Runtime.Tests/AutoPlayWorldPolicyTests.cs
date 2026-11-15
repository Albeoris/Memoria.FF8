using System.Numerics;
using System.Text.Json;
using Memoria.AutoPlay;
using Memoria.FF7.Events;
using Memoria.FF7.Formats;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;

internal static class AutoPlayWorldPolicyTests
{
    public static void Run()
    {
        HistoricalPureTransfersRequireSourceReobservation();
        IncompleteDiscoveryRemainsARefreshFrontier();
        NativeHandoffsRequireTheirOwners();
        ControlledOwnerProjectionIsGuardedAndReadOnly();
        RestoredRemoteWorldGoalRehydratesItsCatalog();
    }

    private static void RestoredRemoteWorldGoalRehydratesItsCatalog()
    {
        string? root = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR");
        string? path = Environment.GetEnvironmentVariable("MEMORIA_FF7_WORLD_TRANSPORT_SAVE");
        if (string.IsNullOrWhiteSpace(root) || string.IsNullOrWhiteSpace(path))
        {
            Console.WriteLine("SKIP restored remote world goal: configure original resources and a world goal fixture.");
            return;
        }
        byte[] original = File.ReadAllBytes(path);
        using MemoryStream input = new(original, writable: false);
        Ff7SessionSave source = CombinedSessionCodec.Read(input).Ff7 ?? throw new InvalidDataException("The supplied fixture contains no FFVII state.");
        if (source.World is null || source.AutoPlay?.Active?.Kind != Ff7AutoPlayTargetKind.WorldMesh)
        {
            Console.WriteLine("SKIP restored remote world goal: the selected world fixture has no active mesh goal.");
            return;
        }
        using Ff7Session session = Ff7Session.Restore(Ff7Installation.Discover(root), source);
        AutoPlayTravelGoal? restoredGoal = source.AutoPlay.NavigationGoal;
        AutoPlayNavigationVisit? goalVisit = source.AutoPlay.Coverage.NavigationVisits.SingleOrDefault(visit => visit.Key == restoredGoal?.NavigationKey);
        bool absentGoal = goalVisit is not null && source.AutoPlay.Coverage.Visits.Single(visit => visit.Visit.Key == goalVisit.VisitKey).Events.SingleOrDefault(item => item.Event.Id == restoredGoal?.EventId) is { Available: false };
        byte[] before = JsonSerializer.SerializeToUtf8Bytes(session.Capture() with { AutoPlay = null });
        using Ff7AutoPlayPolicy policy = new(session, (_, _) => throw new InvalidOperationException("A read-only world planning query attempted to write a choice checkpoint."));
        _ = policy.ChooseInput(new(session));
        if (!source.AutoPlay.Active.Accepted && absentGoal)
        {
            Check(policy.State.NavigationGoal != restoredGoal, "A restored unaccepted world route continued steering toward a natively absent full-field frontier.");
        }
        Check(before.SequenceEqual(JsonSerializer.SerializeToUtf8Bytes(session.Capture() with { AutoPlay = null })), "Rehydrating a remote world goal changed native state or RNG.");
        Check(original.SequenceEqual(File.ReadAllBytes(path)), "Rehydrating a remote world goal changed the supplied source.");
    }

    private static void HistoricalPureTransfersRequireSourceReobservation()
    {
        WorldProgram program = new(new([new(0x8000, 1)], new ushort[] { 0, 0x110, 1, 0x110, 0, 0x318, 0x203 }));
        WorldMachine native = new(program, new byte[1280], new RejectHost());
        string beforeNative = JsonSerializer.Serialize(native.Capture());
        WorldScriptAnalysis analysis = native.AnalyzeMeshDispatch(0, 0, 3);
        Check(analysis.Complete && analysis.IsPureFieldTransfer, "The authored native packet-only callback must provide proved pure-transfer analysis.");
        Ff7AutoPlayWorldScriptObservation source = new("mesh:0:0:script:3", 3, [], analysis, "canonical", "geometry", [new(5, 0x100, new(0, 0, 0, 91, 0))], true, null);
        string condition = Ff7AutoPlayPolicy.WorldCondition(source);
        Ff7AutoPlayState state = new();
        AutoPlayPlanner<Ff7AutoPlayDeferral> planner = new(state.Coverage);
        AutoPlayEvent entrance = new(source.Id, AutoPlayEventKind.Transition, ConditionSignature: condition);
        planner.Observe(new("world", "world-resource"), [entrance]);
        planner.ObserveNavigation("world", "world-nav", [entrance]);
        planner.Observe(new("field", "field-resource"), [new("forward", AutoPlayEventKind.Transition, ConditionSignature: "ready")]);
        planner.ObserveNavigation("field", "field-nav", [new("forward", AutoPlayEventKind.Transition, ConditionSignature: "ready")]);
        planner.Completed("world", source.Id, "field", observedStoryChanged: true, entrySignature: condition, entrySignatureAuthoritative: true);
        AutoPlayVisitCoverage<Ff7AutoPlayDeferral> visit = state.Coverage.Visits.Single(item => item.Visit.Key == "world");
        AutoPlayEventCoverage<Ff7AutoPlayDeferral> coverage = visit.Events.Single();
        AutoPlayEventRoute old = coverage.Routes.Single();
        planner.RecordNavigationRoute("world-nav", "field-nav", source.Id, old);
        Check(planner.PlanNavigation("world-nav") is null, "Historical story-changing metadata must initially exclude this travel edge.");
        string[] consumed = coverage.Conditions!.ConsumedEntries.ToArray();
        Ff7AutoPlayPolicy.ReobserveUnattributedWorldTransfers(visit, [source]);
        Check(coverage.RouteNeedsRediscovery && coverage.Completed && coverage.Conditions.ConsumedEntries.SetEquals(consumed) && coverage.Routes.SequenceEqual(new[] { old }), "Scheduling source reobservation must retain completion, consumed entries and every historical route observation.");
        Check(planner.PlanNavigation("world-nav") is { IsTravel: false } probe && probe.Event.Id == source.Id, "A pure historical transfer must become a real native source probe instead of a fabricated reusable route.");
        planner.Completed("world", source.Id, "field", observedStoryChanged: false, entrySignature: condition, entrySignatureAuthoritative: true);
        planner.RecordNavigationRoute("world-nav", "field-nav", source.Id, coverage.Routes.Single());
        Ff7AutoPlayPolicy.ReobserveUnattributedWorldTransfers(visit, [source]);
        Check(!coverage.RouteNeedsRediscovery && planner.PlanNavigation("world-nav") is { IsTravel: true, Goal.EventId: "forward" }, "Fresh source-only unchanged-story evidence must resolve the probe and restore ordinary shared travel.");
        coverage.Routes.Add(old);
        Ff7AutoPlayPolicy.ReobserveUnattributedWorldTransfers(visit, [source]);
        Check(!coverage.RouteNeedsRediscovery, "A fresh current-condition false route must prevent historical true observations from reopening rediscovery.");
        coverage.Routes = [old];
        Ff7AutoPlayPolicy.ReobserveUnattributedWorldTransfers(visit, [source with { Analysis = analysis with { IsPureFieldTransfer = false } }]);
        Check(!coverage.RouteNeedsRediscovery, "Impure source callbacks must not be reclassified as ordinary world transfers.");
        Ff7AutoPlayPolicy.ReobserveUnattributedWorldTransfers(visit, [source with { Complete = false, Analysis = analysis with { Complete = false, Reason = "Unresolved native host." } }]);
        Check(!coverage.RouteNeedsRediscovery, "Incomplete native analysis must not schedule a purity-based source repair.");
        coverage.Routes = [old with { ConditionSignature = "different-entry" }];
        Ff7AutoPlayPolicy.ReobserveUnattributedWorldTransfers(visit, [source]);
        Check(!coverage.RouteNeedsRediscovery, "A different executed condition must not authorize current-source rediscovery.");
        coverage.Routes = [old];
        Ff7AutoPlayPolicy.ReobserveUnattributedWorldTransfers(visit, [source], new HashSet<string>(StringComparer.Ordinal) { source.Id + ":" + condition });
        Check(!coverage.RouteNeedsRediscovery && coverage.Routes.Single() == old, "An attributed native source story change, including unload side effects, must remain true without perpetual rediscovery.");
        Check(beforeNative == JsonSerializer.Serialize(native.Capture()), "Historical route reobservation metadata must not execute or alter the native world VM.");
    }

    private static void IncompleteDiscoveryRemainsARefreshFrontier()
    {
        Ff7AutoPlayState state = new();
        AutoPlayPlanner<Ff7AutoPlayDeferral> planner = new(state.Coverage);
        AutoPlayEvent entry = new("mesh:0:0:script:3", AutoPlayEventKind.Transition, ConditionSignature: "entry");
        planner.Observe(new("world", "resource"), [entry]);
        planner.ObserveNavigation("world", "world-nav", [entry]);
        planner.Completed("world", entry.Id, entrySignature: "entry", entrySignatureAuthoritative: true);
        FieldVariables variables = new();
        Ff7Progress progress = new() { InitialKernelData = [1], Characters = [], Party = [255, 255, 255], Inventory = [], Materia = [] };
        state.WorldVisits["world"] = new()
        {
            ProgramIdentity = "resource",
            GlobalFingerprint = WorldMachine.ReadGlobalPredicateFingerprint("resource", variables.CopyPersistent(), []),
            ObservedChunks = [new(0, 0)]
        };
        Ff7AutoPlayWorldVisitState visit = state.WorldVisits["world"];
        AutoPlayVisit navigation = new("world-nav", "resource");
        AutoPlayEvent gateway = new("exit", AutoPlayEventKind.Transition, ConditionSignature: "exit-entry");
        planner.Observe(new("field", "field-resource"), [gateway]);
        planner.ObserveNavigation("field", "field-nav", [gateway]);
        planner.Completed("field", gateway.Id, "world", observedStoryChanged: false);
        planner.RecordNavigationRoute("field-nav", "world-nav", gateway.Id, state.Coverage.Visits.Single(item => item.Visit.Key == "field").Events.Single().Routes.Single());
        bool NeedsRefresh(AutoPlayVisit candidate) => Ff7AutoPlayPolicy.NeedsVisitRefresh(state, candidate, variables, progress, new());
        Check(planner.PlanNavigation("field-nav", NeedsRefresh) is { IsTravel: true, Event.Id: "exit", RefreshVisitKey: "world-nav" }, "Partial world discovery must drive the existing shared field-to-world refresh route.");
        visit.DiscoveryComplete = true;
        Check(planner.PlanNavigation("field-nav", NeedsRefresh) is null, "Complete world discovery with unchanged guards must not repeat the consumed source gateway.");
        visit.DiscoveryComplete = false;
        Check(Ff7AutoPlayPolicy.NeedsVisitRefresh(state, navigation, variables, progress, new()), "A legacy or partial native world catalog must remain a shared refresh frontier even with unchanged global guards.");
        AutoPlayPlanStep travel = new("world", entry, true, new("field", "entry", false, []));
        Check(Ff7AutoPlayPolicy.ShouldDiscoverWorldChunk(state, "world", travel, hasQueuedChunks: true), "Pending lazy discovery must precede another completed local entry used only for remote travel.");
        Check(!Ff7AutoPlayPolicy.ShouldDiscoverWorldChunk(state, "world", travel, hasQueuedChunks: false), "An exhausted discovery queue must not invent another catalog step.");
        planner.Observe(new("world", "resource"), [entry, new("unseen", AutoPlayEventKind.Transition, ConditionSignature: "ready")]);
        Check(!Ff7AutoPlayPolicy.ShouldDiscoverWorldChunk(state, "world", travel, hasQueuedChunks: true), "Lazy metadata discovery must not hide an actual unconsumed local native frontier.");
        visit.DiscoveryComplete = true;
        Check(!Ff7AutoPlayPolicy.NeedsVisitRefresh(state, navigation, variables, progress, new()), "A complete native catalog with unchanged canonical guards must not force repeated world visits.");
        Check(Ff7AutoPlayPolicy.ObserveWorldDiscoveryContext(visit, "native-topology-one") && !visit.DiscoveryComplete, "A new native allocation/topology context must invalidate only optional discovery completeness.");
        visit.DiscoveryComplete = true;
        Check(!Ff7AutoPlayPolicy.ObserveWorldDiscoveryContext(visit, "native-topology-one") && visit.DiscoveryComplete, "An unchanged native topology context must retain completed discovery.");
        Check(Ff7AutoPlayPolicy.ObserveWorldDiscoveryContext(visit, "native-topology-two") && !visit.DiscoveryComplete && visit.ObservedChunks.Count == 1 && state.Coverage.Visits.Single(item => item.Visit.Key == "world").Events.Single(item => item.Event.Id == entry.Id).Completed, "Topology reobservation must preserve native event progress and recorded source facts.");
        Check(!state.Clone().WorldVisits["world"].DiscoveryComplete, "Cold restoration lost the pending optional discovery frontier.");
    }

    private static void NativeHandoffsRequireTheirOwners()
    {
        WorldEntitySnapshot idle = new(-1, 0, 0, 0, false, 0, []);
        WorldSnapshot vm = new(1, "authored-world", 0, 1, false, new byte[64], [], idle, []);
        Ff7WorldProcess world = new(0, 0, 7, vm, [], 0, true, 0, 0, null, [], -1, new(), 0, 1);
        Ff7AutoPlayActiveEvent gateway = new("field", "gateway:0", "source", 4, -1, -1, Ff7AutoPlayTargetKind.Gateway, 7, 1, "before");
        Check(Ff7AutoPlayPolicy.IsAcceptedWorldArrival(gateway, world, new(7, 0, 0, 0, 0)), "An actual source-gateway world handoff must complete without an interaction-script Accepted marker or generation increment.");
        Check(!Ff7AutoPlayPolicy.IsAcceptedWorldArrival(gateway with { DestinationField = 8 }, world, new(7, 0, 0, 0, 0)), "A different native world entry selector must not complete a predicted gateway.");
        Ff7AutoPlayActiveEvent mesh = new("world", "mesh:0:0:script:3", "entry", 4, -1, 3, Ff7AutoPlayTargetKind.WorldMesh, null, 1, "before", Accepted: true);
        Ff7AutoPlayWorldDispatchReceipt pending = new("world", mesh.EventId, 100, 1, 12, [200]);
        Check(!Ff7AutoPlayPolicy.HasWorldControlReturned(pending, idle with { ProgramCounter = 120, Priority = 3, Calls = [new(80, 0, 0)] }), "An admitted callback at its original call depth has not returned control.");
        Check(Ff7AutoPlayPolicy.HasWorldControlReturned(pending, idle with { ProgramCounter = 80, Priority = 3 }), "Native call-depth return must be recognized even when the interrupted callback also had priority three.");
        Check(!Ff7AutoPlayPolicy.IsAcceptedWorldDeparture(mesh, pending, 5, 91), "A predicted world boundary must not establish a native field route.");
        Ff7AutoPlayWorldDispatchReceipt departed = pending with { Departure = new(1, -1, 200, 0x100, WorldOperation.FieldTransition), DestinationField = 91 };
        Check(Ff7AutoPlayPolicy.IsAcceptedWorldDeparture(mesh, departed, 5, 91), "An accepted native packet and actual destination allocation must establish the field handoff.");
        Check(!Ff7AutoPlayPolicy.IsAcceptedWorldDeparture(mesh with { Accepted = false }, departed, 5, 91) && !Ff7AutoPlayPolicy.IsAcceptedWorldDeparture(mesh, departed, 4, 91) && !Ff7AutoPlayPolicy.IsAcceptedWorldDeparture(mesh, departed, 5, 92), "Unaccepted sources, unchanged generations and different fields must not complete native world routes.");
        Ff7AutoPlayState observed = new() { Active = mesh, WorldDispatch = departed };
        observed.WorldVisits["world"] = new() { ProgramIdentity = vm.ProgramIdentity, ObservedChunks = [new(0, 0)] };
        Ff7AutoPlayState cold = observed.Clone();
        Check(cold.Active == mesh && cold.WorldDispatch?.Departure == departed.Departure && cold.WorldVisits["world"].ObservedChunks.Single() == new Ff7AutoPlayWorldChunk(0, 0), "Cold observation restoration lost native source ownership or optional lazy discovery facts.");
    }

    private static void ControlledOwnerProjectionIsGuardedAndReadOnly()
    {
        byte[] persistent = new byte[1280];
        WorldProgram program = new(new([new(0x8000, 1)], new ushort[] { 0, 0x100, 0x110, 1, 0x11B, 8, 0x318, 0x203 }));
        RejectHost host = new();
        WorldMachine machine = new(program, persistent, host);
        string before = JsonSerializer.Serialize(machine.Capture());
        WorldScriptAnalysis Analyze(int owner) => machine.AnalyzeMeshDispatch(0, 0, 3, readHost: dependency => Ff7WorldRuntime.ProjectAutoPlayHostDependency(dependency, owner));
        WorldScriptAnalysis first = Analyze(0);
        WorldScriptAnalysis changed = Analyze(1);
        Check(first.Complete && first.HostValues.Single().Value == 0 && first.Transitions.Single().Selector == 0x100, "Native special selector eight must resolve from the saved controlled owner without sampling a live host.");
        Check(changed.Complete && changed.HostValues.Single().Value == 1 && changed.Transitions.Single().Selector == 0x101, "A changed controlled owner must alter native field selector operands and its retained host guard.");
        PcWorldMesh mesh = new(0, 0, [Vector3.Zero, new(100, 0, 0), new(0, 0, 100)], [], [new(0, 1, 2, 3 << 5, Vector2.Zero, Vector2.Zero, Vector2.Zero, 0)]);
        PcWorldFieldTable fields = new([new(0, 0, 0, 91, 0), new(0, 0, 0, 92, 0)]);
        Ff7AutoPlayWorldScriptObservation Catalog(WorldScriptAnalysis analysis) => Ff7WorldRuntime.BuildAutoPlayFrontierCatalog(0, program.Identity, 0, 0, mesh, _ => analysis, machine.ReadPredicateFingerprint, fields.Select).Scripts.Single();
        Check(Catalog(first).PredicateFingerprint != Catalog(changed).PredicateFingerprint, "Resolved host values must invalidate catalog conditions even when the world VM snapshot and bank reads are unchanged.");
        Check(Ff7WorldRuntime.ProjectAutoPlayHostDependency(new(WorldHostDependencyKind.Special, -1, 8), -1) == 0 && Ff7WorldRuntime.ProjectAutoPlayHostDependency(new(WorldHostDependencyKind.Special, -1, 16), 0) is null, "The snapshot projection must retain native missing-owner normalization and leave stochastic selectors incomplete.");
        WorldVariableRead[] globals = [new(0x114, 64), new(0x11C, 10)];
        Check(machine.ReadPredicateFingerprint(globals) == WorldMachine.ReadGlobalPredicateFingerprint(program.Identity, persistent, globals), "Cross-scene global observation must reuse the native typed scalar reader and identical guard signature.");
        Check(before == JsonSerializer.Serialize(machine.Capture()) && persistent.All(value => value == 0) && host.Calls == 0, "Resolved world discovery changed native PC, storage, host or RNG state.");
    }

    private sealed class RejectHost : IWorldHost
    {
        public int Calls { get; private set; }
        public WorldHostReply Execute(WorldHostRequest request)
        {
            Calls++;
            throw new InvalidOperationException("World policy observation must not dispatch a native host.");
        }
        public int ReadSpecial(int selector, int activeModel)
        {
            Calls++;
            throw new InvalidOperationException("World policy observation must not sample a native host.");
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
