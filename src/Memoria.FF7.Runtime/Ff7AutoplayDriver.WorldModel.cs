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
            worldUnproductiveGuideEdges.Clear();
            worldRepeatedTarget = identity;
        }
        Vector3? guidePoint = null;
        (Ff7WorldTerrainReference From, Ff7WorldTerrainReference To)? guideEdge = null;
        string context = $"{identity}:{world.ReadAutoPlayNavigationContext()}";
        if (worldGuideTrialContext != context || worldGuideTrialSource != sourceKey)
        {
            worldUnproductiveGuideEdges.Clear();
            worldGuideTrialContext = context;
            worldGuideTrialSource = sourceKey;
        }
        int guideIndex = worldTerrainGuide is null ? -1 : Array.FindIndex(worldTerrainGuide.Faces, face => face.Source == source.Input.GroundTriangle);
        if (!ReferenceEquals(worldTerrainGuideOwner, world) || worldTerrainGuideContext != context || guideIndex < 0)
        {
            worldTerrainGuide = world.ReadAutoPlayModelNavigationGuide(target, unproductiveEdges: worldUnproductiveGuideEdges);
            worldTerrainGuideOwner = world;
            worldTerrainGuideContext = context;
            guideIndex = 0;
        }
        Ff7AutoPlayWorldGuide guide = worldTerrainGuide ?? throw new InvalidOperationException("Native world route guidance was not initialized.");
        if (guide.Faces.Length == 0)
        {
            worldRoute.Clear();
            Ff7AutoPlayWorldNavigationStatus status = guide.Limited ? Ff7AutoPlayWorldNavigationStatus.SearchLimited : guide.DeferredComponent is { Length: > 0 } ? Ff7AutoPlayWorldNavigationStatus.Blocked : Ff7AutoPlayWorldNavigationStatus.Unsupported;
            return new(status, default, guide.Expanded, guide.Reason, DeferredComponent: guide.DeferredComponent);
        }
        if (guideIndex + 1 < guide.Faces.Length)
        {
            guidePoint = guide.Faces[guideIndex + 1].Center;
            guideEdge = (guide.Faces[guideIndex].Source, guide.Faces[guideIndex + 1].Source);
            identity += $":guide:{guide.Faces[guideIndex + 1].Source}";
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
        Ff7AutoPlayWorldRoute route = SearchWorldModelRoute(source, target, (input, state) => world.PreviewAutoPlayInput(input, state), nodeBudget, repeated, guidePoint);
        physicalExpanded = route.Expanded;
        if (route.Steps.Length == 0 && repeated is { Count: > 0 })
        {
            repeated.Clear();
            if (physicalExpanded == nodeBudget)
            {
                retry = true;
                return new(Ff7AutoPlayWorldNavigationStatus.SearchLimited, default, physicalExpanded, "Native input preferences were renewed; the bounded physical search remains pending.");
            }
            route = SearchWorldModelRoute(source, target, (input, state) => world.PreviewAutoPlayInput(input, state), nodeBudget - physicalExpanded, waypoint: guidePoint);
            physicalExpanded += route.Expanded;
        }
        if (route.Status == Ff7AutoPlayWorldNavigationStatus.SearchLimited && route.Steps.Length == 0 && guideEdge is { } edge)
        {
            if (!worldUnproductiveGuideEdges.Add(edge))
            {
                throw new InvalidOperationException("A world guide repeated an unresolved physical transition.");
            }
            worldTerrainGuideContext = null;
            retry = true;
            return new(Ff7AutoPlayWorldNavigationStatus.SearchLimited, default, physicalExpanded, route.Reason);
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

    private static bool AdmitWorldModelStep(Ff7AutoPlayWorldMovementPreview step, int model, int eventId)
    {
        return step.TerrainBoundary is not { Inert: false } && (step.ModelBoundaries ?? []).All(boundary => boundary.Inert || boundary.Model == model && boundary.Event == eventId);
    }

    internal static Ff7AutoPlayWorldRoute SearchWorldModelRoute(Ff7AutoPlayWorldMovementState source, Ff7AutoPlayWorldModelScriptObservation target, Func<Ff7AutoplayInput, Ff7AutoPlayWorldMovementState, Ff7AutoPlayWorldMovementPreview> preview, int nodeBudget, IReadOnlySet<Ff7AutoplayInput>? avoidFirst = null, Vector3? waypoint = null)
    {
        return SearchWorldPhysicalRoute(source, preview, state => WorldPointDistance(state.Player, waypoint ?? new Vector3(target.Target.X, target.Target.Y, target.Target.Z)), step => AdmitWorldModelStep(step, target.Model, target.Event), step => (step.ModelBoundaries ?? []).Any(boundary => boundary.Model == target.Model && boundary.Event == target.Event), nodeBudget, avoidFirst, held: target.Event == 4 ? (ushort)32 : (ushort)0);
    }
}
