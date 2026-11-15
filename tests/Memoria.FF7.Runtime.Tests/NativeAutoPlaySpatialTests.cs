using System.Numerics;
using System.Text.Json;
using Memoria.AutoPlay;
using Memoria.FF7.Events;
using Memoria.FF7.Formats;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;

internal static class NativeAutoPlaySpatialTests
{
    public static void Run()
    {
        CheckPhysicalGuard();
        CheckSpatialBoundaries();
        CheckSpatialRouteGuidance();
        CheckTransitionCompletion();
        CheckGatewaySourceProgress();
        CheckLegacyVisitGuard();
        CheckOriginalDiscovery();
    }

    private static void CheckTransitionCompletion()
    {
        Ff7AutoPlayActiveEvent active = new("source-visit", "automatic-frontier", "native-predicate", 1, 2, -1, Ff7AutoPlayTargetKind.AutonomousSpatial, 10, 0, "before-native-transition");
        Check(Ff7AutoPlayPolicy.IsSpatialTransitionCompletion(active, 10), "An ordinary native transition to the autonomous frontier's destination did not complete its target.");
        Check(!Ff7AutoPlayPolicy.IsSpatialTransitionCompletion(active, 11), "An unrelated destination falsely completed an autonomous spatial frontier.");
        Check(Ff7AutoPlayPolicy.IsSpatialTransitionCompletion(active with { DestinationField = null, SpatialDestinations = [10, 11] }, 11), "An autonomous frontier with multiple native destinations rejected its observed transition.");
        Check(!Ff7AutoPlayPolicy.IsSpatialTransitionCompletion(active with { DestinationField = null, SpatialDestinations = [10, 11] }, 12), "An unrelated native destination falsely completed a frontier with multiple destinations.");
        Check(!Ff7AutoPlayPolicy.IsSpatialTransitionCompletion(active with { DestinationField = null }, 11), "A spatial frontier without a proved destination claimed an arbitrary native transition.");
        Check(!Ff7AutoPlayPolicy.IsSpatialTransitionCompletion(active with { Kind = Ff7AutoPlayTargetKind.ActorTalk }, 10), "An actor interaction acquired autonomous transition ownership.");
    }

    private static void CheckSpatialBoundaries()
    {
        Ff7AutoPlayTarget planned = new("automatic:2:triangle:42", 2, -1, Ff7AutoPlayTargetKind.AutonomousSpatial, "native-predicate", false, Vector3.Zero, Triangle: 42, ChangesField: true) { TriangleOnly = true };
        Ff7AutoPlayTarget other = planned with { Id = "automatic:3:triangle:26", Entity = 3, Triangle = 26 };
        Ff7AutoPlayTarget coordinatePredicate = planned with { Id = "automatic:4:triangle:12", Entity = 4, Triangle = 12, TriangleOnly = false };
        Ff7AutoPlayTarget ordinary = planned with { Id = "actor:5:script:1", Entity = 5, Script = 1, Kind = Ff7AutoPlayTargetKind.ActorTalk, Triangle = 13 };
        HashSet<int> boundaries = Ff7AutoplayDriver.ReadSpatialBoundaryTriangles([planned, other, coordinatePredicate, ordinary]);
        Check(boundaries.SetEquals([26, 42]), "Spatial move guards treated coordinate-dependent regions or ordinary interactions as whole native triangle boundaries.");
        Ff7Actor sourceActor = new() { Entity = 0, Slot = 0, Model = 0, Triangle = 0 };
        Check(Ff7AutoplayDriver.AllowsSpatialMove(sourceActor, sourceActor with { Triangle = 42 }, boundaries, 42), "The selected native automatic triangle was forbidden during its ordinary physical approach.");
        Check(!Ff7AutoplayDriver.AllowsSpatialMove(sourceActor, sourceActor with { Triangle = 26 }, boundaries, 42), "A physical approach crossed another active automatic transition triangle.");
        Check(!Ff7AutoplayDriver.AllowsSpatialMove(sourceActor, sourceActor with { Triangle = 42 }, boundaries, null), "An ordinary interaction admitted an unrelated native automatic transition triangle.");
        Check(Ff7AutoplayDriver.AllowsSpatialMove(sourceActor, sourceActor with { Triangle = 12 }, boundaries, null), "A coordinate-dependent native predicate blocked the entire sampled triangle.");
        Check(Ff7AutoplayDriver.AllowsSpatialMove(sourceActor with { Triangle = 26 }, sourceActor, boundaries, null), "A preexisting native automatic triangle prevented physical escape to ordinary geometry.");
        Check(!Ff7AutoplayDriver.AllowsSpatialMove(sourceActor with { Triangle = 26 }, sourceActor with { Triangle = 42 }, boundaries, null), "Escaping a preexisting automatic triangle authorized another unselected transition.");

        string original = Ff7AutoplayDriver.ReadSpatialBoundaryFingerprint("physical-geometry", boundaries);
        Check(original == Ff7AutoplayDriver.ReadSpatialBoundaryFingerprint("physical-geometry", Ff7AutoplayDriver.ReadSpatialBoundaryTriangles([other, planned, planned])), "Boundary ordering or duplicate native owners changed physical availability.");
        Check(original != Ff7AutoplayDriver.ReadSpatialBoundaryFingerprint("physical-geometry", Ff7AutoplayDriver.ReadSpatialBoundaryTriangles([planned])), "Disabling an automatic transition retained stale physical route absence.");
        Check(original != Ff7AutoplayDriver.ReadSpatialBoundaryFingerprint("physical-geometry", Ff7AutoplayDriver.ReadSpatialBoundaryTriangles([planned, other, coordinatePredicate with { TriangleOnly = true }])), "Enabling another proved native triangle boundary retained stale physical route absence.");
    }

