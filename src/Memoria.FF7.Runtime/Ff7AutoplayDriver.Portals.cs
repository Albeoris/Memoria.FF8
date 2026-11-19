using System.Numerics;
using Memoria.FF7.Formats;

namespace Memoria.FF7.Runtime;

public sealed partial class Ff7AutoplayDriver
{
    internal static (bool FinalApproach, Vector3 Waypoint) ReadPortalApproachStep(IReadOnlyList<PcWalkmeshTriangle> mesh, int source, int destination, IReadOnlyList<int> previous, Vector3 position, Vector3 target, float radius)
    {
        if (source == destination)
        {
            return (true, target);
        }
        List<(Vector3 Left, Vector3 Right)> portals = [(position, position)];
        HashSet<int> visited = [];
        int current = source;
        while (current != destination)
        {
            int next = previous[current];
            if (next < 0 || !visited.Add(current))
            {
                throw new Ff7AutoPlayNavigationUnavailableException(-1, "FFVII navigation target has no unblocked native walkmesh path.", destination: target, destinationTriangle: destination);
            }
            PcWalkmeshTriangle triangle = mesh[current];
            var edge = triangle.AdjacentAB == next ? (triangle.A, triangle.B) : triangle.AdjacentBC == next ? (triangle.B, triangle.C) : triangle.AdjacentCA == next ? (triangle.C, triangle.A) : throw new InvalidDataException("A navigation corridor does not follow its source triangle's native portal.");
            Vector3 center = (triangle.A + triangle.B + triangle.C) / 3;
            Vector3 following = (mesh[next].A + mesh[next].B + mesh[next].C) / 3;
            Vector2 direction = new(following.X - center.X, following.Y - center.Y);
            Vector2 side = new(edge.Item1.X - edge.Item2.X, edge.Item1.Y - edge.Item2.Y);
            Vector3 left = direction.X * side.Y - direction.Y * side.X >= 0 ? edge.Item1 : edge.Item2;
            Vector3 right = left == edge.Item1 ? edge.Item2 : edge.Item1;
            float length = Vector2.Distance(new(left.X, left.Y), new(right.X, right.Y));
            float inset = length == 0 ? 0.5f : Math.Min(0.5f, radius / length);
            portals.Add((Vector3.Lerp(left, right, inset), Vector3.Lerp(right, left, inset)));
            current = next;
        }
        portals.Add((target, target));
        IReadOnlyList<Vector3> corners = PullPortalString(portals);
        return (false, corners.Skip(1).FirstOrDefault(point => Vector2.DistanceSquared(new(position.X, position.Y), new(point.X, point.Y)) > 1f / (4096 * 4096f), target));
    }

    internal static IReadOnlyList<Vector3> PullPortalString(IReadOnlyList<(Vector3 Left, Vector3 Right)> portals)
    {
        static float Area(Vector3 a, Vector3 b, Vector3 c) => (c.X - a.X) * (b.Y - a.Y) - (b.X - a.X) * (c.Y - a.Y);
        static bool Same(Vector3 a, Vector3 b) => a.X == b.X && a.Y == b.Y;
        List<Vector3> result = [portals[0].Left];
        Vector3 apex = portals[0].Left;
        Vector3 left = apex;
        Vector3 right = apex;
        int apexIndex = 0;
        int leftIndex = 0;
        int rightIndex = 0;
        for (int i = 1; i < portals.Count; i++)
        {
            Vector3 nextLeft = portals[i].Left;
            Vector3 nextRight = portals[i].Right;
            if (Area(apex, right, nextRight) <= 0)
            {
                if (Same(apex, right) || Area(apex, left, nextRight) > 0)
                {
                    right = nextRight;
                    rightIndex = i;
                }
                else
                {
                    result.Add(left);
                    apex = left;
                    apexIndex = leftIndex;
                    left = apex;
                    right = apex;
                    leftIndex = apexIndex;
                    rightIndex = apexIndex;
                    i = apexIndex;
                    continue;
                }
            }
            if (Area(apex, left, nextLeft) >= 0)
            {
                if (Same(apex, left) || Area(apex, right, nextLeft) < 0)
                {
                    left = nextLeft;
                    leftIndex = i;
                }
                else
                {
                    result.Add(right);
                    apex = right;
                    apexIndex = rightIndex;
                    left = apex;
                    right = apex;
                    leftIndex = apexIndex;
                    rightIndex = apexIndex;
                    i = apexIndex;
                }
            }
        }
        if (!Same(result[^1], portals[^1].Left))
        {
            result.Add(portals[^1].Left);
        }
        return result;
    }
}
