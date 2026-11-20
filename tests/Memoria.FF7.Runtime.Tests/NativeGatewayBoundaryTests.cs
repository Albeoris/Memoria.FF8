using System.Diagnostics;
using System.Numerics;
using Memoria.FF7.Formats;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;

internal static class NativeGatewayBoundaryTests
{
    internal static void Run()
    {
        PcWalkmeshTriangle[] mesh =
        [
            new(0, new(0, 0, 0), new(100, 0, 0), new(0, -100, 0), 2, -1, -1),
            new(1, new(1000, 1000, 0), new(1000, 1100, 0), new(1100, 1000, 0), -1, -1, -1),
            new(2, new(0, 0, 0), new(0, 100, 0), new(100, 0, 0), -1, -1, 0)
        ];
        short[] vectors = new short[512];
        byte[] angles = new byte[129];
        for (int heading = 0; heading < 256; heading++)
        {
            vectors[heading * 2] = (short)Math.Round(Math.Sin(heading * Math.PI / 128) * 4096);
            vectors[heading * 2 + 1] = (short)Math.Round(Math.Cos(heading * Math.PI / 128) * 4096);
        }
        for (int index = 0; index < angles.Length; index++)
        {
            angles[index] = (byte)Math.Round(Math.Asin(index / 128.0) * 128 / Math.PI);
        }
        Ff7NativeMovement movement = new(mesh, vectors, angles);
        Ff7Actor actor = new() { Entity = 0, Slot = 0, Model = 0, PlayerControlled = true, Triangle = 2, X4096 = 30 * 4096, Y4096 = 6 * 4096, MoveSpeed = 2048, CollisionRadius = 20 };
        PcFieldGateway gateway = new(new(0, -10, 0), new(100, -10, 0), 0, 0, 112, 100, 0, 0);
        bool Ready(Ff7Actor current)
        {
            var step = movement.StepDirection(current, 0, [actor], [], false);
            return step.Moved && Ff7Session.CrossesGateway(gateway, current, step.Actor);
        }
        Vector3 target = Ff7AutoplayDriver.ReadGatewayCrossingTarget(gateway, new(30, 6, 0), new(30, -10, 0), actor.CollisionRadius);
        var first = Ff7AutoplayDriver.PreviewGoalInput(actor, target, 0, movement, [actor], [], false, (_, _) => true, Ready);
        var adjacent = movement.StepDirection(actor, 0, [actor], [], false);
        var terminal = movement.StepDirection(adjacent.Actor, 0, [actor], [], false);
        Check(first is { Horizontal: 0, Vertical: -1 } && adjacent.Moved && adjacent.Actor.Triangle == 0 && !Ff7Session.CrossesGateway(gateway, actor, adjacent.Actor) && terminal.Moved && Ff7Session.CrossesGateway(gateway, adjacent.Actor, terminal.Actor), "Gateway preparation pinned source triangle2 instead of taking the proved adjacent0 step before crossing.");
        Check(Ff7AutoplayDriver.PreviewGoalInput(actor, target, 0, movement, [actor], [], false, (_, _) => false, Ready) is null, "Constructive gateway preparation bypassed admission guards.");
        Check(Ff7AutoplayDriver.ReadPhysicalFailureGuideContext("source", [1, 0], [], "native-gateway-crossing:1") != Ff7AutoplayDriver.ReadPhysicalFailureGuideContext("source", [1, 0], [], "route-progress:1"), "Old route-progress exhaustion certified a different native gateway goal.");
        Console.WriteLine("PASS native gateway adjacent-triangle preparation, guarded crossing and distinct failure goal");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }

