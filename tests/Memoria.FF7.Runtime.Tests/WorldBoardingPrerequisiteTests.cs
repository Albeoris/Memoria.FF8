using System.Text.Json;
using Memoria.AutoPlay;
using Memoria.FF7.Runtime;
using Memoria.FF7.Resources;
using Memoria.FF7.Events;
using System.Reflection;

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
        Check(session.World is not null && world.ControlledModel != controlled, "Ordinary selected native boarding did not actually change the controlled model.");
        Check(policy.State.NavigationGoal == intended, "Actual native boarding lost or replaced its downstream entry goal.");
        elapsed.Restart();
        int postBoardSlices = 0;
        while (controller.Running && session.World is not null && policy.State.Active?.WorldModeGoal is null && session.Battle is null && elapsed.Elapsed < TimeSpan.FromSeconds(30) && postBoardSlices < 128)
        {
            controller.Advance(1);
            postBoardSlices++;
        }
        Check(policy.State.Active?.WorldModeGoal?.TargetEventId == intended.EventId, "The first native vehicle-to-rider goal displaced the boarding plan's proved downstream entry.");
        Check(original.SequenceEqual(File.ReadAllBytes(path)), "Native boarding continuation changed its standalone source.");
        Console.WriteLine($"PASS native boarding: scheduling polls={polls}, actual callbacks={controller.Steps}, controlled={controlled}->{world.ControlledModel}, downstream={intended.EventId}, first native mode={policy.State.Active?.WorldModeGoal?.TargetEventId}; source unchanged.");
    }

    private static void Check(bool value, string message)
    {
        if (!value)
        {
            throw new Exception(message);
        }
    }
}
