using System.Numerics;

namespace Memoria.FF7.Runtime;

public sealed partial class Ff7AutoplayDriver
{
    internal Ff7AutoPlayWorldNavigationResult ChooseWorldModelInput(Ff7AutoPlayWorldModelScriptObservation target, int nodeBudget = 512)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(nodeBudget, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(nodeBudget, 16384);
        PlanningPending = false;
        int expanded = 0;
        int remaining = nodeBudget;
        while (true)
        {
            Ff7AutoPlayWorldNavigationResult result = ChooseWorldModelInputAttempt(target, remaining, out bool retry, out int physicalExpanded);
            expanded += result.Expanded;
            remaining -= physicalExpanded;
            if (!retry)
            {
                return result with { Expanded = expanded };
            }
            if (remaining == 0)
            {
                PlanningPending = true;
                return result with { Expanded = expanded };
            }
        }
    }

    private Ff7AutoPlayWorldNavigationResult ChooseWorldModelInputAttempt(Ff7AutoPlayWorldModelScriptObservation target, int nodeBudget, out bool retry, out int physicalExpanded)
    {
        retry = false;
        physicalExpanded = 0;
        Ff7WorldRuntime world = session.World ?? throw new InvalidOperationException("World navigation requires an allocated native world.");
        if (!target.Complete || target.Event is not (3 or 4))
        {
            return new(Ff7AutoPlayWorldNavigationStatus.Unsupported, default, 0, target.Reason ?? "The requested native model contact remains incompletely analyzed.");
        }
        if (world.ControlledModel < 0)
        {
            return new(Ff7AutoPlayWorldNavigationStatus.Unsupported, default, 0, "The native controlled world model is not allocated.");
        }
        Ff7AutoPlayWorldMovementState source = world.CaptureAutoPlayMovementState();
        string sourceKey = WorldMovementKey(source);
        string positionKey = WorldPositionKey(source);
        string identity = $"{source.WorldId}:{source.WorldMapState}:{source.Player.Model}:{target.Id}:{target.Model}:{target.Event}:{target.GeometryFingerprint}:{target.PredicateFingerprint}";
        if (worldRepeatedTarget != identity)
        {
            worldRepeatedInputs.Clear();
            worldRepeatedTarget = identity;
        }
        Vector3? guidePoint = null;
        Ff7WorldTerrainReference? guideGround = null;
        string context = $"{identity}:{world.ReadAutoPlayNavigationContext()}";

        int guideIndex = worldTerrainGuide is null ? -1 : Array.FindIndex(worldTerrainGuide.Faces, face => face.Source == source.Input.GroundTriangle);
        if (!ReferenceEquals(worldTerrainGuideOwner, world) || worldTerrainGuideContext != context || guideIndex < 0 && worldTerrainGuide is not { Limited: true, Faces.Length: 0 })
        {
            worldTerrainGuide = world.ReadAutoPlayModelNavigationGuide(target);
            worldTerrainGuideOwner = world;
            worldTerrainGuideContext = context;
            guideIndex = 0;
        }
        Ff7AutoPlayWorldGuide guide = worldTerrainGuide ?? throw new InvalidOperationException("Native world route guidance was not initialized.");
        if (guide.Faces.Length == 0 && !guide.Limited)
        {
            worldRoute.Clear();
            Ff7AutoPlayWorldNavigationStatus status = guide.Limited ? Ff7AutoPlayWorldNavigationStatus.SearchLimited : guide.DeferredComponent is { Length: > 0 } ? Ff7AutoPlayWorldNavigationStatus.Blocked : Ff7AutoPlayWorldNavigationStatus.Unsupported;
            return new(status, default, guide.Expanded, guide.Reason, DeferredComponent: guide.DeferredComponent);
        }
        if (guideIndex + 1 < guide.Faces.Length)
        {
            Ff7AutoPlayWorldGuideFace next = guide.Faces[guideIndex + 1];
            guidePoint = next.Center;
            if (source.Input.GroundTriangle == guide.Faces[guideIndex].Source)
            {
                guidePoint = ReadWorldGuidePortal(guide.Faces[guideIndex], next);
                if (guidePoint is null)
                {
                    return new(Ff7AutoPlayWorldNavigationStatus.Unsupported, default, 0, "The native model approach advice lacks its shared terrain portal.");
                }
                guideGround = next.Source;
            }
            identity += $":guide:{next.Source}";
        }
        if (worldRouteTarget != identity || worldRouteRearming)
        {
            worldRoute.Clear();
            worldRouteTarget = identity;
            worldRouteRearming = false;
        }
        if (worldRoute.TryPeek(out Ff7AutoPlayWorldRouteStep? retained))
        {
            Ff7AutoPlayWorldMovementPreview fresh = world.PreviewAutoPlayInput(retained.Input, source);
            if (retained.SourceKey == sourceKey && fresh is { Supported: true, State: not null } && WorldMovementKey(fresh.State) == retained.DestinationKey && fresh.TerrainBoundary == retained.Boundary && (fresh.ModelBoundaries ?? []).SequenceEqual(retained.ModelBoundaries ?? []) && AdmitWorldModelStep(fresh, target.Model, target.Event))
            {
                worldRoute.Dequeue();
                RememberWorldInput(positionKey, retained.Input);
                return new(Ff7AutoPlayWorldNavigationStatus.Step, retained.Input, 0, PredictedBoundary: fresh.TerrainBoundary, HasInput: true);
            }
            worldRoute.Clear();
        }
        worldRepeatedInputs.TryGetValue(positionKey, out HashSet<Ff7AutoplayInput>? repeated);
        Ff7AutoPlayWorldRoute route = SearchWorldModelRoute(source, target, (input, state) => world.PreviewAutoPlayInput(input, state), nodeBudget, repeated, guidePoint, (create, quantum) => ResumeWorldPhysicalSearch(ReadWorldPhysicalSearchContext(world, source, identity + "|" + guidePoint), create, quantum, world), intermediateGround: guideGround);
        physicalExpanded = route.Expanded;
        if (route.Steps.Length == 0 && route.Status != Ff7AutoPlayWorldNavigationStatus.SearchLimited && repeated is { Count: > 0 })
        {
            repeated.Clear();
            if (physicalExpanded == nodeBudget)
            {
                SearchWorldModelRoute(source, target, (input, state) => world.PreviewAutoPlayInput(input, state), 0, waypoint: guidePoint, resume: (create, quantum) => ResumeWorldPhysicalSearch(ReadWorldPhysicalSearchContext(world, source, identity + "|" + guidePoint), create, quantum, world), intermediateGround: guideGround);
                retry = true;
                return new(Ff7AutoPlayWorldNavigationStatus.SearchLimited, default, physicalExpanded, "Native input preferences were renewed; the bounded physical search remains pending.");
            }
            route = SearchWorldModelRoute(source, target, (input, state) => world.PreviewAutoPlayInput(input, state), nodeBudget - physicalExpanded, waypoint: guidePoint, resume: (create, quantum) => ResumeWorldPhysicalSearch(ReadWorldPhysicalSearchContext(world, source, identity + "|" + guidePoint), create, quantum, world), intermediateGround: guideGround);
            physicalExpanded += route.Expanded;
        }
        foreach (Ff7AutoPlayWorldRouteStep step in route.Steps)
        {
            worldRoute.Enqueue(step);
        }
        if (!worldRoute.TryDequeue(out Ff7AutoPlayWorldRouteStep? first))
        {
            return new(route.Status, default, physicalExpanded, route.Reason);
        }
        RememberWorldInput(positionKey, first.Input);
        return new(route.Status, first.Input, physicalExpanded, route.Reason, first.Boundary, HasInput: true);
    }

