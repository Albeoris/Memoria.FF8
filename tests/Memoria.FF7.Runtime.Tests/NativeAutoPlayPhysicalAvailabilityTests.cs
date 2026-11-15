using System.Numerics;
using System.Text.Json;
using Memoria.FF7.Formats;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;

internal static class NativeAutoPlayPhysicalAvailabilityTests
{
    public static void Run()
    {
        PcWalkmeshTriangle[] mesh =
        [
            new(0, Vector3.Zero, new(0, 100, 0), new(100, 0, 0), 1, -1, -1),
            new(1, new(100, 0, 0), new(0, 100, 0), new(100, 100, 0), 0, -1, -1),
            new(2, new(0, 0, 300), new(0, 100, 300), new(100, 0, 300), -1, -1, -1)
        ];
        byte[] blocked = [0];
        Ff7Actor player = new() { Slot = 0, Entity = 0, Model = 0, Triangle = 0, CollisionRadius = 30 };
        Ff7Actor target = new() { Slot = 1, Entity = 1, Model = 1, Triangle = 2, Z4096 = 300 * 4096, TalkRadius = 80 };
        string before = JsonSerializer.Serialize(new { mesh, blocked, player, target });
        string original = Fingerprint(player, target, blocked);
        Check(original == Fingerprint(player with { Triangle = 1, X4096 = 90 * 4096, Y4096 = 90 * 4096, Direction = 128 }, target, blocked), "Ordinary movement within one native component invalidated physical absence.");
        Check(original != Fingerprint(player with { Triangle = 2 }, target, blocked), "Entering another native floor component failed to refresh physical absence.");
        Check(original != Fingerprint(player, target, [2]), "A blocked-triangle door change failed to refresh physical absence.");
        Check(original != Fingerprint(player, target with { X4096 = 4096 }, blocked), "A moved talk target failed to refresh physical absence.");
        Check(original != Fingerprint(player, target with { TalkEnabled = false }, blocked), "A native talk-admission change failed to refresh physical absence.");
        Check(original != Fingerprint(player with { CollisionRadius = 31 }, target, blocked), "A player interaction-envelope change failed to refresh physical absence.");
        Check(before == JsonSerializer.Serialize(new { mesh, blocked, player, target }), "Reading physical absence mutated authored geometry, blocking or actors.");
        Ff7Actor blocker = target with { Entity = 3, Slot = 2, X4096 = 50 * 4096 };
        Ff7Actor ambient = target with { Entity = 4, Slot = 3, X4096 = 500 * 4096 };
        string occluded = Ff7AutoplayDriver.ReadPhysicalAvailabilityFingerprint("authored-program", mesh, blocked, player, target, [3], [blocker, ambient]);
        Check(occluded != Ff7AutoplayDriver.ReadPhysicalAvailabilityFingerprint("authored-program", mesh, blocked, player, target, [3], [blocker with { X4096 = 51 * 4096 }, ambient]), "A proved competing actor's movement did not refresh physical absence.");
        Check(occluded == Ff7AutoplayDriver.ReadPhysicalAvailabilityFingerprint("authored-program", mesh, blocked, player, target, [3], [blocker, ambient with { X4096 = 501 * 4096 }]), "An unrelated ambient actor invalidated a narrow physical guard.");
        Check(occluded != Ff7AutoplayDriver.ReadPhysicalAvailabilityFingerprint("authored-program", mesh, blocked, player, target, [3], [ambient]), "A removed competing actor failed to refresh physical absence.");
        Check(original == Ff7AutoplayDriver.ReadPhysicalAvailabilityFingerprint("authored-program", mesh, blocked, player, target, [], [blocker with { X4096 = 51 * 4096 }, ambient]), "A pure height/component absence acquired unrelated competing actor dependencies.");
        Ff7Line line = new(1, 18, 0, 0, 300, 100, 100, 300);
        Ff7AutoPlayTarget lineTarget = new("line:1:entity:18:script:4", 18, 4, Ff7AutoPlayTargetKind.LineTouch, "conditions", false, new(50, 50, 300));
        string lineGuard = Ff7AutoplayDriver.ReadPhysicalAvailabilityFingerprint("authored-program", mesh, blocked, player, lineTarget, [line], []);
        Check(lineGuard == Ff7AutoplayDriver.ReadPhysicalAvailabilityFingerprint("authored-program", mesh, blocked, player with { Triangle = 1, X4096 = 90 * 4096 }, lineTarget, [line], []), "A LINE path guard changed within the same native component.");
        Check(lineGuard != Ff7AutoplayDriver.ReadPhysicalAvailabilityFingerprint("authored-program", mesh, blocked, player with { Triangle = 2 }, lineTarget, [line], []), "Entering a LINE target's component did not refresh an absent path.");
        Check(lineGuard != Ff7AutoplayDriver.ReadPhysicalAvailabilityFingerprint("authored-program", mesh, blocked, player, lineTarget, [line with { X1 = 1 }], []), "A moved native LINE failed to refresh an absent path.");
        Check(lineGuard != Ff7AutoplayDriver.ReadPhysicalAvailabilityFingerprint("authored-program", mesh, blocked, player, lineTarget, [line with { Enabled = 0 }], []), "Changed LINE admission failed to refresh an absent path.");
        PcFieldGateway gateway = new(new(0, 0, 300), new(100, 100, 300), 10, 20, 2, 3, 0, 0);
        Ff7AutoPlayTarget gatewayTarget = lineTarget with { Id = "gateway:0", Entity = -1, Script = -1, Kind = Ff7AutoPlayTargetKind.Gateway, GatewayIndex = 0 };
        string gatewayGuard = Ff7AutoplayDriver.ReadPhysicalAvailabilityFingerprint("authored-program", mesh, blocked, player, gatewayTarget, [], [gateway]);
        Check(gatewayGuard != Ff7AutoplayDriver.ReadPhysicalAvailabilityFingerprint("authored-program", mesh, [2], player, gatewayTarget, [], [gateway]), "Changed native blocking failed to refresh a gateway path absence.");
        Check(gatewayGuard != Ff7AutoplayDriver.ReadPhysicalAvailabilityFingerprint("authored-program", mesh, blocked, player, gatewayTarget, [], [gateway with { A = new(1, 0, 300) }]), "A changed native gateway segment failed to refresh its absence.");
        Ff7AutoPlayNavigationUnavailableException pathAbsence = new(-1, "absent authored path", destination: lineTarget.Position, destinationTriangle: 2);
        Check(pathAbsence is { Entity: -1, DestinationTriangle: 2 } && pathAbsence.Destination == lineTarget.Position && pathAbsence.BlockingEntities.Count == 0, "A nonactor path absence lost its exact destination proof.");
        CheckNativeOcclusionPredicate();

        string Fingerprint(Ff7Actor source, Ff7Actor destination, byte[] blocking)
        {
            return Ff7AutoplayDriver.ReadPhysicalAvailabilityFingerprint("authored-program", mesh, blocking, source, destination);
        }
    }

