using Memoria.AutoPlay;
using Memoria.FF7.Formats;

namespace Memoria.FF7.Runtime;

public sealed partial class Ff7AutoPlayPolicy
{
    private void RetainDestinationGoal(AutoPlayPlanStep next, string navigationKey, string condition)
    {
        RetainDestinationGoal(State, next, navigationKey, condition);
    }

    internal static void RetainDestinationGoal(Ff7AutoPlayState state, AutoPlayPlanStep next, string navigationKey, string condition)
    {
        AutoPlayTravelGoal? retained = state.DestinationField is not null && state.NavigationGoal is { EventId: not null } goal && goal.EventId == next.Event.Id && goal.NavigationKey == navigationKey && (goal.Conditions is null || goal.Conditions == condition) ? goal : null;
        state.NavigationGoal = next.Goal ?? retained;
    }

    internal static string? ResolveFieldDestinationGoal(Ff7AutoPlayState state, Ff7AutoPlayObservation observation, Func<ushort, int[]>? readConnections = null, Func<Ff7AutoPlayTarget, bool>? canSelect = null, Func<Ff7AutoPlayTarget, Ff7AutoPlayTarget, bool>? canUnblockGateway = null, Func<int, int[]>? readWorldConnections = null, Func<Ff7AutoPlayTarget, string?>? readDepartureSource = null, Func<ushort, Ff7AutoPlayFieldTopology?>? readTopology = null, Ff7Actor? player = null, byte[]? blocked = null, Ff7AutoPlayTarget? prerequisite = null)
    {
        if (state.DestinationField is not ushort destination)
        {
            return null;
        }
        Ff7AutoPlayTarget[] candidates = observation.Events.Where(target => target.AnalysisComplete && (target.Kind == Ff7AutoPlayTargetKind.Gateway || target.ChangesField) && TargetDestinations(target).Contains(destination) && CanRequestFieldTravel(state, observation, target) && canSelect?.Invoke(target) != false).ToArray();
        Ff7AutoPlayTarget? selected = prerequisite is not null && observation.Events.Contains(prerequisite) && canSelect?.Invoke(prerequisite) != false ? prerequisite : null;
        selected ??= candidates.FirstOrDefault(target => state.NavigationGoal is { } retained && retained.NavigationKey == observation.NavigationKey && retained.EventId == target.Id)
            ?? candidates.FirstOrDefault(target => target.DestinationField == destination)
            ?? candidates.FirstOrDefault();
        bool limited = false;
        if (selected is null && readConnections is not null)
        {
            Dictionary<ushort, int[]> connections = [];
            bool resourceLimit = false;
            int[] ReadConnections(ushort field)
            {
                if (connections.TryGetValue(field, out int[]? known))
                {
                    return known;
                }
                if (connections.Count >= 128)
                {
                    resourceLimit = true;
                    return [];
                }
                int[] result = field is >= 1 and <= 64 ? readWorldConnections?.Invoke(field) ?? [] : readConnections(field);
                connections[field] = result;
                return result;
            }
            if (canUnblockGateway is not null)
            {
                selected = FindFieldDestinationPrerequisite(state, observation, destination, ReadConnections, canSelect, canUnblockGateway);
            }
            if (selected is null && readTopology is not null && player is not null && blocked is not null)
            {
                Ff7AutoPlayFieldTopology? ReadTopology(ushort field)
                {
                    ReadConnections(field);
                    return connections.ContainsKey(field) ? readTopology(field) : null;
                }
                (selected, limited) = FindFieldEntryDestinationAdvice(state, observation, destination, player, blocked, ReadTopology, canSelect);
            }
            if (selected is null)
            {
                (selected, bool fallbackLimited) = FindFieldDestinationAdvice(state, observation, destination, ReadConnections, canSelect);
                limited |= fallbackLimited;
            }
            limited |= resourceLimit;
        }
        if (selected is not null)
        {
            state.NavigationGoal = new(readDepartureSource?.Invoke(selected) ?? observation.NavigationKey, selected.Id, selected.ConditionFingerprint);
            AutoPlayEventCoverage<Ff7AutoPlayDeferral>? coverage = state.Coverage.Visits.SingleOrDefault(visit => visit.Visit.Key == observation.VisitId)?.Events.SingleOrDefault(item => item.Event.Id == selected.Id);
            if (coverage is { Completed: true } && coverage.Conditions?.HasExecutedEntry(selected.ConditionFingerprint) == true)
            {
                // Explicit travel intent rearms the route without erasing native consumption or component receipts.
                coverage.RouteNeedsRediscovery = true;
                coverage.GlobalRouteRediscovery = true;
            }
        }
        else if (readConnections is not null)
        {
            state.NavigationGoal = null;
            return limited ? $"Native field connectivity advice to field {destination} reached its bounded search limit." : $"No currently admitted native field connectivity route to field {destination} is established from this navigation component.";
        }
        return null;
    }

