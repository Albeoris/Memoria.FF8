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
        Console.WriteLine("PASS generic field destination resolution, priority, retention, restoration, native guards and unchanged consumption history");
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
        Check(controller.Error is null && active.DestinationField == destination, "The native planner must select the requested field entrance.");
        Check(policy.State.NavigationGoal is { } goal && goal.EventId == active.EventId && goal.Conditions == active.ConditionFingerprint, "Native local selection must preserve the exact destination goal and entry condition.");
        Check(bytes.SequenceEqual(File.ReadAllBytes(path)), "Native destination selection must leave the standalone fixture unchanged.");
        Console.WriteLine($"PASS original field destination selection and retained native goal: {active.EventId}; source unchanged");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
