using System.Reflection;
using System.Text.Json;
using Memoria.AutoPlay;
using Memoria.FF7.Runtime;
using Memoria.FF7.Resources;
using Memoria.FF7.Events;

internal static class WorldBoardingPrerequisiteTests
{
    internal static void Run()
    {
        const string visit = "ff7-world:0:authored";
        const string navigation = visit + ":nav:world";
        AutoPlayEvent destination = new("mesh:1:2:script:7", AutoPlayEventKind.Transition, ConditionSignature: "destination-condition");
        AutoPlayEvent other = new("mesh:3:4:script:7", AutoPlayEventKind.Transition, ConditionSignature: "other-condition");
        AutoPlayEvent boarding = new("model:9:event:4", AutoPlayEventKind.Interaction, ConditionSignature: "boarding-condition");
        Ff7AutoPlayState state = new();
        var planner = Ff7AutoPlayPolicy.CreatePlanner(state);
        planner.Observe(new(visit, "authored"), [destination, other, boarding]);
        planner.ObserveNavigation(visit, navigation, [destination, other, boarding]);
        planner.Completed(visit, boarding.Id, entrySignature: boarding.ConditionSignature, entrySignatureAuthoritative: true);
        AutoPlayEventAvailability Unavailable(AutoPlayEvent item) => new(item.Id != boarding.Id, [], item.ConditionSignature);

        Check(Ff7AutoPlayPolicy.ShouldPlanWorldBoardingPrerequisite(state, visit, Unavailable), "A completed boarding action hid a prerequisite for unconsumed entries beyond the current foot component.");
        Check(!Ff7AutoPlayPolicy.ShouldPlanWorldBoardingPrerequisite(state, visit, _ => new(false, [], null)), "Transport advice displaced an admitted fresh on-foot frontier.");
        var visitCoverage = state.Coverage.Visits.Single();
        var destinationCoverage = visitCoverage.Events.Single(item => item.Event.Id == destination.Id);
        var otherCoverage = visitCoverage.Events.Single(item => item.Event.Id == other.Id);
        destinationCoverage.UnavailableAttempts.Add(new(destination.ConditionSignature!, visit, "historical-advice", "old-source"));
        otherCoverage.UnavailableAttempts.Add(new(other.ConditionSignature!, visit, "historical-advice", "old-source", other.Id));
        Check(Ff7AutoPlayPolicy.ShouldPlanWorldBoardingPrerequisite(state, visit, _ => new(false, [], null)), "A retryable previously attempted condition monopolized ordinary exploration ahead of a proved transport prerequisite.");
        Check(!destinationCoverage.Unavailable && !otherCoverage.Unavailable, "Historical attempts were incorrectly promoted to native unreachability.");
        destinationCoverage.Event = destination with { ConditionSignature = "changed-native-condition" };
        Check(!Ff7AutoPlayPolicy.ShouldPlanWorldBoardingPrerequisite(state, visit, _ => new(false, [], null)), "A changed native condition did not restore unattempted frontier priority.");
        destinationCoverage.Event = destination;
        AutoPlayEvent variants = destination with { Variants = ["native-arm"] };
        Check(!Ff7AutoPlayPolicy.HasAttemptedEntryCondition(destinationCoverage, variants), "An unrelated attempted arm suppressed a fresh native variant.");
        destinationCoverage.UnavailableAttempts.Add(new(destination.ConditionSignature!, visit, "historical-advice", "old-source", "native-arm"));
        Check(Ff7AutoPlayPolicy.HasAttemptedEntryCondition(destinationCoverage, variants), "Current variant attempt classification diverged from ordinary planner retry classification.");
        state.DestinationField = 321;
        Check(!Ff7AutoPlayPolicy.ShouldPlanWorldBoardingPrerequisite(state, visit, Unavailable), "Ordinary exploration interfered with explicit destination planning.");
        state.DestinationField = null;
        string before = JsonSerializer.Serialize(state);
        int calls = 0;
        using (Ff7NativePlanningCursor search = new(Ff7AutoPlayPolicy.EnumerateWorldBoardingPrerequisites(visit, navigation, [other, destination], [boarding], (contact, target) =>
        {
            calls++;
            return target.Id == destination.Id;
        }).GetEnumerator()))
        {
            search.Resume(new(1, TimeSpan.FromSeconds(1)));
            Check(!search.Completed && search.Result is null && calls == 1, "The first unsuccessful pair terminated exploration instead of yielding retained work.");
            search.Resume(new(1, TimeSpan.FromSeconds(1)));
            AutoPlayPlanStep selected = search.Result ?? throw new Exception("The later connected destination was lost across the cooperative yield.");
            Check(search.Completed && calls == 2 && selected.Event == boarding && !selected.IsTravel && selected.Route is null, "Boarding was not selected as the current native model action.");
            Check(selected.Goal == new AutoPlayTravelGoal(navigation, destination.Id, destination.ConditionSignature), "The transport prerequisite lost its downstream native entry goal.");
        }
        Check(JsonSerializer.Serialize(state) == before, "Read-only transport advice changed coverage, completion, guards or destination intent.");

        calls = 0;
        Ff7NativePlanningCursor cancelled = new(Ff7AutoPlayPolicy.EnumerateWorldBoardingPrerequisites(visit, navigation, [other, destination], [boarding], (_, _) =>
        {
            calls++;
            return false;
        }).GetEnumerator());
        cancelled.Resume(new(1, TimeSpan.FromSeconds(1)));
        cancelled.Dispose();
        cancelled.Resume(new(8, TimeSpan.FromSeconds(1)));
        Check(calls == 1 && cancelled.Result is null, "Cancelled transport advice continued inspecting the old native source.");
        Check(JsonSerializer.Serialize(state) == before, "Cancellation published a negative route or fabricated completion.");

        using Ff7NativePlanningCursor disconnected = new(Ff7AutoPlayPolicy.EnumerateWorldBoardingPrerequisites(visit, navigation, [destination], [boarding], (_, _) => false).GetEnumerator());
        disconnected.Resume(new(8, TimeSpan.FromSeconds(1)));
        Check(disconnected.Completed && disconnected.Result is null && JsonSerializer.Serialize(state) == before, "Nonpositive advice fabricated a terminal native refusal or progress receipt.");

        Ff7AutoPlayPolicy.TrackFrontierProgress(state, 641);
        state.FrontierIdleSteps = Ff7AutoPlayPolicy.FrontierIdleLimit - 1;
        string coverageBefore = JsonSerializer.Serialize(state.Coverage);
        AutoPlayEvent[] distant = Enumerable.Range(0, Ff7AutoPlayPolicy.FrontierIdleLimit + 2).Select(index => destination with { Id = "entry:" + index }).ToArray();
        using Ff7NativePlanningCursor delayed = new(Ff7AutoPlayPolicy.EnumerateWorldBoardingPrerequisites(visit, navigation, distant, [boarding], (_, target) => target == distant[^1]).GetEnumerator());
        for (int poll = 0; poll < distant.Length - 1; poll++)
        {
            delayed.Resume(new(1, TimeSpan.FromSeconds(1)));
            Check(!delayed.Completed, "The authored later transport connection was selected before its actual advice pair.");
            Ff7AutoPlayPolicy.CommitFrontierInput(state, 641, controllable: true, planningPending: true, pendingRestore: false);
        }
        Check(state.FrontierIdleSteps == Ff7AutoPlayPolicy.FrontierIdleLimit - 1 && JsonSerializer.Serialize(state.Coverage) == coverageBefore, "Transport scheduling polls consumed the native input supervisor or altered progress.");
        delayed.Resume(new(1, TimeSpan.FromSeconds(1)));
        Check(delayed.Result is not null, "The later connected transport candidate was lost after prolonged cooperative planning.");
        try
        {
            Ff7AutoPlayPolicy.CommitFrontierInput(state, 641, controllable: true, planningPending: false, pendingRestore: false);
            throw new Exception("The real next input attempt bypassed the existing native supervisor.");
        }
        catch (TimeoutException)
        {
        }
        Check(state.FrontierIdleSteps == Ff7AutoPlayPolicy.FrontierIdleLimit && JsonSerializer.Serialize(state.Coverage) == coverageBefore, "The real input limit changed or fabricated coverage while supervising transport work.");
    }

