using System.Numerics;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Memoria.AutoPlay;
using Memoria.FF7.Events;
using Memoria.FF7.Formats;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;

internal static class AutoPlayFieldDestinationGoalTests
{
    internal static void Run()
    {
        CheckLockedDestinationArrival();
        RunWorldBridge();
        CheckKnownComponentDeparture();
        Ff7AutoPlayTarget entrance = new("entrance", 1, 3, Ff7AutoPlayTargetKind.LineCross, "current", false, Vector3.Zero, DestinationField: 300, ChangesField: true);
        Ff7AutoPlayTarget optional = entrance with { Id = "optional", Entity = 2, DestinationField = 200 };
        Ff7AutoPlayObservation observation = new("source", "native", 1, [optional, entrance], true, true) { NavigationKey = "component" };
        Ff7AutoPlayState state = new();
        AutoPlayPlanner<Ff7AutoPlayDeferral> planner = Ff7AutoPlayPolicy.CreatePlanner(state);
        AutoPlayEvent Convert(Ff7AutoPlayTarget target) => new(target.Id, AutoPlayEventKind.Transition, ConditionSignature: target.ConditionFingerprint);
        void Observe(Ff7AutoPlayObservation current)
        {
            AutoPlayEvent[] events = current.Events.Select(Convert).ToArray();
            planner.Observe(new(current.VisitId, "native"), events);
            planner.ObserveNavigation(current.VisitId, current.NavigationKey, events);
        }
        Observe(observation with { Events = [optional, entrance with { ConditionFingerprint = "previous" }] });
        planner.Completed(observation.VisitId, entrance.Id, entrySignature: "previous", entrySignatureAuthoritative: true);
        Observe(observation);
        string coverage = JsonSerializer.Serialize(state.Coverage);
        Ff7AutoPlayPolicy.ResolveFieldDestinationGoal(state, observation);
        Check(state.NavigationGoal is null, "Exploration without a requested destination must acquire no destination goal.");
        Check(planner.PlanNavigation(observation.NavigationKey)?.Event.Id == optional.Id, "Without a destination, an untouched optional entrance must retain its fairness priority over a historical completion.");
        state.DestinationField = 300;
        Ff7AutoPlayPolicy.ResolveFieldDestinationGoal(state, observation);
        Check(state.NavigationGoal == new AutoPlayTravelGoal(observation.NavigationKey, entrance.Id, "current"), "A native field transition must resolve its current entry condition as the requested local goal.");
        AutoPlayPlanStep next = planner.PlanNavigation(observation.NavigationKey, preferredGoal: state.NavigationGoal)!;
        Check(next.Event.Id == entrance.Id && next.Goal is null, "An explicit destination must precede the optional entrance through existing planner priority, including a local plan with no returned goal.");
        Ff7AutoPlayPolicy.RetainDestinationGoal(state, next, observation.NavigationKey, entrance.ConditionFingerprint);
        Check(state.NavigationGoal?.EventId == entrance.Id, "Selecting the local step must retain the explicit destination goal.");
        Check(JsonSerializer.Serialize(state.Coverage) == coverage, "Resolving, planning and retaining a destination must preserve actual completion and condition consumption history.");
        Ff7AutoPlayState restored = state.Clone();
        Check(restored.NavigationGoal == state.NavigationGoal && restored.DestinationField == state.DestinationField, "Existing saved state must preserve destination intent without a new required field.");
        Check(Ff7AutoPlayPolicy.CreatePlanner(restored).PlanNavigation(observation.NavigationKey, preferredGoal: restored.NavigationGoal)?.Event.Id == entrance.Id, "Restored destination intent must retain the native selection.");
        AutoPlayPlanStep? blocked = planner.PlanNavigation(observation.NavigationKey, preferredGoal: state.NavigationGoal, readAvailability: (_, target) => new(target.Id == entrance.Id, [], "physical"));
        Check(blocked?.Event.Id == optional.Id, "Explicit destination priority must preserve physical admission and allow an available alternative.");
        Ff7AutoPlayPolicy.RetainDestinationGoal(state, blocked!, observation.NavigationKey, optional.ConditionFingerprint);
        Check(state.NavigationGoal is null, "A different selected event must not inherit the local destination event's goal.");
        Ff7AutoPlayPolicy.ResolveFieldDestinationGoal(state, observation with { Events = [entrance with { AnalysisComplete = false }] });
        Check(state.NavigationGoal is null, "Incomplete native analysis must not establish a destination goal.");
        Ff7AutoPlayPolicy.ResolveFieldDestinationGoal(state, observation with { Events = [entrance with { ChangesField = false }] });
        Check(state.NavigationGoal is null, "A nontransition must not establish a destination goal from a destination hint alone.");
        Ff7AutoPlayPolicy.ResolveFieldDestinationGoal(state, observation with { Events = [entrance with { Kind = Ff7AutoPlayTargetKind.Gateway, ChangesField = false }] });
        Check(state.NavigationGoal?.EventId == entrance.Id, "Native gateway discovery must resolve its direct destination without a script ChangesField flag.");
        state.NavigationGoal = null;
        Ff7AutoPlayPolicy.ResolveFieldDestinationGoal(state, observation with { Events = [entrance with { DestinationField = null, SpatialDestinations = [200, 300] }] });
        Check(state.NavigationGoal?.EventId == entrance.Id, "A completely analyzed spatial transition must resolve a proved native destination.");
        state.NavigationGoal = new("remote-component", "remote-entry", "remote-condition");
        Ff7AutoPlayPolicy.ResolveFieldDestinationGoal(state, observation with { Events = [optional] });
        Check(state.NavigationGoal?.NavigationKey == "remote-component", "A field without a matching local destination must preserve an existing remote route goal.");
        Ff7AutoPlayPolicy.RetainDestinationGoal(state, next with { Goal = new("remote-component", "remote-entry", "remote-condition") }, observation.NavigationKey, entrance.ConditionFingerprint);
        Check(state.NavigationGoal?.NavigationKey == "remote-component", "A planner-supplied remote goal must remain authoritative during route selection.");
        state.NavigationGoal = new(observation.NavigationKey, entrance.Id, "previous");
        Ff7AutoPlayPolicy.RetainDestinationGoal(state, next, observation.NavigationKey, entrance.ConditionFingerprint);
        Check(state.NavigationGoal is null, "A local destination goal for an obsolete entry condition must not survive selection.");
        state.DestinationField = null;
        state.NavigationGoal = new(observation.NavigationKey, entrance.Id, entrance.ConditionFingerprint);
        Ff7AutoPlayPolicy.RetainDestinationGoal(state, next, observation.NavigationKey, entrance.ConditionFingerprint);
        Check(state.NavigationGoal is null, "Ordinary exploration must retain its existing local-step goal clearing behavior.");
        CheckConsumedEntranceIntent(observation, entrance, optional);
        CheckConsumedNoOpIntent();
        CheckPossibleDestinationGoals(observation, entrance, optional);
        CheckRemoteDestinationAdvice();
        CheckCausalUnblockPrerequisite();
        CheckSampledDestinationAdvice();
        Console.WriteLine("PASS generic field destination resolution, priority, retention, restoration, native guards and unchanged consumption history");
    }