    private static void CheckNativeOcclusionPredicate()
    {
        string? root = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR");
        string? path = Environment.GetEnvironmentVariable("MEMORIA_FF7_DISCOVERY_SAVE");
        if (string.IsNullOrWhiteSpace(root) || string.IsNullOrWhiteSpace(path))
        {
            Console.WriteLine("SKIP native occlusion predicate: configure original resources and a standalone field fixture.");
            return;
        }
        using FileStream stream = File.OpenRead(path);
        Ff7SessionSave saved = CombinedSessionCodec.Read(stream).Ff7 ?? throw new InvalidDataException("An occlusion fixture requires an FFVII field session.");
        using Ff7Session session = Ff7Session.Restore(Ff7Installation.Discover(root), saved);
        if (session.Host.Process.PlayerLocked || session.Host.Process.Join is not null)
        {
            Console.WriteLine("SKIP native occlusion predicate: the supplied field fixture cannot currently accept actor talk.");
            return;
        }
        string before = JsonSerializer.Serialize(session.Capture());
        Ff7Actor player = new() { Entity = 0, Slot = 0, Model = 0, CollisionRadius = 30, Direction = session.Host.Movement!.Direction(100, 0) };
        Ff7Actor target = new() { Entity = 2, Slot = 2, Model = 2, X4096 = 100 * 4096, TalkRadius = 200 };
        Ff7Actor blocker = target with { Entity = 1, Slot = 1, Model = 1 };
        Check(session.PreviewTalkTarget(player, [target])?.Entity == target.Entity, "The target-only projection did not preserve native talk admission.");
        Check(session.PreviewTalkTarget(player, [target, blocker])?.Entity == blocker.Entity, "A native earlier-slot actor did not win its proved equal-angle selection.");
        Check(session.PreviewTalkTarget(player, [target with { Z4096 = 300 * 4096 }]) is null, "An out-of-height target was incorrectly classified as competitor occlusion.");
        Check(before == JsonSerializer.Serialize(session.Capture()), "Target-only occlusion projection changed authoritative native state.");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