    internal static void RunOriginal()
    {
        string game = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR") ?? throw new InvalidOperationException("Select original resources.");
        string path = Environment.GetEnvironmentVariable("MEMORIA_FF7_WORLD_BOARDING_SAVE") ?? throw new InvalidOperationException("Select an external ready world fixture.");
        byte[] original = File.ReadAllBytes(path);
        using MemoryStream input = new(original);
        CombinedSessionSave envelope = CombinedSessionCodec.Read(input);
        using Ff7Session session = Ff7Session.Restore(Ff7Installation.Discover(game), envelope.Ff7!);
        Ff7WorldRuntime world = session.World ?? throw new InvalidDataException("The fixture has no actual World allocation.");
        string nativeBefore = JsonSerializer.Serialize(session.Capture() with { AutoPlay = null, EventCoverage = null });
        using Ff7AutoPlayPolicy policy = new(session, (_, _) => throw new InvalidOperationException("Unexpected choice boundary."));
        Ff7AutoplayDriver driver = new(session);
        Ff7AutoplayInput selected = default;
        int polls = 0;
        MethodInfo choose = typeof(Ff7AutoPlayPolicy).GetMethod("ChooseInput", BindingFlags.Instance | BindingFlags.NonPublic, [typeof(Ff7AutoplayDriver), typeof(Ff7NativePlanningQuantum), typeof(bool).MakeByRefType()])!;
        var elapsed = System.Diagnostics.Stopwatch.StartNew();
        do
        {
            driver.BeginPlanningStep();
            selected = (Ff7AutoplayInput)choose.Invoke(policy, [driver, new Ff7NativePlanningQuantum(1, TimeSpan.FromSeconds(1)), false])!;
            polls++;
        }
        while (driver.PlanningPending && elapsed.Elapsed < TimeSpan.FromSeconds(30));
        Check(polls > 1, "The native prerequisite regression did not exercise retained caller work across scheduling quanta.");
        Check(nativeBefore == JsonSerializer.Serialize(session.Capture() with { AutoPlay = null, EventCoverage = null }) && original.SequenceEqual(File.ReadAllBytes(path)), "Normal policy selection changed native execution or its source fixture before input application.");
        Ff7AutoPlayActiveEvent active = policy.State.Active ?? throw new Exception("No actual native event was selected.");
        Ff7AutoPlayWorldModelScriptObservation? contact = world.ReadAutoPlayModelFrontiers().SingleOrDefault(item => item.Id == active.EventId);
        Check(!driver.PlanningPending && contact is { Complete: true } && contact.Analysis.Transitions.Any(transition => transition.Operation == WorldOperation.BoardTransport), "Ordinary world planning did not select an actual offered boarding prerequisite.");
        AutoPlayTravelGoal intended = policy.State.NavigationGoal ?? throw new Exception("Native boarding was selected without a downstream entry goal.");
        Check(intended.EventId is not null && intended.EventId != active.EventId, "Boarding's goal is not its downstream native entry.");
        int controlled = world.ControlledModel;
        Ff7AutoPlayController controller = new(session, policy.ChooseInput, chooseDialogueSelection: policy.ChooseDialogueSelection);
        controller.Start();
        int callbacks = 0;
        elapsed.Restart();
        while (controller.Running && session.World is not null && world.ControlledModel == controlled && session.Battle is null && elapsed.Elapsed < TimeSpan.FromSeconds(30) && callbacks < 512)
        {
            controller.Advance(1);
            callbacks++;
        }
        if (session.Battle is not null)
        {
            Check(original.SequenceEqual(File.ReadAllBytes(path)), "Native transport continuation changed its standalone source.");
            Console.WriteLine($"BOUNDARY genuine native battle before boarding completed after {controller.Steps} actual callbacks; no battle outcome was fabricated.");
            return;
        }
        Check(session.World is not null && world.ControlledModel != controlled, "Ordinary selected native boarding did not actually change the controlled model.");
        Check(policy.State.NavigationGoal == intended, "Actual native boarding lost or replaced its downstream entry goal.");
        elapsed.Restart();
        int postBoardSlices = 0;
        while (controller.Running && session.World is not null && policy.State.Active?.WorldModeGoal is null && session.Battle is null && elapsed.Elapsed < TimeSpan.FromSeconds(30) && postBoardSlices < 128)
        {
            controller.Advance(1);
            postBoardSlices++;
        }
        if (session.Battle is not null)
        {
            Check(original.SequenceEqual(File.ReadAllBytes(path)), "Native transport continuation changed its standalone source.");
            Console.WriteLine($"BOUNDARY genuine native battle after boarding after {controller.Steps} actual callbacks; no battle outcome was fabricated.");
            return;
        }
        Check(policy.State.Active?.WorldModeGoal?.TargetEventId == intended.EventId, "The first native vehicle-to-rider goal displaced the boarding plan's proved downstream entry.");
        ContinueOwnedWorldEntry(session, world, policy, controller, intended.EventId!, controlled, path, original);
    }