    private static void CheckCausalUnblockPrerequisite()
    {
        PcWalkmeshTriangle[] mesh =
        [
            new(0, new(0, 0, 0), new(100, 0, 0), new(0, 100, 0), 1, -1, -1),
            new(1, new(100, 0, 0), new(100, 100, 0), new(0, 100, 0), 0, -1, -1),
            new(2, new(300, 0, 0), new(400, 0, 0), new(300, 100, 0), -1, -1, -1)
        ];
        byte[] blocked = [2];
        Ff7Actor player = new() { Slot = 0, Entity = 0, Model = 0, Triangle = 0, CollisionRadius = 1 };
        PcFieldGateway gate = new(new(70, 70, 0), new(80, 70, 0), 0, 0, 0, 200, 0, 0);
        Check(Ff7AutoPlayPolicy.CanUnblockFieldGateway(mesh, blocked, player, gate, [1]), "A declared native unblock must prove that it connects the current source to the blocked gateway.");
        Check(!Ff7AutoPlayPolicy.CanUnblockFieldGateway(mesh, blocked, player, gate, [2]) && !Ff7AutoPlayPolicy.CanUnblockFieldGateway(mesh, blocked, player, gate, [-1]) && !Ff7AutoPlayPolicy.CanUnblockFieldGateway(mesh, blocked, player, gate, [0xFFFF]), "Unrelated, negative and out-of-range triangle effects must not authorize gateway prerequisite advice.");
        Check(!Ff7AutoPlayPolicy.CanUnblockFieldGateway(mesh, [0], player, gate, [1]) && blocked.SequenceEqual(new byte[] { 2 }), "An already reachable gateway requires no unblock prerequisite, and topology preview must preserve the live blocking mask.");
        Ff7AutoPlayTarget gateway = new("gateway", -1, -1, Ff7AutoPlayTargetKind.Gateway, "gateway-condition", false, Vector3.Zero, DestinationField: 200, GatewayIndex: 0);
        Ff7AutoPlayTarget producer = new("producer", 1, 1, Ff7AutoPlayTargetKind.ActorTalk, "producer-condition", false, Vector3.Zero, MayChangeNavigation: true) { UnblockedTriangles = [1] };
        Ff7AutoPlayTarget unrelated = producer with { Id = "unrelated", Entity = 2, UnblockedTriangles = [2] };
        Ff7AutoPlayTarget backtrack = gateway with { Id = "backtrack", DestinationField = 400, GatewayIndex = 1 };
        Ff7AutoPlayObservation observation = new("ff7:100:native", "native", 1, [backtrack, unrelated, producer, gateway], true, true) { NavigationKey = "blocked-component" };
        Ff7AutoPlayState state = new() { DestinationField = 300 };
        AutoPlayPlanner<Ff7AutoPlayDeferral> planner = Ff7AutoPlayPolicy.CreatePlanner(state);
        AutoPlayEvent[] events = observation.Events.Select(target => new AutoPlayEvent(target.Id, target.Kind == Ff7AutoPlayTargetKind.Gateway ? AutoPlayEventKind.Transition : AutoPlayEventKind.Interaction, ConditionSignature: target.ConditionFingerprint)).ToArray();
        planner.Observe(new(observation.VisitId, "native"), events);
        planner.ObserveNavigation(observation.VisitId, observation.NavigationKey, events);
        string coverage = JsonSerializer.Serialize(state.Coverage);
        int[] Read(ushort field) => field switch
        {
            200 => [300],
            400 => [450],
            450 => [300],
            _ => []
        };
        bool Available(Ff7AutoPlayTarget target) => target.Id != gateway.Id;
        bool Unblocks(Ff7AutoPlayTarget target, Ff7AutoPlayTarget selected) => selected.Id == gateway.Id && Ff7AutoPlayPolicy.CanUnblockFieldGateway(mesh, blocked, player, gate, target.UnblockedTriangles);
        Check(Ff7AutoPlayPolicy.ResolveFieldDestinationGoal(state, observation, Read, Available, Unblocks) is null && state.NavigationGoal == new AutoPlayTravelGoal(observation.NavigationKey, producer.Id, producer.ConditionFingerprint), "A current admitted causal prerequisite must precede a longer coarse backtracking route while retaining the final field destination.");
        AutoPlayPlanStep? next = planner.PlanNavigation(observation.NavigationKey, preferredGoal: state.NavigationGoal, readAvailability: (_, target) => new(target.Id == gateway.Id, [], "blocked-triangle"));
        Check(next?.Event.Id == producer.Id && state.DestinationField == 300 && Ff7AutoPlayPolicy.IsDestinationPlan(state, next, observation.NavigationKey), "The existing shared planner must execute the actual producer event rather than claiming that its possible unblock effect has already happened.");
        Check(JsonSerializer.Serialize(state.Coverage) == coverage && blocked[0] == 2, "Causal prerequisite advice must preserve all native blocks, consumed entries, routes and navigation receipts.");
        Ff7AutoPlayState cold = state.Clone();
        Check(Ff7AutoPlayPolicy.ResolveFieldDestinationGoal(cold, observation, Read, Available, Unblocks) is null && cold.NavigationGoal == state.NavigationGoal, "Cold restoration must resolve the same causal intent using current native topology without a required new save marker.");
        planner.Completed(observation.VisitId, producer.Id, observedStoryChanged: false, entrySignature: producer.ConditionFingerprint, entrySignatureAuthoritative: true);
        Check(Ff7AutoPlayPolicy.ResolveFieldDestinationGoal(state, observation, Read, Available, Unblocks) is null && state.NavigationGoal?.EventId != producer.Id && blocked[0] == 2, "An unchanged consumed producer must not be replayed or treated as an executed unblock merely because its analysis has a possible effect.");
        Ff7AutoPlayState fresh = new() { DestinationField = 300 };
        Check(Ff7AutoPlayPolicy.ResolveFieldDestinationGoal(fresh, observation with { Events = [gateway, producer with { AnalysisComplete = false }] }, Read, Available, Unblocks) is not null && fresh.NavigationGoal is null, "An incomplete producer analysis must not establish a causal navigation prerequisite.");
    }

