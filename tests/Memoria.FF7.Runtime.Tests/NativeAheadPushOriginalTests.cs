using System.Diagnostics;
using System.Reflection;
using System.Security.Cryptography;
using Memoria.FF7.Events;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;

internal static class NativeAheadPushOriginalTests
{
    internal static void Run()
    {
        Ff7Installation installation = Ff7Installation.Discover(Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR")!);
        string path = Environment.GetEnvironmentVariable("MEMORIA_FF7_AHEAD_PUSH_SAVE") ?? throw new InvalidOperationException("Select an intermediate gateway fixture.");
        byte[] bytes = File.ReadAllBytes(path);
        using MemoryStream input = new(bytes, writable: false);
        using Ff7Session session = Ff7Session.Restore(installation, CombinedSessionCodec.Read(input).Ff7!);
        using Ff7AutoPlayPolicy policy = new(session, (_, _) => throw new InvalidOperationException("Unexpected choice before the native cliff crossing."));
        Ff7AutoPlayObservation observation = new Ff7AutoPlayDiscovery(session).Observe();
        Ff7AutoPlayTarget gateway = observation.Events.Single(target => target.Id == "gateway:0");
        string native = Ff7AutoplayDriver.ReadNativeRouteFingerprint(session);
        var method = typeof(Ff7AutoPlayPolicy).GetMethod("ReadKnownNavigationPrerequisites", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var prerequisites = (Ff7AutoPlayTarget[])method.Invoke(policy, [observation, gateway])!;
        if (!prerequisites.Any(target => target is { Entity: 7, Script: 2, Kind: Ff7AutoPlayTargetKind.LinePush }) || native != Ff7AutoplayDriver.ReadNativeRouteFingerprint(session))
        {
            throw new InvalidOperationException("The real blocked Push and proved intermediate gateway were not qualified without native mutation.");
        }
        bool jumped = false;
        session.Host.NativeOperationExecuted += (request, _) => jumped |= request is { Entity: 12, Priority: 6, Command: FieldActorJump { X: 34, Y: 1152, Triangle: 53, Duration: 17 } };
        bool returned = false;
        session.NativeTickCompleted += tick => returned |= tick.ScriptReturns.Any(owner => owner.Entity == 7 && owner.Priority == 1);
        bool selectedPrerequisite = false;
        Ff7AutoplayInput Choose(Ff7AutoplayDriver driver)
        {
            Ff7AutoplayInput action = policy.ChooseInput(driver);
            selectedPrerequisite |= policy.State.Active is { Entity: 7, Script: 2, Kind: Ff7AutoPlayTargetKind.LinePush };
            if (!selectedPrerequisite && policy.State.Active?.Kind == Ff7AutoPlayTargetKind.Gateway && (action.Horizontal != 0 || action.Vertical != 0))
            {
                throw new InvalidOperationException("An admitted walk prefix ran before the proved current Push prerequisite.");
            }
            return action;
        }
        Ff7AutoPlayController controller = new(session, Choose, advanceTick: () => session.AdvanceTick(advanceClock: false), chooseDialogueSelection: policy.ChooseDialogueSelection, readPlanningProgress: () => policy.WorkProgress, readFrontierProgress: () => policy.FrontierProgress);
        controller.Start();
        for (int callback = 0; callback < 8 && !selectedPrerequisite && controller.Error is null; callback++)
        {
            controller.Advance(1);
        }
        if (!selectedPrerequisite)
        {
            throw new InvalidOperationException($"An admitted short walk prefix preempted the proved forward Push prerequisite: active={policy.State.Active?.EventId}, status={policy.Status}, input={prerequisites.First(target => target.Entity == 7).EntryInputContract}.");
        }
        Stopwatch timer = Stopwatch.StartNew();
        while (timer.Elapsed.TotalSeconds < 30 && controller.Error is null && !(jumped && returned && !session.Host.Process.PlayerLocked))
        {
            controller.Advance(1);
        }
        while (jumped && returned && timer.Elapsed.TotalSeconds < 30 && controller.Error is null && session.MapId == 459 && session.Battle is null)
        {
            controller.Advance(1);
        }
        Ff7Actor player = session.Host.Process.Actors.Single(actor => actor.PlayerControlled);
        Console.WriteLine($"Native ahead Push: callbacks={controller.Steps}, seconds={timer.Elapsed.TotalSeconds:F3}, field={session.MapId}, tick={session.Machine.Tick}, jumped={jumped}, rootReturned={returned}, triangle={player.Triangle}, battle={session.Battle?.Formation.FormationId}, active={policy.State.Active?.EventId}, destination={policy.State.DestinationField}, status={policy.Status}, error={controller.Failure?.Message ?? controller.Error}");
        if (controller.Error is not null || !jumped || !returned || session.Host.Process.PlayerLocked || session.MapId == 459 && player.Triangle == 76 || policy.State.DestinationField != 460 || !SHA256.HashData(bytes).AsSpan().SequenceEqual(SHA256.HashData(File.ReadAllBytes(path))))
        {
            throw new InvalidOperationException("The proved intermediate hop did not execute its genuine Push/JUMP/control-return prerequisite.", controller.Failure);
        }
    }
}
