using System.Numerics;
using System.Security.Cryptography;
using System.Text.Json;
using Memoria.FF7.Events;

namespace Memoria.FF7.Runtime;

internal enum Ff7NativeGraphEdgeKind { Wait, Portal, Goal, OwnedEvent, OwnerSettlement }
internal sealed record Ff7NativeGraphOwner(int Entity, int Priority, int Root, bool Returned, int Script = -1, int ParentEntity = -1, int ParentPriority = -1);
internal sealed record Ff7NativeGraphDebt(Ff7AutoPlayTarget[] Roots, bool[] Pending, Ff7NativeGraphOwner[] Owners, long? LadderRequest = null, int LadderEntity = -1, int LadderPriority = -1, bool BackwardCompleted = false, int LadderRoot = -1);
internal sealed record Ff7NativeGraphState(Ff7SessionSave Native, Ff7NativeGraphDebt? Debt, Ff7NativeGraphDebt? Recovery = null);
internal sealed record Ff7NativeGraphEdge(Ff7NativeGraphEdgeKind Kind, Vector3 Point, int? Triangle = null, Ff7AutoPlayTarget? Event = null, bool Run = true);

public sealed partial class Ff7AutoplayDriver
{
    internal static string ReadExactNativeGraphFingerprint(Ff7SessionSave native, string supervision)
    {
        return Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new { Native = native with { AutoPlay = null, EventCoverage = null }, Supervision = supervision })));
    }

    private static string NativeGraphKey(Ff7NativeGraphState state) => ReadExactNativeGraphFingerprint(state.Native, JsonSerializer.Serialize(new { state.Debt, state.Recovery }));

    private IEnumerable<int> EnumerateNativeStateGraph(Ff7AutoPlayTarget target, IReadOnlyList<Ff7AutoPlayTarget> targets, Ff7NativeRouteProbe probe)
    {
        if (!SupportsNativeRouteTarget(target) || !HasNativeRouteClockContract(session))
        {
            yield break;
        }
        int initialDistance = ReadNativeRouteDistance(target, targets);
        if (initialDistance == int.MaxValue)
        {
            yield break;
        }
        Ff7SessionSave rootSave = session.CaptureAutoPlayNavigation();
        Ff7NativeGraphState root = new(rootSave, ReadRootNativeDebt(session, targets));
        using Ff7NativeStateGraph<Ff7NativeGraphState, Ff7AutoPlayRouteStep> graph = new(root, NativeGraphKey(root), NativeGraphAdvisory(rootSave.Process!, target.Position));
        long summaryId = ++nativeCandidateId;
        Ff7Actor rootActor = rootSave.Process!.Actors.Single(actor => actor.PlayerControlled);
        nativeCandidateSummaries.Set(summaryId, new("NativeGraph", root.Debt is not null, target.Id, 0, rootSave.Process.Tick, new(rootActor.X4096, rootActor.Y4096, rootActor.Z4096, rootActor.Triangle, rootActor.Direction), false, false, Vector3.Distance(Position3D(rootActor), target.Position)));
        try
        {
            while (graph.TryBeginExpansion(out var node))
            {
                Ff7NativeGraphState state = node!.State!;
                Ff7NativeGraphEdge[] edges;
                using (Ff7Session inspect = session.RestoreAutoPlayNavigation(state.Native))
                {
                    Ff7AutoPlayObservation current = new Ff7AutoPlayDiscovery(inspect).Observe();
                    edges = ReadNativeGraphEdges(inspect, state.Debt, current, target).ToArray();
                }
                foreach (Ff7NativeGraphEdge edge in edges)
                {
                    using IEnumerator<int> expansion = ExpandNativeGraphEdge(graph, node, state, edge, target, initialDistance, probe, summaryId, rootSave.Process!.Actors.Single(actor => actor.PlayerControlled)).GetEnumerator();
                    while (expansion.MoveNext())
                    {
                        yield return expansion.Current;
                        if (probe.Result is not null)
                        {
                            yield break;
                        }
                    }
                    if (probe.Result is not null)
                    {
                        yield break;
                    }
                }
                graph.CompleteExpansion(node);
            }
        }
        finally
        {
            nativeCandidateSummaries.Remove(summaryId);
            nativeGraphWork.Remove(summaryId);
        }
        LastNativeRouteProbe = "The represented native guide graph exhausted without an authenticated goal boundary.";
    }

    private IEnumerable<int> ExpandNativeGraphEdge(Ff7NativeStateGraph<Ff7NativeGraphState, Ff7AutoPlayRouteStep> graph, Ff7NativeGraphNode<Ff7NativeGraphState, Ff7AutoPlayRouteStep> parent, Ff7NativeGraphState source, Ff7NativeGraphEdge edge, Ff7AutoPlayTarget goal, int initialDistance, Ff7NativeRouteProbe probe, long id, Ff7Actor original)
    {
        NativeGraphOwnership ownership = new(source.Debt, edge.Kind == Ff7NativeGraphEdgeKind.OwnedEvent ? edge.Event : null);
        List<Ff7AutoPlayRouteStep> segment = [];
        Ff7NativeGraphState work = source;
        Ff7NativeGraphDebt? recovery = source.Recovery;
        try
        {
            while (true)
            {
                nativeGraphWork[id] = (graph.FrontierCount, graph.VisitedCount, graph.ExpandedCount);
                probe.Progress = $"{goal.Id}:graph:{edge.Kind}:cost{parent.Cost + segment.Count}:nodes{graph.VisitedCount}:frontier{graph.FrontierCount}:expanded{graph.ExpandedCount}";
                yield return 0;
                using Ff7Session branch = session.RestoreAutoPlayNavigation(work.Native);
                ActiveNativeRouteCandidates++;
                try
                {
                    Ff7AutoplayDriver driver = new(branch);
                    Ff7AutoPlayDiscovery discovery = new(branch);
                    Ff7AutoPlayObservation current = discovery.Observe();
                    Ff7AutoPlayTarget[] protectedEvents = current.Events.Where(IsNativeGraphProtectedLine).ToArray();
                    bool selected = false;
                    bool unexpected = false;
                    Ff7Actor startingPlayer = branch.Host.Process.Actors.Single(actor => actor.PlayerControlled);
                    bool forcedSettlement = edge.Kind == Ff7NativeGraphEdgeKind.OwnerSettlement && ownership.HasDebt && (branch.Host.Process.PlayerLocked || branch.Host.Process.Join is not null || startingPlayer.Ladder is not null || branch.Host.HasScriptedMotion(startingPlayer) || ownership.HasPendingRoot);
                    Ff7NativeTickObservation? lastTick = null;
                    branch.NativeTickCompleted += tick => lastTick = tick;
                    branch.InteractionScriptRequested += (entity, script, accepted) =>
                    {
                        if (accepted && forcedSettlement && !ownership.Allows(entity, script) && protectedEvents.FirstOrDefault(item => item.Entity == entity && item.Script == script && !driver.CanSkipNavigationLine(item, branch.Machine.Input, false)) is { } forcedRoot)
                        {
                            ownership.AdoptForcedRoot(forcedRoot);
                        }
                        bool owned = ownership.Requested(entity, script, accepted);
                        selected |= accepted && entity == goal.Entity && script == goal.Script;
                        if (accepted && !owned && (entity != goal.Entity || script != goal.Script) && protectedEvents.Any(item => item.Entity == entity && item.Script == script && !driver.CanSkipNavigationLine(item, branch.Machine.Input, false)))
                        {
                            unexpected = true;
                        }
                    };
                    branch.Machine.ScriptDispatched += ownership.Dispatched;
                    branch.Machine.ScriptReturned += ownership.Returned;
                    // Every predecessor has passed this same executed-operand witness.
                    using Ff7NativeRouteClockWitness clock = new(branch.Machine);
                    driver.BeginPlanningStep();
                    Ff7Actor before = branch.Host.Process.Actors.Single(actor => actor.PlayerControlled);
                    Ff7NativeGraphState lastSafe = work;
                    Int128 distance = NativeGraphFixedDistance(before, edge.Point);
                    Ff7AutoplayInput input;
                    long physical = driver.PhysicalPlanningWork;
                    try
                    {
                        input = ChooseNativeGraphInput(branch, driver, edge, goal, current.Events, ownership);
                    }
                    catch (Ff7AutoPlayNavigationUnavailableException)
                    {
                        if (segment.Count > 0)
                        {
                            graph.AddSuccessor(parent, lastSafe, NativeGraphKey(lastSafe), segment, NativeGraphAdvisory(lastSafe.Native.Process!, goal.Position));
                        }
                        break;
                    }
                    finally
                    {
                        DetachedPhysicalPlanningWork += driver.PhysicalPlanningWork - physical;
                    }
                    bool pending = driver.PlanningPending;
                    if (pending)
                    {
                        input = default;
                        driver.BeginPlanningStep();
                    }
                    segment.Add(new(ReadNativeRouteFingerprint(branch), input));
                    driver.Apply(input);
                    branch.AdvanceNavigationTick();
                    DetachedNativeCallbacks++;
                    Ff7Actor after = branch.Host.Process.Actors.Single(actor => actor.PlayerControlled);
                    ownership.ObserveLadder(branch, after);
                    current = discovery.Observe();
                    protectedEvents = current.Events.Where(IsNativeGraphProtectedLine).ToArray();
                    if (forcedSettlement)
                    {
                        foreach (Ff7AutoPlayTarget forced in protectedEvents.Where(item => item.Id != goal.Id && !ownership.Allows(item.Entity, item.Script)))
                        {
                            if (driver.ReadPendingNavigationLineRequest([forced]) is not null)
                            {
                                ownership.AdoptForcedRoot(forced);
                            }
                        }
                    }
                    Ff7AutoPlayTarget? queued = driver.ReadPendingNavigationLineRequest(protectedEvents.Where(item => item.Id != goal.Id && !ownership.Allows(item.Entity, item.Script)).ToArray());
                    bool unsafeEdge = unexpected || ownership.Invalid || queued is not null || !clock.Independent || !HasNativeRouteClockContract(branch);
                    Ff7Line? line = branch.Host.Process.Lines.LastOrDefault(item => item.Entity == goal.Entity);
                    Ff7AutoPlayNativeIdleFacts idle = new(branch.Host.Process.PlayerLocked, branch.Host.Process.Join is not null, after.Ladder is { Completed: false }, after.Ladder is { Completed: true }, goal.Kind != Ff7AutoPlayTargetKind.Gateway && line?.Enabled != 1, input, line);
                    nativeCandidateSummaries.Set(id, new("NativeGraph", ownership.HasDebt || recovery is not null, goal.Id, checked((int)(parent.Cost + segment.Count)), branch.Host.Process.Tick, new(after.X4096, after.Y4096, after.Z4096, after.Triangle, after.Direction), after.X4096 != original.X4096 || after.Y4096 != original.Y4096 || after.Z4096 != original.Z4096 || after.Triangle != original.Triangle, pending, Vector3.Distance(new(after.X4096 / 4096f, after.Y4096 / 4096f, after.Z4096 / 4096f), goal.Position), idle));
                    if (unsafeEdge)
                    {
                        segment.RemoveAt(segment.Count - 1);
                        if (segment.Count > 0)
                        {
                            graph.AddSuccessor(parent, lastSafe, NativeGraphKey(lastSafe), segment, NativeGraphAdvisory(lastSafe.Native.Process!, goal.Position));
                        }
                        break;
                    }
                    ownership.ObserveQueued(branch, current.Events);
                    if (lastTick is not null && input == default && ownership.HasDebt)
                    {
                        probe.QueuedLineWaitObserved?.Invoke(segment[^1], lastTick);
                    }
                    bool settled = ownership.IsSettled(branch);
                    if (branch.Host.Process.Transition is not null)
                    {
                        if (goal.Kind == Ff7AutoPlayTargetKind.Gateway && branch.GatewayDeparture is { } departure && departure.GatewayIndex == goal.GatewayIndex && departure.Transition.Map == goal.DestinationField && branch.HasNativeGatewayDeparture(departure))
                        {
                            SetNativeGraphProof(graph, parent, segment, branch, goal, probe, gateway: departure);
                        }
                        break;
                    }
                    if (branch.Battle is not null || branch.World is not null || branch.FieldGeneration != session.FieldGeneration)
                    {
                        if (branch.Battle is not null && branch.FieldGeneration == session.FieldGeneration && driver.ReadNativeRouteDistance(goal, current.Events) < initialDistance)
                        {
                            SetNativeGraphProof(graph, parent, segment, branch, goal, probe, battle: true);
                        }
                        break;
                    }
                    if (selected)
                    {
                        SetNativeGraphProof(graph, parent, segment, branch, goal, probe);
                        break;
                    }
                    if (settled && ownership.OwnsLadder)
                    {
                        recovery = ownership.Capture();
                    }
                    bool released = (settled || !ownership.HasDebt) && recovery is not null && new NativeGraphOwnership(recovery, null).CanReleaseSource(branch);
                    if (released && Ff7AutoPlayDiscovery.NativeControlReady(branch) && driver.ReadNativeRouteDistance(goal, current.Events) < initialDistance && probe.SourceRelease?.Invoke(branch, current) == true)
                    {
                        SetNativeGraphProof(graph, parent, segment, branch, goal, probe, released: true);
                        break;
                    }
                    Ff7NativeGraphDebt? debt = settled ? null : ownership.Capture();
                    bool moved = after.X4096 != before.X4096 || after.Y4096 != before.Y4096 || after.Z4096 != before.Z4096;
                    work = new(branch.CaptureAutoPlayNavigation(), debt, recovery);
                    if (edge.Kind is Ff7NativeGraphEdgeKind.Wait or Ff7NativeGraphEdgeKind.OwnerSettlement || debt is not null || settled || pending || after.Triangle != before.Triangle || !moved || NativeGraphFixedDistance(after, edge.Point) >= distance)
                    {
                        graph.AddSuccessor(parent, work, NativeGraphKey(work), segment, NativeGraphAdvisory(work.Native.Process!, goal.Position));
                        break;
                    }
                }
                finally
                {
                    ActiveNativeRouteCandidates--;
                }
            }
        }
        finally
        {
            nativeGraphWork[id] = (graph.FrontierCount, graph.VisitedCount, graph.ExpandedCount);
        }
    }

    private void SetNativeGraphProof(Ff7NativeStateGraph<Ff7NativeGraphState, Ff7AutoPlayRouteStep> graph, Ff7NativeGraphNode<Ff7NativeGraphState, Ff7AutoPlayRouteStep> parent, List<Ff7AutoPlayRouteStep> segment, Ff7Session branch, Ff7AutoPlayTarget target, Ff7NativeRouteProbe probe, bool battle = false, bool released = false, Ff7NativeGatewayDeparture? gateway = null)
    {
        Ff7AutoPlayRouteStep[] steps = [.. graph.ReadTrace(parent), .. segment];
        probe.Result = new(target.Id, target.ConditionFingerprint, session.FieldGeneration, steps, ReadNativeRouteFingerprint(branch), battle, GatewayDeparture: gateway, SourceReleased: released);
        LastNativeRouteProbe = $"Authenticated native graph {target.Id}, callbacks {steps.Length}, battle:{battle}, gateway:{gateway?.GatewayIndex}, sourceReleased:{released}";
    }

    private static IEnumerable<Ff7NativeGraphEdge> ReadNativeGraphEdges(Ff7Session branch, Ff7NativeGraphDebt? debt, Ff7AutoPlayObservation observation, Ff7AutoPlayTarget goal)
    {
        Ff7Actor player = branch.Host.Process.Actors.Single(actor => actor.PlayerControlled);
        Vector3 position = new(player.X4096 / 4096f, player.Y4096 / 4096f, player.Z4096 / 4096f);
        if (debt is not null)
        {
            yield return new(Ff7NativeGraphEdgeKind.OwnerSettlement, position);
            yield break;
        }
        yield return new(Ff7NativeGraphEdgeKind.Wait, position);
        Ff7AutoPlayTarget? currentGoal = observation.Events.SingleOrDefault(item => item.Id == goal.Id && item.Entity == goal.Entity && item.Script == goal.Script && item.Kind == goal.Kind && item.AnalysisComplete);
        if (currentGoal is not null && ReadNativeEventGuide(branch, player, currentGoal, Ff7NativeGraphEdgeKind.Goal) is { } goalEdge)
        {
            yield return goalEdge with { Run = true };
            yield return goalEdge with { Run = false };
        }
        var triangle = branch.Field.Walkmesh[player.Triangle];
        foreach (int adjacent in new[] { triangle.AdjacentAB, triangle.AdjacentBC, triangle.AdjacentCA }.Where(index => index >= 0).Distinct())
        {
            var route = ReadApproachRoutes(branch.Field.Walkmesh, branch.Host.Process.BlockedTriangles, player.Triangle, adjacent, ReadSpatialBoundaryTriangles(observation.Events), goal.Triangle, new Ff7AutoplayDriver(branch).ReadNavigationOwnedPortals(branch.Field.Walkmesh));
            if (route.Distance[player.Triangle] == 1)
            {
                var next = branch.Field.Walkmesh[adjacent];
                yield return new(Ff7NativeGraphEdgeKind.Portal, (next.A + next.B + next.C) / 3, adjacent, Run: true);
                yield return new(Ff7NativeGraphEdgeKind.Portal, (next.A + next.B + next.C) / 3, adjacent, Run: false);
            }
        }
        foreach (Ff7AutoPlayTarget item in observation.Events.Where(item => item.Id != goal.Id && IsNativeGraphProtectedLine(item)))
        {
            if (ReadNativeEventGuide(branch, player, item, Ff7NativeGraphEdgeKind.OwnedEvent) is { } eventEdge)
            {
                yield return eventEdge with { Run = true };
                yield return eventEdge with { Run = false };
            }
        }
    }

    private static Ff7NativeGraphEdge? ReadNativeEventGuide(Ff7Session branch, Ff7Actor player, Ff7AutoPlayTarget target, Ff7NativeGraphEdgeKind kind)
    {
        var contacts = target.Kind == Ff7AutoPlayTargetKind.Gateway
            ? DiscoverGatewayContacts(branch.Field.Walkmesh, branch.Host.Process.BlockedTriangles, player, branch.Field.Triggers.Gateways[target.GatewayIndex!.Value])
            : DiscoverLineContacts(branch.Field.Walkmesh, branch.Host.Process.BlockedTriangles, player, branch.Host.Process.Lines.Last(line => line.Entity == target.Entity));
        if (contacts.Count == 0)
        {
            return null;
        }
        var selected = SelectFloorContact(contacts, Position3D(player), null, player.Triangle);
        return new(kind, selected.Point, selected.Triangle, target);
    }

    private static Ff7AutoplayInput ChooseNativeGraphInput(Ff7Session branch, Ff7AutoplayDriver driver, Ff7NativeGraphEdge edge, Ff7AutoPlayTarget goal, Ff7AutoPlayTarget[] events, NativeGraphOwnership ownership)
    {
        Ff7Actor actor = branch.Host.Process.Actors.Single(actor => actor.PlayerControlled);
        ownership.ObserveLadder(branch, actor);
        if (edge.Kind == Ff7NativeGraphEdgeKind.OwnerSettlement)
        {
            if (actor.Ladder is { Completed: false } ladder && ownership.OwnsLadder)
            {
                return new(Held: BackwardLadderKey(ladder.Mode), Run: false);
            }
            Ff7AutoPlayScriptedInput scripted = new(branch);
            if (scripted.ChooseInput() is Ff7AutoplayInput demanded)
            {
                if (scripted.CurrentDemand is not { } observed || !ownership.OwnsInput(observed.Entity, observed.Priority))
                {
                    throw new NotSupportedException("A detached native input wait is not owned by this graph event.");
                }
                return demanded;
            }
            if (!branch.Host.Process.PlayerLocked)
            {
                FieldScriptInputDemand? owned = Ff7AutoPlayScriptedInput.SelectDemand(branch.Machine.ReadCurrentInputDemands().Where(demand => ownership.OwnsInput(demand.Entity, demand.Priority)));
                if (owned is not null)
                {
                    return Ff7AutoPlayScriptedInput.SelectInput(owned, branch.Machine.Input);
                }
            }
            return default;
        }
        if (edge.Kind == Ff7NativeGraphEdgeKind.Wait || branch.Host.Process.PlayerLocked || branch.Host.Process.Join is not null)
        {
            return default;
        }
        Ff7AutoPlayTarget selected = edge.Event ?? goal;
        driver.ConfigureSpatialBoundaries(events, selected);
        return driver.ChooseTargetInput(selected, events.Where(item => item.Id != goal.Id || item.Id == selected.Id).ToArray(), run =>
        {
            if (edge.Kind == Ff7NativeGraphEdgeKind.Portal)
            {
                return driver.Approach(edge.Point, edge.Triangle, run);
            }
            if (selected.Kind == Ff7AutoPlayTargetKind.Gateway)
            {
                return driver.GatewayAt(selected.GatewayIndex!.Value, run);
            }
            Ff7Line line = branch.Host.Process.Lines.Last(line => line.Entity == selected.Entity);
            if (selected.Kind == Ff7AutoPlayTargetKind.LineLeave)
            {
                return line.Touch ? driver.LeaveLine(selected.Entity, run) : driver.Approach(edge.Point, edge.Triangle, run);
            }
            if (selected.Kind == Ff7AutoPlayTargetKind.LineEnter && line.Touch)
            {
                return driver.LeaveLine(selected.Entity, run);
            }
            if (selected.Kind == Ff7AutoPlayTargetKind.LineTalk)
            {
                return driver.TalkToLine(selected.Entity, run);
            }
            int distance = Ff7Session.DistanceToSegment(new(line.X1, line.Y1, line.Z1), new(line.X2, line.Y2, line.Z2), new(actor.X4096 >> 12, actor.Y4096 >> 12, actor.Z4096 >> 12), out _);
            return distance >= 0 && distance < unchecked(actor.CollisionRadius * actor.CollisionRadius) && selected.Kind is Ff7AutoPlayTargetKind.LineCross or Ff7AutoPlayTargetKind.LinePush
                ? driver.ApproachLine(selected.Entity, selected.Kind, run)
                : driver.Approach(edge.Point, edge.Triangle, run);
        }, requiredRun: edge.Run);
    }

    private static bool IsNativeGraphProtectedLine(Ff7AutoPlayTarget target) => target.AnalysisComplete && target.Kind is >= Ff7AutoPlayTargetKind.LineTalk and <= Ff7AutoPlayTargetKind.LineLeave && (target.ChangesField || target.MayChangeNavigation);

    private float NativeGraphAdvisory(Ff7FieldProcess process, Vector3 target)
    {
        Ff7Actor player = process.Actors.Single(actor => actor.PlayerControlled);
        return Vector3.Distance(new(player.X4096 / 4096f, player.Y4096 / 4096f, player.Z4096 / 4096f), target) / Math.Max(1, session.GetPlayerMovementSpeed(true) / 256f);
    }

    private static Int128 NativeGraphFixedDistance(Ff7Actor actor, Vector3 point)
    {
        Int128 dx = actor.X4096 - (long)(point.X * 4096);
        Int128 dy = actor.Y4096 - (long)(point.Y * 4096);
        Int128 dz = actor.Z4096 - (long)(point.Z * 4096);
        return dx * dx + dy * dy + dz * dz;
    }

    private static Ff7NativeGraphDebt? ReadRootNativeDebt(Ff7Session branch, IReadOnlyList<Ff7AutoPlayTarget> targets)
    {
        // Already queued native owners are obligations, even if their LINE was disabled after latching.
        Ff7AutoPlayTarget[] roots = Ff7Session.ReadNativeLineScriptRequests(() => branch.Host).Select(request => targets.FirstOrDefault(target => target.Entity == request.Entity && target.Script == request.Script) ?? new Ff7AutoPlayTarget($"queued-line:{request.Entity}:{request.Script}", request.Entity, request.Script, request.Script switch { 1 => Ff7AutoPlayTargetKind.LineTalk, 2 => Ff7AutoPlayTargetKind.LinePush, 3 => Ff7AutoPlayTargetKind.LineCross, 4 => Ff7AutoPlayTargetKind.LineTouch, 5 => Ff7AutoPlayTargetKind.LineEnter, 6 => Ff7AutoPlayTargetKind.LineLeave, _ => throw new InvalidDataException("Invalid queued native LINE owner.") }, "queued-native-owner", false, default)).ToArray();
        Ff7Actor player = branch.Host.Process.Actors.Single(actor => actor.PlayerControlled);
        List<Ff7NativeGraphOwner> owners = [];
        if (branch.Host.Process.PlayerLocked || player.Ladder is not null || branch.Host.HasScriptedMotion(player))
        {
            var vm = branch.Machine.Capture();
            if (vm.Entities[player.Entity].Priority < 7)
            {
                owners.Add(new(player.Entity, vm.Entities[player.Entity].Priority, -1, false));
            }
            if (player.Ladder is { } ladder && vm.PendingRequests?.SingleOrDefault(item => item.Id == ladder.RequestId && item.Command is FieldActorLadder) is { } request)
            {
                owners.Add(new(request.Entity, request.Priority, -1, false));
            }
        }
        return roots.Length == 0 && owners.Count == 0 ? null : new(roots, roots.Select(_ => true).ToArray(), owners.Distinct().ToArray());
    }

    internal sealed class NativeGraphOwnership
    {
        private readonly List<Ff7AutoPlayTarget> roots;
        private readonly List<bool> pending;
        private readonly List<Ff7NativeGraphOwner> owners;
        private long? ladderRequest;
        private int ladderEntity;
        private int ladderPriority;
        private int ladderRoot;
        private bool backwardCompleted;
        internal bool Invalid { get; private set; }
        internal bool OwnsLadder => ladderRequest is not null;
        internal bool HasDebt => owners.Count > 0 || HasPendingRoot;
        internal bool HasPendingRoot => pending.Any(value => value);

        internal NativeGraphOwnership(Ff7NativeGraphDebt? source, Ff7AutoPlayTarget? chosen)
        {
            roots = source?.Roots.ToList() ?? (chosen is null ? [] : [chosen]);
            pending = source?.Pending.ToList() ?? roots.Select(_ => false).ToList();
            owners = source?.Owners.ToList() ?? [];
            ladderRequest = source?.LadderRequest;
            ladderEntity = source?.LadderEntity ?? -1;
            ladderPriority = source?.LadderPriority ?? -1;
            ladderRoot = source?.LadderRoot ?? -1;
            if (ladderRoot < 0)
            {
                ladderRoot = owners.LastOrDefault(owner => owner.Entity == ladderEntity && owner.Priority == ladderPriority)?.Root ?? -1;
            }
            backwardCompleted = source?.BackwardCompleted ?? false;
        }

        internal bool OwnsInput(int entity, int priority) => owners.Any(owner => owner.Entity == entity && owner.Priority == priority && !owner.Returned);

        internal bool Allows(int entity, int script)
        {
            for (int root = 0; root < roots.Count; root++)
            {
                if (roots[root].Entity == entity && roots[root].Script == script && (pending[root] || !owners.Any(owner => owner.Entity == entity && owner.Priority == 1 && owner.Root == root) || owners.Any(owner => owner.Entity == entity && owner.Priority == 1 && owner.Root == root && !owner.Returned)))
                {
                    return true;
                }
            }
            return false;
        }

        internal void AdoptForcedRoot(Ff7AutoPlayTarget target)
        {
            if (!Allows(target.Entity, target.Script))
            {
                roots.Add(target);
                pending.Add(true);
            }
        }

        internal bool Requested(int entity, int script, bool accepted)
        {
            int index = roots.FindLastIndex(root => root.Entity == entity && root.Script == script);
            if (index < 0)
            {
                return false;
            }
            pending[index] = false;
            if (accepted)
            {
                Invalid |= OwnsInput(entity, 1);
                owners.Add(new(entity, 1, index, false, script));
            }
            return true;
        }

        internal void Dispatched(FieldScriptDispatch dispatch)
        {
            if (!dispatch.Accepted)
            {
                return;
            }
            Ff7NativeGraphOwner? parent = owners.LastOrDefault(owner => owner.Entity == dispatch.SourceEntity && owner.Priority == dispatch.SourcePriority && !owner.Returned);
            if (parent is not null)
            {
                Invalid |= OwnsInput(dispatch.TargetEntity, dispatch.TargetPriority);
                owners.Add(new(dispatch.TargetEntity, dispatch.TargetPriority, parent.Root, false, dispatch.TargetScript, dispatch.SourceEntity, dispatch.SourcePriority));
            }
            else if (OwnsInput(dispatch.TargetEntity, dispatch.TargetPriority))
            {
                Invalid = true;
            }
        }

        internal void Returned(FieldScriptReturn returned)
        {
            int index = owners.FindLastIndex(owner => owner.Entity == returned.Entity && owner.Priority == returned.Priority && !owner.Returned);
            if (index >= 0)
            {
                owners[index] = owners[index] with { Returned = true };
            }
        }

        internal void ObserveQueued(Ff7Session branch, Ff7AutoPlayTarget[] events)
        {
            for (int root = 0; root < roots.Count; root++)
            {
                Ff7AutoPlayTarget target = roots[root];
                if (!owners.Any(owner => owner.Entity == target.Entity && owner.Priority == 1 && owner.Root == root) && branch.Host.Process.Lines.Any(line => line.Entity == target.Entity && HasNativeLineRequest(target.Kind, line)))
                {
                    pending[root] = true;
                }
            }
        }

        internal void ObserveLadder(Ff7Session branch, Ff7Actor actor)
        {
            if (actor.Ladder is not { } ladder)
            {
                return;
            }
            if (ladderRequest is null && branch.Machine.Capture().PendingRequests?.SingleOrDefault(item => item.Id == ladder.RequestId && item.Command is FieldActorLadder) is { } request && owners.LastOrDefault(owner => owner.Entity == request.Entity && owner.Priority == request.Priority && !owner.Returned) is { } owner)
            {
                ladderRequest = request.Id;
                ladderEntity = request.Entity;
                ladderPriority = request.Priority;
                ladderRoot = owner.Root;
            }
            backwardCompleted |= ladderRequest == ladder.RequestId && ladder is { Completed: true, Step: 0 };
        }

        internal bool IsSettled(bool nativeControlReady) => HasDebt && pending.All(value => !value) && owners.All(owner => owner.Returned) && nativeControlReady;
        internal bool IsSettled(Ff7Session branch) => IsSettled(Ff7AutoPlayDiscovery.NativeControlReady(branch));
        internal bool HasReturnedLadder => backwardCompleted && ladderRequest is not null && ladderRoot >= 0 && ladderRoot < roots.Count && owners.Any(owner => owner.Entity == ladderEntity && owner.Priority == ladderPriority && owner.Root == ladderRoot && owner.Returned) && owners.Any(owner => owner.Entity == roots[ladderRoot].Entity && owner.Priority == 1 && owner.Root == ladderRoot && owner.Returned);
        internal bool CanReleaseSource(Ff7Session branch) => HasReturnedLadder && branch.Host.Process.Actors.Single(actor => actor.PlayerControlled).Ladder is null && branch.Machine.Capture().PendingRequests?.Any(request => request.Id == ladderRequest) != true;
        internal Ff7NativeGraphDebt? Capture() => HasDebt ? new(roots.ToArray(), pending.ToArray(), owners.OrderBy(owner => owner.Root).ThenBy(owner => owner.Entity).ThenBy(owner => owner.Priority).ToArray(), ladderRequest, ladderEntity, ladderPriority, backwardCompleted, ladderRoot) : null;
    }
}