    private static void CheckSampledDestinationAdvice()
    {
        Ff7AutoPlayTarget broad = new("near", 7, -1, Ff7AutoPlayTargetKind.AutonomousSpatial, "sample", false, Vector3.Zero, ChangesField: true) { SpatialDestinations = [200, 300], PlanningDestinationFields = [200] };
        Ff7AutoPlayTarget onward = broad with { Id = "onward", Triangle = 9, PlanningDestinationFields = [300] };
        Ff7AutoPlayObservation observation = new("ff7:100:native", "native", 1, [broad, onward], true, true) { NavigationKey = "sample-component" };
        Ff7AutoPlayState state = new() { DestinationField = 300 };
        Check(Ff7AutoPlayPolicy.ResolveFieldDestinationGoal(state, observation) is null && state.NavigationGoal?.EventId == onward.Id, "A broad supported child branch must not override its proved sample-specific direct destination.");
        state.DestinationField = 400;
        int[] Read(ushort field) => field == 200 ? [100] : field == 300 ? [400] : [];
        Check(Ff7AutoPlayPolicy.ResolveFieldDestinationGoal(state, observation, Read) is null && state.NavigationGoal?.EventId == onward.Id, "Remote field advice must begin with the sampled branch's destination rather than an unreachable arm of the child support union.");
        Check(Ff7AutoPlayPolicy.ResolveFieldDestinationGoal(state, observation with { Events = [broad with { PlanningDestinationFields = [] }] }, Read) is not null && state.NavigationGoal is null, "A proved empty sample outcome must not fall back to broad child support destinations.");
        state.DestinationField = 300;
        Check(Ff7AutoPlayPolicy.ResolveFieldDestinationGoal(state, observation with { Events = [broad with { PlanningDestinationFields = null }] }) is null && state.NavigationGoal?.EventId == broad.Id, "Absent optional sample advice must retain conservative old-save discovery support.");
        Check(broad.SpatialDestinations!.SequenceEqual([200, 300]) && broad.DestinationField is null, "Planning advice changed native support or completion ownership metadata.");
    }

    private static void CheckConsumedNoOpIntent()
    {
        foreach (bool selfReturn in new[] { false, true })
        {
            Ff7AutoPlayTarget target = new("guarded-exit", 2, 2, Ff7AutoPlayTargetKind.LinePush, "current", false, Vector3.Zero, DestinationField: 300, ChangesField: true);
            Ff7AutoPlayObservation observation = new("ff7:100:native", "native", 1, [target], true, true) { NavigationKey = "source-component" };
            Ff7AutoPlayState state = new() { DestinationField = 300 };
            AutoPlayPlanner<Ff7AutoPlayDeferral> planner = Ff7AutoPlayPolicy.CreatePlanner(state);
            AutoPlayEvent native = new(target.Id, AutoPlayEventKind.Transition, ConditionSignature: "previous");
            planner.Observe(new(observation.VisitId, "native"), [native]);
            planner.ObserveNavigation(observation.VisitId, observation.NavigationKey, [native]);
            planner.Observe(new("ff7:300:native", "native"), []);
            planner.Completed(observation.VisitId, target.Id, "ff7:300:native", observedStoryChanged: false, entrySignature: "previous", entrySignatureAuthoritative: true);
            native = native with { ConditionSignature = target.ConditionFingerprint };
            planner.Observe(new(observation.VisitId, "native"), [native]);
            planner.ObserveNavigation(observation.VisitId, observation.NavigationKey, [native]);
            planner.Completed(observation.VisitId, target.Id, selfReturn ? observation.VisitId : null, observedStoryChanged: false, entrySignature: target.ConditionFingerprint, entrySignatureAuthoritative: true);
            state.NavigationGoal = new(observation.NavigationKey, target.Id, target.ConditionFingerprint);
            string before = JsonSerializer.Serialize(state.Coverage);
            Check(Ff7AutoPlayPolicy.ResolveFieldDestinationGoal(state, observation, _ => []) is not null && state.NavigationGoal is null, "An unchanged consumed no-op must not become explicit travel merely because a previous condition transferred or its analysis declares a possible destination.");
            Check(JsonSerializer.Serialize(state.Coverage) == before, "Rejecting consumed no-op intent must preserve all completion, condition, route and component evidence.");
            Ff7AutoPlayTarget changed = target with { ConditionFingerprint = "changed" };
            native = native with { ConditionSignature = changed.ConditionFingerprint };
            planner.Observe(new(observation.VisitId, "native"), [native]);
            planner.ObserveNavigation(observation.VisitId, observation.NavigationKey, [native]);
            Check(Ff7AutoPlayPolicy.ResolveFieldDestinationGoal(state, observation with { Events = [changed] }, _ => []) is null && state.NavigationGoal?.Conditions == changed.ConditionFingerprint, "A genuinely unconsumed changed native guard must remain eligible without requiring an earlier successful transfer.");
        }
    }

