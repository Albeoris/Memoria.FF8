using System.Diagnostics;
using System.Security.Cryptography;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;

internal static class NativeNavigationPrerequisiteOriginalTests
{
    internal static void RunOriginal()
    {
        string path = Environment.GetEnvironmentVariable("MEMORIA_FF7_NAVIGATION_PREREQUISITE_SAVE") ?? throw new InvalidOperationException("Select a native prerequisite fixture.");
        byte[] bytes = File.ReadAllBytes(path);
        byte[] hash = SHA256.HashData(bytes);
        using MemoryStream input = new(bytes, writable: false);
        var saved = CombinedSessionCodec.Read(input);
        using var session = Ff7Session.Restore(Ff7Installation.Discover(Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR")!), saved.Ff7!);
        using var policy = new Ff7AutoPlayPolicy(session, (_, _) => throw new InvalidOperationException("The bounded prerequisite continuation reached a new choice."));
        var originalGoal = policy.State.NavigationGoal!;
        var goalVisit = policy.State.Coverage.NavigationVisits.Single(visit => visit.Key == originalGoal.NavigationKey).VisitKey;
        var initialCoverage = policy.State.Coverage.Visits.Single(visit => visit.Visit.Key == goalVisit).Events.Single(item => item.Event.Id == originalGoal.EventId);
        int routesBefore = initialCoverage.Routes.Count;
        bool supportAdmitted = false;
        bool supportCleared = false;
        bool lineDeparted = false;
        bool goalAdmitted = false;
        bool returned = false;
        bool nextMoved = false;
        Ff7AutoPlayActiveEvent? completedActivation = null;
        session.NativeActorTalkRequested += admission =>
        {
            if (session.MapId == 367 && admission is { Entity: 12, Script: 1, Priority: 1, Accepted: true, Started: true })
            {
                supportAdmitted = true;
                Console.WriteLine($"Actual prerequisite admission: tick{session.Machine.Tick}, entity12/script1/priority1, goalRetained={policy.State.NavigationGoal == originalGoal}");
                if (policy.State.NavigationGoal != originalGoal)
                {
                    throw new InvalidOperationException("The prerequisite stole the ultimate goal.");
                }
            }
        };
        session.NativeInteractionScriptRequested += admission => goalAdmitted |= session.MapId == 378 && admission is { Entity: 4, Script: 4, Accepted: true, Started: true };
        session.NativeTickCompleted += tick =>
        {
            if (tick.Field == "junmin1" && supportAdmitted && !supportCleared && session.Host.Process.Actors.SingleOrDefault(actor => actor.EntityBound && actor.Entity == 12) is { CollisionEnabled: false, TalkEnabled: false, Visible: false })
            {
                supportCleared = true;
                Console.WriteLine($"Actual prerequisite cleared collider: tick{tick.Tick}");
            }
            if (tick.Field == "junmin1" && tick.Transition is { Map: 378 } && tick.TransitionOwner is { Entity: 3, Priority: 1 })
            {
                lineDeparted = true;
                Console.WriteLine($"Actual owned LINE departure: tick{tick.Tick}, owner3/1/PC{tick.TransitionOwner.ProgramCounter}, map378");
            }
            if (lineDeparted && tick.Field == "junpb_2" && tick.Transition is { Map: 367 })
            {
                returned = true;
                completedActivation = tick.CompletedSynchronousActivation;
                Console.WriteLine($"Actual source activation boundary: tick{tick.Tick}, ambientOwner{tick.TransitionOwner?.Entity}/{tick.TransitionOwner?.Priority}/PC{tick.TransitionOwner?.ProgramCounter}, returned={completedActivation?.Returned}, transitionOwned={completedActivation?.TransitionOwned}");
            }
        };
        var controller = new Ff7AutoPlayController(session, driver =>
        {
            var result = policy.ChooseInput(driver);
            nextMoved |= returned && policy.State.NavigationGoal != originalGoal && (result.Horizontal != 0 || result.Vertical != 0 || result.Talk);
            return result;
        }, advanceTick: () => session.AdvanceTick(advanceClock: false), chooseDialogueSelection: policy.ChooseDialogueSelection, readPlanningProgress: () => policy.WorkProgress, readFrontierProgress: () => policy.FrontierProgress);
        controller.Start();
        Stopwatch timer = Stopwatch.StartNew();
        for (int polls = 0; polls < 100000 && timer.Elapsed.TotalSeconds < 45 && !returned && controller.Error is null; polls++)
        {
            controller.Advance(1);
        }
        for (int polls = 0; returned && polls < 1000 && timer.Elapsed.TotalSeconds < 45 && !nextMoved && controller.Error is null; polls++)
        {
            controller.Advance(1);
        }
        var coverage = policy.State.Coverage.Visits.Single(visit => visit.Visit.Key == goalVisit).Events.Single(item => item.Event.Id == originalGoal.EventId);
        bool consumed = coverage.Conditions?.HasExecutedEntry(originalGoal.Conditions!) == true;
        Console.WriteLine($"Native prerequisite continuation: callbacks={controller.Steps}, seconds={timer.Elapsed.TotalSeconds:F3}, map={session.MapId}, tick={session.Machine.Tick}, consumed={consumed}, routes={coverage.Routes.Count}/{routesBefore}, nextMoved={nextMoved}, nextActive={policy.State.Active?.EventId}, error={controller.Error}");
        if (!hash.AsSpan().SequenceEqual(SHA256.HashData(File.ReadAllBytes(path))))
        {
            throw new InvalidOperationException("The original prerequisite source changed.");
        }
        if (!supportAdmitted || !supportCleared || !lineDeparted || !returned || !goalAdmitted || !consumed || coverage.Routes.Count != routesBefore || !nextMoved || completedActivation is not { Returned: false, TransitionOwned: false } || completedActivation.ConditionFingerprint != originalGoal.Conditions || controller.Error is not null || session.Progress.Gil != saved.Ff7!.Progress.Gil)
        {
            throw new InvalidOperationException("The normal prerequisite continuation did not complete its exact source activation and continue without a fabricated return or route.");
        }
    }
}
