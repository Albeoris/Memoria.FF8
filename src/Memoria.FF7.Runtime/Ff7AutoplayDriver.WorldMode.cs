namespace Memoria.FF7.Runtime;

public sealed partial class Ff7AutoplayDriver
{
    internal Ff7AutoPlayWorldNavigationResult ChooseWorldVehicleApproachInput(Ff7AutoPlayWorldScriptObservation target, Ff7AutoPlayWorldModeGuide guide, int nodeBudget = 512)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(nodeBudget, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(nodeBudget, 16384);
        PlanningPending = false;
        Ff7WorldRuntime world = session.World ?? throw new InvalidOperationException("Vehicle approach requires an allocated native world.");
        Ff7AutoPlayWorldMovementState source = world.CaptureAutoPlayMovementState();
        int index = Array.FindIndex(guide.TransportGuide.Faces, face => face.Source == source.Input.GroundTriangle);
        if (index < 0 || guide.LandingSource is null || guide.RiderReachableFaces.Length == 0)
        {
            worldRoute.Clear();
            return new(guide.Limited ? Ff7AutoPlayWorldNavigationStatus.SearchLimited : Ff7AutoPlayWorldNavigationStatus.Unsupported, default, 0, "The current native ground has no retained vehicle-to-rider approach advice.");
        }
        Ff7AutoPlayWorldGuideFace waypoint = guide.TransportGuide.Faces[Math.Min(index + 1, guide.TransportGuide.Faces.Length - 1)];
        bool seekingLanding = source.Input.GroundTriangle == guide.LandingSource;
        HashSet<Ff7WorldTerrainReference> riderComponent = guide.RiderReachableFaces.ToHashSet();
        bool HasLanding(Ff7AutoPlayWorldMovementState state)
        {
            Ff7AutoPlayWorldLandingPreview landing = world.PreviewAutoPlaySurfaceLanding(state);
            return landing.Supported && landing.Rider?.Model == guide.RiderModel && landing.Ground is not null && riderComponent.Contains(landing.Ground);
        }
        if (HasLanding(source))
        {
            worldRoute.Clear();
            return new(Ff7AutoPlayWorldNavigationStatus.Step, default, 0, "The actual native pose admits a landing in the rider goal component.");
        }
        string sourceKey = WorldMovementKey(source);
        string positionKey = WorldPositionKey(source);
        string identity = $"{source.WorldId}:{source.WorldMapState}:{source.Player.Model}:{target.Id}:{target.GeometryFingerprint}:{target.PredicateFingerprint}:vehicle-approach:{guide.RiderModel}:{guide.LandingSource}:{waypoint.Source}:{seekingLanding}";
        if (worldRepeatedTarget != identity)
        {
            worldRepeatedInputs.Clear();
            worldRepeatedTarget = identity;
        }
        if (worldRouteTarget != identity || worldRouteRearming)
        {
            worldRoute.Clear();
            worldRouteTarget = identity;
            worldRouteRearming = false;
        }
        static bool Admit(Ff7AutoPlayWorldMovementPreview step)
        {
            return step.TerrainBoundary is not { Inert: false } && (step.ModelBoundaries?.Length ?? 0) == 0;
        }
        if (worldRoute.TryPeek(out Ff7AutoPlayWorldRouteStep? retained))
        {
            Ff7AutoPlayWorldMovementPreview fresh = world.PreviewAutoPlayInput(retained.Input, source);
            if (retained.SourceKey == sourceKey && fresh is { Supported: true, State: not null } && WorldMovementKey(fresh.State) == retained.DestinationKey && fresh.TerrainBoundary == retained.Boundary && (fresh.ModelBoundaries ?? []).SequenceEqual(retained.ModelBoundaries ?? []) && Admit(fresh))
            {
                worldRoute.Dequeue();
                RememberWorldInput(positionKey, retained.Input);
                return new(Ff7AutoPlayWorldNavigationStatus.Step, retained.Input, 0, PredictedBoundary: fresh.TerrainBoundary, HasInput: true);
            }
            worldRoute.Clear();
        }
        bool Reached(Ff7AutoPlayWorldMovementPreview step)
        {
            return seekingLanding ? HasLanding(step.State!) : step.State!.Input.GroundTriangle == waypoint.Source;
        }
        Ff7AutoPlayWorldRoute Search(int budget, IReadOnlySet<Ff7AutoplayInput>? avoidFirst)
        {
            return SearchWorldPhysicalRoute(source, (input, state) => world.PreviewAutoPlayInput(input, state), state => WorldPointDistance(state.Player, waypoint.Center), Admit, Reached, budget, avoidFirst);
        }
        worldRepeatedInputs.TryGetValue(positionKey, out HashSet<Ff7AutoplayInput>? repeated);
        Ff7AutoPlayWorldRoute route = Search(nodeBudget, repeated);
        int expanded = route.Expanded;
        if (route.Steps.Length == 0 && repeated is { Count: > 0 })
        {
            repeated.Clear();
            if (expanded < nodeBudget)
            {
                route = Search(nodeBudget - expanded, null);
                expanded += route.Expanded;
            }
            else
            {
                PlanningPending = true;
            }
        }
        foreach (Ff7AutoPlayWorldRouteStep step in route.Steps)
        {
            worldRoute.Enqueue(step);
        }
        if (!worldRoute.TryDequeue(out Ff7AutoPlayWorldRouteStep? first))
        {
            return route.Status == Ff7AutoPlayWorldNavigationStatus.SearchLimited
                ? new(Ff7AutoPlayWorldNavigationStatus.Unsupported, default, expanded, "The bounded native vehicle approach did not establish an advancing input; its advisory edge or landing pose requires refinement.")
                : new(route.Status, default, expanded, route.Reason);
        }
        RememberWorldInput(positionKey, first.Input);
        return new(route.Status, first.Input, expanded, route.Reason, first.Boundary, HasInput: true);
    }
}
