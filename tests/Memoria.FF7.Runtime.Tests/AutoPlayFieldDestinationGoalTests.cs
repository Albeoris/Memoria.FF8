using System.Numerics;
using System.Text.Json;
using Memoria.AutoPlay;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;

internal static class AutoPlayFieldDestinationGoalTests
{
    internal static void Run()
    {
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
        CheckPossibleDestinationGoals(observation, entrance, optional);
        CheckRemoteDestinationAdvice();
        Console.WriteLine("PASS generic field destination resolution, priority, retention, restoration, native guards and unchanged consumption history");
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
        planner.Observe(new("destination", "native"), []);
        planner.ObserveNavigation("destination", "destination-component", []);
        planner.Completed(observation.VisitId, native.Id, "destination", observedStoryChanged: false, entrySignature: native.ConditionSignature, entrySignatureAuthoritative: true);
        AutoPlayEventCoverage<Ff7AutoPlayDeferral> coverage = state.Coverage.Visits.Single(visit => visit.Visit.Key == observation.VisitId).Events.Single(item => item.Event.Id == native.Id);
        planner.RecordNavigationRoute(observation.NavigationKey, "destination-component", native.Id, coverage.Routes.Single());
        string consumed = JsonSerializer.Serialize(coverage.Conditions);
        string routes = JsonSerializer.Serialize(state.Coverage.NavigationRoutes);
        string receipts = JsonSerializer.Serialize(state.Coverage.NavigationVisits);
        Ff7AutoPlayPolicy.ResolveFieldDestinationGoal(state, observation);
        Check(planner.PlanNavigation(observation.NavigationKey, preferredGoal: state.NavigationGoal)?.Event.Id == entrance.Id, "An explicit field destination must allow repeat travel through a consumed native entrance with confirmed connectivity.");
        Check(coverage.Completed && JsonSerializer.Serialize(coverage.Conditions) == consumed && JsonSerializer.Serialize(state.Coverage.NavigationRoutes) == routes && JsonSerializer.Serialize(state.Coverage.NavigationVisits) == receipts, "Rearming explicit route intent must preserve completion, consumed conditions, native route history and component confirmation receipts.");
        Check(planner.PlanNavigation(observation.NavigationKey, preferredGoal: state.NavigationGoal, readAvailability: (_, target) => new(target.Id == entrance.Id, [], "blocked"))?.Event.Id == optional.Id, "A consumed requested entrance must still honor physical unavailability.");
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
        Check(controller.Error is null && selected.AnalysisComplete && (selected.DestinationField == destination || selected.PossibleDestinationFields.Contains(destination) || selected.SpatialDestinations?.Contains(destination) == true), "The native planner must select an entrance whose complete native analysis includes the requested field.");
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
