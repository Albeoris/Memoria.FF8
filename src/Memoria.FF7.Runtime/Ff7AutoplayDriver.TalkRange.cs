using System.Numerics;
using System.Security.Cryptography;
using System.Text;
using Memoria.FF7.Formats;

namespace Memoria.FF7.Runtime;

public sealed partial class Ff7AutoplayDriver
{
    internal bool AdmitsSelectedTalkApproach(Ff7AutoPlayTarget target, Ff7AutoplayInput input)
    {
        if (target.Kind != Ff7AutoPlayTargetKind.ActorTalk)
        {
            return false;
        }
        if (session.GetTalkTarget()?.Entity == target.Entity)
        {
            return input.Talk;
        }
        if (input.Horizontal == 0 && input.Vertical == 0)
        {
            return false;
        }
        Ff7Actor player = session.Host.Process.Actors.Single(actor => actor.PlayerControlled);
        var direction = Directions.Single(value => value.X == input.Horizontal && value.Y == input.Vertical);
        Ff7Actor controlled = player with { MoveSpeed = unchecked((ushort)session.GetPlayerMovementSpeed(input.Run)) };
        int rotation = (input.Held & 8) != 0 ? 224 : (input.Held & 4) != 0 ? 32 : 0;
        Ff7NativeMoveResult preview = session.Host.Movement!.StepDirection(controlled, (byte)((direction.Direction + session.Field.Triggers.ControlDirection + rotation) & 255), session.Host.Process.Actors, session.Host.Process.BlockedTriangles, playerLocked: false, lineBlocked: session.Host.Process.LineBlocked);
        return session.PreviewTalkTarget(preview.Actor)?.Entity == target.Entity;
    }

    internal static string ReadNavigationKey(Ff7Session session, string visitId)
    {
        Ff7Actor? player = session.Host.Process.Actors.SingleOrDefault(actor => actor.PlayerControlled);
        return ReadNavigationKey(visitId, session.Field.Walkmesh, session.Host.Process.BlockedTriangles, player?.Triangle ?? -1);
    }

    internal static string ReadNavigationKey(string visitId, IReadOnlyList<PcWalkmeshTriangle> walkmesh, byte[] blockedTriangles, int triangle)
    {
        int component = triangle < 0 ? -1 : ReachableTriangles(walkmesh, blockedTriangles, triangle).Min();
        return $"{visitId}:nav:{Convert.ToHexString(blockedTriangles)}:{component}";
    }

    private HashSet<int> ReachableTriangles(int start)
    {
        return ReachableTriangles(session.Field.Walkmesh, session.Host.Process.BlockedTriangles, start);
    }

    private static HashSet<int> ReachableTriangles(IReadOnlyList<PcWalkmeshTriangle> walkmesh, byte[] blockedTriangles, int start)
    {
        HashSet<int> reachable = [start];
        Queue<int> queue = new();
        queue.Enqueue(start);
        while (queue.TryDequeue(out int index))
        {
            var triangle = walkmesh[index];
            foreach (int adjacent in new int[] { triangle.AdjacentAB, triangle.AdjacentBC, triangle.AdjacentCA })
            {
                if (adjacent >= 0 && (blockedTriangles[adjacent >> 3] & (1 << (adjacent & 7))) == 0 && reachable.Add(adjacent))
                {
                    queue.Enqueue(adjacent);
                }
            }
        }
        return reachable;
    }

    public string ReadPhysicalAvailabilityFingerprint(int entity, IReadOnlyList<int>? blockingEntities = null, int? sourceTriangle = null)
    {
        Ff7Actor player = session.Host.Process.Actors.Single(actor => actor.PlayerControlled);
        if (sourceTriangle is int triangle)
        {
            player = player with { Triangle = triangle };
        }
        Ff7Actor target = session.Host.GetActor(entity);
        return WithSpatialBoundaryFingerprint(ReadPhysicalAvailabilityFingerprint(session.Machine.ProgramIdentity, session.Field.Walkmesh, session.Host.Process.BlockedTriangles, player, target, blockingEntities, session.Host.Process.Actors, session.Host.Process.MapJumpDisabled, session.Field.Triggers.Gateways));
    }

