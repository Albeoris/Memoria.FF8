using System.Numerics;
using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8;

internal sealed partial class GameWorld
{
    /// <summary>Regenerates an approach from current geometry through ordinary native movement and follower tracing.</summary>
    private bool ApproachSemanticActor(ReplaySemanticTrigger trigger)
    {
        ArgumentNullException.ThrowIfNull(trigger);
        trigger.Validate();
        if (trigger.Kind != ReplaySemanticTriggerKind.ActorInteraction)
        {
            throw new ArgumentException("A semantic actor approach requires an actor interaction.", nameof(trigger));
        }
        if (Field is not { } field || field.Id != trigger.FieldId || Events.FieldId != trigger.FieldId || (uint)trigger.ObjectId >= Events.Actors.Count)
        {
            throw new InvalidDataException("Semantic actor approach lost its current field and target.");
        }
        if (trigger.ProgramHash != Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(field.Jsm)))
        {
            throw new InvalidDataException("Semantic actor approach differs from its actual current program.");
        }
        _ = Events.IsSemanticAdmissionPending(trigger);
        ActorState target = Events.Actors[trigger.ObjectId];
        if (!Events.Scene.NativeActorObjectIds.Contains(target.ObjectId) || !target.TalkEnabled)
        {
            throw new InvalidDataException("Semantic actor approach requires its currently enabled native target.");
        }
        ActorState leader = Events.PlayerActor ?? throw new NotSupportedException("Semantic actor approach requires its current native player binding.");
        Vector2? incoming = trigger.Approach is { } approach ? Vector2.Normalize(Vector2.Transform(new Vector2(approach.Horizontal, approach.Vertical), Matrix3x2.CreateRotation(Events.InputAxisDirection * MathF.Tau / 256))) : null;
        bool SatisfiesSide()
        {
            ActorState currentLeader = Events.PlayerActor ?? throw new NotSupportedException("Semantic actor approach lost its current leader.");
            if (incoming is not Vector2 direction)
            {
                return true;
            }
            Vector2 towardTarget = new(target.X - currentLeader.X, target.Y - currentLeader.Y);
            float along = Vector2.Dot(towardTarget, direction);
            float across = towardTarget.X * direction.Y - towardTarget.Y * direction.X;
            // The retained direction selects a current45-degree sector, not an old approach pose or exact angle.
            return along > 0 && along >= MathF.Abs(across);
        }
        if (Events.SelectInteractionTarget() == trigger.ObjectId && SatisfiesSide())
        {
            return true;
        }
        if (!Events.CanAcceptMovementInput)
        {
            return false;
        }
        Vector2 destination = new(target.X, target.Y);
        if (incoming is Vector2 incomingDirection)
        {
            float talkReach = leader.PushRadius + target.TalkRadius;
            float collisionReach = target.CollisionEnabled ? (leader.PushRadius + target.PushRadius) / 2 + leader.PushRadius : 0;
            if (talkReach <= 0)
            {
                throw new NotSupportedException("Current actor radii provide no positive native talk range.");
            }
            if (talkReach <= collisionReach && trigger.Approach is { Inferred: true } && Events.SelectInteractionTarget() == trigger.ObjectId)
            {
                return true;
            }
            // The expanded radius bounds every current native fan probe. It is advisory, so a closed conservative ring is not proof that the actual selector is unreachable.
            float approachDistance = collisionReach < talkReach ? (talkReach + collisionReach) / 2 : MathF.BitDecrement(talkReach);
            destination -= incomingDirection * approachDistance;
        }
        IReadOnlyList<Vector2>? planned = PlanSemanticActorApproach(field, leader, target, destination, incoming);
        if (planned is null)
        {
            if (trigger.Approach is { Inferred: true } && Events.SelectInteractionTarget() == trigger.ObjectId)
            {
                return true;
            }
            throw new NotSupportedException("Current walkmesh has no reachable triangle on the requested actor approach side.");
        }
        Queue<Vector2> waypoints = new(planned);
        Vector2 previous = new(leader.X, leader.Y);
        int stationary = 0;
        for (int callback = 0; callback < 4096; callback++)
        {
            if (!ReferenceEquals(Field, field) || session.State.Scene != SceneKind.Field || session.State.FieldId != field.Id || !Events.CanAcceptMovementInput)
            {
                return false;
            }
            if (Events.SelectInteractionTarget() == trigger.ObjectId && SatisfiesSide())
            {
                return true;
            }
            leader = Events.PlayerActor ?? throw new NotSupportedException("Semantic actor approach lost its current player.");
            Vector2 position = new(leader.X, leader.Y);
            while (waypoints.TryPeek(out Vector2 waypoint) && Vector2.DistanceSquared(position, waypoint) <= 24 * 24)
            {
                waypoints.Dequeue();
            }
            Vector2 goal = waypoints.TryPeek(out Vector2 next) ? next : new Vector2(target.X, target.Y);
            Vector2 delta = goal - position;
            if (delta.LengthSquared() < 0.0001f)
            {
                throw new NotSupportedException("The current actor approach has no remaining direction but its native interaction selector is unsatisfied.");
            }
            Vector2 axes = Vector2.Transform(Vector2.Normalize(delta), Matrix3x2.CreateRotation(-Events.InputAxisDirection * MathF.Tau / 256));
            PlayerActionResult result = Apply(new PlayerAction(PlayerActionKind.Move, Horizontal: axes.X, Vertical: axes.Y));
            if (!result.Accepted)
            {
                if (!Events.CanAcceptMovementInput || !ReferenceEquals(Field, field) || session.State.FieldId != field.Id)
                {
                    return false;
                }
                throw new NotSupportedException("Current native movement refused the generated semantic actor approach.");
            }
            leader = Events.PlayerActor ?? throw new NotSupportedException("Semantic actor approach lost its native leader after movement.");
            Vector2 after = new(leader.X, leader.Y);
            stationary = Vector2.DistanceSquared(previous, after) < 0.0001f ? stationary + 1 : 0;
            previous = after;
            if (stationary >= 16)
            {
                if (trigger.Approach is { Inferred: true } && Events.SelectInteractionTarget() == trigger.ObjectId)
                {
                    return true;
                }
                throw new NotSupportedException($"Current geometry cannot complete the semantic approach to actor{target.ObjectId} in field{field.Id}: player({leader.X},{leader.Y}) target({target.X},{target.Y}), selector{Events.SelectInteractionTarget()}, side{SatisfiesSide()}, destination{destination}, next{goal}; native movement remains authoritative.");
            }
        }
        throw new NotSupportedException("The generated current actor approach exceeded its bounded ordinary-input budget.");
    }

    private IReadOnlyList<Vector2>? PlanSemanticActorApproach(FieldAssets field, ActorState leader, ActorState target, Vector2 destination, Vector2? incoming)
    {
        IReadOnlyList<WalkmeshTriangle> triangles = field.Walkmesh.Triangles;
        if ((uint)leader.WalkmeshTriangle >= triangles.Count)
        {
            throw new NotSupportedException("Current actor approach requires the actual player walkmesh triangle.");
        }
        HashSet<int> locked = Events.Scene.LockedTriangles.ToHashSet();
        Dictionary<int, float> costs = new() { [leader.WalkmeshTriangle] = 0 };
        Dictionary<int, int> parents = [];
        PriorityQueue<int, float> frontier = new();
        frontier.Enqueue(leader.WalkmeshTriangle, 0);
        int closest = -1;
        float closestDistance = float.PositiveInfinity;
        bool exactDestination = false;
        List<int> destinationTriangles = [];
        Vector2? finalPortal = null;
        int fixedDestinationX = checked((int)(destination.X * 4096));
        int fixedDestinationY = checked((int)(destination.Y * 4096));
        int visited = 0;
        while (frontier.TryDequeue(out int triangle, out float cost))
        {
            if (cost != costs[triangle])
            {
                continue;
            }
            if (++visited > 8192)
            {
                throw new NotSupportedException("Current actor approach exceeds its bounded walkmesh graph.");
            }
            float distance = Vector2.DistanceSquared(Center(triangle), destination);
            Vector2 toTarget = new Vector2(target.X, target.Y) - Center(triangle);
            bool correctSide = incoming is not Vector2 direction || Vector2.Dot(toTarget, direction) > 0 && Vector2.Dot(toTarget, direction) >= MathF.Abs(toTarget.X * direction.Y - toTarget.Y * direction.X);
            if (correctSide && distance < closestDistance)
            {
                closest = triangle;
                closestDistance = distance;
            }
            FieldNativeGroundProbe ground = FieldNativeMovementProbe.Probe(field.Walkmesh, triangle, fixedDestinationX, fixedDestinationY, 0, 0, locked);
            if (ground.Supported && ground.Steering == 0 && ground.Triangle == triangle && ground.NativeHeight is int height && MathF.Abs(height - target.Z) < 256)
            {
                destinationTriangles.Add(triangle);
            }
            WalkmeshTriangle current = triangles[triangle];
            foreach (int neighbor in new[] { current.NeighborA, current.NeighborB, current.NeighborC })
            {
                if ((uint)neighbor >= triangles.Count || locked.Contains(neighbor))
                {
                    continue;
                }
                Vector2 center = Center(neighbor);
                Vector2 previous = Position(triangle);
                float candidate = cost + Vector2.Distance(previous, center) + AdvisoryActorPenalty(previous, center);
                if (costs.TryGetValue(neighbor, out float previousCost) && candidate >= previousCost)
                {
                    continue;
                }
                costs[neighbor] = candidate;
                parents[neighbor] = triangle;
                frontier.Enqueue(neighbor, candidate);
            }
        }
        float finalCost = float.PositiveInfinity;
        foreach (int triangle in destinationTriangles)
        {
            if (triangle == leader.WalkmeshTriangle)
            {
                float direct = Vector2.Distance(Position(triangle), destination) + AdvisoryActorPenalty(Position(triangle), destination);
                if (direct < finalCost)
                {
                    finalCost = direct;
                    closest = triangle;
                    finalPortal = null;
                    exactDestination = true;
                }
            }
            WalkmeshTriangle endpoint = triangles[triangle];
            foreach (int neighbor in new[] { endpoint.NeighborA, endpoint.NeighborB, endpoint.NeighborC })
            {
                if (!costs.TryGetValue(neighbor, out float previousCost) || PathCrossesDestination(neighbor, triangle))
                {
                    continue;
                }
                WalkmeshTriangle previousTriangle = triangles[neighbor];
                if (previousTriangle.NeighborA != triangle && previousTriangle.NeighborB != triangle && previousTriangle.NeighborC != triangle)
                {
                    continue;
                }
                Vector2 portal = Portal(neighbor, triangle);
                Vector2 previous = Position(neighbor);
                float candidate = previousCost + Vector2.Distance(previous, portal) + AdvisoryActorPenalty(previous, portal) + Vector2.Distance(portal, destination) + AdvisoryActorPenalty(portal, destination);
                if (candidate < finalCost)
                {
                    finalCost = candidate;
                    closest = neighbor;
                    finalPortal = portal;
                    exactDestination = true;
                }
            }
        }
        if (closest < 0)
        {
            return null;
        }
        List<int> route = [closest];
        while (route[^1] != leader.WalkmeshTriangle)
        {
            route.Add(parents[route[^1]]);
        }
        route.Reverse();
        // These points are advice, not a collision certificate. Every accepted step uses the current native adapter.
        if (!exactDestination)
        {
            return route.Skip(1).Select(Center).ToArray();
        }
        List<Vector2> points = route.Skip(1).Select(Center).ToList();
        if (finalPortal is Vector2 portalPoint)
        {
            points.Add(portalPoint);
        }
        points.Add(destination);
        return points;

        Vector2 Center(int index)
        {
            WalkmeshTriangle triangle = triangles[index];
            return new Vector2((triangle.A.X + triangle.B.X + triangle.C.X) / 3, (triangle.A.Y + triangle.B.Y + triangle.C.Y) / 3);
        }

        Vector2 Position(int triangle)
        {
            return triangle == leader.WalkmeshTriangle ? new Vector2(leader.X, leader.Y) : Center(triangle);
        }

        Vector2 Portal(int from, int to)
        {
            WalkmeshTriangle previous = triangles[from];
            (Vector3 first, Vector3 second) = previous.NeighborA == to ? (previous.A, previous.B) : previous.NeighborB == to ? (previous.B, previous.C) : (previous.C, previous.A);
            return new Vector2((first.X + second.X) / 2, (first.Y + second.Y) / 2);
        }

        bool PathCrossesDestination(int from, int destinationTriangle)
        {
            for (int current = from; current != leader.WalkmeshTriangle; current = parents[current])
            {
                if (current == destinationTriangle)
                {
                    return true;
                }
            }
            return false;
        }

        float AdvisoryActorPenalty(Vector2 from, Vector2 to)
        {
            float penalty = 0;
            Vector2 segment = to - from;
            float lengthSquared = segment.LengthSquared();
            foreach (int objectId in Events.Scene.NativeActorObjectIds)
            {
                ActorState actor = Events.Actors[objectId];
                int heightDifference = (int)MathF.Floor(actor.Z) - (int)MathF.Floor(leader.Z);
                // Retained ownership, model sentinels and height bounds follow the native contact reader; visibility and Active are not exclusions.
                if (actor.ObjectId == leader.ObjectId || actor.ModelId is -1 or ushort.MaxValue || !actor.CollisionEnabled && !actor.PushEnabled || heightDifference is <= -127 or >= 128)
                {
                    continue;
                }
                Vector2 actorPosition = new(actor.X, actor.Y);
                float amount = lengthSquared > 0 ? Math.Clamp(Vector2.Dot(actorPosition - from, segment) / lengthSquared, 0, 1) : 0;
                float clearance = (leader.PushRadius + actor.PushRadius) / 2 + leader.PushRadius;
                float distance = Vector2.Distance(from + segment * amount, actorPosition);
                if (actor.CollisionEnabled && distance < clearance)
                {
                    penalty += (clearance - distance) * 16;
                }
            }
            return penalty;
        }
    }
}