    internal static Vector3? ReadWorldGuidePortal(Ff7AutoPlayWorldGuideFace current, Ff7AutoPlayWorldGuideFace next)
    {
        static bool Same(Vector3 a, Vector3 b) => a.Y == b.Y && WorldDelta(a.X - b.X, WorldWidth) == 0 && WorldDelta(a.Z - b.Z, WorldDepth) == 0;
        Vector3[] adjacent = [next.A, next.B, next.C];
        Vector3[] shared = new[] { current.A, current.B, current.C }.Where(point => adjacent.Any(other => Same(point, other))).Distinct().ToArray();
        if (shared.Length != 2)
        {
            return null;
        }
        return shared[0] + new Vector3((float)WorldDelta(shared[1].X - shared[0].X, WorldWidth), shared[1].Y - shared[0].Y, (float)WorldDelta(shared[1].Z - shared[0].Z, WorldDepth)) / 2;
    }

    private static bool AdmitWorldModelStep(Ff7AutoPlayWorldMovementPreview step, int model, int eventId)
    {
        return step.TerrainBoundary is not { Inert: false } && (step.ModelBoundaries ?? []).All(boundary => boundary.Inert || boundary.Model == model && boundary.Event == eventId);
    }

    internal static Ff7AutoPlayWorldRoute SearchWorldModelRoute(Ff7AutoPlayWorldMovementState source, Ff7AutoPlayWorldModelScriptObservation target, Func<Ff7AutoplayInput, Ff7AutoPlayWorldMovementState, Ff7AutoPlayWorldMovementPreview> preview, int nodeBudget, IReadOnlySet<Ff7AutoplayInput>? avoidFirst = null, Vector3? waypoint = null, Func<Func<WorldPhysicalSearchCursor>, int, Ff7AutoPlayWorldRoute>? resume = null, Ff7WorldTerrainReference? intermediateGround = null)
    {
        return SearchWorldPhysicalRoute(source, preview, state => WorldPointDistance(state.Player, waypoint ?? new Vector3(target.Target.X, target.Target.Y, target.Target.Z)), step => AdmitWorldModelStep(step, target.Model, target.Event), step => (step.ModelBoundaries ?? []).Any(boundary => boundary.Model == target.Model && boundary.Event == target.Event) || intermediateGround is not null && intermediateGround != source.Input.GroundTriangle && step.State?.Input.GroundTriangle == intermediateGround, nodeBudget, avoidFirst, held: target.Event == 4 ? (ushort)32 : (ushort)0, resume: resume);
    }
}
