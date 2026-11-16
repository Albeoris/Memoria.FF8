using System.Security.Cryptography;
using Memoria.FF7.Events;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;

internal static class NativeLineEntryAvoidanceTests
{
    public static void Run()
    {
        CheckOriginalSlopedBoundary();
        string? root = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR");
        string? path = Environment.GetEnvironmentVariable("MEMORIA_FF7_LINE_ENTRY_SAVE");
        if (string.IsNullOrWhiteSpace(root) || string.IsNullOrWhiteSpace(path))
        {
            Console.WriteLine("SKIP original line-entry avoidance: configure resources and an external standalone movement fixture.");
            return;
        }
        byte[] source = File.ReadAllBytes(path);
        using MemoryStream stream = new(source, writable: false);
        Ff7SessionSave saved = CombinedSessionCodec.Read(stream).Ff7!;
        using Ff7Session session = Ff7Session.Restore(Ff7Installation.Discover(root), saved);
        using Ff7AutoPlayPolicy policy = new(session, (_, _) => throw new InvalidOperationException("Unexpected choice before the native exit."));
        Ff7AutoplayDriver driver = new(session);
        int origin = session.MapId;
        int callbacks = 0;
        Ff7AutoPlayMovementFailure? failedRegion = null;
        bool departed = false;
        while (session.MapId == origin && session.Battle is null && callbacks < 256)
        {
            Ff7AutoplayInput input = policy.ChooseInput(driver);
            failedRegion ??= policy.State.NavigationGuards.Values.FirstOrDefault(guard => guard.EventId == policy.State.Active?.EventId && guard.MovementFailure is not null)?.MovementFailure;
            driver.Apply(input);
            session.AdvanceTick(advanceClock: false);
            callbacks++;
            Ff7Actor moved = session.Host.Process.Actors.Single(actor => actor.PlayerControlled);
            if (failedRegion is not null && (moved.X4096 < failedRegion.MinX || moved.X4096 > failedRegion.MaxX || moved.Y4096 < failedRegion.MinY || moved.Y4096 > failedRegion.MaxY))
            {
                departed = true;
                break;
            }
        }
        if (!departed || session.MapId != origin || policy.State.Active is not { Accepted: false })
        {
            Ff7Actor player = session.Host.Process.Actors.Single(actor => actor.PlayerControlled);
            throw new InvalidOperationException($"Normal autoplay did not leave the retained failed approach: callbacks={callbacks}, tick={session.Host.Process.Tick}, triangle={player.Triangle}, xyz={player.X4096},{player.Y4096},{player.Z4096}, pending={session.HasPendingTick}, status={policy.Status}.");
        }
        if (!SHA256.HashData(source).SequenceEqual(SHA256.HashData(File.ReadAllBytes(path))))
        {
            throw new InvalidOperationException("The normal-policy regression changed its standalone source.");
        }
        Console.WriteLine($"PASS original normal-policy departure from retained failed approach after {callbacks} callbacks");
    }

    private static void CheckOriginalSlopedBoundary()
    {
        string? root = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR");
        string? path = Environment.GetEnvironmentVariable("MEMORIA_FF7_SLOPED_LINE_SAVE");
        if (root is null || path is null)
        {
            return;
        }
        byte[] bytes = File.ReadAllBytes(path);
        using MemoryStream input = new(bytes, writable: false);
        Ff7SessionSave source = CombinedSessionCodec.Read(input).Ff7!;
        using Ff7Session session = Ff7Session.Restore(Ff7Installation.Discover(root), source);
        using Ff7AutoPlayPolicy policy = new(session, (_, _) => throw new InvalidOperationException("Unexpected choice during the bounded native contact approach."));
        policy.State.FrontierProgressKey = null;
        policy.State.FrontierIdleSteps = 0;
        bool interrupted = false;
        session.NativeTickCompleted += tick =>
        {
            interrupted |= policy.State.Active is { Kind: Ff7AutoPlayTargetKind.ActorTalk, Entity: 6 } && tick.TransitionOwner?.Entity == 27;
        };
        Ff7AutoPlayController controller = new(session, policy.ChooseInput, chooseDialogueSelection: policy.ChooseDialogueSelection);
        controller.Start();
        bool attempted = false;
        bool finished = false;
        for (int step = 0; step < 800 && controller.Running && !finished && !interrupted; step++)
        {
            controller.Advance(1);
            bool active = policy.State.Active is { Kind: Ff7AutoPlayTargetKind.ActorTalk, Entity: 6 };
            attempted |= active;
            finished = attempted && !active;
        }
        Check(controller.Failure is null && attempted && finished && !interrupted, $"A native sloped contact approach entered its unrelated neighbouring LINE or never resolved: {controller.Error}");
        Check(bytes.SequenceEqual(File.ReadAllBytes(path)), "Sloped contact validation changed its standalone source.");
        Console.WriteLine($"PASS original sloped contact approach resolves without the unrelated LINE transition after {controller.Steps} ordinary callbacks; source unchanged.");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
