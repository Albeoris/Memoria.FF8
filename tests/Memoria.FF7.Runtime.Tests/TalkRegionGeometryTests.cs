using System.Numerics;
using System.Text.Json;
using Memoria.FF7.Formats;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;

internal static class TalkRegionGeometryTests
{
    internal static void Run()
    {
        PcWalkmeshTriangle[] mesh =
        [
            new(0, new(-205, -200, 0), new(0, 200, 0), new(0, -200, 0), 1, -1, -1),
            new(1, new(-205, -200, 0), new(-205, 200, 0), new(0, 200, 0), -1, -1, 0)
        ];
        short[] vectors = new short[512];
        for (int direction = 0; direction < 256; direction++)
        {
            vectors[direction * 2] = (short)Math.Round(Math.Sin(direction * Math.PI / 128) * 4096);
            vectors[direction * 2 + 1] = (short)Math.Round(Math.Cos(direction * Math.PI / 128) * 4096);
        }
        byte[] angles = Enumerable.Range(0, 129).Select(index => (byte)Math.Round(Math.Asin(index / 128d) * 128 / Math.PI)).ToArray();
        Ff7NativeMovement movement = new(mesh, vectors, angles);
        byte[] headings = [0, 32, 64, 96, 128, 160, 192, 224];
        Ff7Actor player = new() { Slot = 0, Entity = 0, Model = 0, PlayerControlled = true, CollisionRadius = 46, Triangle = 0 };
        Ff7Actor target = new() { Slot = 1, Entity = 1, Model = 1, X4096 = 121 * 4096, Z4096 = -168 * 4096, TalkRadius = 109 };
        List<Ff7Actor> admitted = [];
        foreach (int triangle in new[] { 0, 1 })
        {
            var points = Ff7AutoplayDriver.ReadTalkRegionPoints(mesh, new HashSet<int> { 0, 1 }, triangle, new(121, 0, -168), 155, 46, headings.SelectMany(heading => movement.ReadFootprintOffsets(heading, 46)).ToArray());
            foreach (Vector3 point in points)
            {
                Ff7Actor projected = player with { Triangle = triangle, X4096 = (int)(point.X * 4096), Y4096 = (int)(point.Y * 4096), Z4096 = (int)(point.Z * 4096) };
                Ff7Actor? contact = Ff7AutoplayDriver.ReadNativeTalkFloorContact(projected, movement, [projected, target], [0], false, headings, actual => Ff7Session.SelectNativeTalkTarget(actual, [target], movement)?.Entity == target.Entity);
                if (contact is not null)
                {
                    admitted.Add(contact);
                }
            }
        }
        Check(admitted.Count > 0, "The finite boundary arrangement missed a narrow cross-ledge talk region.");
        Ff7Actor fixedFacing = player with { MovementFacingDisabled = 1, Direction = 192 };
        Ff7Actor fixedProjection = Ff7AutoplayDriver.ProjectTalkRegionContact(fixedFacing, new(-33, -17, 0), 0, target, movement);
        Check(fixedProjection.Direction == 192 && Ff7AutoplayDriver.ReadNativeTalkFloorContact(fixedProjection, movement, [fixedProjection, target], [0], false, headings, actual => Ff7Session.SelectNativeTalkTarget(actual, [target], movement)?.Entity == target.Entity) is null, "Contact projection invented a facing unavailable to the fixed-facing actor.");
        Ff7Actor turnable = Ff7AutoplayDriver.ProjectTalkRegionContact(player, new(-33, -17, 0), 0, target, movement);
        Check(turnable.Direction == movement.Direction(154, 17), "An ordinary contact lost its derived native facing.");
        Check(admitted.All(actor => (actor.X4096 >> 12) < 0 && Math.Abs(actor.Z4096 >> 12) < 1), "A contact routed to the NPC center or invented its floor height.");
        Check(Ff7AutoplayDriver.ReadTalkRegionPoints(mesh, new HashSet<int> { 0, 1 }, 0, new(121, 0, 900), 155, 46, []).Count == 0, "A talk region ignored its native height slab.");
        PcWalkmeshTriangle[] directed = [new(0, Vector3.Zero, Vector3.UnitX, Vector3.UnitY, 1, -1, -1), new(1, Vector3.UnitX, new(1, 1, 0), Vector3.UnitY, -1, -1, -1)];
        Check(Ff7AutoplayDriver.ReadApproachRoutes(directed, [0], 0, 1).Previous[0] == 1 && Ff7AutoplayDriver.ReadApproachRoutes(directed, [0], 1, 0).Previous[1] < 0, "Route discovery assumed reciprocal native portals.");
        var pulled = Ff7AutoplayDriver.PullPortalString([(new(0, 0, 0), new(0, 0, 0)), (new(1, 1, 0), new(1, -1, 0)), (new(2, 0, 0), new(2, 0, 0))]);
        Check(pulled.Count == 2 && pulled[1] == new Vector3(2, 0, 0), "An unobstructed portal corridor retained centroid zigzags.");
        var bent = Ff7AutoplayDriver.PullPortalString([(new(0, 0, 0), new(0, 0, 0)), (new(1, 2, 0), new(1, -1, 0)), (new(2, 2, 0), new(2, 1, 0)), (new(1, 3, 0), new(3, 3, 0)), (new(2, 4, 0), new(2, 4, 0))]);
        Check(bent.Count == 3 && bent[1] == new Vector3(2, 2, 0) && bent[2] == new Vector3(2, 4, 0), "The funnel chose the wrong side of a bent portal corridor.");
        Console.WriteLine($"PASS finite talk-region boundary/lattice contacts ({admitted.Count}), native facing/footprint, height slab, directed portals and funnel.");
    }

