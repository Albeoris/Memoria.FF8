using System.Diagnostics;
using System.Reflection;
using System.Text.Json;
using Memoria.AutoPlay;
using Memoria.FF7.Events;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;

internal static class PresentationActivationTests
{
    public static void Run()
    {
        byte[] script = [0x6B, 0, 0, 40, 0, 0, 8, 5, 255, 0x6C, 0x6B, 0, 0, 80, 0, 0, 8, 6, 0, 0x6C, 0x12, 20, 0];
        Ff7FadeProcess fade = new(5, 248, 8, 40, 0, 0, false);
        Ff7AutoPlayState state = new();
        AutoPlayPlanner<Ff7AutoPlayDeferral> planner = Ff7AutoPlayPolicy.CreatePlanner(state);
        planner.Observe(new("source", "program"), [new("line", AutoPlayEventKind.Interaction, ConditionSignature: "admitted")]);
        planner.BeginEvent("source", "line");
        Ff7AutoPlayActiveEvent active = new("source", "line", "admitted", 1, 0, 4, Ff7AutoPlayTargetKind.LineTouch, null, 641, "before", Accepted: true, NavigationKey: "component");
        state.Active = active;
        AutoPlayTravelGoal unrelated = new("elsewhere", "other", "other-condition");
        state.NavigationGoal = unrelated;
        Ff7AutoPlayObservation observation = new("source", "program", 1, [], true, true) { NavigationKey = "component" };
        FieldEntitySnapshot owner = new(9, 1, 0, [0, 0, 0, 0, 0, 0, 0, 22], new FieldSyncState[8], Enumerable.Repeat(-1, 8).ToArray());
        FieldHostRequest request = new(1, 0, 1, 9, new FieldFadeCommand(0, Wait: true));
        FieldVmSnapshot vm = new(1, FieldMachine.SemanticProfile, "program", 5, true, new byte[1280], new byte[256], [owner], PendingRequests: [request]);
        Ff7FieldProcess process = new() { Actors = [new() { Slot = 0, Entity = 1, Model = 0, EntityBound = true, PlayerControlled = true }], Lines = [new(0, 0, 0, 0, 0, 1, 0, 0, Enabled: 0)], FadeState = fade };
        bool Can(FieldVmSnapshot? native = null, Ff7FieldProcess? current = null) => Ff7AutoPlayPolicy.CanCompletePresentationActivation(state, observation, native ?? vm, current ?? process, script);
        string before = JsonSerializer.Serialize(new { state, vm, process });
        Check(Can(), "A genuine admitted presentation activation was not recognized.");
        foreach (Ff7AutoPlayActiveEvent wrong in new[] { active with { Accepted = false }, active with { Returned = true }, active with { Generation = 2 }, active with { Entity = 1 }, active with { NavigationKey = "other" }, active with { ConditionFingerprint = "other" }, active with { NavigationProbeKey = "probe" }, active with { DestinationField = 2 } })
        {
            state.Active = wrong;
            Check(!Can(), "An unauthenticated, stale or semantically owned activation was consumed.");
        }
        state.Active = active;
        Check(!Can(vm with { Entities = [owner with { Priority = 2 }] }), "A different native priority authenticated the activation.");
        Check(!Can(vm with { ProgramIdentity = "other" }), "A different native program authenticated the activation.");
        Check(!Can(vm with { PendingRequests = [] }) && !Can(vm with { PendingRequests = [request with { ProgramCounter = 19 }] }) && !Can(vm with { PendingRequests = [request with { Entity = 1 }] }), "A missing or wrong native FadeWait request authenticated the activation.");
        Check(!Can(vm with { PendingRequests = [request, request with { Id = 2, Priority = 0 }] }), "Additional owned request debt was ignored.");
        FieldEntitySnapshot child = owner with { SyncStates = [FieldSyncState.Waiting], SyncOwners = [0] };
        Check(!Can(vm with { Entities = [owner, child] }), "An outstanding owned semantic request was ignored.");
        Check(!Can(current: process with { PlayerLocked = true }) && !Can(current: process with { MenuDisabled = true }) && !Can(current: process with { Lines = [process.Lines[0] with { Enabled = 1 }] }), "Held native control or an enabled trigger was ignored.");
        foreach (byte opcode in new byte[] { 0x00, 0x03, 0x30, 0x80, 0x6D, 0x60, 0xF2 })
        {
            byte[] semantic = (byte[])script.Clone();
            semantic[10] = opcode;
            Check(!Ff7AutoPlayPolicy.IsClosedPresentationCycle(semantic, 9, fade), "A semantic, input, request or open-tail instruction was classified as pure presentation.");
        }
        Check(!Ff7AutoPlayPolicy.IsClosedPresentationCycle(script, 9, fade with { Speed = 0 }) && !Ff7AutoPlayPolicy.IsClosedPresentationCycle(script, 9, fade with { Red = 80 }), "A frozen or mismatched native fade authenticated the cycle.");
        byte[] longJump = (byte[])script.Clone();
        longJump[20] = 0x13;
        Check(Ff7AutoPlayPolicy.IsClosedPresentationCycle(longJump, 9, fade), "Native word-sized backward jump changed the closed presentation cycle.");
        byte[] escaped = (byte[])script.Clone();
        escaped[21] = 10;
        Check(!Ff7AutoPlayPolicy.IsClosedPresentationCycle(escaped, 9, fade), "A one-way prefix into another cycle was treated as an owner already inside its SCC.");
        byte[] persistent = (byte[])script.Clone();
        persistent[7] = 7;
        Check(!Ff7AutoPlayPolicy.IsClosedPresentationCycle(persistent, 9, fade), "A persistent tint entered the finite fade cycle certificate.");
        AutoPlayConditionCoverage conditions = state.Coverage.Visits.Single().Events.Single().Conditions!;
        conditions.ActiveEntry = null;
        Check(!Can(), "Missing authenticated source admission was replaced by inferred credit.");
        conditions.ActiveEntry = active.ConditionFingerprint;
        Check(before == JsonSerializer.Serialize(new { state, vm, process }), "The activation certificate mutated native execution or history.");
        Ff7AutoPlayPolicy.RecordControlReturn(planner, active, observation, false, null, false, []);
        Check(!Can() && state.Coverage.Visits.Single().Events.Single().Conditions!.ConsumedEntries.SetEquals(["admitted"]), "Activation accounting consumed a different condition or permitted duplicate credit.");
        Check(!active.Returned && state.Coverage.NavigationRoutes.Count == 0, "Activation accounting fabricated RET or a route.");
        Check(state.NavigationGoal == unrelated && !Ff7AutoPlayPolicy.IsNavigationGoalCompleted(unrelated, active, observation.NavigationKey) && Ff7AutoPlayPolicy.IsNavigationGoalCompleted(new("component", "line", "admitted"), active, observation.NavigationKey), "Activation accounting discarded unrelated intent or failed to identify its exact matching goal.");
        RunOriginal();
        Console.WriteLine("PASS authenticated persistent-presentation activation");
    }

