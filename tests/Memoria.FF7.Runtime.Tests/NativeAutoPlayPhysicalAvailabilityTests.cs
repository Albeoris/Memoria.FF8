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
        string navigation = Ff7AutoplayDriver.ReadNavigationKey("authored-visit", mesh, blocked, 0);
        Check(navigation == Ff7AutoplayDriver.ReadNavigationKey("authored-visit", mesh, blocked, 1), "A navigation identity changed inside one connected native floor.");
        Check(navigation != Ff7AutoplayDriver.ReadNavigationKey("authored-visit", mesh, blocked, 2), "A stacked disconnected floor shared a navigation identity.");
        Check(navigation != Ff7AutoplayDriver.ReadNavigationKey("authored-visit", mesh, [2], 0), "Changed native blocking retained an obsolete navigation topology identity.");
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
        string lineBoundary = Ff7AutoplayDriver.ReadPhysicalAvailabilityFingerprint("authored-program", mesh, blocked, player, lineTarget, [line], [gateway]);
        Check(lineBoundary != Ff7AutoplayDriver.ReadPhysicalAvailabilityFingerprint("authored-program", mesh, blocked, player, lineTarget, [line], [gateway], mapJumpDisabled: true), "Native map-jump admission changed without expiring LINE physical absence.");
        Check(lineBoundary != Ff7AutoplayDriver.ReadPhysicalAvailabilityFingerprint("authored-program", mesh, blocked, player, lineTarget, [line], [gateway with { A = new(1, 0, 300) }]), "An unselected native gateway moved without expiring LINE physical absence.");
        string actorBoundary = Ff7AutoplayDriver.ReadPhysicalAvailabilityFingerprint("authored-program", mesh, blocked, player, target, gateways: [gateway]);
        Check(actorBoundary != Ff7AutoplayDriver.ReadPhysicalAvailabilityFingerprint("authored-program", mesh, blocked, player, target, mapJumpDisabled: true, gateways: [gateway]), "Native map-jump admission changed without expiring actor physical absence.");
        Check(actorBoundary != Ff7AutoplayDriver.ReadPhysicalAvailabilityFingerprint("authored-program", mesh, blocked, player, target, gateways: [gateway with { B = new(101, 100, 300) }]), "An unselected native gateway moved without expiring actor physical absence.");
        Ff7AutoPlayNavigationUnavailableException pathAbsence = new(-1, "absent authored path", destination: lineTarget.Position, destinationTriangle: 2);
        Check(pathAbsence is { Entity: -1, DestinationTriangle: 2 } && pathAbsence.Destination == lineTarget.Position && pathAbsence.BlockingEntities.Count == 0, "A nonactor path absence lost its exact destination proof.");
        PcWalkmeshTriangle[] stacked =
        [
            new(0, Vector3.Zero, new(1000, 0, 0), new(0, 1000, 0), -1, -1, -1),
            new(1, new(40, 40, 20), new(70, 40, 20), new(40, 70, 20), -1, -1, -1)
        ];
        Ff7Line lowerLine = new(0, 17, 50, 40, 0, 50, 60, 0);
        Ff7Actor lowerPlayer = player with { X4096 = 100 * 4096, Y4096 = 100 * 4096 };
        Vector3 lineCenter = new(50, 50, 0);
        Check(stacked.MinBy(triangle => Vector3.DistanceSquared((triangle.A + triangle.B + triangle.C) / 3, lineCenter))!.Index == 1, "The authored stacked fixture did not expose the global nearest-centroid trap.");
        var contacts = Ff7AutoplayDriver.DiscoverLineContacts(stacked, [0], lowerPlayer, lowerLine);
        Check(contacts.Count > 0 && contacts.All(candidate => candidate.Triangle == 0), "A reachable native LINE contact was lost to an unreachable stacked layer.");
        Check(contacts.All(candidate => Ff7Session.DistanceToSegment(new(50, 40, 0), new(50, 60, 0), candidate.Point, out _) is >= 0 and < 900), "LINE candidate discovery admitted a floor point outside the native three-dimensional contact radius.");
        Check(Ff7AutoplayDriver.DiscoverLineContacts(stacked, [0], lowerPlayer, lowerLine with { Z1 = 300, Z2 = 300 }).Count == 0, "An out-of-height LINE was falsely exposed as a reachable contact.");
        Check(Ff7AutoplayDriver.DiscoverLineContacts(stacked, [0], lowerPlayer, lowerLine with { Enabled = 0 }).Count == 0, "A disabled LINE was advertised as a native contact candidate.");
        CheckNativeGoalPreview();
        CheckBlockedPortalSearch();
        CheckGatewayAdmission();
        CheckOriginalTargetBoundary();
        CheckNativeOcclusionPredicate();

        string Fingerprint(Ff7Actor source, Ff7Actor destination, byte[] blocking)
        {
            return Ff7AutoplayDriver.ReadPhysicalAvailabilityFingerprint("authored-program", mesh, blocking, source, destination);
        }
    }

    private static void CheckGatewayAdmission()
    {
        PcFieldGateway exit = new(new(0, -100, 0), new(0, 100, 0), 0, 0, 0, 2, 0, 0);
        PcFieldGateway selected = exit with { A = new(100, -100, 0), B = new(100, 100, 0), DestinationField = 3 };
        Ff7Actor source = new() { Slot = 0, Entity = 0, Model = 0, X4096 = -4096, CollisionRadius = 30 };
        Ff7Actor to = source with { X4096 = 4096 };
        Check(Ff7Session.CrossesGateway(exit, source, to), "The authored actor approach did not cross its native exit.");
        Check(!Ff7AutoplayDriver.AllowsGatewayMove([exit], source, to, null), "An interaction without a boundary contract admitted an unintended native gateway.");
        Check(Ff7AutoplayDriver.AllowsGatewayMove([exit], source, to, new HashSet<int> { 0 }), "Explicit travel rejected its selected native gateway.");
        Check(!Ff7AutoplayDriver.AllowsGatewayMove([exit, selected], source, to, new HashSet<int> { 1 }), "Explicit travel admitted an unselected native gateway.");
        Check(Ff7AutoplayDriver.AllowsGatewayMove([exit], source, source with { X4096 = -8192 }, null), "An interaction rejected a native step remaining on the same exit side.");
        Ff7AutoPlayTarget line = new("authored-line", 1, 4, Ff7AutoPlayTargetKind.LineTouch, "conditions", false, Vector3.Zero);
        Check(!Ff7AutoplayDriver.AllowsGatewayMove([exit], source, to, Ff7AutoplayDriver.ReadTargetGatewayIndices(line, [exit])), "A local LINE handler admitted an unrelated native exit.");
        Ff7AutoPlayTarget coupled = line with { ChangesField = true, DestinationField = exit.DestinationField };
        Check(Ff7AutoplayDriver.AllowsGatewayMove([exit], source, to, Ff7AutoplayDriver.ReadTargetGatewayIndices(coupled, [exit])), "A decoded unique-destination LINE lost its coupled native gateway.");
        Check(!Ff7AutoplayDriver.AllowsGatewayMove([exit], source, to, Ff7AutoplayDriver.ReadTargetGatewayIndices(coupled with { DestinationField = selected.DestinationField }, [exit])), "A decoded LINE admitted a different-destination native exit.");
        Check(!Ff7AutoplayDriver.AllowsGatewayMove([exit], source, to, Ff7AutoplayDriver.ReadTargetGatewayIndices(coupled with { DestinationField = null }, [exit])), "A multi-destination LINE fabricated unique native gateway admission.");
        Check(!Ff7AutoplayDriver.AllowsGatewayMove([exit], source, to, Ff7AutoplayDriver.ReadTargetGatewayIndices(coupled with { AnalysisComplete = false }, [exit])), "An incomplete handler analysis authorized a coupled native exit.");
        Ff7AutoPlayTarget explicitGate = line with { GatewayIndex = 1, ChangesField = true, DestinationField = exit.DestinationField };
        Check(!Ff7AutoplayDriver.AllowsGatewayMove([exit, selected], source, to, Ff7AutoplayDriver.ReadTargetGatewayIndices(explicitGate, [exit, selected])), "Unique-destination coupling overrode an explicitly selected gateway index.");
    }

    private static void CheckBlockedPortalSearch()
    {
        Ff7Actor source = new() { Slot = 0, Entity = 0, Model = 0, X4096 = 0, Y4096 = 0, Z4096 = 100 * 4096, Triangle = 0, MoveSpeed = 4096 };
        Vector3 waypoint = new(10, 0, 100);
        int Remaining(int triangle) => triangle == 0 ? 2 : triangle == 1 ? 1 : 4;
        Ff7NativeMoveResult Blocked(Ff7Actor actor, Ff7AutoplayInput input) => new(actor with { Triangle = 1 }, false, false, [3]);
        var blocked = Ff7AutoplayDriver.SearchPhysicalEscape(source, waypoint, Remaining, Blocked, (_, _) => true);
        Check(blocked is { Status: Ff7AutoPlayPhysicalSearchStatus.Exhausted, Inputs.Length: 0 }, "A blocked native triangle-only relabel was accepted as physical transit through a portal.");
        Ff7NativeMoveResult Preview(Ff7Actor actor, Ff7AutoplayInput input)
        {
            if (actor.Y4096 == 0 && input.Horizontal == 1 && input.Vertical == 0)
            {
                return Blocked(actor, input);
            }
            if (actor.Y4096 == 0 && input.Horizontal == 0 && input.Vertical == 1)
            {
                return new(actor with { Y4096 = 10 * 4096 }, false, true, []);
            }
            if (actor.Y4096 == 10 * 4096 && input.Horizontal == 1 && input.Vertical == 0)
            {
                return new(actor with { X4096 = 10 * 4096, Triangle = 1 }, false, true, []);
            }
            return new(actor, false, false, []);
        }
        var detour = Ff7AutoplayDriver.SearchPhysicalEscape(source, waypoint, Remaining, Preview, (_, _) => true, 16);
        Check(detour.Status == Ff7AutoPlayPhysicalSearchStatus.Progress && detour.Inputs.SequenceEqual(new[] { new Ff7AutoplayInput(0, 1), new Ff7AutoplayInput(1, 0) }), "The bounded native search failed to take a physically admitted away detour around a blocked portal.");
        var refused = Ff7AutoplayDriver.SearchPhysicalEscape(source, waypoint, Remaining, Preview, (_, _) => false, 16);
        Check(refused is { Status: Ff7AutoPlayPhysicalSearchStatus.Exhausted, Inputs.Length: 0 }, "A physical detour bypassed selected-target movement admission.");
        var limited = Ff7AutoplayDriver.SearchPhysicalEscape(source, waypoint, Remaining, (actor, _) => new(actor with { Y4096 = actor.Y4096 + 10 * 4096 }, false, true, []), (_, _) => true, 1);
        Check(limited is { Status: Ff7AutoPlayPhysicalSearchStatus.Limited, Inputs.Length: 0 }, "A bounded search failure became global physical absence or an unproved advancing route.");
        Ff7Actor lower = source with { X4096 = 10 * 4096, Z4096 = 0 };
        Check(!Ff7AutoplayDriver.HasPhysicalRouteProgress(source, lower, waypoint, _ => 0), "Lower-floor XY proximity falsely completed a three-dimensional terminal approach.");
        PcWalkmeshTriangle[] mesh = [new(0, new(0, 0, 100), new(10, 0, 100), new(0, 10, 100), 1, -1, -1), new(1, new(10, 0, 100), new(10, 10, 110), new(0, 10, 100), 0, -1, -1)];
        Ff7AutoPlayMovementFailure region = Ff7AutoplayDriver.ReadApproachRegion(mesh, source, 1);
        Check(region is { MinX: 0, MaxX: 40960, MinY: 0, MaxY: 40960, MinZ: 409600, MaxZ: 450560 }, "A local approach deferral lost the native fixed-point source/portal height region or invented a margin.");
        Ff7Actor controlledSource = source with { PlayerControlled = true };
        string inside = Ff7AutoPlayPolicy.ReadMovementFailureFingerprint("physical", region, [controlledSource]);
        Check(inside == Ff7AutoPlayPolicy.ReadMovementFailureFingerprint("physical", region, [controlledSource with { Triangle = 1, X4096 = 5 * 4096, Y4096 = 5 * 4096, Z4096 = 105 * 4096 }]), "Ordinary movement within the native portal region or a triangle-only relabel prematurely reopened its local deferral.");
        Check(inside == Ff7AutoPlayPolicy.ReadMovementFailureFingerprint("physical", region, [controlledSource with { Z4096 = 99 * 4096 }]), "Unchanged native portal eligibility was released by player height alone.");
        Ff7AutoPlayTarget line = new("authored-line", 8, 2, Ff7AutoPlayTargetKind.LinePush, "conditions", false, waypoint);
        Ff7Actor blocker = source with { Entity = 3, Slot = 1, Model = 1, CollisionEnabled = true, CollisionRadius = 30 };
        Ff7Actor ambient = blocker with { Entity = 4, Slot = 2 };
        string original = Ff7AutoplayDriver.ReadPhysicalAvailabilityFingerprint("authored", mesh, [0], source, line, [], [], blockingEntities: [3], actors: [blocker, ambient]);
        Check(original != Ff7AutoplayDriver.ReadPhysicalAvailabilityFingerprint("authored", mesh, [0], source, line, [], [], blockingEntities: [3], actors: [blocker with { CollisionEnabled = false }, ambient]), "Disabling a relevant LINE-route collider did not rearm physical availability.");
        Check(original != Ff7AutoplayDriver.ReadPhysicalAvailabilityFingerprint("authored", mesh, [0], source, line, [], [], blockingEntities: [3], actors: [blocker with { CollisionRadius = 31 }, ambient]), "Changing a relevant LINE-route collider radius did not rearm physical availability.");
        Check(original == Ff7AutoplayDriver.ReadPhysicalAvailabilityFingerprint("authored", mesh, [0], source, line, [], [], blockingEntities: [3], actors: [blocker, ambient with { X4096 = 4096 }]), "An unrelated ambient actor invalidated a scoped LINE-route collider guard.");
    }

    private static void CheckOriginalTargetBoundary()
    {
        string? root = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR");
        string? path = Environment.GetEnvironmentVariable("MEMORIA_FF7_DISCOVERY_SAVE");
        string? targetId = Environment.GetEnvironmentVariable("MEMORIA_FF7_PHYSICAL_BOUNDARY_TARGET");
        if (string.IsNullOrWhiteSpace(root) || string.IsNullOrWhiteSpace(path) || string.IsNullOrWhiteSpace(targetId))
        {
            Console.WriteLine("SKIP native selected-target boundary: configure original resources, a standalone field and its target identity.");
            return;
        }
        byte[] bytes = File.ReadAllBytes(path);
        using MemoryStream stream = new(bytes, writable: false);
        using Ff7Session session = Ff7Session.Restore(Ff7Installation.Discover(root), CombinedSessionCodec.Read(stream).Ff7!);
        string before = JsonSerializer.Serialize(session.Capture());
        Ff7AutoPlayTarget target = new Ff7AutoPlayDiscovery(session).Observe().Events.Single(target => target.Id == targetId);
        Ff7AutoplayDriver driver = new(session);
        Ff7AutoplayInput input = driver.ChooseTargetInput(target, () => target.Kind switch
        {
            Ff7AutoPlayTargetKind.ActorTalk => driver.TalkTo(target.Entity),
            Ff7AutoPlayTargetKind.ActorContact => driver.Contact(target.Entity),
            Ff7AutoPlayTargetKind.Gateway => driver.GatewayAt(target.GatewayIndex!.Value),
            Ff7AutoPlayTargetKind.LineTalk => driver.TalkToLine(target.Entity),
            Ff7AutoPlayTargetKind.LineLeave => driver.LeaveLine(target.Entity),
            _ => driver.ApproachLine(target.Entity, target.Kind)
        });
        if (input.Horizontal != 0 || input.Vertical != 0)
        {
            Ff7Actor player = session.Host.Process.Actors.Single(actor => actor.PlayerControlled) with { MoveSpeed = checked((ushort)session.GetPlayerMovementSpeed(input.Run)) };
            int direction = input.Vertical < 0 ? input.Horizontal < 0 ? 32 : input.Horizontal > 0 ? 224 : 0 : input.Vertical > 0 ? input.Horizontal < 0 ? 96 : input.Horizontal > 0 ? 160 : 128 : input.Horizontal < 0 ? 64 : 192;
            int rotation = (input.Held & 8) != 0 ? 224 : (input.Held & 4) != 0 ? 32 : 0;
            Ff7NativeMoveResult preview = session.Host.Movement!.StepDirection(player, unchecked((byte)(direction + session.Field.Triggers.ControlDirection + rotation)), session.Host.Process.Actors, session.Host.Process.BlockedTriangles, false, session.Host.Process.LineBlocked);
            Check(session.Host.Process.MapJumpDisabled || Ff7AutoplayDriver.AllowsGatewayMove(session.Field.Triggers.Gateways, player, preview.Actor, Ff7AutoplayDriver.ReadTargetGatewayIndices(target, session.Field.Triggers.Gateways)), "The native selected-target dry run crossed an unadmitted gateway.");
        }
        Check(before == JsonSerializer.Serialize(session.Capture()) && bytes.AsSpan().SequenceEqual(File.ReadAllBytes(path)), "The native selected-target dry run changed its live snapshot or standalone source.");
    }

    private static void CheckNativeGoalPreview()
    {
        PcWalkmeshTriangle[] mesh = [new(0, Vector3.Zero, new(0, 1000, 0), new(1000, 0, 0), -1, -1, -1)];
        short[] vectors = new short[512];
        for (int direction = 0; direction < 256; direction++)
        {
            vectors[direction * 2] = (short)Math.Round(Math.Sin(direction * Math.PI / 128) * 4096);
            vectors[direction * 2 + 1] = (short)Math.Round(Math.Cos(direction * Math.PI / 128) * 4096);
        }
        byte[] angles = new byte[129];
        for (int index = 0; index < angles.Length; index++)
        {
            angles[index] = (byte)Math.Round(Math.Asin(index / 128d) * 128 / Math.PI);
        }
        Ff7NativeMovement movement = new(mesh, vectors, angles);
        Ff7Actor player = new() { Slot = 0, Entity = 0, Model = 0, PlayerControlled = true, X4096 = 600 * 4096, Y4096 = 100 * 4096, MoveSpeed = 4096, CollisionRadius = 10 };
        Vector3 goal = new(150, 600, 0);
        int nativeSteps = 0;
        int successfulPredicates = 0;
        string before = JsonSerializer.Serialize(player);
        bool Reached(Ff7Actor projected)
        {
            double dx = projected.X4096 / 4096d - goal.X;
            double dy = projected.Y4096 / 4096d - goal.Y;
            bool reached = dx * dx + dy * dy < 10000;
            if (reached)
            {
                successfulPredicates++;
            }
            return reached;
        }
        Ff7AutoplayInput? direct = Ff7AutoplayDriver.PreviewGoalInput(player, goal, 0, movement, [player], [0], false, (_, _) =>
        {
            nativeSteps++;
            return true;
        }, Reached);
        Check(direct is not null && (direct.Value.Horizontal != 0 || direct.Value.Vertical != 0) && nativeSteps > 1 && successfulPredicates == 1, $"Direct goal admission did not prove the complete native path and actual success predicate before returning its first physical input: {direct}, steps={nativeSteps}, successes={successfulPredicates}.");
        Check(before == JsonSerializer.Serialize(player), "Read-only native goal admission changed the source actor.");
        int refused = 0;
        Ff7AutoplayInput? blocked = Ff7AutoplayDriver.PreviewGoalInput(player, goal, 0, movement, [player], [0], false, (_, _) =>
        {
            refused++;
            return false;
        }, Reached);
        Check(blocked is null && refused == 1, "A forbidden native movement/contact probe was admitted as direct travel.");
        int beforeBlockedContact = 0;
        Ff7AutoplayInput? laterBlocked = Ff7AutoplayDriver.PreviewGoalInput(player, goal, 0, movement, [player], [0], false, (_, _) => ++beforeBlockedContact < 4, Reached);
        Check(laterBlocked is null && beforeBlockedContact == 4, "A clear first move authorized travel although a later native contact probe was forbidden.");
        Check(Ff7AutoplayDriver.PreviewGoalInput(player, goal, 0, movement, [player], [0], false, (_, _) => true, _ => false) is null, "Reaching coordinates without the requested native interaction predicate falsely authorized travel.");
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