    internal static void RunOriginal()
    {
        string path = Environment.GetEnvironmentVariable("MEMORIA_FF7_GEOMETRIC_ROUTE_SAVE") ?? throw new InvalidOperationException("Select an external geometric-route fixture.");
        string root = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR") ?? throw new InvalidOperationException("Original resources are required.");
        int entity = int.Parse(Environment.GetEnvironmentVariable("MEMORIA_FF7_GEOMETRIC_ROUTE_ENTITY") ?? "-1");
        int? gateway = Environment.GetEnvironmentVariable("MEMORIA_FF7_GEOMETRIC_ROUTE_GATEWAY") is { } gatewayText ? int.Parse(gatewayText) : null;
        Check(entity >= 0 || gateway is not null, "Select an external actor or gateway target.");
        byte[] bytes = File.ReadAllBytes(path);
        using MemoryStream stream = new(bytes);
        var envelope = CombinedSessionCodec.Read(stream);
        Ff7SessionSave saved = envelope.Ff7!;
        using Ff7Session session = Ff7Session.Restore(Ff7Installation.Discover(root), saved);
        string before = JsonSerializer.Serialize(session.Capture());
        Ff7AutoplayDriver driver = new(session);
        Ff7Actor player = session.Host.Process.Actors.Single(actor => actor.PlayerControlled);
        int contactCount = gateway is { } gatewayIndex ? Ff7AutoplayDriver.DiscoverGatewayContacts(session.Field.Walkmesh, session.Host.Process.BlockedTriangles, player, session.Field.Triggers.Gateways[gatewayIndex]).Count : driver.DiscoverTalkContacts(session.Host.GetActor(entity), player).Contacts.Count;
        Check(contactCount > 0, "The actual geometric fixture still has an empty native contact catalog.");
        Check(before == JsonSerializer.Serialize(session.Capture()), "Contact geometry advanced or modified the native source.");
        Console.WriteLine($"PASS original zero-callback contact geometry: {contactCount} native-valid contacts, source unchanged.");
        if (Environment.GetEnvironmentVariable("MEMORIA_FF7_ROUTE_GRAPH") == "1")
        {
            if (Environment.GetEnvironmentVariable("MEMORIA_FF7_GRAPH_AFTER_NEUTRAL") == "1")
            {
                driver.Apply(default);
                session.AdvanceTick(false);
            }
            string graphBefore = JsonSerializer.Serialize(session.Capture());
            Ff7AutoPlayObservation observation = new Ff7AutoPlayDiscovery(session).Observe();
            Ff7NativeGraphDebt? debt = Ff7AutoplayDriver.ReadRootNativeDebt(session, observation.Events);
            Console.WriteLine($"SOURCE graph debt: {JsonSerializer.Serialize(debt is null ? null : new { Roots = debt.Roots.Select(item => item.Id), debt.Pending, debt.Owners, debt.LadderRequest })}");
            Ff7AutoPlayTarget target = observation.Events.Single(item => gateway is { } index ? item.Kind == Ff7AutoPlayTargetKind.Gateway && item.GatewayIndex == index : item.Kind == Ff7AutoPlayTargetKind.ActorTalk && item.Entity == entity);
            Ff7NativeRouteProbe probe = new() { SourceRelease = (branch, actual) => Ff7AutoPlayPolicy.CanReleaseNativeSource(saved.AutoPlay!, observation, target, branch, actual) };
            var graphWall = System.Diagnostics.Stopwatch.StartNew();
            using var graph = Ff7AutoplayDriver.EnumerateFairNativeCandidates(driver.EnumerateNativeRouteModes(target, observation.Events, probe), () => probe.Result is not null).GetEnumerator();
            while (graphWall.Elapsed < TimeSpan.FromSeconds(10) && graph.MoveNext())
            {
            }
            Console.WriteLine($"GRAPH bounded source release: wall={graphWall.Elapsed.TotalSeconds:F3}s, callbacks={driver.DetachedNativeCallbacks}, proof={probe.Result?.Steps.Length}, released={probe.Result?.SourceReleased}, phase={probe.Progress}.");
            Check(graphBefore == JsonSerializer.Serialize(session.Capture()) && bytes.SequenceEqual(File.ReadAllBytes(path)), "The bounded graph changed its source session/file.");
            if (Environment.GetEnvironmentVariable("MEMORIA_FF7_ROUTE_REPLAY") == "1" && probe.Result is { } proof)
            {
                foreach (Ff7AutoPlayRouteStep step in proof.Steps)
                {
                    Check(Ff7AutoplayDriver.ReadNativeRouteFingerprint(session) == step.Before, "The native release replay diverged before an input.");
                    driver.Apply(step.Input);
                    session.AdvanceTick(false);
                }
                Check(Ff7AutoplayDriver.ReadNativeRouteFingerprint(session) == proof.After, "The native release replay diverged after its inputs.");
                bool reached = false;
                session.NativeActorTalkRequested += admission => reached |= admission.Entity == entity && admission.Accepted;
                var replayWall = System.Diagnostics.Stopwatch.StartNew();
                long replayStart = session.Host.Process.Tick;
                while (!reached && session.Battle is null && replayWall.Elapsed < TimeSpan.FromSeconds(20))
                {
                    driver.BeginPlanningStep();
                    Ff7AutoPlayObservation actual = new Ff7AutoPlayDiscovery(session).Observe();
                    Ff7AutoPlayTarget selected = actual.Events.Single(item => item.Id == target.Id);
                    driver.ConfigureSpatialBoundaries(actual.Events, selected);
                    Ff7AutoplayInput next = driver.ChooseTargetInput(selected, actual.Events, run => driver.TalkTo(entity, run));
                    if (driver.PlanningPending)
                    {
                        continue;
                    }
                    driver.Apply(next);
                    session.AdvanceTick(false);
                    if (session.Host.Process.Actors.Single(actor => actor.PlayerControlled).Ladder is not null || session.Host.Process.PlayerLocked)
                    {
                        break;
                    }
                }
                Ff7Actor final = session.Host.Process.Actors.Single(actor => actor.PlayerControlled);
                Check(bytes.SequenceEqual(File.ReadAllBytes(path)), "The native release replay changed the source file.");
                Console.WriteLine($"REPLAY calculated native route: callbacks={session.Host.Process.Tick - replayStart}, actorAccepted={reached}, pose=({final.X4096},{final.Y4096},{final.Z4096})/tri{final.Triangle}, ladder={final.Ladder is not null}, locked={session.Host.Process.PlayerLocked}, wall={replayWall.Elapsed.TotalSeconds:F3}s.");
            }
            return;
        }
        using Ff7AutoPlayPolicy policy = new(session, (_, _) => throw new InvalidOperationException("The bounded route diagnostic cannot create choice checkpoints."));
        ushort? destinationField = null;
        if (Environment.GetEnvironmentVariable("MEMORIA_FF7_ROUTE_DESTINATION") is { Length: > 0 } destination)
        {
            destinationField = ushort.Parse(destination, System.Globalization.CultureInfo.InvariantCulture);
            policy.SetDestinationField(destinationField.Value);
        }
        Ff7AutoPlayController controller = new(session, policy.ChooseInput, chooseDialogueSelection: policy.ChooseDialogueSelection, readPlanningProgress: () => policy.WorkProgress);
        long start = session.Host.Process.Tick;
        bool accepted = false;
        bool completed = false;
        session.NativeActorTalkRequested += admission => accepted |= admission.Entity == entity && admission.Accepted;
        var wall = System.Diagnostics.Stopwatch.StartNew();
        string? previousOwner = null;
        int reported = 0;
        int calls = 0;
        double longestCall = 0;
        var deferred = (Dictionary<(string Navigation, string Event), (string Condition, string Physical)>)typeof(Ff7AutoPlayPolicy).GetField("ordinaryDeferredGoals", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.GetValue(policy)!;
        double seconds = double.Parse(Environment.GetEnvironmentVariable("MEMORIA_FF7_ROUTE_SECONDS") ?? "60", System.Globalization.CultureInfo.InvariantCulture);
        controller.Start();
        while (controller.Running && !completed && wall.Elapsed < TimeSpan.FromSeconds(seconds))
        {
            string? priorTarget = policy.State.Active?.EventId;
            long callStarted = System.Diagnostics.Stopwatch.GetTimestamp();
            controller.Advance(1);
            longestCall = Math.Max(longestCall, System.Diagnostics.Stopwatch.GetElapsedTime(callStarted).TotalSeconds);
            calls++;
            player = session.Host.Process.Actors.Single(actor => actor.PlayerControlled);
            if (Environment.GetEnvironmentVariable("MEMORIA_FF7_DROPPED_ROUTE_SAVE") is { Length: > 0 } terminalSave && priorTarget == $"actor:{entity}:script:1" && policy.State.Active is null)
            {
                using FileStream output = new(terminalSave, FileMode.CreateNew);
                CombinedSessionCodec.Write(output, envelope with { Ff7 = session.Capture() with { AutoPlay = policy.State.Clone() } });
                Console.WriteLine($"DROPPED native target: {policy.State.Status}, pose=({player.X4096},{player.Y4096},{player.Z4096})/tri{player.Triangle}/dir{player.Direction}.");
                break;
            }
            completed = destinationField == session.MapId || accepted && policy.State.Coverage.Visits.SelectMany(visit => visit.Events).Any(item => item.Event.Id == $"actor:{entity}:script:1" && item.Completed);
            string? goalNavigation = policy.State.NavigationGoal?.NavigationKey;
            string owner = $"{policy.State.Active?.EventId}/goal:{policy.State.NavigationGoal?.EventId}@{goalNavigation?[(goalNavigation.LastIndexOf(':') + 1)..]}/ladder:{player.Ladder is not null}/battle:{session.Battle is not null}";
            if (owner != previousOwner && reported++ < 24)
            {
                string actorDeferrals = string.Join(',', deferred.Where(item => item.Key.Event == $"actor:{entity}:script:1").Select(item => $"{item.Key.Navigation[(item.Key.Navigation.LastIndexOf(':') + 1)..]}:{item.Value.Physical[..8]}"));
                string localDeferred = string.Join(',', deferred.Where(item => item.Key.Navigation == policy.State.Active?.NavigationKey).Select(item => item.Key.Event));
                Console.WriteLine($"ROUTE owner native={session.Host.Process.Tick - start} tri={player.Triangle} animation={player.AnimationStatus} {owner}/actorDeferrals:{actorDeferrals}/localDeferred:{localDeferred}");
                previousOwner = owner;
            }
            if (session.Battle is not null)
            {
                break;
            }
            if (session.Host.Process.Transition is { } transition)
            {
                Console.WriteLine($"TRANSITION native route: map={transition.Map}, gateway={session.GatewayDeparture?.GatewayIndex}, authenticated={session.GatewayDeparture is { } departure && session.HasNativeGatewayDeparture(departure)}.");
                break;
            }
        }
        Check(bytes.SequenceEqual(File.ReadAllBytes(path)), "The planned-input route check changed its source file.");
        var alternativeDrivers = (Dictionary<string, Ff7AutoplayDriver>)typeof(Ff7AutoPlayPolicy).GetField("ordinaryAlternativeDrivers", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.GetValue(policy)!;
        var controllerDriver = (Ff7AutoplayDriver)typeof(Ff7AutoPlayController).GetField("driver", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.GetValue(controller)!;
        object summaries = typeof(Ff7AutoplayDriver).GetField("nativeCandidateSummaries", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.GetValue(controllerDriver)!;
        var entries = (Dictionary<long, Ff7NativeCandidateSummary>)summaries.GetType().GetField("entries", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.GetValue(summaries)!;
        Console.WriteLine($"Selected graph state: {JsonSerializer.Serialize(entries.Values.Where(item => item.EventId == $"actor:{entity}:script:1"))}");
        Console.WriteLine($"Scheduling diagnostics: noReadyCache={typeof(Ff7AutoPlayPolicy).GetField("ordinaryNoReadySource", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.GetValue(policy) is not null}, alternativeCursors={alternativeDrivers.Count}, pending={string.Join(',', alternativeDrivers.Where(item => item.Value.PlanningPending).Select(item => item.Key))}.");
        Console.WriteLine($"Policy route ended: native={session.Host.Process.Tick - start}, accepted={accepted}, completed={completed}, battle={session.Battle is not null}, wall={wall.Elapsed.TotalSeconds:F3}s, calls={calls}, longestCall={longestCall:F3}s, generation={policy.NativePlanningGeneration}, cancellation={policy.NativePlanningCancellation}, publication={policy.NativePlanningPublication}, phase={policy.NativePlanningPhase}, pose=({player.X4096}, {player.Y4096}, {player.Z4096})/tri{player.Triangle}/dir{player.Direction}, active={policy.State.Active?.EventId}, goal={policy.State.NavigationGoal}, work={policy.WorkProgress}, error={controller.Error}.");
        if (Environment.GetEnvironmentVariable("MEMORIA_FF7_REQUIRE_ROUTE_DESTINATION") == "1")
        {
            Check(destinationField is not null && session.MapId == destinationField && controller.Error is null, "The public autoplay did not reach the required native destination.");
        }
    }

    private static void Check(bool value, string reason)
    {
        if (!value)
        {
            throw new InvalidOperationException(reason);
        }
    }
}
