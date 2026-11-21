using System.Diagnostics;
using System.Security.Cryptography;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;

internal static class NativeLocalDestinationOriginalTests
{
    internal static void Run()
    {
        string path = Environment.GetEnvironmentVariable("MEMORIA_FF7_LOCAL_DESTINATION_SAVE") ?? throw new InvalidOperationException("Select a native local destination fixture.");
        byte[] bytes = File.ReadAllBytes(path);
        using MemoryStream input = new(bytes, writable: false);
        using var session = Ff7Session.Restore(Ff7Installation.Discover(Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR")!), CombinedSessionCodec.Read(input).Ff7!);
        using var policy = new Ff7AutoPlayPolicy(session, (key, _) => "memory:" + key);
        bool started = false;
        bool landed = false;
        bool returnedControl = false;
        bool helperStarted = false;
        bool helperReturned = false;
        Ff7NativeGatewayDeparture? departure = null;
        session.NativeInteractionScriptRequested += admission =>
        {
            if (admission is { Entity: 4 or 5, Script: 4, Priority: 1, Accepted: true, Started: true })
            {
                started = true;
            }
            helperStarted |= admission is { Entity: 7, Script: 4, Priority: 1, Accepted: true, Started: true };
        };
        session.NativeTickCompleted += tick =>
        {
            if (session.MapId == 627)
            {
                landed |= session.Host.Process.Actors.Any(actor => actor is { PlayerControlled: true, Triangle: 45, Jump.Completed: true });
                returnedControl |= landed && !session.Host.Process.PlayerLocked && !session.Host.Process.MenuDisabled;
                helperReturned |= helperStarted && tick.ScriptReturns.Any(returned => returned.Entity == 7 && returned.Priority == 1);
            }
        };
        session.NativeFieldDeparting += _ => departure = session.Capture().GatewayDeparture;
        var controller = new Ff7AutoPlayController(session, policy.ChooseInput, advanceTick: () => session.AdvanceTick(advanceClock: false), chooseDialogueSelection: policy.ChooseDialogueSelection, readPlanningProgress: () => policy.WorkProgress, readFrontierProgress: () => policy.FrontierProgress);
        controller.Start();
        Stopwatch timer = Stopwatch.StartNew();
        while (controller.Error is null && session.MapId != 628 && controller.Steps < 5000 && timer.Elapsed.TotalSeconds < 90)
        {
            controller.Advance(1);
        }
        bool battleBoundary = session.MapId == 627 && session.Battle is not null && controller.Failure is NotSupportedException && controller.Failure.Message == "FFVII battle action opcode 0xB5 at 3 requires its native execution contract." && helperReturned;
        Console.WriteLine($"Local destination: map={session.MapId}, tick={session.Machine.Tick}, callbacks={controller.Steps}, seconds={timer.Elapsed.TotalSeconds:F3}, jump={started}/{landed}/{returnedControl}, helper={helperStarted}/{helperReturned}, gateway={departure?.GatewayIndex}, formation={session.Battle?.Capture().FormationId}, battleBoundary={battleBoundary}, physical={controller.PlanningProgress.PhysicalNodes}, detached={controller.PlanningProgress.DetachedNativeCallbacks}");
        bool arrived = session.MapId == 628 && controller.Error is null && departure?.GatewayIndex == 0 && departure.Transition.Map == 628;
        if ((!arrived && !battleBoundary) || !started || !landed || !returnedControl || !SHA256.HashData(bytes).AsSpan().SequenceEqual(SHA256.HashData(File.ReadAllBytes(path))))
        {
            throw new InvalidOperationException("The normal local destination continuation did not reach its native exit unchanged.");
        }
    }
}