    private static void CheckSpatialRouteGuidance()
    {
        PcWalkmeshTriangle[] mesh = Enumerable.Range(0, 43).Select(index => new PcWalkmeshTriangle(index, Vector3.Zero, Vector3.UnitX, Vector3.UnitY, -1, -1, -1)).ToArray();
        mesh[0] = mesh[0] with { AdjacentAB = 26, AdjacentBC = 1 };
        mesh[26] = mesh[26] with { AdjacentAB = 0, AdjacentBC = 42 };
        mesh[1] = mesh[1] with { AdjacentAB = 0, AdjacentBC = 2 };
        mesh[2] = mesh[2] with { AdjacentAB = 1, AdjacentBC = 42 };
        mesh[42] = mesh[42] with { AdjacentAB = 26, AdjacentBC = 2 };
        byte[] blocked = new byte[6];
        var ordinary = Ff7AutoplayDriver.ReadApproachRoutes(mesh, blocked, 0, 42);
        Check(ordinary.Previous[0] == 26 && ordinary.Distance[0] == 2, "The authored route fixture did not prefer its shorter unplanned automatic region.");
        var planned = Ff7AutoplayDriver.ReadApproachRoutes(mesh, blocked, 0, 42, new HashSet<int> { 26, 42 }, 42);
        Check(planned.Previous[0] == 1 && planned.Previous[1] == 2 && planned.Previous[2] == 42 && planned.Distance[0] == 3 && planned.Previous[26] == -1, "Native route guidance failed to choose ordinary geometry around an unselected automatic transition triangle.");
        var escape = Ff7AutoplayDriver.ReadApproachRoutes(mesh, blocked, 26, 0, new HashSet<int> { 26, 42 });
        Check(escape.Previous[26] == 0 && escape.Distance[26] == 1, "Native route guidance could not escape a preexisting automatic transition triangle.");
        var unselected = Ff7AutoplayDriver.ReadApproachRoutes(mesh, blocked, 0, 42, new HashSet<int> { 26, 42 });
        Check(unselected.Previous[0] == -1, "An ordinary target's route guidance entered an unselected automatic destination.");
        blocked[0] = 1 << 2;
        var absent = Ff7AutoplayDriver.ReadApproachRoutes(mesh, blocked, 0, 42, new HashSet<int> { 26, 42 }, 42);
        Check(absent.Previous[0] == -1, "Spatial route guidance bypassed native blocking when its safe alternative closed.");
    }