    private static void RunOriginal()
    {
        string? root = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR");
        string? path = Environment.GetEnvironmentVariable("MEMORIA_FF7_PRESENTATION_ACTIVATION_SAVE");
        if (string.IsNullOrWhiteSpace(root) || string.IsNullOrWhiteSpace(path))
        {
            Console.WriteLine("SKIP external persistent-presentation activation.");
            return;
        }
        byte[] source = File.ReadAllBytes(path);
        using MemoryStream stream = new(source, writable: false);
        Ff7SessionSave saved = CombinedSessionCodec.Read(stream).Ff7!;
        Ff7AutoPlayActiveEvent original = saved.AutoPlay!.Active!;
        using Ff7Session session = Ff7Session.Restore(Ff7Installation.Discover(root), saved);
        using Ff7AutoPlayPolicy policy = new(session, (_, _) => throw new InvalidOperationException("Unexpected choice at an activation-only boundary."));
        policy.State.FrontierProgressKey = null;
        policy.State.FrontierIdleSteps = 0;
        AutoPlayEventCoverage<Ff7AutoPlayDeferral> Coverage() => policy.State.Coverage.Visits.Single(visit => visit.Visit.Key == original.VisitId).Events.Single(item => item.Event.Id == original.EventId);
        string routes = JsonSerializer.Serialize(Coverage().Routes);
        string probes = JsonSerializer.Serialize(policy.State.NavigationProbes);
        HashSet<string> consumed = new(Coverage().Conditions!.ConsumedEntries, StringComparer.Ordinal);
        bool accounted = false;
        Ff7AutoplayInput Choose(Ff7AutoplayDriver driver)
        {
            string native = JsonSerializer.Serialize(new { Vm = session.Machine.Capture(), Process = session.Host.Capture(), session.Progress });
            bool before = Coverage().Conditions!.HasExecutedEntry(original.ConditionFingerprint);
            Ff7AutoplayInput input = policy.ChooseInput(driver);
            if (!before && Coverage().Conditions!.HasExecutedEntry(original.ConditionFingerprint))
            {
                accounted = true;
                Check(native == JsonSerializer.Serialize(new { Vm = session.Machine.Capture(), Process = session.Host.Capture(), session.Progress }), "Activation accounting changed a PC, request, native fade or progress.");
            }
            return input;
        }
        Ff7AutoPlayController controller = new(session, Choose, chooseDialogueSelection: policy.ChooseDialogueSelection, advanceTick: () => session.AdvanceTick(advanceClock: false));
        controller.Start();
        Stopwatch elapsed = Stopwatch.StartNew();
        int polls = 0;
        while (controller.Running && polls++ < 32 && elapsed.Elapsed < TimeSpan.FromSeconds(3))
        {
            controller.Advance(1);
            if (policy.State.Active is { } next && next.EventId != original.EventId)
            {
                break;
            }
        }
        FieldVmSnapshot after = session.Machine.Capture();
        Console.WriteLine($"External activation: {elapsed.Elapsed.TotalSeconds:F3}s, {polls} polls, {controller.Steps} callbacks, next {policy.State.Active?.EventId}, original owner p{after.Entities[original.Entity].Priority}/pc{after.Entities[original.Entity].ProgramCounter:X4}, error {controller.Error}");
        Check(controller.Error is null && accounted && policy.State.Active is { } selected && selected.EventId != original.EventId && after.Entities[original.Entity].Priority == 1 && Ff7AutoPlayPolicy.IsClosedPresentationCycle(session.Field.Scripts.Data.Span, after.Entities[original.Entity].ProgramCounter, session.Host.Process.FadeState), "Ordinary policy did not hand off while preserving the live native presentation owner.");
        consumed.Add(original.ConditionFingerprint);
        Check(Coverage().Conditions!.ConsumedEntries.SetEquals(consumed), "Activation accounting credited a current or unrelated condition.");
        Check(routes == JsonSerializer.Serialize(Coverage().Routes) && probes == JsonSerializer.Serialize(policy.State.NavigationProbes), "Activation accounting fabricated route or probe receipts.");
        MethodInfo finish = typeof(Ff7AutoPlayPolicy).GetMethod("CompletePresentationActivation", BindingFlags.Instance | BindingFlags.NonPublic)!;
        Check(!(bool)finish.Invoke(policy, [new Ff7AutoPlayDiscovery(session).Observe()])! && Coverage().Conditions!.ConsumedEntries.SetEquals(consumed), "The same activation was recorded twice.");
        Check(source.AsSpan().SequenceEqual(File.ReadAllBytes(path)), "The standalone source changed.");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