    internal static void RunLandingOriginal()
    {
        string game = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR") ?? throw new InvalidOperationException("Select original resources.");
        string path = Environment.GetEnvironmentVariable("MEMORIA_FF7_WORLD_LANDING_SAVE") ?? throw new InvalidOperationException("Select an external native landing fixture.");
        byte[] original = File.ReadAllBytes(path);
        using MemoryStream input = new(original);
        using Ff7Session session = Ff7Session.Restore(Ff7Installation.Discover(game), CombinedSessionCodec.Read(input).Ff7!);
        Ff7WorldRuntime world = session.World ?? throw new Exception("The fixture has no native World allocation.");
        using Ff7AutoPlayPolicy policy = new(session, (_, _) => throw new Exception("Unexpected choice."));
        string intended = policy.State.Active?.WorldModeGoal?.TargetEventId ?? throw new Exception("Native landing has no owned downstream goal.");
        int rider = world.ControlledModel;
        string entries = JsonSerializer.Serialize(policy.State.Coverage.Visits.Single(visit => visit.Visit.Key.StartsWith("ff7-world:", StringComparison.Ordinal)).Events.Single(item => item.Event.Id == intended).Conditions?.ConsumedEntries);
        Ff7AutoPlayController controller = new(session, policy.ChooseInput);
        controller.Start();
        controller.Advance(1);
        Ff7AutoPlayActiveEvent entry = policy.State.Active ?? throw new Exception("Native landing did not retain its owned entry plan.");
        Check(entry.EventId == intended && entry.Kind == Ff7AutoPlayTargetKind.WorldMesh && !entry.Accepted && !entry.Returned && !entry.TransitionOwned && !entry.CompletionRecorded, "Landing fabricated native entry admission or displaced its owned continuation.");
        var source = intended.Split(':');
        var target = world.ReadAutoPlayFrontierCatalog(int.Parse(source[1]), int.Parse(source[2])).Scripts.Single(item => item.Script == int.Parse(source[4]));
        Check(entry.ConditionFingerprint == Ff7AutoPlayPolicy.WorldCondition(target), "The landing entry plan retained an obsolete native condition.");
        Check(entries == JsonSerializer.Serialize(policy.State.Coverage.Visits.Single(visit => visit.Visit.Key.StartsWith("ff7-world:", StringComparison.Ordinal)).Events.Single(item => item.Event.Id == intended).Conditions?.ConsumedEntries), "Landing consumed an entry without a native dispatch.");
        ContinueOwnedWorldEntry(session, world, policy, controller, intended, rider, path, original);
    }