    internal static string ReadPhysicalAvailabilityFingerprint(string programIdentity, IReadOnlyList<PcWalkmeshTriangle> walkmesh, byte[] blockedTriangles, Ff7Actor player, Ff7Actor target, IReadOnlyList<int>? blockingEntities = null, IReadOnlyList<Ff7Actor>? actors = null, bool mapJumpDisabled = false, IReadOnlyList<PcFieldGateway>? gateways = null)
    {
        int component = ReachableTriangles(walkmesh, blockedTriangles, player.Triangle).Min();
        StringBuilder facts = new($"{programIdentity}|component:{component}|blocked:{Convert.ToHexString(blockedTriangles)}|player:{player.Entity}:{player.Model}:{player.CollisionRadius}|target:{target.Entity}:{target.Slot}:{target.Model}:{target.EntityBound}:{target.X4096}:{target.Y4096}:{target.Z4096}:{target.Triangle}:{target.TalkEnabled}:{target.TalkRadius}:{target.CollisionEnabled}:{target.CollisionRadius}");
        AppendGatewayAvailability(facts, mapJumpDisabled, gateways);
        AppendBlockingAvailability(facts, blockingEntities, actors);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(facts.ToString())));
    }

    private Ff7AutoplayInput ApproachTalkRange(Ff7Actor target, bool run)
    {
        Ff7Actor player = session.Host.Process.Actors.Single(actor => actor.PlayerControlled);
        Ff7Actor controlled = player with { MoveSpeed = checked((ushort)session.GetPlayerMovementSpeed(run)) };
        Vector3 directGoal = new(target.X4096 / 4096f, target.Y4096 / 4096f, target.Z4096 / 4096f);
        Ff7AutoplayInput? direct = PreviewGoalInput(controlled, directGoal, session.Field.Triggers.ControlDirection, session.Host.Movement!, session.Host.Process.Actors, session.Host.Process.BlockedTriangles, session.Host.Process.LineBlocked, AllowsPlannedMove, projected => session.PreviewTalkTarget(projected)?.Entity == target.Entity, run);
        if (direct is Ff7AutoplayInput input)
        {
            return input;
        }
        HashSet<int> reachable = ReachableTriangles(player.Triangle);
        Vector3 targetPoint = new(target.X4096 >> 12, target.Y4096 >> 12, target.Z4096 >> 12);
        if (reachable.Contains(target.Triangle))
        {
            return Approach(targetPoint, target.Triangle, run);
        }
        var (candidates, blockingEntities) = DiscoverTalkContacts(target, player, reachable);
        if (candidates.Count == 0)
        {
            throw new Ff7AutoPlayNavigationUnavailableException(target.Entity, "FFVII talk target has no reachable native floor point satisfying its original interaction predicate.", blockingEntities.Order().ToArray());
        }
        Vector3 current = new(player.X4096 / 4096f, player.Y4096 / 4096f, player.Z4096 / 4096f);
        var goal = candidates.OrderBy(candidate => Vector3.DistanceSquared(current, candidate.Point)).ThenBy(candidate => candidate.Triangle).First();
        return Approach(goal.Point, goal.Triangle, run);
    }

    internal (IReadOnlyList<(Vector3 Point, int Triangle)> Contacts, int[] BlockingEntities) DiscoverTalkContacts(Ff7Actor target, Ff7Actor player, IReadOnlySet<int>? reachable = null)
    {
        reachable ??= ReachableTriangles(player.Triangle);
        Vector3 targetPoint = new(target.X4096 >> 12, target.Y4096 >> 12, target.Z4096 >> 12);
        List<(Vector3 Point, int Triangle)> candidates = [];
        HashSet<int> blockingEntities = [];
        byte[] headings = Directions.Select(direction => (byte)((direction.Direction + session.Field.Triggers.ControlDirection) & 255)).ToArray();
        foreach (int index in reachable.Order())
        {
            var triangle = session.Field.Walkmesh[index];
            Vector3 center = (triangle.A + triangle.B + triangle.C) / 3;
            List<Vector3> points = [center, (center + triangle.A) / 2, (center + triangle.B) / 2, (center + triangle.C) / 2];
            if (Contains(triangle, targetPoint, out float floorHeight))
            {
                points.Add(new(targetPoint.X, targetPoint.Y, floorHeight));
            }
            foreach (var edge in new[] { (triangle.A, triangle.B), (triangle.B, triangle.C), (triangle.C, triangle.A) })
            {
                Vector2 delta = new(edge.Item2.X - edge.Item1.X, edge.Item2.Y - edge.Item1.Y);
                float ratio = delta.LengthSquared() == 0 ? 0 : Math.Clamp(Vector2.Dot(new(targetPoint.X - edge.Item1.X, targetPoint.Y - edge.Item1.Y), delta) / delta.LengthSquared(), 0, 1);
                Vector3 nearest = Vector3.Lerp(edge.Item1, edge.Item2, ratio);
                points.Add(nearest);
                float length = Vector2.Distance(new(nearest.X, nearest.Y), new(center.X, center.Y));
                points.Add(Vector3.Lerp(nearest, center, length == 0 ? 1 : Math.Min(1, player.CollisionRadius / length)));
            }
            foreach (Vector3 point in points)
            {
                Ff7Actor projected = player with { X4096 = (int)(point.X * 4096), Y4096 = (int)(point.Y * 4096), Z4096 = (int)(point.Z * 4096), Triangle = index };
                int dx = (target.X4096 >> 12) - (projected.X4096 >> 12);
                int dy = (target.Y4096 >> 12) - (projected.Y4096 >> 12);
                if (dx == 0 && dy == 0)
                {
                    continue;
                }
                projected = projected with { Direction = session.Host.Movement!.Direction(dx, dy) };
                Ff7Actor? selected = session.PreviewTalkTarget(projected);
                if (selected?.Entity == target.Entity)
                {
                    Ff7Actor? admitted = ReadNativeTalkFloorContact(projected, session.Host.Movement!, session.Host.Process.Actors, session.Host.Process.BlockedTriangles, session.Host.Process.LineBlocked, headings, actual =>
                    {
                        Ff7Actor? actualSelected = session.PreviewTalkTarget(actual);
                        if (actualSelected is not null && actualSelected.Entity != target.Entity && session.PreviewTalkTarget(actual, [target])?.Entity == target.Entity)
                        {
                            blockingEntities.Add(actualSelected.Entity);
                        }
                        return actualSelected?.Entity == target.Entity;
                    }, blockingEntities);
                    if (admitted is not null)
                    {
                        candidates.Add((Position3D(admitted), admitted.Triangle));
                    }
                }
                else if (selected is not null && session.PreviewTalkTarget(projected, [target])?.Entity == target.Entity)
                {
                    blockingEntities.Add(selected.Entity);
                }
            }
        }
        return (candidates, blockingEntities.Order().ToArray());
    }

    internal static Ff7Actor? ReadNativeTalkFloorContact(Ff7Actor projected, Ff7NativeMovement movement, IReadOnlyList<Ff7Actor> actors, byte[] blockedTriangles, bool lineBlocked, IReadOnlyList<byte> headings, Func<Ff7Actor, bool> acceptsTalk, ISet<int>? blockingEntities = null)
    {
        // Zero stride evaluates the native footprint while retaining its corrected floor and facing.
        return SelectNativeTalkFloorContact(headings, heading => movement.StepDirection(projected with { MoveSpeed = 0 }, heading, actors, blockedTriangles, playerLocked: false, lineBlocked: lineBlocked), acceptsTalk, blockingEntities);
    }

    internal static Ff7Actor? SelectNativeTalkFloorContact(IReadOnlyList<byte> headings, Func<byte, Ff7NativeMoveResult> preview, Func<Ff7Actor, bool> acceptsTalk, ISet<int>? blockingEntities = null)
    {
        foreach (byte heading in headings)
        {
            Ff7NativeMoveResult footprint = preview(heading);
            if (footprint.Moved && acceptsTalk(footprint.Actor))
            {
                return footprint.Actor;
            }
            foreach (int entity in footprint.PushEntities)
            {
                blockingEntities?.Add(entity);
            }
        }
        return null;
    }

    /// <summary>Admits a direct physical approach only after the native mover reaches the actual interaction predicate.</summary>
    internal static Ff7AutoplayInput? PreviewGoalInput(Ff7Actor player, Vector3 goal, byte controlDirection, Ff7NativeMovement movement, IReadOnlyList<Ff7Actor> actors, byte[] blockedTriangles, bool lineBlocked, Func<Ff7Actor, Ff7NativeMoveResult, bool> admitsMove, Func<Ff7Actor, bool> reached, bool run = true)
    {
        double DistanceSquared(Ff7Actor actor)
        {
            double dx = goal.X - actor.X4096 / 4096d;
            double dy = goal.Y - actor.Y4096 / 4096d;
            return dx * dx + dy * dy;
        }
        double remaining = DistanceSquared(player);
        double projectedStride = player.MoveSpeed / 256d * Math.Cos(Math.PI / 8);
        int budget = checked((int)Math.Ceiling(Math.Sqrt(remaining) / Math.Max(1, projectedStride)) + 1);
        Ff7Actor projected = player;
        Ff7AutoplayInput? first = null;
        for (int step = 0; step <= budget; step++)
        {
            if (reached(projected))
            {
                return first;
            }
            int dx = (int)goal.X - (projected.X4096 >> 12);
            int dy = (int)goal.Y - (projected.Y4096 >> 12);
            if (dx == 0 && dy == 0)
            {
                return null;
            }
            int desired = movement.Direction(dx, dy);
            var direction = Directions.MinBy(direction => Math.Abs(((direction.Direction + controlDirection - desired + 128) & 255) - 128));
            Ff7AutoplayInput input = new(direction.X, direction.Y, Run: run);
            Ff7NativeMoveResult probe = movement.StepDirection(projected, unchecked((byte)(direction.Direction + controlDirection)), actors, blockedTriangles, playerLocked: false, lineBlocked: lineBlocked);
            double next = DistanceSquared(probe.Actor);
            if (!probe.Moved || next >= remaining || !admitsMove(projected, probe))
            {
                return null;
            }
            first ??= input;
            projected = probe.Actor;
            remaining = next;
        }
        return null;
    }
}