    private static void CheckPhysicalGuard()
    {
        PcWalkmeshTriangle[] mesh =
        [
            new(0, Vector3.Zero, new(0, 100, 0), new(100, 0, 0), 1, -1, -1),
            new(1, new(100, 0, 0), new(0, 100, 0), new(100, 100, 0), 0, -1, -1)
        ];
        Ff7Actor player = new() { Entity = 0, Slot = 0, Model = 0, PlayerControlled = true, Triangle = 0, CollisionRadius = 30 };
        Ff7AutoPlayTarget target = new("automatic:2:triangle:1", 2, -1, Ff7AutoPlayTargetKind.AutonomousSpatial, "native-predicate", false, new(200f / 3, 200f / 3, 0), Triangle: 1, ChangesField: true);
        string before = JsonSerializer.Serialize(new { mesh, player, target });
        string original = Fingerprint(player, target);
        Check(original == Fingerprint(player with { Triangle = 1, X4096 = 60 * 4096, Y4096 = 60 * 4096 }, target), "Movement within one connected native component invalidated spatial path absence.");
        Check(original != Fingerprint(player, target with { Triangle = 0 }), "A different autonomous sample triangle retained a stale spatial path guard.");
        Check(original != Fingerprint(player, target with { Position = target.Position + Vector3.UnitX }), "Changed autonomous sample geometry retained a stale spatial path guard.");
        Check(original != Fingerprint(player, target with { Position = target.Position + Vector3.UnitZ }), "Changed autonomous sample height retained a stale spatial path guard.");
        Check(before == JsonSerializer.Serialize(new { mesh, player, target }), "Reading an autonomous spatial guard mutated its native geometry or actor.");

        string Fingerprint(Ff7Actor source, Ff7AutoPlayTarget candidate)
        {
            return Ff7AutoplayDriver.ReadPhysicalAvailabilityFingerprint("authored-native-program", mesh, [0], source, candidate, [], []);
        }
    }

    private static void CheckGatewaySourceProgress()
    {
        Check(!Ff7AutoPlayPolicy.SourceStoryChanged(Ff7AutoPlayTargetKind.Gateway, 100, 101), "Destination initialization was attributed to the source's pure native gateway transfer.");
        Check(Ff7AutoPlayPolicy.SourceStoryChanged(Ff7AutoPlayTargetKind.AutonomousSpatial, 100, 101), "An autonomous script's observed story change was discarded as a pure gateway transfer.");
        Check(Ff7AutoPlayPolicy.SourceStoryChanged(Ff7AutoPlayTargetKind.ActorTalk, 100, 101), "An actor script's observed story change was discarded as a pure gateway transfer.");
        Check(!Ff7AutoPlayPolicy.SourceStoryChanged(Ff7AutoPlayTargetKind.ActorTalk, 100, 100), "An unchanged native story value was reported as source progress.");

        const string source = "source-field";
        const string destination = "destination-field";
        const string oldCondition = "native-program:gateway:0:destination:triangle";
        string currentCondition = oldCondition + ":native-transfer:1";
        Ff7AutoPlayState state = new();
        AutoPlayPlanner<Ff7AutoPlayDeferral> planner = new(state.Coverage);
        AutoPlayEvent gateway = new("gateway:0", AutoPlayEventKind.Transition, ConditionSignature: oldCondition);
        planner.Observe(new(source, "source-resource"), [gateway]);
        planner.Observe(new(destination, "destination-resource"), []);
        planner.BeginEvent(source, gateway.Id);
        planner.Completed(source, gateway.Id, destination, observedStoryChanged: true, targetId: gateway.Id);
        AutoPlayEventCoverage<Ff7AutoPlayDeferral> coverage = state.Coverage.Visits.Single(visit => visit.Visit.Key == source).Events.Single();
        AutoPlayEventRoute historical = coverage.Routes.Single();
        Check(planner.Plan(source, needsRefresh: visit => visit.Key == destination) is null, "A historical story-changing route was silently accepted as repeatable travel.");

        planner.Observe(new(source, "source-resource"), [gateway with { ConditionSignature = currentCondition }]);
        AutoPlayPlanStep? fresh = planner.Plan(source, needsRefresh: visit => visit.Key == destination);
        Check(fresh is { IsTravel: false } && fresh.Event.Id == gateway.Id && fresh.Event.ConditionSignature == currentCondition, "A revised pure-transfer contract did not require one ordinary gateway traversal.");
        Check(coverage.Routes.Count == 1 && coverage.Routes[0] == historical && historical.ObservedStoryChanged == true, "Refreshing gateway admission rewrote the historical source-progress observation.");

        planner.BeginEvent(source, gateway.Id);
        planner.Completed(source, gateway.Id, destination, observedStoryChanged: Ff7AutoPlayPolicy.SourceStoryChanged(Ff7AutoPlayTargetKind.Gateway, 100, 101), targetId: gateway.Id);
        AutoPlayPlanStep? travel = planner.Plan(source, needsRefresh: visit => visit.Key == destination);
        Check(travel is { IsTravel: true, RefreshVisitKey: destination } && travel.Route is { ObservedStoryChanged: false } && travel.Route.ConditionSignature == currentCondition, "A completed ordinary pure gateway transfer did not restore travel for bounded remote reobservation.");
        Check(coverage.Routes.Count == 2 && coverage.Routes.Contains(historical), "Recording corrected gateway traversal discarded historical route evidence.");
        Check(coverage.Conditions!.ConsumedEntries.Contains(oldCondition) && coverage.Conditions.ConsumedEntries.Contains(currentCondition), "Corrected gateway traversal discarded previously executed condition coverage.");
    }

