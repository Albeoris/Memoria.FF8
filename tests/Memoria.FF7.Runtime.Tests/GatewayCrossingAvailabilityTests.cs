using System.Diagnostics;
using System.Numerics;
using System.Text.Json;
using Memoria.FF7.Formats;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;

internal static class GatewayCrossingAvailabilityTests
{
    public static void Run()
    {
        PcWalkmeshTriangle[] mesh =
        [
            new(0, new(-100, -100, 0), new(0, 100, 0), new(0, -100, 0), -1, 1, -1),
            new(1, new(0, 100, 0), new(100, 100, 0), new(0, -100, 0), -1, -1, 0)
        ];
        Ff7Actor player = new() { Slot = 0, Entity = 0, Model = 0, Triangle = 0, X4096 = -50 << 12, Y4096 = -20 << 12, CollisionRadius = 10 };
        PcFieldGateway gateway = new(new(9, -50, 0), new(9, 50, 0), 0, 0, 0, 2, 0, 0);
        bool? Read(byte mask, PcFieldGateway? gate = null, Ff7Actor? actor = null, PcWalkmeshTriangle[]? faces = null) => Ff7AutoPlayDiscovery.ReadGatewayCrossingAvailability(faces ?? mesh, [mask], actor ?? player, gate ?? gateway, 16);
        string before = JsonSerializer.Serialize(new { mesh, player, gateway });
        Check(Read(2) == false, "A blocked native crossing corridor remained an intrinsic gateway goal.");
        Check(Read(0) is null, "Opening the corridor retained its old current-topology exclusion.");
        Check(Read(2, gateway with { A = new(0, -50, 0), B = new(0, 50, 0) }) is null, "An on-line endpoint was excluded without a strict sign proof.");
        Check(Read(0, gateway with { A = new(-50, -50, 0), B = new(-50, 50, 0) }) is null, "A straddling reachable region was excluded.");
        Check(Read(2, actor: player with { X4096 = 200 << 12 }) is null, "An off-floor current pose authenticated a floor-bounds proof.");
        Check(Read(2, actor: player with { Z4096 = 1 }) is null, "An unsupported height authenticated floor confinement.");
        Check(Read(2, gateway with { B = gateway.B with { Z = 1 } }) is null, "A nonconstant gateway Z projection authenticated an unsupported distance bound.");
        Check(Ff7AutoPlayDiscovery.ReadGatewayCrossingAvailability(mesh, [], player, gateway, 16) is null, "An incomplete native mask authenticated reachability.");
        Check(Read(2, actor: player with { CollisionRadius = int.MaxValue }) is null, "Overflowing native distance arithmetic authenticated a negative proof.");
        PcWalkmeshTriangle[] extreme = [new(0, new(-32760, -32760, 0), new(32760, 32760, 0), new(32760, -32760, 0), -1, -1, -1)];
        Check(Read(0, actor: player with { X4096 = 0, Y4096 = -4096 }, faces: extreme) is null, "Overflowing native face determinants authenticated floor confinement.");
        Check(Ff7NativeMovement.ReadMaximumPlanarStep(0) == 0 && Ff7NativeMovement.ReadMaximumPlanarStep(4096) == 16 && Ff7NativeMovement.ReadMaximumPlanarStep(4097) == 17, "Q12 native axis displacement lost its ceiling bound.");
        Check(before == JsonSerializer.Serialize(new { mesh, player, gateway }), "Gateway eligibility changed native geometry or actors.");
        RunOriginal();
        Console.WriteLine("PASS native current-topology gateway crossing availability");
    }

    private static void RunOriginal()
    {
        string? root = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR");
        string? path = Environment.GetEnvironmentVariable("MEMORIA_FF7_GATEWAY_AVAILABILITY_SAVE");
        if (string.IsNullOrWhiteSpace(root) || string.IsNullOrWhiteSpace(path))
        {
            Console.WriteLine("SKIP external gateway availability continuation.");
            return;
        }
        byte[] source = File.ReadAllBytes(path);
        using MemoryStream stream = new(source, writable: false);
        Ff7SessionSave saved = CombinedSessionCodec.Read(stream).Ff7!;
        using Ff7Session session = Ff7Session.Restore(Ff7Installation.Discover(root), saved);
        Ff7AutoPlayDiscovery discovery = new(session);
        Ff7AutoPlayObservation observation = discovery.Observe();
        Check(!observation.Events.Any(target => target.Id == saved.AutoPlay!.Active!.EventId), "The proved-empty current gateway crossing region remained offered.");
        string conditionHistory = JsonSerializer.Serialize(saved.AutoPlay!.Coverage.Visits.Single(visit => visit.Visit.Key == saved.AutoPlay.Active!.VisitId).Events.Single(item => item.Event.Id == saved.AutoPlay.Active!.EventId).Conditions);
        using Ff7AutoPlayPolicy policy = new(session, (_, _) => throw new InvalidOperationException("Unexpected choice during bounded gateway replan."));
        Ff7AutoPlayController controller = new(session, policy.ChooseInput, chooseDialogueSelection: policy.ChooseDialogueSelection, advanceTick: () => session.AdvanceTick(advanceClock: false), readPlanningProgress: () => policy.WorkProgress);
        controller.Start();
        Stopwatch elapsed = Stopwatch.StartNew();
        int polls = 0;
        while (controller.Running && elapsed.Elapsed < TimeSpan.FromSeconds(3) && polls++ < 32)
        {
            controller.Advance(1);
            if (policy.State.Active is { } active && active.EventId != saved.AutoPlay.Active!.EventId)
            {
                break;
            }
        }
        Console.WriteLine($"External gateway eligibility: {elapsed.Elapsed.TotalSeconds:F3}s, {polls} polls, {controller.Steps} callbacks, active {policy.State.Active?.EventId}, physical {controller.PlanningProgress.PhysicalNodes}, error {controller.Error}");
        Check(controller.Error is null && policy.State.Active is { } selected && selected.EventId != saved.AutoPlay.Active!.EventId, "Ordinary policy did not replan away from its currently impossible gateway.");
        Check(conditionHistory == JsonSerializer.Serialize(policy.State.Coverage.Visits.Single(visit => visit.Visit.Key == saved.AutoPlay.Active!.VisitId).Events.Single(item => item.Event.Id == saved.AutoPlay.Active!.EventId).Conditions), "Derived availability fabricated gateway execution history.");
        Check(source.AsSpan().SequenceEqual(File.ReadAllBytes(path)), "The standalone source changed.");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