    internal static string? ReadKnownFieldDepartureSource(Ff7AutoPlayState state, Ff7AutoPlayObservation observation, Ff7AutoPlayTarget target)
    {
        Dictionary<string, AutoPlayNavigationVisit> visits = state.Coverage.NavigationVisits.ToDictionary(visit => visit.Key, StringComparer.Ordinal);
        HashSet<string> reached = [observation.NavigationKey];
        Queue<string> pending = new();
        pending.Enqueue(observation.NavigationKey);
        var read = CreateRetainedNavigationAvailabilityReader(state);
        while (pending.TryDequeue(out string? source))
        {
            foreach (AutoPlayNavigationRoute route in state.Coverage.NavigationRoutes.Where(route => route.SourceKey == source))
            {
                if (!visits.TryGetValue(route.SourceKey, out var origin) || !visits.TryGetValue(route.DestinationKey, out var destination))
                {
                    continue;
                }
                AutoPlayEvent? entry = origin.Events.SingleOrDefault(item => item.Id == route.EventId && item.ConditionSignature == route.Route.ConditionSignature);
                if (entry is null || read(origin, entry)?.Unavailable == true)
                {
                    continue;
                }
                if (route.EventId == target.Id && route.Route.ConditionSignature == target.ConditionFingerprint && TargetDestinations(target).Contains(ReadVisitField(destination.VisitKey)))
                {
                    return source;
                }
                if (origin.VisitKey == observation.VisitId && destination.VisitKey == observation.VisitId && reached.Add(destination.Key))
                {
                    pending.Enqueue(destination.Key);
                }
            }
        }
        return null;
    }

    private static Ff7AutoPlayTarget? FindFieldDestinationPrerequisite(Ff7AutoPlayState state, Ff7AutoPlayObservation observation, ushort destination, Func<ushort, int[]> readConnections, Func<Ff7AutoPlayTarget, bool>? canSelect, Func<Ff7AutoPlayTarget, Ff7AutoPlayTarget, bool> canUnblockGateway)
    {
        Ff7AutoPlayTarget[] producers = observation.Events.Where(target => target.AnalysisComplete && target.MayChangeNavigation && !target.ChangesField && (target.UnblockedTriangles.Length != 0 || target.LadderLandings.Length != 0 || target.TerminalJumpLandings.Length != 0 || target.PossibleJumpLandings.Length != 0) && canSelect?.Invoke(target) != false).Where(target =>
        {
            AutoPlayEventCoverage<Ff7AutoPlayDeferral>? coverage = state.Coverage.Visits.SingleOrDefault(visit => visit.Visit.Key == observation.VisitId)?.Events.SingleOrDefault(item => item.Event.Id == target.Id);
            return coverage is null || !coverage.PolicyDeferred && coverage.BlockedReason is null && ((coverage.Conditions is { } conditions ? !conditions.HasExecutedEntry(target.ConditionFingerprint) : !coverage.Completed) || HasRecordedLocalNavigationStep(state, observation, target) || HasInterruptedLocalNavigationStep(state, observation, target));
        }).ToArray();
        Ff7AutoPlayTarget[] gateways = observation.Events.Where(target => target.AnalysisComplete && target.Kind == Ff7AutoPlayTargetKind.Gateway && canSelect?.Invoke(target) == false && producers.Any(producer => canUnblockGateway(producer, target))).ToArray();
        if (gateways.Length == 0)
        {
            return null;
        }
        var (gateway, _) = FindFieldDestinationAdvice(state, observation with { Events = gateways }, destination, readConnections, null);
        return gateway is null ? null : producers.First(producer => canUnblockGateway(producer, gateway));
    }