    private static void CheckLegacyVisitGuard()
    {
        FieldVariables variables = new();
        variables.WriteByte(FieldBank.Persistent0Byte, 8, 23);
        Ff7Progress progress = new() { InitialKernelData = [1], Characters = [], Party = [255, 255, 255], Inventory = [new(23, 4)], Materia = [], Gil = 500 };
        Ff7FieldProcess process = new();
        Ff7AutoPlayState state = new();
        AutoPlayPlanner<Ff7AutoPlayDeferral> planner = new(state.Coverage);
        planner.Observe(new("remote-field", "native-resource"), [new("completed-talk", AutoPlayEventKind.Interaction)]);
        planner.ObserveNavigation("remote-field", "remote-component", [new("completed-talk", AutoPlayEventKind.Interaction)]);
        state.Coverage.Visits.Single().Events.Single().Completed = true;
        AutoPlayVisit remote = new("remote-component", "native-resource");
        Ff7AutoPlayVisitGuard current = Ff7AutoPlayPolicy.CaptureVisitGuard([], [], variables, progress, process);
        Check(current.DiscoveryRevision == Ff7AutoPlayPolicy.CurrentDiscoveryRevision, "A new observational receipt omitted the current discovery revision.");
        string legacyJson = JsonSerializer.Serialize(new { current.Reads, current.HostReads, current.Fingerprint });
        Ff7AutoPlayVisitGuard legacy = JsonSerializer.Deserialize<Ff7AutoPlayVisitGuard>(legacyJson)!;
        Check(legacy.DiscoveryRevision == 0, "An older visit guard without a discovery revision was not restored with its legacy default.");
        state.VisitGuards["remote-field"] = legacy;
        Ff7AutoPlayState restored = state.Clone();
        string before = JsonSerializer.Serialize(new { Persistent = variables.CopyPersistent(), Map = variables.CopyMap(), progress, process, restored.Coverage });
        Check(Ff7AutoPlayPolicy.NeedsVisitRefresh(restored, remote, variables, progress, process), "A legacy discovery receipt prevented the bounded reobservation of newly supported frontiers.");
        Check(before == JsonSerializer.Serialize(new { Persistent = variables.CopyPersistent(), Map = variables.CopyMap(), progress, process, restored.Coverage }), "Checking an older discovery receipt changed native progress or replayed completed coverage.");
        restored.VisitGuards["remote-field"] = current;
        Check(!Ff7AutoPlayPolicy.NeedsVisitRefresh(restored, remote, variables, progress, process), "A current discovery receipt did not bound the legacy refresh.");
        Check(restored.Coverage.Visits.Single().Events.Single().Completed, "Refreshing discovery discarded recorded interaction completion.");
        restored.VisitGuards["remote-field"] = current with { DiscoveryRevision = current.DiscoveryRevision - 1 };
        Check(Ff7AutoPlayPolicy.NeedsVisitRefresh(restored, remote, variables, progress, process), "A changed discovery revision retained an obsolete frontier catalog.");
    }

