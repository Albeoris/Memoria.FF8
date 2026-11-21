using System.Numerics;
using Memoria.FF7.Formats;

namespace Memoria.FF7.Runtime;

public sealed partial class Ff7AutoplayDriver
{
    internal static int[] ReadAheadNavigationLinePrerequisites(IReadOnlyList<PcWalkmeshTriangle> mesh, byte[] blocked, Ff7Actor player, Vector3 goal, int destination, IReadOnlyList<Ff7AutoPlayTarget> targets, IReadOnlyList<Ff7Line> lines)
    {
        if ((uint)player.Triangle >= mesh.Count || (uint)destination >= mesh.Count)
        {
            return [];
        }
        var (previous, distance) = ReadApproachRoutes(mesh, blocked, player.Triangle, destination);
        if (previous[player.Triangle] < 0)
        {
            return [];
        }
        Vector3 source = Position3D(player);
        List<Vector3> route = [source];
        HashSet<int> visited = [];
        int current = player.Triangle;
        while (current != destination && visited.Add(current))
        {
            current = previous[current];
            if (current < 0)
            {
                return [];
            }
            PcWalkmeshTriangle triangle = mesh[current];
            route.Add((triangle.A + triangle.B + triangle.C) / 3);
        }
        route.Add(goal);
        List<(int Entity, int RouteIndex, float Fraction)> result = [];
        foreach (Ff7AutoPlayTarget target in targets.Where(target => target is { Kind: Ff7AutoPlayTargetKind.LinePush, AnalysisComplete: true, MayChangeNavigation: true, ChangesField: false, ChangesStory: false }))
        {
            Ff7Line? line = lines.LastOrDefault(line => line.Entity == target.Entity && line.Enabled == 1);
            if (line is null || DiscoverLineContacts(mesh, blocked, player, line).Count == 0)
            {
                continue;
            }
            Vector3 a = new(line.X1, line.Y1, line.Z1);
            Vector3 b = new(line.X2, line.Y2, line.Z2);
            float Side(Vector3 point) => (b.X - a.X) * (point.Y - a.Y) - (b.Y - a.Y) * (point.X - a.X);
            bool forwardLanding = target.PossibleJumpLandings.Any(landing => landing.Entity == player.Entity && landing.Literal && landing.Command.Duration > 0 && (uint)landing.Command.Triangle < mesh.Count && distance[landing.Command.Triangle] >= 0 && distance[landing.Command.Triangle] < distance[player.Triangle] && landing.Command.Triangle >> 3 < blocked.Length && (blocked[landing.Command.Triangle >> 3] & 1 << (landing.Command.Triangle & 7)) == 0 && Contains(mesh[landing.Command.Triangle], new(landing.Command.X, landing.Command.Y, 0), out _) && Side(source) * Side(new(landing.Command.X, landing.Command.Y, 0)) < 0);
            if (!forwardLanding)
            {
                continue;
            }
            for (int index = 1; index < route.Count; index++)
            {
                Vector3 start = route[index - 1];
                Vector3 end = route[index];
                float before = Side(start);
                float after = Side(end);
                if (before * after >= 0)
                {
                    continue;
                }
                float fraction = before / (before - after);
                Vector3 crossing = Vector3.Lerp(start, end, fraction);
                Vector2 segment = new(b.X - a.X, b.Y - a.Y);
                float projection = Vector2.Dot(new(crossing.X - a.X, crossing.Y - a.Y), segment);
                if (projection < 0 || projection > segment.LengthSquared())
                {
                    continue;
                }
                Vector3 native = new((int)crossing.X, (int)crossing.Y, (int)crossing.Z);
                int nativeDistance = Ff7Session.DistanceToSegment(a, b, native, out _);
                if (nativeDistance >= 0 && nativeDistance < unchecked(player.CollisionRadius * player.CollisionRadius))
                {
                    result.Add((target.Entity, index, fraction));
                    break;
                }
            }
        }
        return result.OrderBy(item => item.RouteIndex).ThenBy(item => item.Fraction).Select(item => item.Entity).Distinct().ToArray();
    }
}