    private static void CheckLockedDestinationArrival()
    {
        Ff7Session session = (Ff7Session)RuntimeHelpers.GetUninitializedObject(typeof(Ff7Session));
        Ff7FieldHost host = (Ff7FieldHost)RuntimeHelpers.GetUninitializedObject(typeof(Ff7FieldHost));
        typeof(Ff7FieldHost).GetProperty(nameof(Ff7FieldHost.Process))!.SetValue(host, new Ff7FieldProcess { PlayerLocked = true, MenuDisabled = true });
        FieldMachine machine = new(new FieldProgram([0, 0], [new ushort[32]]));
        machine.Initialize();
        typeof(Ff7Session).GetProperty(nameof(Ff7Session.Host))!.SetValue(session, host);
        typeof(Ff7Session).GetProperty(nameof(Ff7Session.Machine))!.SetValue(session, machine);
        typeof(Ff7Session).GetProperty(nameof(Ff7Session.MapId))!.SetValue(session, (ushort)100);
        session.AutoPlayState = new() { DestinationField = 300 };
        using Ff7AutoPlayPolicy policy = new(session, (_, _) => throw new InvalidOperationException("Arrival metadata must not create checkpoints."));
        Ff7AutoPlayActiveEvent active = new("source", "native-owner", "condition", 7, 0, 3, Ff7AutoPlayTargetKind.LineCross, 300, 1, "before", Accepted: true);
        Ff7AutoPlayNativeRoute proof = new(active.EventId, active.ConditionFingerprint, active.Generation, [new(new('A', 64), new(1, 0))], new('B', 64));
        AutoPlayTravelGoal goal = new("source-component", active.EventId, active.ConditionFingerprint);
        policy.State.Active = active;
        policy.State.NativeRoute = proof;
        policy.State.NavigationGoal = goal;
        AutoPlayPlanner<Ff7AutoPlayDeferral> planner = Ff7AutoPlayPolicy.CreatePlanner(policy.State);
        planner.Observe(new("source", "native"), [new(active.EventId, AutoPlayEventKind.Transition, ConditionSignature: active.ConditionFingerprint)]);
        string native = JsonSerializer.Serialize(new { Process = host.Process, Vm = machine.Capture() });
        string history = JsonSerializer.Serialize(policy.State.Coverage);
        void Tick()
        {
            Action<Ff7NativeTickObservation> observed = (Action<Ff7NativeTickObservation>)typeof(Ff7Session).GetField("NativeTickCompleted", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(session)!;
            observed(new("destination", 99, 1, [], null, "native"));
        }
        Tick();
        Check(policy.State.DestinationField == 300, "A predicted destination or unrelated native tick completed a field request before actual arrival.");
        typeof(Ff7Session).GetProperty(nameof(Ff7Session.MapId))!.SetValue(session, (ushort)300);
        Tick();
        Check(host.Process.PlayerLocked && policy.State.DestinationField is null && session.AutoPlayState?.DestinationField is null, "A locked script-only arrival must publish completion before owner-generation or input-readiness early returns.");
        Check(ReferenceEquals(policy.State.Active, active) && ReferenceEquals(policy.State.NativeRoute, proof) && ReferenceEquals(policy.State.NavigationGoal, goal), "Destination arrival replaced an accepted native owner, proof or navigation goal.");
        Check(JsonSerializer.Serialize(policy.State.Coverage) == history && JsonSerializer.Serialize(new { Process = host.Process, Vm = machine.Capture() }) == native, "Destination metadata completion changed native progress, PCs, locks or event consumption history.");
        typeof(Ff7Session).GetProperty(nameof(Ff7Session.MapId))!.SetValue(session, (ushort)400);
        Tick();
        Check(policy.State.DestinationField is null, "A later automatic departure revived the completed field request.");
        policy.State.DestinationField = 400;
        policy.State.PendingRestore = new("pending-choice");
        MethodInfo choose = typeof(Ff7AutoPlayPolicy).GetMethods(BindingFlags.Instance | BindingFlags.NonPublic).Single(method => method.Name == "ChooseInput" && method.GetParameters().Length == 3);
        choose.Invoke(policy, [new Ff7AutoplayDriver(session), new Ff7NativePlanningQuantum(), false]);
        Check(policy.State.DestinationField is null && session.AutoPlayState?.DestinationField is null && policy.State.PendingRestore is not null && ReferenceEquals(policy.State.Active, active) && ReferenceEquals(policy.State.NativeRoute, proof), "A queued continuation prevented arrival completion or lost native ownership before its early return.");
    }

    private static void CheckKnownComponentDeparture()
    {
        Ff7AutoPlayTarget gateway = new("exit", -1, -1, Ff7AutoPlayTargetKind.Gateway, "gate", false, Vector3.Zero, DestinationField: 200);
        Ff7AutoPlayObservation observation = new("ff7:100:native", "native", 1, [gateway], true, true) { NavigationKey = "upper" };
        AutoPlayEvent transport = new("ladder", AutoPlayEventKind.Transition, ConditionSignature: "ladder-entry");
        AutoPlayEvent departure = new(gateway.Id, AutoPlayEventKind.Transition, ConditionSignature: gateway.ConditionFingerprint);
        Ff7AutoPlayState state = new() { DestinationField = 300 };
        AutoPlayPlanner<Ff7AutoPlayDeferral> planner = Ff7AutoPlayPolicy.CreatePlanner(state);
        planner.Observe(new(observation.VisitId, "native"), [transport, departure]);
        planner.ObserveNavigation(observation.VisitId, "upper", [transport, departure]);
        planner.ObserveNavigation(observation.VisitId, "lower", [transport, departure]);
        planner.Observe(new("ff7:200:native", "native"), []);
        planner.ObserveNavigation("ff7:200:native", "outside", []);
        state.Coverage.NavigationRoutes.Add(new("upper", "lower", transport.Id, new(observation.VisitId, "ladder-entry", false, null)));
        state.Coverage.NavigationRoutes.Add(new("lower", "outside", departure.Id, new("ff7:200:native", "gate", false, null)));
        string? ReadSource(Ff7AutoPlayTarget target) => Ff7AutoPlayPolicy.ReadKnownFieldDepartureSource(state, observation, target);
        Check(ReadSource(gateway) == "lower", "A known departure behind a recorded same-field ladder was lost because the current component has no contact.");
        Ff7AutoPlayPolicy.ResolveFieldDestinationGoal(state, observation, field => field == 200 ? [300] : [], target => ReadSource(target) is not null, readDepartureSource: ReadSource);
        Check(state.NavigationGoal == new AutoPlayTravelGoal("lower", gateway.Id, "gate") && state.DestinationField == 300, "Field advice bound the departure to the current floor instead of its recorded source or discarded the explicit destination.");
        state.Coverage.NavigationRoutes.RemoveAt(0);
        Check(ReadSource(gateway) is null, "An unreachable recorded departure invented a same-field prerequisite route.");
        state.Coverage.NavigationRoutes.Insert(0, new("upper", "lower", transport.Id, new(observation.VisitId, "changed-entry", false, null)));
        Check(ReadSource(gateway) is null, "A mismatched current first-action condition authorized a historical departure source.");
    }

    private static void CheckRemoteDestinationAdvice()
    {
        Ff7AutoPlayTarget exit = new("exit", 2, 2, Ff7AutoPlayTargetKind.LinePush, "exit-condition", false, Vector3.Zero, DestinationField: 200, ChangesField: true);
        Ff7AutoPlayTarget optional = exit with { Id = "optional", Entity = 3, DestinationField = 400 };
        Ff7AutoPlayObservation observation = new("ff7:100:native", "native", 1, [optional, exit], true, true) { NavigationKey = "inn-component" };
        Ff7AutoPlayState state = new() { DestinationField = 300 };
        AutoPlayPlanner<Ff7AutoPlayDeferral> planner = Ff7AutoPlayPolicy.CreatePlanner(state);
        AutoPlayEvent native = new(exit.Id, AutoPlayEventKind.Transition, ConditionSignature: exit.ConditionFingerprint);
        AutoPlayEvent other = new(optional.Id, AutoPlayEventKind.Transition, ConditionSignature: optional.ConditionFingerprint);
        planner.Observe(new(observation.VisitId, "native"), [other, native]);
        planner.ObserveNavigation(observation.VisitId, observation.NavigationKey, [other, native]);
        planner.Observe(new("ff7:200:native", "native"), []);
        planner.ObserveNavigation("ff7:200:native", "outside-component", []);
        planner.Completed(observation.VisitId, native.Id, "ff7:200:native", observedStoryChanged: false, entrySignature: native.ConditionSignature, entrySignatureAuthoritative: true);
        AutoPlayEventCoverage<Ff7AutoPlayDeferral> coverage = state.Coverage.Visits.Single(visit => visit.Visit.Key == observation.VisitId).Events.Single(item => item.Event.Id == native.Id);
        planner.RecordNavigationRoute(observation.NavigationKey, "outside-component", native.Id, coverage.Routes.Single());
        string consumption = JsonSerializer.Serialize(coverage.Conditions);
        string routes = JsonSerializer.Serialize(state.Coverage.NavigationRoutes);
        string receipts = JsonSerializer.Serialize(state.Coverage.NavigationVisits);
        Dictionary<ushort, int[]> graph = new() { [200] = [250], [250] = [300], [400] = [401], [401] = [] };
        int[] Read(ushort field) => graph.TryGetValue(field, out int[]? destinations) ? destinations : [];
        Check(Ff7AutoPlayPolicy.ResolveFieldDestinationGoal(state, observation, Read) is null, "Native multi-hop advice must connect the currently observed exit to a remote requested field.");
        AutoPlayPlanStep next = planner.PlanNavigation(observation.NavigationKey, preferredGoal: state.NavigationGoal)!;
        Check(next.Event.Id == exit.Id && Ff7AutoPlayPolicy.IsDestinationPlan(state, next, observation.NavigationKey), "A consumed inn exit toward the remote destination must precede an unrelated optional room.");
        Check(coverage.Completed && JsonSerializer.Serialize(coverage.Conditions) == consumption && JsonSerializer.Serialize(state.Coverage.NavigationRoutes) == routes && JsonSerializer.Serialize(state.Coverage.NavigationVisits) == receipts, "Native route advice must preserve completion, consumption, recorded routes and component receipts.");
        AutoPlayPlanStep? blocked = planner.PlanNavigation(observation.NavigationKey, preferredGoal: state.NavigationGoal, readAvailability: (_, target) => new(target.Id == exit.Id, [], "current-native-refusal"));
        Check(!Ff7AutoPlayPolicy.IsDestinationPlan(state, blocked, observation.NavigationKey), "A currently refused required exit must not silently become optional exploration.");
        Ff7AutoPlayState restored = state.Clone();
        Ff7AutoPlayTarget onward = exit with { Id = "onward", DestinationField = 250, ConditionFingerprint = "outside-condition" };
        Ff7AutoPlayObservation outside = observation with { VisitId = "ff7:200:native", NavigationKey = "outside-component", Generation = 2, Events = [onward] };
        Check(Ff7AutoPlayPolicy.ResolveFieldDestinationGoal(restored, outside, Read) is null && restored.DestinationField == 300 && restored.NavigationGoal == new AutoPlayTravelGoal(outside.NavigationKey, onward.Id, onward.ConditionFingerprint), "Cold field handoff must preserve the final destination and resolve the next native entrance without replaying previous travel.");
        Check(Ff7AutoPlayPolicy.ResolveFieldDestinationGoal(state, observation, _ => [])?.Contains("No currently admitted native field connectivity route", StringComparison.Ordinal) == true && state.NavigationGoal is null && !Ff7AutoPlayPolicy.IsDestinationPlan(state, next, observation.NavigationKey), "A disconnected requested destination must report its route boundary and suppress unrelated frontiers.");
        Ff7AutoPlayTarget alternate = exit with { Id = "alternate", Entity = 4, DestinationField = 210, ConditionFingerprint = "alternate-condition" };
        AutoPlayEvent alternateEvent = native with { Id = alternate.Id, ConditionSignature = alternate.ConditionFingerprint };
        planner.Observe(new(observation.VisitId, "native"), [other, native, alternateEvent]);
        planner.ObserveNavigation(observation.VisitId, observation.NavigationKey, [other, native, alternateEvent]);
        graph[210] = [211];
        graph[211] = [212];
        graph[212] = [300];
        graph[400] = [100];
        graph[100] = [200];
        Check(Ff7AutoPlayPolicy.ResolveFieldDestinationGoal(state, observation, Read, target => target.Id != exit.Id) is not null && state.NavigationGoal is null, "An optional out-and-back trip must not reenter the current field's unfiltered resource graph and bypass its blocked required exit.");
        Ff7AutoPlayObservation alternatives = observation with { Events = [optional, exit, alternate] };
        Check(Ff7AutoPlayPolicy.ResolveFieldDestinationGoal(state, alternatives, Read, target => target.Id != exit.Id) is null && state.NavigationGoal?.EventId == alternate.Id, "An unavailable shortest corridor must leave a longer currently admitted corridor to the requested destination eligible.");
        AutoPlayPlanStep? longer = planner.PlanNavigation(observation.NavigationKey, preferredGoal: state.NavigationGoal, readAvailability: (_, target) => new(target.Id == exit.Id, [], "short-corridor-blocked"));
        Check(longer?.Event.Id == alternate.Id && Ff7AutoPlayPolicy.IsDestinationPlan(state, longer, observation.NavigationKey), "The shared planner must admit the longer goal-reaching exit while excluding an unrelated optional room.");
        state.DestinationField = 1000;
        string? limited = Ff7AutoPlayPolicy.ResolveFieldDestinationGoal(state, observation with { Events = [exit] }, field => [(int)field + 1]);
        Check(limited?.Contains("bounded search limit", StringComparison.Ordinal) == true && state.NavigationGoal is null, "An unfinished bounded native connectivity search must report its limit instead of exploring unrelated fields.");
        state.DestinationField = null;
        Check(Ff7AutoPlayPolicy.IsDestinationPlan(state, blocked, observation.NavigationKey), "Ordinary exploration must retain its existing admission and frontier policy when no destination is requested.");
    }

    private static void CheckPossibleDestinationGoals(Ff7AutoPlayObservation observation, Ff7AutoPlayTarget entrance, Ff7AutoPlayTarget optional)
    {
        Ff7AutoPlayTarget multiple = entrance with { DestinationField = null, PossibleDestinationFields = [200, 300] };
        Check(multiple.SpatialDestinations is null && optional.PossibleDestinationFields.Length == 0, "Ordinary possible destinations must default empty and must not populate spatial ownership/completion metadata.");
        Ff7AutoPlayState state = new();
        AutoPlayPlanner<Ff7AutoPlayDeferral> planner = Ff7AutoPlayPolicy.CreatePlanner(state);
        AutoPlayEvent[] events = [new(optional.Id, AutoPlayEventKind.Transition, Order: 0, ConditionSignature: optional.ConditionFingerprint), new(multiple.Id, AutoPlayEventKind.Transition, Order: 1, ConditionSignature: multiple.ConditionFingerprint)];
        planner.Observe(new(observation.VisitId, "native"), events);
        planner.ObserveNavigation(observation.VisitId, observation.NavigationKey, events);
        Ff7AutoPlayObservation current = observation with { Events = [optional, multiple] };
        Ff7AutoPlayPolicy.ResolveFieldDestinationGoal(state, current);
        Check(state.NavigationGoal is null && planner.PlanNavigation(current.NavigationKey)?.Event.Id == optional.Id, "Multi-destination analysis must leave ordinary exploration order unchanged without a requested destination.");
        state.DestinationField = 300;
        string before = JsonSerializer.Serialize(state.Coverage);
        Ff7AutoPlayPolicy.ResolveFieldDestinationGoal(state, current);
        AutoPlayPlanStep next = planner.PlanNavigation(current.NavigationKey, preferredGoal: state.NavigationGoal)!;
        Check(next.Event.Id == multiple.Id && state.NavigationGoal?.Conditions == multiple.ConditionFingerprint, "A completely analyzed ordinary multi-destination entrance must become the requested exact-condition goal ahead of optional exploration.");
        Ff7AutoPlayPolicy.RetainDestinationGoal(state, next, current.NavigationKey, multiple.ConditionFingerprint);
        Check(state.NavigationGoal?.EventId == multiple.Id && JsonSerializer.Serialize(state.Coverage) == before, "Multi-destination goal retention must preserve existing native coverage and consumption.");
        Check(planner.PlanNavigation(current.NavigationKey, preferredGoal: state.NavigationGoal, readAvailability: (_, target) => new(target.Id == multiple.Id, [], "blocked"))?.Event.Id == optional.Id, "Possible destinations must not override native physical admission.");
        state.NavigationGoal = null;
        Ff7AutoPlayPolicy.ResolveFieldDestinationGoal(state, current with { Events = [multiple with { AnalysisComplete = false }] });
        Check(state.NavigationGoal is null, "Incomplete multi-destination analysis must establish no destination goal.");
        Ff7AutoPlayPolicy.ResolveFieldDestinationGoal(state, current with { Events = [multiple with { ChangesField = false }] });
        Check(state.NavigationGoal is null, "Goal-only possible destinations must not grant a nontransition field-change capability.");
        state.DestinationField = 400;
        Ff7AutoPlayPolicy.ResolveFieldDestinationGoal(state, current);
        Check(state.NavigationGoal is null, "A requested field absent from all analyzed destinations must establish no local goal.");
        state.DestinationField = 200;
        Ff7AutoPlayPolicy.ResolveFieldDestinationGoal(state, current with { Events = [multiple, optional] });
        Check(state.NavigationGoal?.EventId == optional.Id && optional.DestinationField == 200, "An exact single destination must retain its shortcut and precedence over an analyzed alternative.");
    }

    private static void CheckConsumedEntranceIntent(Ff7AutoPlayObservation observation, Ff7AutoPlayTarget entrance, Ff7AutoPlayTarget optional)
    {
        Ff7AutoPlayState state = new() { DestinationField = 300 };
        AutoPlayPlanner<Ff7AutoPlayDeferral> planner = Ff7AutoPlayPolicy.CreatePlanner(state);
        AutoPlayEvent native = new(entrance.Id, AutoPlayEventKind.Transition, ConditionSignature: entrance.ConditionFingerprint);
        AutoPlayEvent other = new(optional.Id, AutoPlayEventKind.Transition, ConditionSignature: optional.ConditionFingerprint);
        planner.Observe(new(observation.VisitId, "native"), [native, other]);
        planner.ObserveNavigation(observation.VisitId, observation.NavigationKey, [native, other]);
        planner.Observe(new("ff7:300:native", "native"), []);
        planner.ObserveNavigation("ff7:300:native", "destination-component", []);
        planner.Completed(observation.VisitId, native.Id, "ff7:300:native", observedStoryChanged: false, entrySignature: native.ConditionSignature, entrySignatureAuthoritative: true);
        AutoPlayEventCoverage<Ff7AutoPlayDeferral> coverage = state.Coverage.Visits.Single(visit => visit.Visit.Key == observation.VisitId).Events.Single(item => item.Event.Id == native.Id);
        planner.RecordNavigationRoute(observation.NavigationKey, "destination-component", native.Id, coverage.Routes.Single());
        string consumed = JsonSerializer.Serialize(coverage.Conditions);
        string routes = JsonSerializer.Serialize(state.Coverage.NavigationRoutes);
        string receipts = JsonSerializer.Serialize(state.Coverage.NavigationVisits);
        Ff7AutoPlayPolicy.ResolveFieldDestinationGoal(state, observation);
        Check(planner.PlanNavigation(observation.NavigationKey, preferredGoal: state.NavigationGoal)?.Event.Id == entrance.Id, "An explicit field destination must allow repeat travel through a consumed native entrance with confirmed connectivity.");
        Check(coverage.Completed && JsonSerializer.Serialize(coverage.Conditions) == consumed && JsonSerializer.Serialize(state.Coverage.NavigationRoutes) == routes && JsonSerializer.Serialize(state.Coverage.NavigationVisits) == receipts, "Rearming explicit route intent must preserve completion, consumed conditions, native route history and component confirmation receipts.");
        Check(planner.PlanNavigation(observation.NavigationKey, preferredGoal: state.NavigationGoal, readAvailability: (_, target) => new(target.Id == entrance.Id, [], "blocked"))?.Event.Id == optional.Id, "A consumed requested entrance must still honor physical unavailability.");
        Ff7AutoPlayObservation transit = observation with { Events = [entrance with { DestinationField = 250 }] };
        Check(Ff7AutoPlayPolicy.ResolveFieldDestinationGoal(state, transit, field => field == 250 ? [300] : []) is null && state.NavigationGoal?.EventId == entrance.Id, "A same-condition actual field transfer must remain authentic when native initialization passes through a declared intermediate field before reaching its recorded destination.");
    }

    private static void RunWorldBridge()
    {
        Ff7AutoPlayTarget exit = new("world-exit", -1, -1, Ff7AutoPlayTargetKind.Gateway, "native-exit", false, Vector3.Zero, DestinationField: 15, ChangesField: true);
        Ff7AutoPlayTarget incompatible = exit with { Id = "other-world", DestinationField = 30 };
        Ff7AutoPlayObservation observation = new("ff7:100:native", "native", 1, [incompatible, exit], true, true) { NavigationKey = "component" };
        Ff7AutoPlayState state = new() { DestinationField = 300 };
        int[] World(int selector) => Ff7WorldRuntime.ResolveWorldId(selector) == 0 ? [300] : [];
        string? Resolve(Func<Ff7AutoPlayTarget, bool>? admit = null) => Ff7AutoPlayPolicy.ResolveFieldDestinationGoal(state, observation, _ => [], admit, readWorldConnections: World);
        Check(Resolve() is null && state.NavigationGoal?.EventId == exit.Id && state.DestinationField == 300, "A native compatible world entrance must bridge the retained field goal without selecting an incompatible world allocation.");
        Check(Resolve(target => target.Id != exit.Id) is not null && state.NavigationGoal is null, "World advice must not bypass current physical admission of its required field exit.");
        AutoPlayPlanner<Ff7AutoPlayDeferral> planner = Ff7AutoPlayPolicy.CreatePlanner(state);
        planner.Observe(new(observation.VisitId, "native"), [new(exit.Id, AutoPlayEventKind.Transition, ConditionSignature: exit.ConditionFingerprint)]);
        planner.Completed(observation.VisitId, exit.Id, "ff7-world:0:world-program", observedStoryChanged: false, entrySignature: exit.ConditionFingerprint, entrySignatureAuthoritative: true);
        string consumed = JsonSerializer.Serialize(state.Coverage.Visits.Single().Events.Single().Conditions);
        Check(Resolve() is null && state.NavigationGoal?.EventId == exit.Id && JsonSerializer.Serialize(state.Coverage.Visits.Single().Events.Single().Conditions) == consumed, "An actual same-entry native world transfer must authenticate repeat travel without erasing consumption.");
        Ff7AutoPlayState cold = state.Clone();
        Check(Ff7AutoPlayPolicy.ResolveFieldDestinationGoal(cold, observation, _ => [], readWorldConnections: World) is null && cold.DestinationField == 300 && cold.NavigationGoal == state.NavigationGoal, "Cold restoration lost the final field destination or its authenticated world hop.");
        AutoPlayEventCoverage<Ff7AutoPlayDeferral> coverage = state.Coverage.Visits.Single().Events.Single();
        AutoPlayEventRoute receipt = coverage.Routes.Single();
        coverage.Routes[0] = receipt with { DestinationVisitKey = "ff7-world:2:other-program" };
        Check(Resolve() is not null, "A receipt for a different native world allocation authenticated an unchanged consumed exit.");
        coverage.Routes[0] = receipt with { ConditionSignature = "other-native-entry" };
        Check(Resolve() is not null, "A different entry condition authenticated repeat world travel.");
        state.Coverage.Visits.Single().Events.Single().Routes.Clear();
        Check(Resolve() is not null && state.NavigationGoal is null, "A consumed native no-op must not be replayed merely because its analysis mentions a world entrance.");
        Ff7AutoPlayState fresh = new() { DestinationField = 300 };
        Check(Ff7AutoPlayPolicy.ResolveFieldDestinationGoal(fresh, observation with { Events = [exit with { AnalysisComplete = false }] }, _ => [], readWorldConnections: World) is not null, "Incomplete field analysis must not authenticate a native world exit.");
        Ff7AutoPlayTarget corridor = exit with { Id = "corridor", DestinationField = 200 };
        Check(Ff7AutoPlayPolicy.ResolveFieldDestinationGoal(fresh, observation with { Events = [corridor] }, field => field == 200 ? [15] : [], readWorldConnections: World) is null && fresh.NavigationGoal?.EventId == corridor.Id, "Bounded ordinary field advice must retain a future native world hop rather than stop at its encoded selector.");
        Check(Ff7AutoPlayPolicy.ResolveFieldDestinationGoal(fresh, observation, _ => [], readWorldConnections: _ => []) is not null && fresh.NavigationGoal is null, "An absent native world entrance must remain an explicit planning boundary.");
    }

    internal static void RunOriginalSelection()
    {
        string root = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR") ?? throw new InvalidOperationException("Select original field resources.");
        string path = Environment.GetEnvironmentVariable("MEMORIA_FF7_FIELD_DESTINATION_SAVE") ?? throw new InvalidOperationException("Select an external field destination fixture.");
        ushort destination = ushort.Parse(Environment.GetEnvironmentVariable("MEMORIA_FF7_FIELD_DESTINATION") ?? throw new InvalidOperationException("Select the fixture's destination field."));
        byte[] bytes = File.ReadAllBytes(path);
        using MemoryStream input = new(bytes, writable: false);
        using Ff7Session session = Ff7Session.Restore(Ff7Installation.Discover(root), CombinedSessionCodec.Read(input).Ff7!);
        using Ff7AutoPlayPolicy policy = new(session, (_, _) => throw new InvalidOperationException("Unexpected choice during native destination selection."));
        policy.SetDestinationField(destination);
        // Isolate selection from a supplied attempt's already exhausted observational deadline.
        policy.State.FrontierProgressKey = null;
        policy.State.FrontierIdleSteps = 0;
        Ff7AutoPlayController controller = new(session, policy.ChooseInput, chooseDialogueSelection: policy.ChooseDialogueSelection);
        controller.Start();
        for (int step = 0; step < 3 && controller.Running && policy.State.Active is null; step++)
        {
            controller.Advance(1);
        }
        Ff7AutoPlayActiveEvent active = policy.State.Active ?? throw new InvalidOperationException($"No native destination was selected: {controller.Error ?? policy.Status}");
        Ff7AutoPlayTarget selected = new Ff7AutoPlayDiscovery(session).FindTarget(active.EventId) ?? throw new InvalidOperationException("The selected native entrance was absent from discovery.");
        bool worldBridge = selected.DestinationField is int selector && selector is >= 1 and <= 64 && session.ReadAutoPlayWorldConnections(selector).Contains(destination);
        Check(controller.Error is null && selected.AnalysisComplete && (selected.DestinationField == destination || selected.PossibleDestinationFields.Contains(destination) || selected.SpatialDestinations?.Contains(destination) == true || worldBridge), "The native planner must select an entrance whose complete native analysis includes the requested field or a compatible native world handoff.");
        Check(policy.State.DestinationField == destination, "A native world bridge replaced the final requested field destination with its intermediate selector.");
        Check(active.SpatialDestinations is null || selected.SpatialDestinations is not null, "Ordinary goal metadata must not create spatial transition ownership or completion evidence.");
        if (selected.Entity >= 0 && selected.Script >= 0)
        {
            int[] analyzed = session.Machine.AnalyzeScript(selected.Entity, selected.Script).DestinationFields;
            Check(selected.PossibleDestinationFields.SequenceEqual(analyzed) && selected.DestinationField == (analyzed.Length == 1 ? analyzed[0] : (int?)null), "Ordinary discovery must preserve every analyzed destination while retaining the exact single-destination shortcut.");
        }
        Check(policy.State.NavigationGoal is { } goal && goal.EventId == active.EventId && goal.Conditions == active.ConditionFingerprint, "Native local selection must preserve the exact destination goal and entry condition.");
        Check(bytes.SequenceEqual(File.ReadAllBytes(path)), "Native destination selection must leave the standalone fixture unchanged.");
        Console.WriteLine($"PASS original field destination selection and retained native goal: {active.EventId}; possible destinations=[{string.Join(',', selected.PossibleDestinationFields)}]; source unchanged");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
