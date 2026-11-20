using System.Diagnostics;
using System.Text.Json;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;

internal static class ProductiveOrdinarySearchTests
{
    public static void Run()
    {
        Ff7AutoPlayTarget target = new("gateway:0", -1, -1, Ff7AutoPlayTargetKind.Gateway, "current", false, default, DestinationField: 2);
        Ff7AutoPlayObservation observation = new("source", "program", 1, [target], true, true) { NavigationKey = "component" };
        Ff7AutoPlayState state = new();
        Ff7AutoPlayActiveEvent owner = new("source", target.Id, target.ConditionFingerprint, 1, -1, -1, target.Kind, 2, 641, "before", NavigationKey: "component");
        state.Active = owner;
        bool Retain(bool pending = true, bool advanced = true, bool unknown = false) => Ff7AutoPlayPolicy.CanRetainProductiveOrdinarySearch(state, observation, owner, pending, advanced, unknown);
        string before = JsonSerializer.Serialize(state);
        Check(Retain(), "A progressed ordinary leg lost its retained physical cursor to unrelated walking.");
        Check(!Retain(pending: false), "Terminal or absent physical work pinned an ordinary goal.");
        Check(!Retain(advanced: false), "Unproductive Pending alone pinned an ordinary goal.");
        Check(!Retain(unknown: true), "Unknown contact work lost its existing sibling escape scheduling.");
        foreach (Ff7AutoPlayActiveEvent changed in new[]
        {
            owner with { Accepted = true }, owner with { Returned = true }, owner with { TransitionOwned = true },
            owner with { CompletionRecorded = true }, owner with { Generation = 2 }, owner with { VisitId = "other" },
            owner with { NavigationKey = "other" }, owner with { ConditionFingerprint = "changed" }
        })
        {
            state.Active = changed;
            Check(!Ff7AutoPlayPolicy.CanRetainProductiveOrdinarySearch(state, observation, changed, true, true, false), "Changed ownership or native source retained stale ordinary work.");
        }
        state.Active = owner;
        Check(!Ff7AutoPlayPolicy.CanRetainProductiveOrdinarySearch(state, observation with { Events = [] }, owner, true, true, false), "A missing current event retained stale work.");
        Check(before == JsonSerializer.Serialize(state), "Scheduling preference changed progress or coverage.");
        RunOriginal();
        Console.WriteLine("PASS productive ordinary physical search retention");
    }

    private static void RunOriginal()
    {
        string? root = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR");
        string? path = Environment.GetEnvironmentVariable("MEMORIA_FF7_PRODUCTIVE_SEARCH_SAVE");
        if (string.IsNullOrWhiteSpace(root) || string.IsNullOrWhiteSpace(path))
        {
            Console.WriteLine("SKIP external productive ordinary search continuation.");
            return;
        }
        byte[] source = File.ReadAllBytes(path);
        using MemoryStream stream = new(source, writable: false);
        Ff7SessionSave saved = CombinedSessionCodec.Read(stream).Ff7!;
        using Ff7Session session = Ff7Session.Restore(Ff7Installation.Discover(root), saved);
        int initialMap = session.MapId;
        using Ff7AutoPlayPolicy policy = new(session, (_, _) => throw new InvalidOperationException("Unexpected choice before the selected ordinary route."));
        // Match the App's explicit new-run diagnostic lifecycle; native history and progress stay intact.
        policy.State.FrontierProgressKey = null;
        policy.State.FrontierIdleSteps = 0;
        Ff7AutoplayDriver? live = null;
        Ff7AutoPlayController controller = new(session, driver =>
        {
            live = driver;
            return policy.ChooseInput(driver);
        }, chooseDialogueSelection: policy.ChooseDialogueSelection, advanceTick: () => session.AdvanceTick(advanceClock: false), readPlanningProgress: () => policy.WorkProgress, readFrontierProgress: () => policy.FrontierProgress);
        controller.Start();
        bool retained = false;
        Stopwatch elapsed = Stopwatch.StartNew();
        int polls = 0;
        while (controller.Running && session.MapId == initialMap && elapsed.Elapsed < TimeSpan.FromSeconds(10))
        {
            controller.Advance(1);
            polls++;
            if (live?.HasRetainedPhysicalSearch == true && policy.State.Active?.EventId == saved.AutoPlay!.Active!.EventId)
            {
                retained = true;
            }
        }
        Console.WriteLine($"External productive search: {elapsed.Elapsed.TotalSeconds:F3}s, {polls} polls, {controller.Steps} native callbacks, map {session.MapId}, tick {session.Machine.Tick}, active {policy.State.Active?.EventId}, physical {controller.PlanningProgress.PhysicalNodes}, detached {controller.PlanningProgress.DetachedNativeCallbacks}, retained {retained}");
        Check(controller.Error is null, $"Ordinary continuation failed: {controller.Error}");
        Check(retained, "The selected physical leg never retained genuine pending work.");
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