    internal static bool HasRecordedLocalNavigationStep(Ff7AutoPlayState state, Ff7AutoPlayObservation observation, Ff7AutoPlayTarget target)
    {
        return state.DestinationField is not null && state.Coverage.NavigationRoutes.Any(route => route.SourceKey == observation.NavigationKey && route.DestinationKey != route.SourceKey && route.EventId == target.Id && route.Route.TargetId == target.Id && route.Route.ConditionSignature == target.ConditionFingerprint && route.Route.DestinationVisitKey == observation.VisitId && state.Coverage.NavigationVisits.Any(visit => visit.Key == route.DestinationKey && visit.VisitKey == observation.VisitId));
    }

    internal static bool HasInterruptedLocalNavigationStep(Ff7AutoPlayState state, Ff7AutoPlayObservation observation, Ff7AutoPlayTarget target)
    {
        return state.DestinationField is not null && state.Coverage.NavigationVisits.Any(visit => visit.Key == observation.NavigationKey && visit.VisitKey == observation.VisitId) && observation.Events.Contains(target) && target.AnalysisComplete && target.MayChangeNavigation && !target.ChangesField && !target.ChangesStory && target.Kind is >= Ff7AutoPlayTargetKind.LineTalk and <= Ff7AutoPlayTargetKind.LineLeave && (target.LadderLandings.Length != 0 || target.PossibleJumpLandings.Length != 0 || target.UnblockedTriangles.Length != 0) && state.Coverage.NavigationRoutes.Any(route => route.SourceKey == observation.NavigationKey && route.EventId == target.Id && route.Route.TargetId == target.Id && route.Route.ConditionSignature == target.ConditionFingerprint && route.Route.DestinationVisitKey != observation.VisitId && state.Coverage.NavigationVisits.Any(visit => visit.Key == route.DestinationKey && visit.VisitKey == route.Route.DestinationVisitKey));
    }

    private bool CanUnblockDestinationGateway(Ff7AutoPlayTarget producer, Ff7AutoPlayTarget gateway, IReadOnlyList<Ff7AutoPlayTarget> targets)
    {
        if (gateway.GatewayIndex is not int index || (uint)index >= session.Field.Triggers.Gateways.Count)
        {
            return false;
        }
        Ff7Actor player = session.Host.Process.Actors.Single(actor => actor.PlayerControlled);
        PcFieldGateway native = session.Field.Triggers.Gateways[index];
        return CanUnblockFieldGateway(session.Field.Walkmesh, session.Host.Process.BlockedTriangles, player, native, producer.UnblockedTriangles) || CanEstablishFieldGatewayLanding(session.Field.Walkmesh, session.Host.Process.BlockedTriangles, player, native, producer.LadderLandings) || CanEstablishFieldGatewayJumpLanding(session.Field.Walkmesh, session.Host.Process.BlockedTriangles, player, native, producer.TerminalJumpLandings) || CanEstablishFieldGatewayComponentRoute(session.Field.Walkmesh, session.Host.Process.BlockedTriangles, player, native, producer, targets, session.Host.Process.Lines);
    }