    private static void ContinueOwnedWorldEntry(Ff7Session session, Ff7WorldRuntime world, Ff7AutoPlayPolicy policy, Ff7AutoPlayController controller, string intended, int rider, string path, byte[] original)
    {
        var elapsed = System.Diagnostics.Stopwatch.StartNew();
        var report = System.Diagnostics.Stopwatch.StartNew();
        bool landed = world.ControlledModel == rider;
        int slices = 0;
        while (controller.Running && ReferenceEquals(session.World, world) && world.Process.FieldDeparture is null && session.Battle is null && elapsed.Elapsed < TimeSpan.FromMinutes(2) && slices < 4096)
        {
            controller.Advance(1);
            slices++;
            landed |= world.ControlledModel == rider;
            if (report.Elapsed >= TimeSpan.FromSeconds(10))
            {
                var player = world.Process.Entities.Single(entity => entity.Model == world.ControlledModel);
                Console.WriteLine($"Native world progress: callbacks={controller.Steps}, tick={world.Process.Tick}, model={world.ControlledModel}, XYZ={player.X},{player.Y},{player.Z}, active={policy.State.Active?.EventId}, intended={intended}");
                report.Restart();
            }
        }
        Check(original.SequenceEqual(File.ReadAllBytes(path)), "Native transport continuation changed its standalone source.");
        Console.WriteLine($"Native transport boundary: callbacks={controller.Steps}, landed={landed}, intended={intended}, owner={policy.State.WorldDispatch?.EventId}, departure={world.Process.FieldDeparture}, battle={session.Battle is not null}, error={controller.Error}; source unchanged.");
        Check(controller.Error is null, $"Native transport continuation failed: {controller.Error}");
        if (session.Battle is not null)
        {
            Console.WriteLine("BOUNDARY genuine native battle; no battle outcome or selected field entry was fabricated.");
            return;
        }
        Check(landed && world.Process.FieldDeparture is not null && policy.State.WorldDispatch?.EventId == intended, "Normal native landing and the owned world entry were not both established within the external fixture check.");
    }

    private static void Check(bool value, string message)
    {
        if (!value)
        {
            throw new Exception(message);
        }
    }
}
