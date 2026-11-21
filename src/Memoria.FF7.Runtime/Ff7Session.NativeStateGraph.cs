using System.Numerics;
using System.Security.Cryptography;
using System.Text.Json;
using Memoria.FF7.Events;
using Memoria.FF7.Formats;

namespace Memoria.FF7.Runtime;

internal enum Ff7NativeGraphEdgeKind { Wait, Primitive, Portal, Goal, OwnedEvent, OwnerSettlement }
internal sealed record Ff7NativeGraphOwner(int Entity, int Priority, int Root, bool Returned, int Script = -1, int ParentEntity = -1, int ParentPriority = -1);
internal sealed record Ff7NativeGraphDebt(Ff7AutoPlayTarget[] Roots, bool[] Pending, Ff7NativeGraphOwner[] Owners, long? LadderRequest = null, int LadderEntity = -1, int LadderPriority = -1, bool BackwardCompleted = false, int LadderRoot = -1, bool[]? AdmissionObserved = null);
internal sealed record Ff7NativeGraphState(Ff7SessionSave Native, Ff7NativeGraphDebt? Debt, Ff7NativeGraphDebt? Recovery = null, bool GoalAdmitted = false);
internal sealed record Ff7NativeGraphEdge(Ff7NativeGraphEdgeKind Kind, Vector3 Point, int? Triangle = null, Ff7AutoPlayTarget? Event = null, bool Run = true, Ff7AutoplayInput? Input = null);

