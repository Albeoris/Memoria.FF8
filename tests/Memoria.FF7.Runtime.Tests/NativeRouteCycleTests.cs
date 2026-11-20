using System.Diagnostics;
using System.Text.Json;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;

internal static class NativeRouteCycleTests
{
    internal static void RunOriginal()
    {
        string path = Environment.GetEnvironmentVariable("MEMORIA_FF7_ROUTE_CYCLE_SAVE") ?? throw new InvalidOperationException("Select a native route-cycle fixture.");
        string game = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR") ?? throw new InvalidOperationException("Select original resources.");
        byte[] bytes = File.ReadAllBytes(path);
        using MemoryStream input = new(bytes, false);
        using Ff7Session session = Ff7Session.Restore(Ff7Installation.Discover(game), CombinedSessionCodec.Read(input).Ff7!);
        using Ff7AutoPlayPolicy policy = new(session, (_, _) => throw new InvalidOperationException("The route continuation reached a choice boundary."));
        var goal = policy.State.NavigationGoal ?? throw new InvalidOperationException("The fixture lacks its native source-visit goal.");
        if (goal.EventId is not null || !goal.NavigationKey.StartsWith("ff7-world:", StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Select a source-only native-world continuation fixture.");
        }
        var originalCoverage = policy.State.Coverage.Visits.SelectMany(visit => visit.Events).Where(item => item.Event.Id == "actor:15:script:1").ToArray();
        string history = JsonSerializer.Serialize(originalCoverage.Select(item => new { item.Completed, item.Conditions?.ConsumedEntries, item.Routes }));
        bool selectedExit = false;
        bool realDeparture = false;
        Ff7AutoPlayController controller = new(session, policy.ChooseInput, advanceTick: () => session.AdvanceTick(advanceClock: false), chooseDialogueSelection: policy.ChooseDialogueSelection, readPlanningProgress: () => policy.WorkProgress, readFrontierProgress: () => policy.FrontierProgress);
        session.NativeTickCompleted += tick =>
        {
            if (tick.Transition is not null)
            {
                if (tick.Transition.Map <= 64)
                {
                    if (policy.State.NavigationGoal != goal || policy.State.Active is not { Kind: Ff7AutoPlayTargetKind.Gateway })
                    {
                        throw new InvalidOperationException("The native world departure lost the retained source-only intent.");
                    }
                    realDeparture = true;
                }
            }
        };
        controller.Start();
        Stopwatch elapsed = Stopwatch.StartNew();
        TimeSpan previousClock = TimeSpan.Zero;
        for (int poll = 0; poll < 4096 && controller.Running && session.World is null && controller.Steps < 256 && elapsed.Elapsed < TimeSpan.FromSeconds(10); poll++)
        {
            TimeSpan now = elapsed.Elapsed;
            session.AdvanceClock(now - previousClock);
            previousClock = now;
            controller.Advance(1);
            if (session.World is null && policy.State.Active is { Accepted: false, Kind: Ff7AutoPlayTargetKind.Gateway })
            {
                if (policy.State.NavigationGoal != goal || policy.State.TravelCommitment?.Goal != goal)
                {
                    throw new InvalidOperationException("A competing frontier preempted the authenticated source-only trip.");
                }
                selectedExit = true;
            }
        }
        // Arrival is published by the next normal policy observation after native World allocation.
        if (session.World is not null && policy.State.TravelCommitment is not null)
        {
            controller.Advance(1);
        }
        if (controller.Error is not null || !selectedExit || !realDeparture || session.World is null || policy.State.TravelCommitment is not null || policy.State.NavigationGoal is not null || history != JsonSerializer.Serialize(originalCoverage.Select(item => new { item.Completed, item.Conditions?.ConsumedEntries, item.Routes })) || !bytes.AsSpan().SequenceEqual(File.ReadAllBytes(path)))
        {
            throw new InvalidOperationException($"Source continuation assertion: world={session.World is not null}, selected={selectedExit}, departure={realDeparture}, marker={policy.State.TravelCommitment}, goal={policy.State.NavigationGoal}, callbacks={controller.Steps}, historySame={history == JsonSerializer.Serialize(originalCoverage.Select(item => new { item.Completed, item.Conditions?.ConsumedEntries, item.Routes }))}, error={controller.Error}");
        }
        Console.WriteLine($"PASS original authenticated source-only trip through native gateway to World; callbacks={controller.Steps}; worldLoaded={session.World is not null}; elapsed={elapsed.Elapsed.TotalSeconds:F3}s; marker released, competing history/source unchanged");
    }
}
