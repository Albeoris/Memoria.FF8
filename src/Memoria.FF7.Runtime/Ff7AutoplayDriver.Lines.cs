using System.Numerics;

namespace Memoria.FF7.Runtime;

public sealed partial class Ff7AutoplayDriver
{
    private long avoidedLineGeneration = -1;
    private HashSet<int> avoidedLineEntities = [];

    public void AvoidLinePush(params int[] entities)
    {
        avoidedLineGeneration = session.FieldGeneration;
        avoidedLineEntities = entities.ToHashSet();
    }

    public Ff7AutoplayInput TalkToLine(int entity, bool run = true)
    {
        Ff7FieldProcess process = session.Host.Process;
        Ff7Actor? player = process.Actors.SingleOrDefault(actor => actor.PlayerControlled);
        Ff7Line line = process.Lines.Last(line => line.Entity == entity);
        if (player is null || process.PlayerLocked || process.Join is not null || line.Enabled != 1)
        {
            return default;
        }
        Vector3 a = new(line.X1, line.Y1, line.Z1);
        Vector3 b = new(line.X2, line.Y2, line.Z2);
        Vector3 position = new(player.X4096 >> 12, player.Y4096 >> 12, player.Z4096 >> 12);
        int distance = Ff7Session.DistanceToSegment(a, b, position, out Vector3 nearest);
        if (distance == -1 || distance >= unchecked(player.CollisionRadius * player.CollisionRadius))
        {
            return Approach((a + b) / 2, run: run);
        }
        int desired = line.OnLine || position.X == nearest.X && position.Y == nearest.Y ? line.ProximityAngle : session.Host.Movement!.Direction((int)(nearest.X - position.X), (int)(nearest.Y - position.Y));
        var direction = Directions.MinBy(direction => Math.Abs(((direction.Direction + session.Field.Triggers.ControlDirection - desired + 128) & 255) - 128));
        bool talk = line.OnLine && ((line.ProximityAngle - player.Direction + 32) & 255) < 64;
        return new(direction.X, direction.Y, Held: talk ? (ushort)0x20 : (ushort)0, Talk: talk, Run: run);
    }

    private bool AllowsPlannedMove(Ff7Actor from, Ff7Actor to)
    {
        if (avoidedLineGeneration != session.FieldGeneration)
        {
            return true;
        }
        Vector3 start = new(from.X4096 >> 12, from.Y4096 >> 12, from.Z4096 >> 12);
        Vector3 end = new(to.X4096 >> 12, to.Y4096 >> 12, start.Z);
        int radiusSquared = from.CollisionRadius * from.CollisionRadius;
        foreach (Ff7Line line in session.Host.Process.Lines.Where(line => line.Enabled == 1 && avoidedLineEntities.Contains(line.Entity)))
        {
            Vector3 a = new(line.X1, line.Y1, line.Z1);
            Vector3 b = new(line.X2, line.Y2, line.Z2);
            int before = Ff7Session.DistanceToSegment(a, b, start, out Vector3 nearest);
            if (before >= 0 && before < radiusSquared)
            {
                bool exact = start.X == nearest.X && start.Y == nearest.Y;
                int angle = exact ? line.ProximityAngle : session.Host.Movement!.Direction((int)(nearest.X - start.X), (int)(nearest.Y - start.Y));
                if (exact || ((angle - to.Direction + 64) & 255) < 128)
                {
                    return false;
                }
            }
            int after = Ff7Session.DistanceToSegment(a, b, end, out _);
            if (after >= 0 && after < radiusSquared && (before < 0 || after < before))
            {
                return false;
            }
        }
        return true;
    }
}