public sealed partial class Ff7AutoplayDriver
{
    internal static string ReadExactNativeGraphFingerprint(Ff7SessionSave native, string supervision)
    {
        return Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new { Native = native with { AutoPlay = null, EventCoverage = null }, Supervision = supervision })));
    }

    private static string NativeGraphKey(Ff7NativeGraphState state) => ReadExactNativeGraphFingerprint(state.Native, JsonSerializer.Serialize(new { state.Debt, state.Recovery, state.GoalAdmitted }));

    private IEnumerable<int> EnumerateNativeStateGraph(Ff7AutoPlayTarget target, IReadOnlyList<Ff7AutoPlayTarget> targets, Ff7NativeRouteProbe probe)
    {
        if (!SupportsNativeRouteTarget(target) || !HasNativeRouteClockContract(session))
        {
            yield break;
        }
        int initialDistance = ReadNativeRouteDistance(target, targets);
        Ff7SessionSave rootSave = session.CaptureAutoPlayNavigation();
        Ff7NativeGraphState root = new(rootSave, ReadRootNativeDebt(session, targets));
        using Ff7NativeStateGraph<Ff7NativeGraphState, Ff7AutoPlayRouteStep> graph = new(root, NativeGraphKey(root), NativeGraphAdvisory(session, target, targets));
        long summaryId = ++nativeCandidateId;
        Ff7Actor rootActor = rootSave.Process!.Actors.Single(actor => actor.PlayerControlled);
        nativeCandidateSummaries.Set(summaryId, new("NativeGraph", root.Debt is not null, target.Id, 0, rootSave.Process.Tick, new(rootActor.X4096, rootActor.Y4096, rootActor.Z4096, rootActor.Triangle, rootActor.Direction), false, false, Vector3.Distance(Position3D(rootActor), target.Position)));
        try
        {
            while (graph.TryBeginExpansion(out var node))
            {
                Ff7NativeGraphState state = node!.State!;
                Ff7NativePrimitiveReuse primitiveReuse = new();
                Ff7NativeGraphEdge[] edges;
                using (Ff7Session inspect = session.RestoreAutoPlayNavigation(state.Native))
                {
                    Ff7AutoPlayObservation current = new Ff7AutoPlayDiscovery(inspect).Observe();
                    edges = ReadNativeGraphEdges(inspect, state.Debt, current, target, probe.NavigationPrerequisites).ToArray();
                }
                foreach (Ff7NativeGraphEdge edge in edges)
                {
                    if (edge.Kind == Ff7NativeGraphEdgeKind.OwnedEvent && edge.Input is Ff7AutoplayInput repeated && edge.Event is { } eventTarget && primitiveReuse.CanSkip(repeated, eventTarget.Entity, eventTarget.Script))
                    {
                        continue;
                    }
                    using IEnumerator<int> expansion = ExpandNativeGraphEdge(graph, node, state, edge, target, initialDistance, probe, summaryId, rootSave.Process!.Actors.Single(actor => actor.PlayerControlled), primitiveReuse).GetEnumerator();
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

    private IEnumerable<int> ExpandNativeGraphEdge(Ff7NativeStateGraph<Ff7NativeGraphState, Ff7AutoPlayRouteStep> graph, Ff7NativeGraphNode<Ff7NativeGraphState, Ff7AutoPlayRouteStep> parent, Ff7NativeGraphState source, Ff7NativeGraphEdge edge, Ff7AutoPlayTarget goal, int initialDistance, Ff7NativeRouteProbe probe, long id, Ff7Actor original, Ff7NativePrimitiveReuse primitiveReuse)
    {
        NativeGraphOwnership ownership = new(source.Debt, edge.Kind == Ff7NativeGraphEdgeKind.OwnedEvent ? edge.Event : null);
        List<Ff7AutoPlayRouteStep> segment = [];
        Ff7NativeGraphState work = source;
        Ff7NativeGraphDebt? recovery = source.Recovery;
        try
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
                bool selected = source.GoalAdmitted;
                bool selectedActorEligible = current.Events.Any(item => item.Id == goal.Id && item.Kind == goal.Kind && item.Entity == goal.Entity && item.Script == goal.Script && item.ConditionFingerprint == goal.ConditionFingerprint && item.AnalysisComplete);
                NativeGraphOwnership selectedOwner = new(source.GoalAdmitted ? source.Debt : null, goal);
                bool unexpected = false;
                Ff7AutoPlayTarget? prerequisiteAdmitted = null;
                HashSet<(int Entity, int Script)> startedRoots = [];
                Ff7Actor startingPlayer = branch.Host.Process.Actors.Single(actor => actor.PlayerControlled);
                bool forcedSettlement = edge.Kind == Ff7NativeGraphEdgeKind.OwnerSettlement && ownership.HasDebt && (branch.Host.Process.PlayerLocked || branch.Host.Process.Join is not null || startingPlayer.Ladder is not null || branch.Host.HasScriptedMotion(startingPlayer) || ownership.HasPendingRoot);
                Ff7NativeTickObservation? lastTick = null;
                branch.NativeTickCompleted += tick => lastTick = tick;
                branch.NativeInteractionScriptRequested += admission =>
                {
                    int entity = admission.Entity;
                    int script = admission.Script;
                    bool accepted = admission.Accepted;
                    if (admission.Started)
                    {
                        startedRoots.Add((entity, script));
                    }
                    if (accepted && forcedSettlement && !ownership.Allows(entity, script) && protectedEvents.FirstOrDefault(item => item.Entity == entity && item.Script == script && !driver.CanSkipNavigationLine(item, branch.Machine.Input, false)) is { } forcedRoot)
                    {
                        ownership.AdoptForcedRoot(forcedRoot);
                    }
                    if (probe.ExpectedNavigationKey is not null && accepted && entity == goal.Entity && script == goal.Script)
                    {
                        ownership.AdoptForcedRoot(goal);
                    }
                    bool owned = ownership.Requested(entity, script, accepted, admission.Started);
                    prerequisiteAdmitted ??= probe.NavigationPrerequisites.FirstOrDefault(candidate => candidate.Kind == Ff7AutoPlayTargetKind.LineTouch && current.Events.Contains(candidate) && (edge.Kind == Ff7NativeGraphEdgeKind.OwnedEvent && edge.Event == candidate || source.Debt?.Roots.Contains(candidate) == true) && IsStartedNavigationLinePrerequisite(candidate, admission, owned && ownership.OwnsInput(entity, admission.Priority)));
                    if (goal.Kind != Ff7AutoPlayTargetKind.ActorTalk && accepted && entity == goal.Entity && script == goal.Script)
                    {
                        selected = true;
                        selectedOwner.Requested(entity, script, accepted, admission.Started);
                    }
                    if (accepted && !owned && (entity != goal.Entity || script != goal.Script) && protectedEvents.Any(item => item.Entity == entity && item.Script == script && !driver.CanSkipNavigationLine(item, branch.Machine.Input, false)))
                    {
                        unexpected = true;
                    }
                };
                branch.NativeActorTalkRequested += admission =>
                {
                    bool matches = selectedActorEligible && IsSelectedNativeActorAdmission(goal, admission);
                    bool prerequisite = edge.Kind == Ff7NativeGraphEdgeKind.OwnedEvent && edge.Event is { } chosen && probe.NavigationPrerequisites.Contains(chosen) && current.Events.Contains(chosen) && admission.Started && IsSelectedNativeActorAdmission(chosen, admission) && ownership.OwnsInput(admission.Entity, admission.Priority);
                    unexpected |= admission.Accepted && !matches && !prerequisite;
                    if (prerequisite)
                    {
                        prerequisiteAdmitted = edge.Event;
                    }
                    if (matches)
                    {
                        selected = true;
                        selectedOwner.Requested(admission.Entity, admission.Script, true, admission.Started);
                    }
                };
                branch.Machine.ScriptDispatched += selectedOwner.Dispatched;
                branch.Machine.ScriptReturned += selectedOwner.Returned;
                branch.Machine.ScriptDispatched += ownership.Dispatched;
                branch.Machine.ScriptReturned += ownership.Returned;
                // Every predecessor has passed this same executed-operand witness.
                using Ff7NativeRouteClockWitness clock = new(branch.Machine);
                driver.BeginPlanningStep();
                Ff7AutoplayInput input;
                long physical = driver.PhysicalPlanningWork;
                try
                {
                    input = ChooseNativeGraphInput(branch, driver, edge, goal, current.Events, ownership);
                }
                catch (Ff7AutoPlayNavigationUnavailableException)
                {
                    yield break;
                }
                finally
                {
                    DetachedPhysicalPlanningWork += driver.PhysicalPlanningWork - physical;
                }
                bool pending = driver.PlanningPending;
                if (pending)
                {
                    throw new InvalidOperationException("A native graph callback cannot discard a pending physical search.");
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
                bool prerequisiteBoundary = prerequisiteAdmitted is not null && HasNativeNavigationPrerequisiteBoundary(branch.Host.Process, branch.Clock.CountdownSeconds, branch.Machine.Capture().PendingRequests ?? [], ownership);
                bool unsafeEdge = unexpected || ownership.Invalid || selectedOwner.Invalid || queued is not null || !clock.Independent || !prerequisiteBoundary && !HasNativeGraphTerminalContract(branch, goal, selected, selectedOwner);
                Ff7Line? line = branch.Host.Process.Lines.LastOrDefault(item => item.Entity == goal.Entity);
                Ff7AutoPlayNativeIdleFacts idle = new(branch.Host.Process.PlayerLocked, branch.Host.Process.Join is not null, after.Ladder is { Completed: false }, after.Ladder is { Completed: true }, goal.Kind is >= Ff7AutoPlayTargetKind.LineTalk and <= Ff7AutoPlayTargetKind.LineLeave && line?.Enabled != 1, input, line);
                nativeCandidateSummaries.Set(id, new("NativeGraph", ownership.HasDebt || recovery is not null, goal.Id, checked((int)(parent.Cost + segment.Count)), branch.Host.Process.Tick, new(after.X4096, after.Y4096, after.Z4096, after.Triangle, after.Direction), after.X4096 != original.X4096 || after.Y4096 != original.Y4096 || after.Z4096 != original.Z4096 || after.Triangle != original.Triangle, pending, Vector3.Distance(new(after.X4096 / 4096f, after.Y4096 / 4096f, after.Z4096 / 4096f), goal.Position), idle));
                if (selected && !unexpected && !ownership.Invalid && !selectedOwner.Invalid && clock.Independent && branch.Host.Process.Transition is not null && goal.Kind is Ff7AutoPlayTargetKind.ActorTalk or Ff7AutoPlayTargetKind.LineTalk)
                {
                    throw new NotSupportedException("A selected native talk entry requested a field transition without a supported departure receipt.");
                }
                if (unsafeEdge)
                {
                    segment.RemoveAt(segment.Count - 1);
                    yield break;
                }
                if (prerequisiteBoundary)
                {
                    SetNativeGraphProof(graph, parent, segment, branch, goal, probe);
                    probe.Result = probe.Result! with { NavigationPrerequisite = prerequisiteAdmitted };
                    yield break;
                }
                ownership.ObserveQueued(branch.Host.Process.Lines);
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
                    yield break;
                }
                if (branch.Battle is not null || branch.World is not null || branch.FieldGeneration != session.FieldGeneration)
                {
                    if (branch.Battle is not null && branch.FieldGeneration == session.FieldGeneration && driver.ReadNativeRouteDistance(goal, current.Events) < initialDistance)
                    {
                        SetNativeGraphProof(graph, parent, segment, branch, goal, probe, battle: true);
                    }
                    yield break;
                }
                if (selected && (probe.ExpectedNavigationKey is null || settled))
                {
                    if (probe.ExpectedNavigationKey is null || current.NavigationKey == probe.ExpectedNavigationKey)
                    {
                        SetNativeGraphProof(graph, parent, segment, branch, goal, probe);
                    }
                    yield break;
                }
                if (selected && probe.ExpectedNavigationKey is not null && !ownership.HasDebt)
                {
                    yield break;
                }
                if (settled && ownership.CanReleaseSource(branch))
                {
                    recovery = ownership.Capture();
                }
                bool released = (settled || !ownership.HasDebt) && recovery is not null && new NativeGraphOwnership(recovery, null).CanReleaseSource(branch);
                if (released && Ff7AutoPlayDiscovery.NativeControlReady(branch) && probe.SourceRelease?.Invoke(branch, current) == true)
                {
                    SetNativeGraphProof(graph, parent, segment, branch, goal, probe, released: true);
                    yield break;
                }
                Ff7NativeGraphDebt? debt = settled ? null : ownership.Capture();
                work = new(branch.CaptureAutoPlayNavigation(), debt, recovery, selected);
                graph.AddSuccessor(parent, work, NativeGraphKey(work), segment, NativeGraphAdvisory(branch, goal, current.Events));
                if (edge.Kind == Ff7NativeGraphEdgeKind.Primitive && edge.Input == input && debt is null)
                {
                    primitiveReuse.Remember(input, Ff7Session.ReadNativeLineScriptRequests(() => branch.Host).Concat(startedRoots));
                }
                yield break;
            }
            finally
            {
                ActiveNativeRouteCandidates--;
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

    private static IEnumerable<Ff7NativeGraphEdge> ReadNativeGraphEdges(Ff7Session branch, Ff7NativeGraphDebt? debt, Ff7AutoPlayObservation observation, Ff7AutoPlayTarget goal, IReadOnlyList<Ff7AutoPlayTarget> prerequisites)
    {
        Ff7Actor player = branch.Host.Process.Actors.Single(actor => actor.PlayerControlled);
        Vector3 position = new(player.X4096 / 4096f, player.Y4096 / 4096f, player.Z4096 / 4096f);
        if (debt is not null)
        {
            NativeGraphOwnership ownership = new(debt, null);
            ownership.ObserveLadder(branch, player);
            FieldHostRequest[] requests = player.Ladder is { Completed: false } ? branch.Machine.Capture().PendingRequests ?? [] : [];
            foreach (Ff7AutoplayInput? input in ReadNativeLadderSettlementInputs(player, branch.Host.Process.PlayerLocked, requests, ownership))
            {
                yield return new(Ff7NativeGraphEdgeKind.OwnerSettlement, position, Input: input);
            }
            yield break;
        }
        yield return new(Ff7NativeGraphEdgeKind.Wait, position);
        foreach (Ff7AutoplayInput input in ReadNativeMovementInputs())
        {
            yield return new(Ff7NativeGraphEdgeKind.Primitive, position, Run: input.Run, Input: input);
        }
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
        foreach (Ff7AutoPlayTarget item in observation.Events.Where(item => item.Id != goal.Id && (IsNativeGraphProtectedLine(item) || prerequisites.Contains(item))))
        {
            foreach (Ff7AutoplayInput input in ReadNativeMovementInputs())
            {
                yield return new(Ff7NativeGraphEdgeKind.OwnedEvent, item.Position, Event: item, Run: input.Run, Input: input);
            }
            if (ReadNativeEventGuide(branch, player, item, Ff7NativeGraphEdgeKind.OwnedEvent) is { } eventEdge)
            {
                yield return eventEdge with { Run = true };
                yield return eventEdge with { Run = false };
            }
        }
    }

    internal static Ff7AutoplayInput?[] ReadNativeLadderSettlementInputs(Ff7Actor player, bool playerLocked, IReadOnlyList<FieldHostRequest> requests, NativeGraphOwnership ownership)
    {
        if (!player.PlayerControlled || playerLocked || player.Ladder is not { Completed: false } ladder)
        {
            return [null];
        }
        if (!ownership.OwnsCurrentLadder(ladder, requests))
        {
            throw new NotSupportedException("A controllable native ladder has no authenticated graph request owner.");
        }
        return [new(Held: ForwardLadderKey(ladder.Mode), Run: false), new(Held: BackwardLadderKey(ladder.Mode), Run: false), new Ff7AutoplayInput(Run: false)];
    }

    internal static IEnumerable<Ff7AutoplayInput> ReadNativeMovementInputs()
    {
        foreach (var direction in Directions)
        {
            yield return new(direction.X, direction.Y, Run: true);
            yield return new(direction.X, direction.Y, Run: false);
        }
    }

    private static Ff7NativeGraphEdge? ReadNativeEventGuide(Ff7Session branch, Ff7Actor player, Ff7AutoPlayTarget target, Ff7NativeGraphEdgeKind kind)
    {
        if (target.Kind == Ff7AutoPlayTargetKind.ActorTalk && branch.GetTalkTarget()?.Entity == target.Entity)
        {
            return new(kind, Position3D(player), player.Triangle, target, Input: new(Talk: true));
        }
        var contacts = ReadNativeTargetContacts(branch, player, target);
        if (contacts.Count == 0)
        {
            return null;
        }
        var selected = SelectFloorContact(contacts, Position3D(player), null, player.Triangle);
        return new(kind, selected.Point, selected.Triangle, target);
    }

    internal static int[] ReadNativeGoalBlockers(Ff7Session branch, Ff7AutoPlayTarget goal, IReadOnlyList<Ff7AutoPlayTarget> targets)
    {
        Ff7Actor player = branch.Host.Process.Actors.Single(actor => actor.PlayerControlled);
        if (ReadNativeEventGuide(branch, player, goal, Ff7NativeGraphEdgeKind.Goal) is not { } guide)
        {
            return [];
        }
        Ff7AutoplayDriver driver = new(branch);
        driver.ConfigureSpatialBoundaries(targets, goal);
        HashSet<int> blocked = [];
        driver.ChooseTargetInput(goal, targets, _ =>
        {
            foreach (bool run in new[] { false, true })
            {
                Ff7AutoplayInput input = driver.Steer(guide.Point, run);
                if (input.Horizontal == 0 && input.Vertical == 0)
                {
                    continue;
                }
                var direction = Directions.Single(item => item.X == input.Horizontal && item.Y == input.Vertical);
                Ff7Actor controlled = player with { MoveSpeed = unchecked((ushort)branch.GetPlayerMovementSpeed(run)) };
                Ff7NativeMoveResult move = branch.Host.Movement!.StepDirection(controlled, unchecked((byte)(direction.Direction + branch.Field.Triggers.ControlDirection)), branch.Host.Process.Actors, branch.Host.Process.BlockedTriangles, false, branch.Host.Process.LineBlocked);
                if (!move.Moved && driver.AllowsPlannedMove(controlled, move))
                {
                    blocked.UnionWith(move.PushEntities);
                }
            }
            return default;
        }, requiredRun: true);
        return blocked.Order().ToArray();
    }

    internal static int[] ReadNativeGoalLineBlockers(Ff7Session branch, Ff7AutoPlayTarget goal, IReadOnlyList<Ff7AutoPlayTarget> targets)
    {
        Ff7Actor player = branch.Host.Process.Actors.Single(actor => actor.PlayerControlled);
        var contacts = ReadNativeTargetContacts(branch, player, goal);
        if (contacts.Count == 0)
        {
            return [];
        }
        var contact = SelectFloorContact(contacts, Position3D(player), null, player.Triangle);
        var (previous, distance) = ReadApproachRoutes(branch.Field.Walkmesh, branch.Host.Process.BlockedTriangles, player.Triangle, contact.Triangle);
        if (previous[player.Triangle] < 0)
        {
            return [];
        }
        Vector3 waypoint = ReadPortalApproachStep(branch.Field.Walkmesh, player.Triangle, contact.Triangle, previous, Position3D(player), contact.Point, player.CollisionRadius).Waypoint;
        Ff7AutoplayDriver driver = new(branch);
        driver.ConfigureSpatialBoundaries(targets, goal);
        HashSet<int> blocked = [];
        try
        {
            driver.ChooseTargetInput(goal, targets, _ =>
            {
                Ff7AutoPlayTarget[] protectedTargets = driver.navigationLineTargets ?? [];
                foreach (bool run in new[] { false, true })
                {
                    foreach (var direction in Directions)
                    {
                        Ff7AutoplayInput input = new(Horizontal: direction.X, Vertical: direction.Y, Run: run);
                        driver.navigationLineInput = ProjectNavigationInput(input, branch.Machine.Input);
                        driver.navigationLineTalk = false;
                        Ff7Actor controlled = player with { MoveSpeed = unchecked((ushort)branch.GetPlayerMovementSpeed(run)) };
                        Ff7NativeMoveResult move = branch.Host.Movement!.StepDirection(controlled, unchecked((byte)(direction.Direction + branch.Field.Triggers.ControlDirection)), branch.Host.Process.Actors, branch.Host.Process.BlockedTriangles, false, branch.Host.Process.LineBlocked);
                        if (!move.Moved || distance[move.Actor.Triangle] > distance[player.Triangle] || distance[move.Actor.Triangle] == distance[player.Triangle] && Vector3.DistanceSquared(Position3D(move.Actor), waypoint) >= Vector3.DistanceSquared(Position3D(player), waypoint) || driver.AllowsPlannedMove(controlled, move))
                        {
                            continue;
                        }
                        foreach (int entity in protectedTargets.Where(target => target.Kind == Ff7AutoPlayTargetKind.LineTouch && target.AnalysisComplete).Select(target => target.Entity).Distinct())
                        {
                            try
                            {
                                driver.navigationLineTargets = protectedTargets.Where(target => target.Entity != entity).ToArray();
                                if (driver.AllowsPlannedMove(controlled, move))
                                {
                                    blocked.Add(entity);
                                }
                            }
                            finally
                            {
                                driver.navigationLineTargets = protectedTargets;
                            }
                        }
                    }
                }
                return default;
            }, requiredRun: true);
        }
        catch (Ff7AutoPlayNavigationUnavailableException) when (blocked.Count != 0)
        {
        }
        return blocked.Order().ToArray();
    }

    private static Ff7AutoplayInput ChooseNativeGraphInput(Ff7Session branch, Ff7AutoplayDriver driver, Ff7NativeGraphEdge edge, Ff7AutoPlayTarget goal, Ff7AutoPlayTarget[] events, NativeGraphOwnership ownership)
    {
        Ff7Actor actor = branch.Host.Process.Actors.Single(actor => actor.PlayerControlled);
        ownership.ObserveLadder(branch, actor);
        if (edge.Kind == Ff7NativeGraphEdgeKind.OwnerSettlement)
        {
            if (actor.Ladder is { Completed: false } ladder && ownership.OwnsLadder)
            {
                if (edge.Input is Ff7AutoplayInput ladderInput)
                {
                    if (!ReadNativeLadderSettlementInputs(actor, branch.Host.Process.PlayerLocked, branch.Machine.Capture().PendingRequests ?? [], ownership).Contains(ladderInput))
                    {
                        throw new NotSupportedException("A native ladder callback does not belong to the current owned request.");
                    }
                    return ladderInput;
                }
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
            if (edge.Input is Ff7AutoplayInput primitive)
            {
                return primitive;
            }
            if (selected.Kind == Ff7AutoPlayTargetKind.ActorTalk)
            {
                return driver.ChooseNativeTalkGuideInput(selected.Entity, edge.Point, run);
            }
            if (selected.Kind == Ff7AutoPlayTargetKind.LineTalk && branch.Host.Process.Lines.LastOrDefault(line => line.Entity == selected.Entity) is { } talk && Ff7FieldHost.CanRequestLineTalk(talk, actor))
            {
                return new(Talk: true, Run: run);
            }
            return driver.Steer(edge.Point, run);
        }, requiredRun: edge.Run);
    }
    private static bool IsNativeGraphProtectedLine(Ff7AutoPlayTarget target) => target.AnalysisComplete && target.Kind is >= Ff7AutoPlayTargetKind.LineTalk and <= Ff7AutoPlayTargetKind.LineLeave && (target.ChangesField || target.MayChangeNavigation);

    private float NativeGraphAdvisory(Ff7Session branch, Ff7AutoPlayTarget target, IReadOnlyList<Ff7AutoPlayTarget> targets)
    {
        Ff7Actor player = branch.Host.Process.Actors.Single(actor => actor.PlayerControlled);
        Ff7AutoPlayTarget current = targets.SingleOrDefault(item => item.Id == target.Id && item.Entity == target.Entity && item.Script == target.Script && item.Kind == target.Kind) ?? target;
        var contacts = ReadNativeTargetContacts(branch, player, current);
        return ReadNativeRouteAdvisory(branch.Field.Walkmesh, branch.Host.Process.BlockedTriangles, player, current.Position, contacts, branch.GetPlayerMovementSpeed(false), ReadSpatialBoundaryTriangles(targets), current.Triangle, new Ff7AutoplayDriver(branch).ReadNavigationOwnedPortals(branch.Field.Walkmesh));
    }

    private static IReadOnlyList<(Vector3 Point, int Triangle)> ReadNativeTargetContacts(Ff7Session branch, Ff7Actor player, Ff7AutoPlayTarget target)
    {
        if (target.Kind == Ff7AutoPlayTargetKind.ActorTalk)
        {
            Ff7Actor? actor = branch.Host.Process.Actors.SingleOrDefault(actor => actor.Entity == target.Entity && actor.EntityBound);
            return actor is null ? [] : new Ff7AutoplayDriver(branch).DiscoverTalkContacts(actor, player).Contacts;
        }
        if (target.Kind == Ff7AutoPlayTargetKind.Gateway)
        {
            return DiscoverGatewayContacts(branch.Field.Walkmesh, branch.Host.Process.BlockedTriangles, player, branch.Field.Triggers.Gateways[target.GatewayIndex!.Value]);
        }
        return branch.Host.Process.Lines.LastOrDefault(line => line.Entity == target.Entity) is { } line ? DiscoverLineContacts(branch.Field.Walkmesh, branch.Host.Process.BlockedTriangles, player, line) : [];
    }

    internal static bool IsSelectedNativeActorAdmission(Ff7AutoPlayTarget target, FieldExternalScriptAdmission admission)
    {
        return admission.Accepted && target.Kind == Ff7AutoPlayTargetKind.ActorTalk && target.Entity == admission.Entity && target.Script == admission.Script && admission.Priority == 1;
    }

    internal static bool IsStartedNavigationLinePrerequisite(Ff7AutoPlayTarget target, FieldExternalScriptAdmission admission, bool owned)
    {
        return owned && target.Kind == Ff7AutoPlayTargetKind.LineTouch && admission is { Accepted: true, Started: true, Priority: 1 } && target.Entity == admission.Entity && target.Script == admission.Script;
    }

    private static bool HasNativeGraphTerminalContract(Ff7Session branch, Ff7AutoPlayTarget target, bool selected, NativeGraphOwnership selectedOwner)
    {
        if (HasNativeRouteClockContract(branch))
        {
            return true;
        }
        if (!selected || target.Kind is not (Ff7AutoPlayTargetKind.ActorTalk or Ff7AutoPlayTargetKind.LineTalk))
        {
            return false;
        }
        return HasNativeTalkTerminalContract(branch.Host.Process, branch.Clock.CountdownSeconds, branch.Machine.Capture().PendingRequests ?? [], selectedOwner);
    }

    internal static bool HasNativeTalkTerminalContract(Ff7FieldProcess process, uint countdownSeconds, IReadOnlyList<FieldHostRequest> requests, NativeGraphOwnership selectedOwner)
    {
        bool Owned(long id, bool dialogue) => requests.Any(request => request.Id == id && selectedOwner.OwnsInput(request.Entity, request.Priority) && (dialogue ? request.Command is FieldDialogueCommand : request.Command is FieldMenuCommand));
        return countdownSeconds == 0 && process.MusicVolumeFade is null && process.SoundChannelVolumeFades.All(fade => fade is null) && process.SoundChannelPanFades.All(fade => fade is null) && process.SoundChannelFrequencyFades.All(fade => fade is null) && !selectedOwner.Invalid && process.Dialogues.All(dialogue => dialogue.Completed || Owned(dialogue.RequestId, true)) && (process.Menu is not { Completed: false } menu || Owned(menu.RequestId, false));
    }

    internal static bool HasNativeNavigationPrerequisiteBoundary(Ff7FieldProcess process, uint countdownSeconds, IReadOnlyList<FieldHostRequest> requests, NativeGraphOwnership owner)
    {
        return process.GlobalCommand == Ff7GlobalCommand.None && process.Transition is null && process.Join is null && process.Movie is not { Playing: true } && process.Dialogues.All(dialogue => dialogue.Completed || dialogue.FirstChoice is null) && process.Menu is not { Completed: false } && HasNativeTalkTerminalContract(process, countdownSeconds, requests, owner);
    }

    internal static float ReadNativeRouteAdvisory(IReadOnlyList<PcWalkmeshTriangle> mesh, byte[] blocked, Ff7Actor player, Vector3 target, IReadOnlyList<(Vector3 Point, int Triangle)> contacts, int movementSpeed, IReadOnlySet<int>? boundaries = null, int? admittedTriangle = null, IReadOnlySet<(int From, int To)>? ownedPortals = null)
    {
        Vector3 source = Position3D(player);
        float best = float.PositiveInfinity;
        foreach (var contact in contacts)
        {
            var route = ReadApproachRoutes(mesh, blocked, player.Triangle, contact.Triangle, boundaries, admittedTriangle, ownedPortals);
            if (route.Distance[player.Triangle] == int.MaxValue)
            {
                continue;
            }
            Vector3 point = source;
            float length = 0;
            int triangle = player.Triangle;
            while (triangle != contact.Triangle)
            {
                triangle = route.Previous[triangle];
                PcWalkmeshTriangle next = mesh[triangle];
                Vector3 centroid = (next.A + next.B + next.C) / 3;
                length += Vector3.Distance(point, centroid);
                point = centroid;
            }
            best = Math.Min(best, length + Vector3.Distance(point, contact.Point));
        }
        float distance = float.IsFinite(best) ? best : Vector3.Distance(source, target);
        return distance / Math.Max(1, movementSpeed / 256f);
    }

    internal static Ff7NativeGraphDebt? ReadRootNativeDebt(Ff7Session branch, IReadOnlyList<Ff7AutoPlayTarget> targets)
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
        private readonly List<bool> admissionObserved;
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
            admissionObserved = source?.AdmissionObserved?.ToList() ?? roots.Select(_ => false).ToList();
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

        internal bool OwnsCurrentLadder(Ff7LadderProcess ladder, IReadOnlyList<FieldHostRequest> requests)
        {
            return ladderRequest == ladder.RequestId && requests.Any(request => request.Id == ladderRequest && request.Entity == ladderEntity && request.Priority == ladderPriority && request.Command is FieldActorLadder command && command.X << 12 == ladder.TargetX && command.Y << 12 == ladder.TargetY && command.Z << 12 == ladder.TargetZ && command.Triangle == ladder.TargetTriangle && command.Mode == ladder.Mode) && owners.Any(owner => owner.Entity == ladderEntity && owner.Priority == ladderPriority && owner.Root == ladderRoot && !owner.Returned);
        }

        internal bool OwnsInput(int entity, int priority) => owners.Any(owner => owner.Entity == entity && owner.Priority == priority && !owner.Returned);

        internal bool Allows(int entity, int script)
        {
            for (int root = 0; root < roots.Count; root++)
            {
                if (roots[root].Entity == entity && roots[root].Script == script && (pending[root] || !admissionObserved[root] && !owners.Any(owner => owner.Entity == entity && owner.Priority == 1 && owner.Root == root) || owners.Any(owner => owner.Entity == entity && owner.Priority == 1 && owner.Root == root && !owner.Returned)))
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
                admissionObserved.Add(false);
            }
        }

        internal bool Requested(int entity, int script, bool accepted, bool started = true)
        {
            int index = roots.FindLastIndex(root => root.Entity == entity && root.Script == script);
            if (index < 0)
            {
                return false;
            }
            pending[index] = false;
            admissionObserved[index] = true;
            if (accepted && started)
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

        internal void ObserveQueued(IReadOnlyList<Ff7Line> lines)
        {
            for (int root = 0; root < roots.Count; root++)
            {
                Ff7AutoPlayTarget target = roots[root];
                if (!admissionObserved[root] && !owners.Any(owner => owner.Entity == target.Entity && owner.Priority == 1 && owner.Root == root) && lines.Any(line => line.Entity == target.Entity && HasNativeLineRequest(target.Kind, line)))
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
        internal bool HasReturnedOwnedEvent => !Invalid && owners.Count > 0 && !pending.Any(value => value) && owners.All(owner => owner.Returned && owner.Root >= 0 && owner.Root < roots.Count && owners.Any(root => root.Entity == roots[owner.Root].Entity && root.Priority == 1 && root.Root == owner.Root && root.Script == roots[owner.Root].Script && root.Returned));
        internal bool CanReleaseSource(Ff7Session branch) => Ff7AutoPlayDiscovery.NativeControlReady(branch) && CanReleaseSource(branch.Host.Process.Actors.Single(actor => actor.PlayerControlled), branch.Machine.Capture().PendingRequests ?? []);
        internal bool CanReleaseSource(Ff7Actor player, IReadOnlyList<FieldHostRequest> requests)
        {
            if (Invalid || player.Ladder is not null)
            {
                return false;
            }
            if (ladderRequest is not null)
            {
                return HasReturnedLadder && IsSettled(true) && !requests.Any(request => request.Id == ladderRequest);
            }
            return HasReturnedOwnedEvent && !requests.Any(request => owners.Any(owner => owner.Entity == request.Entity && owner.Priority == request.Priority));
        }
        internal Ff7NativeGraphDebt? Capture() => HasDebt ? new(roots.ToArray(), pending.ToArray(), owners.OrderBy(owner => owner.Root).ThenBy(owner => owner.Entity).ThenBy(owner => owner.Priority).ToArray(), ladderRequest, ladderEntity, ladderPriority, backwardCompleted, ladderRoot, admissionObserved.ToArray()) : null;
    }
}

internal sealed class Ff7NativePrimitiveReuse
{
    private readonly Dictionary<Ff7AutoplayInput, HashSet<(int Entity, int Script)>> safe = [];

    internal void Remember(Ff7AutoplayInput input, IEnumerable<(int Entity, int Script)> startedOrQueued)
    {
        safe[input] = startedOrQueued.ToHashSet();
    }

    internal bool CanSkip(Ff7AutoplayInput input, int entity, int script)
    {
        return safe.TryGetValue(input, out var queued) && !queued.Contains((entity, script));
    }
}