    private static void CheckOriginalDiscovery()
    {
        string? root = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR");
        string? path = Environment.GetEnvironmentVariable("MEMORIA_FF7_SPATIAL_SAVE");
        if (string.IsNullOrWhiteSpace(root) || string.IsNullOrWhiteSpace(path))
        {
            Console.WriteLine("SKIP native autonomous spatial discovery: configure original resources and an external standalone field fixture.");
            return;
        }
        byte[] source = File.ReadAllBytes(path);
        using MemoryStream stream = new(source, writable: false);
        CombinedSessionSave envelope = CombinedSessionCodec.Read(stream);
        Ff7Installation installation = Ff7Installation.Discover(root);
        using Ff7Session session = Ff7Session.Restore(installation, envelope.Ff7 ?? throw new InvalidDataException("A spatial fixture requires an FFVII field session."));
        Ff7AutoPlayDiscovery discovery = new(session);
        Ff7AutoPlayObservation observed = discovery.Observe();
        int initialMap = session.MapId;
        long initialGeneration = session.FieldGeneration;
        short initialStory = session.Machine.Variables.ReadWord(FieldBank.Persistent0Word, 0);
        string initialProgress = JsonSerializer.Serialize(session.Progress);
        for (int cycle = 0; cycle < 256 && (!observed.CanAcceptInput || !observed.Events.Any(target => target.Kind == Ff7AutoPlayTargetKind.AutonomousSpatial)); cycle++)
        {
            session.SetInput(0, 0);
            session.AdvanceTick(advanceClock: false);
            Check(session.MapId == initialMap && session.FieldGeneration == initialGeneration, "Neutral fixture initialization crossed a native field boundary before spatial discovery.");
            Check(session.Machine.Variables.ReadWord(FieldBank.Persistent0Word, 0) == initialStory, "Neutral fixture initialization changed the native story boundary before spatial discovery.");
            string nextProgress = JsonSerializer.Serialize(session.Progress);
            using JsonDocument originalProgress = JsonDocument.Parse(initialProgress);
            using JsonDocument currentProgress = JsonDocument.Parse(nextProgress);
            string changedProperties = string.Join(",", originalProgress.RootElement.EnumerateObject().Where(property => property.Value.GetRawText() != currentProgress.RootElement.GetProperty(property.Name).GetRawText()).Select(property => property.Name));
            // Ordinary native callbacks may consume field RNG; the subsequent inspection must preserve the complete resulting state.
            Check(changedProperties.Length == 0 || changedProperties == nameof(Ff7Progress.FieldRandomIndex), $"Neutral fixture initialization changed recorded native progress before spatial discovery: {changedProperties}.");
            observed = discovery.Observe();
        }
        string before = JsonSerializer.Serialize(session.Capture());
        Ff7AutoPlayTarget[] spatial = observed.Events.Where(target => target.Kind == Ff7AutoPlayTargetKind.AutonomousSpatial).ToArray();
        Check(spatial.Length > 0, "Native automatic map-change predicates did not expose any physical spatial frontier.");
        Check(observed.CanAcceptInput, "The native spatial fixture did not reach ordinary physical input admission within its bounded initialization.");
        Check(spatial.Any(target => target.TriangleOnly), "The native triangle fixture did not retain a proved triangle-only predicate for transition admission.");
        Check(spatial.Select(target => target.Id).Distinct(StringComparer.Ordinal).Count() == spatial.Length, "Autonomous spatial samples share an unstable target identity.");
        foreach (Ff7AutoPlayTarget target in spatial)
        {
            CheckTarget(session, target);
            Check(discovery.FindTarget(target.Id) == target, "An autonomous spatial frontier could not be resolved by stable identity.");
        }
        Ff7AutoplayDriver driver = new(session);
        driver.ConfigureSpatialBoundaries(observed.Events);
        string physical = driver.ReadPhysicalAvailabilityFingerprint(spatial[0]);
        driver.ConfigureSpatialBoundaries(observed.Events, spatial[0]);
        Check(physical == driver.ReadPhysicalAvailabilityFingerprint(spatial[0]), "Selecting a spatial frontier invalidated physical absence without a native boundary change.");
        if (spatial.Any(target => target.TriangleOnly))
        {
            driver.ConfigureSpatialBoundaries([]);
            Check(physical != driver.ReadPhysicalAvailabilityFingerprint(spatial[0]), "The live physical guard ignored changed native automatic transition availability.");
        }
        CheckOriginalProposal(session, driver, observed, spatial);
        Check(JsonSerializer.Serialize(observed) == JsonSerializer.Serialize(discovery.Observe()), "Autonomous spatial discovery was not deterministic.");
        Check(before == JsonSerializer.Serialize(session.Capture()), "Autonomous spatial discovery changed native script PCs, inputs, progress or RNG.");
        using MemoryStream serialized = new();
        CombinedSessionCodec.Write(serialized, envelope with { Ff7 = session.Capture() });
        serialized.Position = 0;
        using Ff7Session cold = Ff7Session.Restore(installation, CombinedSessionCodec.Read(serialized).Ff7!);
        Check(JsonSerializer.Serialize(observed) == JsonSerializer.Serialize(new Ff7AutoPlayDiscovery(cold).Observe()), "Cold restoration changed autonomous spatial frontier geometry, conditions or identities.");
        Check(source.AsSpan().SequenceEqual(File.ReadAllBytes(path)), "Autonomous spatial discovery changed its external standalone source.");
    }

