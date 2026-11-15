using System.Security.Cryptography;
using Memoria.FF7.Events;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;

internal static class NativeLineEntryAvoidanceTests
{
    public static void Run()
    {
        CheckInputAdmission();
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

    private static void CheckInputAdmission()
    {
        Ff7AutoPlayTarget selected = new("automatic", 1, 5, Ff7AutoPlayTargetKind.LineEnter, "entry", false, default);
        FieldScriptInputDemand demand = new("gate", "program", 2, 1, 0, FieldScriptInputKind.Pressed, 0x220, true, true);
        Ff7AutoPlayTarget candidate = new("gated", 2, 4, Ff7AutoPlayTargetKind.LineTouch, "gate", false, default) { EntryInputDemand = demand };
        Check(!Ff7AutoPlayPolicy.RequiresMovementLineAvoidance(candidate, selected, new()), "Ordinary movement was forbidden by an independent interaction-button gate.");
        Check(Ff7AutoPlayPolicy.RequiresMovementLineAvoidance(candidate with { EntryInputDemand = null }, selected, new()), "An unproved automatic transition lost contact avoidance.");
        Check(Ff7AutoPlayPolicy.RequiresMovementLineAvoidance(candidate with { EntryInputDemand = demand with { Mask = 0x40 } }, selected, new()), "A native run-button gate was treated as independent of movement.");
        Check(Ff7AutoPlayPolicy.RequiresMovementLineAvoidance(candidate, selected with { Kind = Ff7AutoPlayTargetKind.ActorTalk }, new()), "A planned interaction lost conservative neighbouring-line admission.");
        Check(Ff7AutoPlayPolicy.RequiresMovementLineAvoidance(candidate, selected with { EntryInputContract = "independent" }, new()), "An independently gated goal lost conservative neighbouring-line admission.");
        Check(Ff7AutoPlayPolicy.RequiresMovementLineAvoidance(candidate, selected with { EntryInputDemand = demand }, new()), "A typed input-gated goal lost conservative neighbouring-line admission.");
        Ff7AutoPlayTarget released = candidate with { EntryInputDemand = demand with { Kind = FieldScriptInputKind.Released } };
        Check(!Ff7AutoPlayPolicy.RequiresMovementLineAvoidance(released, selected, new()), "An impossible release edge blocked ordinary movement.");
        Check(Ff7AutoPlayPolicy.RequiresMovementLineAvoidance(released, selected, new(Held: 0x20)), "Releasing the previous native input could bypass a neighbouring gate.");
        Check(Ff7AutoPlayPolicy.RequiresMovementLineAvoidance(released with { EntryInputDemand = released.EntryInputDemand! with { Mapped = false } }, selected, new(RawHeld: 0x20)), "Raw input release admission used mapped state.");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