    internal static bool CanEstablishFieldGatewayComponentRoute(IReadOnlyList<PcWalkmeshTriangle> mesh, byte[] blocked, Ff7Actor player, PcFieldGateway gateway, Ff7AutoPlayTarget first, IReadOnlyList<Ff7AutoPlayTarget> targets, IReadOnlyList<Ff7Line> lines)
    {
        bool Supported(Ff7AutoPlayTarget target) => target.AnalysisComplete && target.MayChangeNavigation && !target.ChangesField && !target.ChangesStory && target.Kind is >= Ff7AutoPlayTargetKind.LineTalk and <= Ff7AutoPlayTargetKind.LineLeave;
        if (!Supported(first) || !targets.Contains(first) || Ff7AutoplayDriver.HasGatewayTopologyPath(mesh, blocked, player, gateway, out _))
        {
            return false;
        }
        bool Contact(Ff7Actor actor, Ff7AutoPlayTarget target) => lines.Any(line => line.Entity == target.Entity && line.Enabled == 1 && Ff7AutoplayDriver.DiscoverLineContacts(mesh, blocked, actor, line).Count != 0);
        IEnumerable<Ff7Actor> Endpoints(Ff7AutoPlayTarget target)
        {
            foreach (var landing in target.PossibleJumpLandings.Where(landing => landing.Entity == player.Entity && landing.Literal && landing.Command.Duration > 0))
            {
                var jump = landing.Command;
                if ((uint)jump.Triangle < mesh.Count && jump.Triangle >> 3 < blocked.Length && (blocked[jump.Triangle >> 3] & 1 << (jump.Triangle & 7)) == 0 && Ff7AutoplayDriver.Contains(mesh[jump.Triangle], new(jump.X, jump.Y, 0), out float height))
                {
                    yield return player with { X4096 = jump.X << 12, Y4096 = jump.Y << 12, Z4096 = (int)(height * 4096), Triangle = jump.Triangle };
                }
            }
            foreach (var landing in target.LadderLandings.Where(landing => landing.Entity == player.Entity && landing.Literal && landing.Command.Mode <= 3))
            {
                var ladder = landing.Command;
                if ((uint)ladder.Triangle < mesh.Count && ladder.Triangle >> 3 < blocked.Length && (blocked[ladder.Triangle >> 3] & 1 << (ladder.Triangle & 7)) == 0 && Ff7AutoplayDriver.Contains(mesh[ladder.Triangle], new(ladder.X, ladder.Y, ladder.Z), out float height) && height >= ladder.Z - 255 && height < ladder.Z + 256)
                {
                    yield return player with { X4096 = ladder.X << 12, Y4096 = ladder.Y << 12, Z4096 = ladder.Z << 12, Triangle = ladder.Triangle };
                }
            }
        }
        if (!Contact(player, first))
        {
            return false;
        }
        Queue<Ff7Actor> pending = new(Endpoints(first));
        HashSet<int> visited = [player.Triangle];
        while (pending.TryDequeue(out Ff7Actor? current))
        {
            if (!visited.Add(current.Triangle))
            {
                continue;
            }
            if (Ff7AutoplayDriver.HasGatewayTopologyPath(mesh, blocked, current, gateway, out _))
            {
                return true;
            }
            foreach (Ff7AutoPlayTarget target in targets.Where(Supported).Where(target => Contact(current, target)))
            {
                foreach (Ff7Actor endpoint in Endpoints(target))
                {
                    pending.Enqueue(endpoint);
                }
            }
        }
        return false;
    }

    internal static bool CanEstablishFieldGatewayJumpLanding(IReadOnlyList<PcWalkmeshTriangle> walkmesh, byte[] blockedTriangles, Ff7Actor player, PcFieldGateway gateway, IReadOnlyList<Memoria.FF7.Events.FieldActorJumpLanding> landings)
    {
        if (Ff7AutoplayDriver.HasGatewayTopologyPath(walkmesh, blockedTriangles, player, gateway, out _))
        {
            return false;
        }
        var effects = landings.Where(landing => landing.Entity == player.Entity).Distinct().ToArray();
        if (effects.Length != 1 || !effects[0].Literal || effects[0].Command is not { Duration: > 0 } jump || (uint)jump.Triangle >= walkmesh.Count || jump.Triangle >> 3 >= blockedTriangles.Length || (blockedTriangles[jump.Triangle >> 3] & 1 << (jump.Triangle & 7)) != 0)
        {
            return false;
        }
        System.Numerics.Vector3 point = new(jump.X, jump.Y, 0);
        if (!Ff7AutoplayDriver.Contains(walkmesh[jump.Triangle], point, out float height))
        {
            return false;
        }
        Ff7Actor projected = player with { X4096 = jump.X << 12, Y4096 = jump.Y << 12, Z4096 = (int)(height * 4096), Triangle = jump.Triangle };
        return Ff7AutoplayDriver.HasGatewayTopologyPath(walkmesh, blockedTriangles, projected, gateway, out _);
    }

