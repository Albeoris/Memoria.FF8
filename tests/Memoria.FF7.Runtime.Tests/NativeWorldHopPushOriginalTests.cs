using System.Diagnostics;
using System.Reflection;
using System.Security.Cryptography;
using Memoria.FF7.Events;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;

internal static class NativeWorldHopPushOriginalTests
{
    internal static void Run()
    {
        string path = Environment.GetEnvironmentVariable("MEMORIA_FF7_WORLD_HOP_PUSH_SAVE") ?? throw new InvalidOperationException("Select a native world-hop prerequisite fixture.");
        byte[] bytes = File.ReadAllBytes(path);
        using MemoryStream input = new(bytes, writable: false);
        using Ff7Session session = Ff7Session.Restore(Ff7Installation.Discover(Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR")!), CombinedSessionCodec.Read(input).Ff7!);
        using Ff7AutoPlayPolicy policy = new(session, (_, _) => throw new InvalidOperationException("Unexpected choice before the native world-hop jump."));
        Ff7AutoPlayObservation observation = new Ff7AutoPlayDiscovery(session).Observe();
        Ff7AutoPlayTarget gateway = observation.Events.Single(target => target.Id == "gateway:0");
        string native = Ff7AutoplayDriver.ReadNativeRouteFingerprint(session);
        MethodInfo read = typeof(Ff7AutoPlayPolicy).GetMethod("ReadKnownNavigationPrerequisites", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var prerequisites = (Ff7AutoPlayTarget[])read.Invoke(policy, [observation, gateway])!;
        if (prerequisites.FirstOrDefault(target => target.Kind == Ff7AutoPlayTargetKind.LinePush)?.Entity != 5 || native != Ff7AutoplayDriver.ReadNativeRouteFingerprint(session))
        {
            throw new InvalidOperationException("The native current world-selector hop lost its real blocking prerequisite or changed native state during qualification.");
        }
        bool selected = false;
        bool jumped = false;
        bool returned = false;
        session.Host.NativeOperationExecuted += (request, _) => jumped |= request is { Entity: 11, Priority: 6, Command: FieldActorJump { X: 2, Y: -456, Triangle: 114, Duration: 17 } };
        session.NativeTickCompleted += tick => returned |= tick.ScriptReturns.Any(owner => owner.Entity == 5 && owner.Priority == 1);
        Ff7AutoplayInput Choose(Ff7AutoplayDriver driver)
        {
            Ff7AutoplayInput action = policy.ChooseInput(driver);
            selected |= policy.State.Active is { Entity: 5, Script: 2, Kind: Ff7AutoPlayTargetKind.LinePush };
            if (!selected && (action.Horizontal != 0 || action.Vertical != 0))
            {
                throw new InvalidOperationException("Movement toward the world gateway preceded its genuine current-route prerequisite.");
            }
            return action;
        }
        Ff7AutoPlayController controller = new(session, Choose, advanceTick: () => session.AdvanceTick(advanceClock: false), chooseDialogueSelection: policy.ChooseDialogueSelection);
        controller.Start();
        Stopwatch timer = Stopwatch.StartNew();
        while (controller.Error is null && timer.Elapsed.TotalSeconds < 20 && !(jumped && returned && !session.Host.Process.PlayerLocked && !session.Host.Process.MenuDisabled))
        {
            controller.Advance(1);
        }
        Ff7Actor player = session.Host.Process.Actors.Single(actor => actor.PlayerControlled);
        Console.WriteLine($"Native world-hop Push: callbacks={controller.Steps}, seconds={timer.Elapsed.TotalSeconds:F3}, field={session.MapId}, tick={session.Machine.Tick}, selected={selected}, jumped={jumped}, returned={returned}, triangle={player.Triangle}, status={policy.Status}, error={controller.Failure?.Message ?? controller.Error}");
        if (controller.Error is not null || !selected || !jumped || !returned || session.Host.Process.PlayerLocked || session.Host.Process.MenuDisabled || player.Triangle != 114 || policy.State.DestinationField != 353 || !SHA256.HashData(bytes).AsSpan().SequenceEqual(SHA256.HashData(File.ReadAllBytes(path))))
        {
            throw new InvalidOperationException("The current world-selector hop failed to execute its actual blocking jump and native control return.", controller.Failure);
        }
    }
}
