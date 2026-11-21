using System.Diagnostics;
using System.Security.Cryptography;
using Memoria.FF7.Events;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;

internal static class NativeSnowboardDepartureOriginalTests
{
    internal static void Run()
    {
        string path = Environment.GetEnvironmentVariable("MEMORIA_FF7_SNOWBOARD_DEPARTURE_SAVE") ?? throw new InvalidOperationException("Select a native departure-priority fixture.");
        byte[] bytes = File.ReadAllBytes(path);
        using MemoryStream input = new(bytes, writable: false);
        using var session = Ff7Session.Restore(Ff7Installation.Discover(Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR")!), CombinedSessionCodec.Read(input).Ff7!);
        using var policy = new Ff7AutoPlayPolicy(session, (key, _) => "memory:" + key);
        var optional = policy.State.Active;
        bool started = false;
        bool cycle = false;
        session.NativeTickCompleted += _ =>
        {
            cycle |= policy.State.Status?.Contains("native navigation cycle", StringComparison.Ordinal) == true;
        };
        session.NativeInteractionScriptRequested += admission => started |= admission is { Entity: 11, Script: 2, Priority: 1, Accepted: true, Started: true };
        var controller = new Ff7AutoPlayController(session, policy.ChooseInput, advanceTick: () => session.AdvanceTick(advanceClock: false), chooseDialogueSelection: policy.ChooseDialogueSelection, readPlanningProgress: () => policy.WorkProgress, readFrontierProgress: () => policy.FrontierProgress);
        controller.Start();
        Stopwatch timer = Stopwatch.StartNew();
        for (int poll = 0; poll < 20000 && controller.Steps < 4000 && timer.Elapsed.TotalSeconds < 30 && controller.Error is null && session.Host.PendingMinigame is null && session.MapId == 654 && session.World is null && !cycle; poll++)
        {
            controller.Advance(1);
        }
        FieldHostRequest? request = session.Host.PendingMinigame;
        if (cycle)
        {
            throw new InvalidOperationException("The native departure approach repeated a physical navigation cycle before admission.");
        }
        bool optionalConsumed = optional is not null && policy.State.Coverage.Visits.Single(visit => visit.Visit.Key == optional.VisitId).Events.Single(item => item.Event.Id == optional.EventId).Conditions?.HasExecutedEntry(optional.ConditionFingerprint) == true;
        Console.WriteLine($"Native departure priority: callbacks={controller.Steps}, seconds={timer.Elapsed.TotalSeconds:F3}, tick={session.Machine.Tick}, started={started}, minigame={request?.Command}, optionalConsumed={optionalConsumed}, status={policy.State.Status}, error={controller.Error}");
        if (!started || optionalConsumed || controller.Error is not null || request is not { Entity: 11, Priority: 1, ProgramCounter: 0x0BC1, Command: FieldMinigameCommand { Game: 2, Return: { Map: 658, X: -201, Y: 3116, Triangle: 71, Direction: 64 } } } || (session.Machine.Variables.ReadByte(FieldBank.Persistent0Byte, 0x45) & 0x10) == 0 || (session.Machine.Variables.ReadByte(FieldBank.Persistent0Byte, 0x82) & 0x42) != 0x42 || !SHA256.HashData(bytes).AsSpan().SequenceEqual(SHA256.HashData(File.ReadAllBytes(path))))
        {
            throw new InvalidOperationException("The ready native departure did not replace optional planning and admit its actual minigame request unchanged.");
        }
    }
}