    internal static void RunOriginal()
    {
        string path = Environment.GetEnvironmentVariable("MEMORIA_FF7_GATEWAY_BOUNDARY_SAVE") ?? throw new InvalidOperationException("Select a native gateway fixture.");
        string game = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR") ?? throw new InvalidOperationException("Select resources.");
        byte[] bytes = File.ReadAllBytes(path);
        using MemoryStream stream = new(bytes, false);
        Ff7SessionSave save = CombinedSessionCodec.Read(stream).Ff7!;
        using Ff7Session session = Ff7Session.Restore(Ff7Installation.Discover(game), save);
        int gatewayIndex = Enumerable.Range(0, session.Field.Triggers.Gateways.Count).Single(index => session.Field.Triggers.Gateways[index].DestinationField != short.MaxValue);
        var gateway = session.Field.Triggers.Gateways[gatewayIndex];
        Ff7NativeGatewayDeparture? departure = null;
        session.NativeFieldDeparting += _ => departure = session.Capture().GatewayDeparture;
        using Ff7AutoPlayPolicy policy = new(session, (_, _) => throw new InvalidOperationException("Unexpected choice boundary."));
        int LineEntries() => policy.State.Coverage.Visits.SelectMany(visit => visit.Events).Where(item => item.Event.Id.StartsWith("line:", StringComparison.Ordinal)).Sum(item => item.Conditions?.ConsumedEntries.Count ?? 0);
        int before = LineEntries();
        Ff7AutoPlayController controller = new(session, policy.ChooseInput, advanceTick: () => session.AdvanceTick(advanceClock: false), chooseDialogueSelection: policy.ChooseDialogueSelection, readPlanningProgress: () => policy.WorkProgress, readFrontierProgress: () => policy.FrontierProgress);
        controller.Start();
        Stopwatch elapsed = Stopwatch.StartNew();
        TimeSpan previous = TimeSpan.Zero;
        while (controller.Running && session.FieldGeneration == save.FieldGeneration && elapsed.Elapsed < TimeSpan.FromSeconds(5))
        {
            TimeSpan now = elapsed.Elapsed;
            session.AdvanceClock(now - previous);
            previous = now;
            controller.Advance(1);
        }
        if (controller.Error is not null || departure is null || departure.Generation != save.FieldGeneration || departure.GatewayIndex != gatewayIndex || departure.Transition.Map != gateway.DestinationField || session.MapId != gateway.DestinationField || LineEntries() != before || !bytes.AsSpan().SequenceEqual(File.ReadAllBytes(path)))
        {
            throw new InvalidOperationException($"Original gateway continuation failed: {controller.Error}; callbacks={controller.Steps}, field={session.MapId}, active={policy.State.Active?.EventId}, departure={departure}");
        }
        Console.WriteLine($"PASS original gateway boundary: callbacks={controller.Steps}, destination={session.MapId}/{departure.Transition.Triangle}, gateway={departure.GatewayIndex}, elapsed={elapsed.Elapsed.TotalSeconds:F3}s; old refusal retained, line credit/source unchanged");
    }

    internal static void RunSemanticOriginal()
    {
        string path = Environment.GetEnvironmentVariable("MEMORIA_FF7_GATEWAY_SEMANTIC_SAVE") ?? throw new InvalidOperationException("Select a native gateway semantic fixture.");
        string game = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR") ?? throw new InvalidOperationException("Select resources.");
        int sourceMap = int.Parse(Environment.GetEnvironmentVariable("MEMORIA_FF7_GATEWAY_SEMANTIC_MAP")!);
        int gatewayIndex = int.Parse(Environment.GetEnvironmentVariable("MEMORIA_FF7_GATEWAY_SEMANTIC_INDEX")!);
        byte[] bytes = File.ReadAllBytes(path);
        using MemoryStream stream = new(bytes, false);
        using Ff7Session session = Ff7Session.Restore(Ff7Installation.Discover(game), CombinedSessionCodec.Read(stream).Ff7!);
        using Ff7AutoPlayPolicy policy = new(session, (_, _) => throw new InvalidOperationException("Unexpected choice boundary."));
        policy.State.FrontierProgressKey = null;
        policy.State.FrontierIdleSteps = 0;
        Ff7NativeGatewayDeparture? departure = null;
        session.NativeFieldDeparting += _ =>
        {
            if (session.MapId == sourceMap)
            {
                departure = session.Capture().GatewayDeparture;
            }
        };
        Ff7AutoPlayController controller = new(session, policy.ChooseInput, advanceTick: () => session.AdvanceTick(advanceClock: false), chooseDialogueSelection: policy.ChooseDialogueSelection, readPlanningProgress: () => policy.WorkProgress, readFrontierProgress: () => policy.FrontierProgress);
        controller.Start();
        Stopwatch elapsed = Stopwatch.StartNew();
        int polls = 0;
        while (controller.Running && departure is null && polls < 100000 && elapsed.Elapsed < TimeSpan.FromSeconds(30))
        {
            controller.Advance(1);
            polls++;
        }
        Console.WriteLine($"Native gateway semantic continuation: callbacks={controller.Steps}, polls={polls}, elapsed={elapsed.Elapsed.TotalSeconds:F3}s, map={session.MapId}, tick={session.Machine.Tick}, active={policy.State.Active?.EventId}, physical={controller.PlanningProgress.PhysicalNodes}, departure={departure}, error={controller.Error}");
        Check(controller.Error is null && departure?.GatewayIndex == gatewayIndex, "Ordinary gateway navigation did not reach the selected native departure.");
        Check(bytes.AsSpan().SequenceEqual(File.ReadAllBytes(path)), "The external standalone source changed.");
        Console.WriteLine("PASS ordinary native gateway semantic approach and selected admission");
    }
}
