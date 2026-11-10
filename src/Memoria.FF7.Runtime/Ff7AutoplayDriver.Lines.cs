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