    internal static bool CanEstablishFieldGatewayLanding(IReadOnlyList<PcWalkmeshTriangle> walkmesh, byte[] blockedTriangles, Ff7Actor player, PcFieldGateway gateway, IReadOnlyList<Memoria.FF7.Events.FieldActorLadderLanding> landings)
    {
        if (Ff7AutoplayDriver.HasGatewayTopologyPath(walkmesh, blockedTriangles, player, gateway, out _))
        {
            return false;
        }
        var effects = landings.Where(landing => landing.Entity == player.Entity).Distinct().ToArray();
        if (effects.Length != 1 || !effects[0].Literal || effects[0].Command is not { Mode: <= 3 } ladder || (uint)ladder.Triangle >= walkmesh.Count || ladder.Triangle >> 3 >= blockedTriangles.Length || (blockedTriangles[ladder.Triangle >> 3] & 1 << (ladder.Triangle & 7)) != 0)
        {
            return false;
        }
        System.Numerics.Vector3 point = new(ladder.X, ladder.Y, ladder.Z);
        if (!Ff7AutoplayDriver.Contains(walkmesh[ladder.Triangle], point, out float height) || height < ladder.Z - 255 || height >= ladder.Z + 256)
        {
            return false;
        }
        Ff7Actor projected = player with { X4096 = ladder.X << 12, Y4096 = ladder.Y << 12, Z4096 = ladder.Z << 12, Triangle = ladder.Triangle };
        return Ff7AutoplayDriver.HasGatewayTopologyPath(walkmesh, blockedTriangles, projected, gateway, out _);
    }

    internal static bool CanUnblockFieldGateway(IReadOnlyList<PcWalkmeshTriangle> walkmesh, byte[] blockedTriangles, Ff7Actor player, PcFieldGateway gateway, IReadOnlyList<int> unblockedTriangles)
    {
        if (unblockedTriangles.Count == 0 || unblockedTriangles.Any(triangle => (uint)triangle >= walkmesh.Count || triangle >> 3 >= blockedTriangles.Length) || Ff7AutoplayDriver.HasGatewayTopologyPath(walkmesh, blockedTriangles, player, gateway, out _))
        {
            return false;
        }
        byte[] opened = (byte[])blockedTriangles.Clone();
        foreach (int triangle in unblockedTriangles)
        {
            opened[triangle >> 3] &= unchecked((byte)~(1 << (triangle & 7)));
        }
        return Ff7AutoplayDriver.HasGatewayTopologyPath(walkmesh, opened, player, gateway, out _);
    }

