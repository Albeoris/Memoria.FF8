using System.Numerics;

namespace Memoria.FF7.Runtime;

public sealed partial class Ff7AutoplayDriver
{
    private readonly Queue<Vector2> obstacleHistory = new();
    private readonly Queue<EscapeStep> obstacleEscape = new();
    private long obstacleGeneration = -1;
    private Vector3? obstacleTarget;
    private int obstacleSpeed;

    private sealed record EscapeStep(Ff7AutoplayInput Input, int X4096, int Y4096, int Triangle);
    private sealed record SearchNode(Ff7Actor Actor, SearchNode? Parent, Ff7AutoplayInput Input, int Frames, float Cost);

    private void ResetObstacleEscape()
    {
        obstacleHistory.Clear();
        obstacleEscape.Clear();
        obstacleGeneration = -1;
    }

    private bool TryObstacleEscape(Ff7Actor player, Vector3 target, Vector3 waypoint, Ff7NativeMovement movement, out Ff7AutoplayInput input)
    {
        input = default;
        if (obstacleGeneration != session.FieldGeneration || obstacleTarget != target || obstacleSpeed != player.MoveSpeed)
        {
            ResetObstacleEscape();
            obstacleGeneration = session.FieldGeneration;
            obstacleTarget = target;
            obstacleSpeed = player.MoveSpeed;
        }
        if (TakeEscapeStep(player, movement, out input))
        {
            return true;
        }
        Vector2 position = Position(player);
        obstacleHistory.Enqueue(position);
        if (obstacleHistory.Count < 24)
        {
            return false;
        }
        Vector2 previous = obstacleHistory.Dequeue();
        float step = player.MoveSpeed / 256f;
        if (Vector2.DistanceSquared(position, previous) >= step * step * 64 || Vector3.DistanceSquared(new(position, player.Z4096 / 4096f), target) <= step * step * 4 || session.Host.Process.LineBlocked)
        {
            return false;
        }
        obstacleHistory.Clear();
        FindObstacleEscape(player, new(target.X, target.Y), movement);
        return TakeEscapeStep(player, movement, out input);
    }

    private bool TakeEscapeStep(Ff7Actor player, Ff7NativeMovement movement, out Ff7AutoplayInput input)
    {
        input = default;
        if (!obstacleEscape.TryPeek(out EscapeStep? next))
        {
            return false;
        }
        if (player.X4096 != next.X4096 || player.Y4096 != next.Y4096 || player.Triangle != next.Triangle)
        {
            obstacleEscape.Clear();
            return false;
        }
        var direction = Directions.Single(direction => direction.X == next.Input.Horizontal && direction.Y == next.Input.Vertical);
        byte heading = unchecked((byte)(direction.Direction + session.Field.Triggers.ControlDirection));
        Ff7NativeMoveResult preview = movement.StepDirection(player, heading, session.Host.Process.Actors, session.Host.Process.BlockedTriangles, false, session.Host.Process.LineBlocked);
        if (!preview.Moved && preview.Actor.Triangle == player.Triangle)
        {
            obstacleEscape.Clear();
            return false;
        }
        obstacleEscape.Dequeue();
        input = next.Input;
        return true;
    }

    private void FindObstacleEscape(Ff7Actor player, Vector2 waypoint, Ff7NativeMovement movement)
    {
        const int maximumNodes = 1024;
        const int framesPerEdge = 1;
        float cell = Math.Max(1, player.MoveSpeed * 4);
        (int X, int Y, int Triangle) Key(Ff7Actor actor) => ((int)MathF.Floor(actor.X4096 / cell), (int)MathF.Floor(actor.Y4096 / cell), actor.Triangle);
        float initialDistance = Vector2.Distance(Position(player), waypoint);
        float improvement = Math.Max(16, player.MoveSpeed / 4096f * 64);
        SearchNode source = new(player, null, default, 0, 0);
        SearchNode best = source;
        float bestDistance = initialDistance;
        Dictionary<(int X, int Y, int Triangle), float> costs = new() { [Key(player)] = 0 };
        PriorityQueue<SearchNode, float> pending = new();
        pending.Enqueue(source, initialDistance);
        int expanded = 0;
        while (pending.TryDequeue(out SearchNode? current, out _) && expanded++ < maximumNodes)
        {
            if (costs[Key(current.Actor)] < current.Cost)
            {
                continue;
            }
            float distance = Vector2.Distance(Position(current.Actor), waypoint);
            if (distance < bestDistance)
            {
                best = current;
                bestDistance = distance;
            }
            if (distance <= initialDistance - improvement)
            {
                break;
            }
            foreach (var direction in Directions)
            {
                Ff7Actor actor = current.Actor;
                byte heading = unchecked((byte)(direction.Direction + session.Field.Triggers.ControlDirection));
                int frames = 0;
                for (; frames < framesPerEdge; frames++)
                {
                    Ff7NativeMoveResult preview = movement.StepDirection(actor, heading, session.Host.Process.Actors, session.Host.Process.BlockedTriangles, false, false);
                    if (!preview.Moved && preview.Actor.Triangle == actor.Triangle)
                    {
                        break;
                    }
                    actor = preview.Actor;
                }
                if (frames == 0)
                {
                    continue;
                }
                float cost = current.Cost + Vector2.Distance(Position(current.Actor), Position(actor));
                var key = Key(actor);
                if (costs.TryGetValue(key, out float known) && known <= cost)
                {
                    continue;
                }
                costs[key] = cost;
                SearchNode candidate = new(actor, current, new(direction.X, direction.Y), frames, cost);
                pending.Enqueue(candidate, cost + Vector2.Distance(Position(actor), waypoint));
            }
        }
        if (bestDistance >= initialDistance - Math.Max(4, player.MoveSpeed / 4096f * 16))
        {
            return;
        }
        Stack<SearchNode> path = new();
        for (SearchNode? node = best; node?.Parent is not null; node = node.Parent)
        {
            path.Push(node);
        }
        Ff7Actor projected = player;
        foreach (SearchNode node in path)
        {
            var direction = Directions.Single(direction => direction.X == node.Input.Horizontal && direction.Y == node.Input.Vertical);
            byte heading = unchecked((byte)(direction.Direction + session.Field.Triggers.ControlDirection));
            for (int frame = 0; frame < node.Frames; frame++)
            {
                obstacleEscape.Enqueue(new(node.Input, projected.X4096, projected.Y4096, projected.Triangle));
                projected = movement.StepDirection(projected, heading, session.Host.Process.Actors, session.Host.Process.BlockedTriangles, false, false).Actor;
            }
        }
    }

    private static Vector2 Position(Ff7Actor actor) => new(actor.X4096 / 4096f, actor.Y4096 / 4096f);
}