    private static void CheckOriginalProposal(Ff7Session session, Ff7AutoplayDriver driver, Ff7AutoPlayObservation observed, Ff7AutoPlayTarget[] spatial)
    {
        Ff7AutoPlayTarget selected = spatial.FirstOrDefault(target => target.Triangle == 42 && target.TriangleOnly) ?? spatial.First(target => target.TriangleOnly);
        driver.ConfigureSpatialBoundaries(observed.Events, selected);
        HashSet<int> boundaries = Ff7AutoplayDriver.ReadSpatialBoundaryTriangles(observed.Events);
        Ff7Actor player = session.Host.Process.Actors.Single(actor => actor.PlayerControlled);
        var routes = Ff7AutoplayDriver.ReadApproachRoutes(session.Field.Walkmesh, session.Host.Process.BlockedTriangles, player.Triangle, selected.Triangle!.Value, boundaries, selected.Triangle);
        bool hasRoute = routes.Previous[player.Triangle] >= 0;
        bool unavailable = false;
        try
        {
            Ff7AutoplayInput input = driver.ChooseTargetInput(selected, () => driver.Approach(selected.Position, selected.Triangle));
            Check(hasRoute, "An original spatial approach bypassed a proved absence of an admitted native triangle route.");
            if (input.Horizontal != 0 || input.Vertical != 0)
            {
                Ff7Actor controlled = player with { MoveSpeed = unchecked((ushort)session.GetPlayerMovementSpeed(input.Run)) };
                int direction = input.Vertical < 0 ? input.Horizontal < 0 ? 32 : input.Horizontal > 0 ? 224 : 0 : input.Vertical > 0 ? input.Horizontal < 0 ? 96 : input.Horizontal > 0 ? 160 : 128 : input.Horizontal < 0 ? 64 : 192;
                int rotation = (input.Held & 8) != 0 ? 224 : (input.Held & 4) != 0 ? 32 : 0;
                Ff7NativeMoveResult preview = session.Host.Movement!.StepDirection(controlled, unchecked((byte)(direction + session.Field.Triggers.ControlDirection + rotation)), session.Host.Process.Actors, session.Host.Process.BlockedTriangles, false, session.Host.Process.LineBlocked);
                Check(preview.Moved && Ff7AutoplayDriver.AllowsSpatialMove(controlled, preview.Actor, boundaries, selected.Triangle), "The original native proposal entered an unselected automatic transition triangle or failed native movement admission.");
            }
        }
        catch (Ff7AutoPlayNavigationUnavailableException absence) when (!hasRoute)
        {
            unavailable = true;
            Check(absence.DestinationTriangle == selected.Triangle && absence.Destination == selected.Position, "Original spatial route absence lost its selected native destination proof.");
        }
        Check(hasRoute || unavailable, "An original spatial target without a safe triangle route omitted its explicit navigation boundary.");
    }

    internal static void CheckTarget(Ff7Session session, Ff7AutoPlayTarget target)
    {
        Check(target.Script == -1 && (uint)target.Entity < session.Field.Scripts.Entities.Count && target.ChangesField, "An autonomous spatial frontier lost its native automatic owner or became a directly requested script.");
        Check(target.Triangle is int triangle && session.Field.Walkmesh.Any(item => item.Index == triangle && target.Position == (item.A + item.B + item.C) / 3), "An autonomous spatial frontier lost its actual native walkmesh sample.");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