    private static (Ff7AutoPlayTarget? Target, bool Limited) FindFieldDestinationAdvice(Ff7AutoPlayState state, Ff7AutoPlayObservation observation, ushort destination, Func<ushort, int[]> readConnections, Func<Ff7AutoPlayTarget, bool>? canSelect)
    {
        const int maximumFields = 128;
        Queue<(ushort Field, Ff7AutoPlayTarget First)> pending = new();
        int current = ReadVisitField(observation.VisitId);
        HashSet<ushort> visited = current > 64 && current < short.MaxValue ? [(ushort)current] : [];
        foreach (Ff7AutoPlayTarget target in observation.Events.Where(target => target.AnalysisComplete && (target.Kind == Ff7AutoPlayTargetKind.Gateway || target.ChangesField) && CanRequestFieldTravel(state, observation, target) && canSelect?.Invoke(target) != false))
        {
            foreach (int field in TargetDestinations(target).Where(field => field > 0 && field < short.MaxValue && field != current))
            {
                pending.Enqueue(((ushort)field, target));
            }
        }
        while (pending.Count != 0 && visited.Count < maximumFields)
        {
            var step = pending.Dequeue();
            if (!visited.Add(step.Field))
            {
                continue;
            }
            if (step.Field == destination)
            {
                return (step.First, false);
            }
            IEnumerable<int> recorded = state.Coverage.Visits.Where(visit => ReadVisitField(visit.Visit.Key) == step.Field).SelectMany(visit => visit.Events).SelectMany(item => item.Routes).Select(route => ReadVisitField(route.DestinationVisitKey));
            foreach (int field in readConnections(step.Field).Concat(recorded).Where(field => field > 0 && field < short.MaxValue).Distinct())
            {
                pending.Enqueue(((ushort)field, step.First));
            }
        }
        return (null, pending.Count != 0);
    }

    internal static bool IsDestinationPlan(Ff7AutoPlayState state, AutoPlayPlanStep? next, string navigationKey)
    {
        return state.DestinationField is null || state.NavigationGoal is { } goal && next is not null && (next.Goal == goal || goal.NavigationKey == navigationKey && next.Event.Id == goal.EventId && next.Event.ConditionSignature == goal.Conditions);
    }

    private static IEnumerable<int> TargetDestinations(Ff7AutoPlayTarget target)
    {
        return target.PlanningDestinationFields ?? (target.DestinationField is int field ? new[] { field } : []).Concat(target.PossibleDestinationFields).Concat(target.SpatialDestinations ?? []).Distinct();
    }

    private static bool CanRequestFieldTravel(Ff7AutoPlayState state, Ff7AutoPlayObservation observation, Ff7AutoPlayTarget target)
    {
        AutoPlayEventCoverage<Ff7AutoPlayDeferral>? coverage = state.Coverage.Visits.SingleOrDefault(visit => visit.Visit.Key == observation.VisitId)?.Events.SingleOrDefault(item => item.Event.Id == target.Id);
        if (coverage is not { Completed: true } || coverage.Conditions?.HasExecutedEntry(target.ConditionFingerprint) != true)
        {
            return true;
        }
        // Possible branches are advice; repeat travel requires an actual transfer under this same entry condition.
        return coverage.Routes.Any(route => route.ConditionSignature == target.ConditionFingerprint && (ReadVisitField(route.DestinationVisitKey) > 64 && ReadVisitField(route.DestinationVisitKey) != ReadVisitField(observation.VisitId) || TargetDestinations(target).Any(selector => selector is >= 1 and <= 64 && route.DestinationVisitKey.StartsWith($"ff7-world:{Ff7WorldRuntime.ResolveWorldId(selector)}:", StringComparison.Ordinal))));
    }

    private static int ReadVisitField(string visit)
    {
        string[] parts = visit.Split(':', 3);
        return parts.Length == 3 && parts[0] == "ff7" && int.TryParse(parts[1], out int field) ? field : -1;
    }

    /// <summary>Sets a planning destination without changing native execution or selecting physical inputs.</summary>
    public void SetDestinationField(ushort? destination)
    {
        if (destination is ushort field && !session.IsKnownAutoPlayDestination(field))
        {
            throw new ArgumentOutOfRangeException(nameof(destination), "An autoplay destination must identify an ordinary native field.");
        }
        if (State.Active is { Accepted: true } || State.Choice is not null || State.PendingRestore is not null)
        {
            throw new InvalidOperationException("The current native event must return before changing its planning destination.");
        }
        State.DestinationField = destination;
        State.Active = null;
        State.NavigationGoal = null;
        worldModeCatalog.Clear();
        worldModeQueue.Clear();
        worldModeCandidatesObserved = false;
        State.Status = destination is null ? "Resuming native frontier exploration" : $"Planning a native route to field {destination}";
        Publish();
    }
}
