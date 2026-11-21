using System.Diagnostics;
using System.Security.Cryptography;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;

internal static class NativeCorridorOriginalTests
{
    internal static void Run()
    {
        string path = Environment.GetEnvironmentVariable("MEMORIA_FF7_NATIVE_CORRIDOR_SAVE") ?? throw new InvalidOperationException("Select a native corridor fixture.");
        byte[] bytes = File.ReadAllBytes(path);
        byte[] hash = SHA256.HashData(bytes);
        using MemoryStream input = new(bytes, writable: false);
        var saved = CombinedSessionCodec.Read(input);
        using var session = Ff7Session.Restore(Ff7Installation.Discover(Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR")!), saved.Ff7!);
        using var policy = new Ff7AutoPlayPolicy(session, (key, _) => "memory:" + key);
        Ff7NativeGatewayDeparture? departure = null;
        ushort sourceMap = session.MapId;
        bool arrivalStarted = false;
        bool arrivalReturnedControl = false;
        session.NativeInteractionScriptRequested += admission =>
        {
            if (session.MapId == sourceMap && admission is { Entity: 2, Script: 4, Priority: 1, Accepted: true, Started: true })
            {
                arrivalStarted = true;
            }
        };
        session.NativeFieldDeparting += _ => departure = session.Capture().GatewayDeparture;
        HashSet<int> triangles = [];
        session.NativeTickCompleted += _ =>
        {
            if (session.MapId == sourceMap)
            {
                triangles.Add(session.Host.Process.Actors.Single(actor => actor.PlayerControlled).Triangle);
                arrivalReturnedControl |= arrivalStarted && !session.Host.Process.PlayerLocked && !session.Host.Process.MenuDisabled && policy.State.Active is not { Accepted: true, Returned: false };
            }
        };
        var controller = new Ff7AutoPlayController(session, policy.ChooseInput, advanceTick: () => session.AdvanceTick(advanceClock: false), chooseDialogueSelection: policy.ChooseDialogueSelection);
        controller.Start();
        Stopwatch timer = Stopwatch.StartNew();
        for (int polls = 0; polls < 1000000 && controller.Steps < 5000 && timer.Elapsed.TotalSeconds < 90 && controller.Error is null && session.MapId != 627; polls++)
        {
            controller.Advance(1);
        }
        var progress = controller.PlanningProgress;
        Console.WriteLine($"Corridor continuation: callbacks={controller.Steps}, seconds={timer.Elapsed.TotalSeconds:F3}, map={session.MapId}, tick={session.Machine.Tick}, triangles={triangles.Count}, arrival={arrivalStarted}/{arrivalReturnedControl}, gateway={departure?.GatewayIndex}, physical={progress.PhysicalNodes}, detached={progress.DetachedNativeCallbacks}, graphExpanded={progress.NativeGraphExpanded}, error={controller.Error}");
        bool sourceUnchanged = hash.AsSpan().SequenceEqual(SHA256.HashData(File.ReadAllBytes(path)));
        if (!sourceUnchanged || controller.Error is not null || session.MapId != 627 || departure?.GatewayIndex != 2 || departure.Transition.Map != 627 || triangles.Count < 2 || !arrivalStarted || !arrivalReturnedControl)
        {
            throw new InvalidOperationException("The ordinary native corridor continuation did not complete its arrival scene and selected gateway, or changed the original fixture.");
        }
    }
}
