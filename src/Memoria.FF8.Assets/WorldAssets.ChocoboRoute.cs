using System.Numerics;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets;

public sealed partial class WorldAssets
{
    private const int ChocoboRouteExpansionLimit = 16;
    private const int ChocoboRouteDepthLimit = 63;
    private const int ChocoboRouteDirectionLimit = 8;
    private static readonly double ChocoboRadiansPerUnit = BitConverter.Int32BitsToSingle(0x3AC90FDB);
    private static readonly double ChocoboUnitsPerRadian = BitConverter.Int32BitsToSingle(0x4422F983);

    public NativeChocoboEscapeRoute AdvanceNativeChocoboEscapeRoute(NativeChocoboEscapeRoute route, NativeWorldGroundCacheState cache, IReadOnlyDictionary<int, int> replacements, Func<NativeWorldPathPosition, bool> isBlocked, bool collisionEnabled = true)
    {
        route.Validate();
        cache.Validate();
        if (route.Finished)
        {
            return route;
        }
        if (route.Depth == 0)
        {
            route = route with { SearchYaw = unchecked((short)(route.StartYaw + (route.DirectionAttempt << 9))), SearchPitch = 0, RetryingEdge = false };
        }

        // The original works through at most sixteen edge probes per callback.
        for (int expansion = 0; expansion < ChocoboRouteExpansionLimit; expansion++)
        {
            int depth = route.Depth;
            NativeChocoboEscapeWaypoint node = route.SearchPath[depth];
            WorldTriangle triangle = ResolveNativeGroundIdentity(node.Ground, replacements);
            ChocoboRouteEdge[] edges = ReadChocoboRouteEdges(route.StartPosition, node.Ground, triangle, route.SearchYaw);
            int closestEdge = FindChocoboRouteEdge(edges, second: false);
            NativeWorldPathPosition midpoint = edges[closestEdge].Midpoint;
            route = route with { SearchPath = route.SearchPath.SetItem(depth, node with { Position = midpoint }) };

            NativeWorldPathPosition probe = ProbeChocoboRouteEdge(midpoint, route.SearchYaw, route.SearchPitch);
            WorldGroundSelection? ground = LookupChocoboRouteGround(probe, cache, replacements);
            if (ground is null)
            {
                // Native lookup failure leaves the same edge to the next callback.
                return route;
            }

            uint flags = ground.Ground.Triangle.GroundType | ((uint)ground.Ground.Triangle.TextureFlags << 8) | ((uint)ground.Ground.Triangle.VertexFlags << 16);
            if (NativeWorldTerrain.CanTraverse(flags, 49, collisionEnabled) && (!collisionEnabled || !isBlocked(probe)) && node.Ground != ground.Identity)
            {
                route = PreserveChocoboRoutePrefix(route) with { RetryingEdge = false };
                if (depth == ChocoboRouteDepthLimit)
                {
                    return route with { Finished = true };
                }
                NativeChocoboEscapeWaypoint next = new(probe, ground.Identity);
                route = route with { Depth = depth + 1, SearchPath = route.SearchPath.Length == depth + 1 ? route.SearchPath.Add(next) : route.SearchPath.SetItem(depth + 1, next) };
                continue;
            }

            if (route.DirectionAttempt >= ChocoboRouteDirectionLimit)
            {
                return route with { Finished = true };
            }
            if (depth == 0 || route.RetryingEdge && route.BestPath.Length >= depth)
            {
                return route with { DirectionAttempt = route.DirectionAttempt + 1, Depth = 0, RetryingEdge = false };
            }
            if (route.RetryingEdge)
            {
                route = PreserveChocoboRoutePrefix(route) with { DirectionAttempt = route.DirectionAttempt + 1, Depth = 1, RetryingEdge = false };
                continue;
            }

            // The native decrement is canceled by the loop increment. The second
            // edge changes the search direction for this same polygon.
            ChocoboRouteEdge alternate = edges[FindChocoboRouteEdge(edges, second: true)];
            route = route with
            {
                SearchYaw = ChocoboRouteAngle(alternate.XFromCenter, alternate.ZFromCenter),
                SearchPitch = ChocoboRouteAngle(-alternate.Midpoint.Y, NativeWorldVectorMagnitude.Truncate(alternate.XFromCenter, 0, alternate.ZFromCenter)),
                RetryingEdge = true
            };
        }
        return route;
    }

    private static NativeChocoboEscapeRoute PreserveChocoboRoutePrefix(NativeChocoboEscapeRoute route)
    {
        // The just-tested midpoint occupies slot Depth. Only the preceding
        // complete prefix is copied to the retained route.
        return route.Depth > route.BestPath.Length ? route with { BestPath = route.SearchPath.Slice(0, route.Depth) } : route;
    }

    private WorldGroundSelection? LookupChocoboRouteGround(NativeWorldPathPosition probe, NativeWorldGroundCacheState cache, IReadOnlyDictionary<int, int> replacements)
    {
        WorldGroundSelection? selected = LookupNativeGround(probe.X, -probe.Z, replacements, cache.Alternatives[0].Entries);
        foreach (NativeWorldGroundAlternative alternative in cache.Alternatives.Skip(1))
        {
            WorldGroundSelection? candidate = LookupNativeGround(probe.X, -probe.Z, replacements, alternative.Entries);
            if (candidate?.Identity != selected?.Identity)
            {
                throw new NotSupportedException("Retained ground-cache alternatives choose different Chocobo escape polygons.");
            }
        }
        return selected;
    }

    private readonly record struct ChocoboRouteEdge(NativeWorldPathPosition Midpoint, short XFromCenter, short ZFromCenter, int AngularError);

    private static ChocoboRouteEdge[] ReadChocoboRouteEdges(NativeWorldPathPosition start, NativeWorldGroundIdentity ground, WorldTriangle triangle, short yaw)
    {
        int cell = ground.Cell;
        int column = cell % 128;
        int row = cell / 128;
        int cellX = (column - 64) << 11;
        int cellZ = (48 - row) << 11;
        int startCell = NativeCell((int)Wrap(start.X, 262144), (int)Wrap(-start.Z, 196608));
        int deltaColumn = column - startCell % 128;
        int deltaRow = row - startCell / 128;
        deltaColumn = deltaColumn is > 64 ? deltaColumn - 128 : deltaColumn is < -64 ? deltaColumn + 128 : deltaColumn;
        deltaRow = deltaRow is > 48 ? deltaRow - 96 : deltaRow is < -48 ? deltaRow + 96 : deltaRow;
        int routeCellX = (start.X & ~2047) + unchecked((short)(deltaColumn << 11));
        int routeCellZ = ((start.Z + 2047) & ~2047) + unchecked((short)(-deltaRow << 11));
        NativeWorldPathPosition[] vertices = [ToLocal(triangle.A), ToLocal(triangle.B), ToLocal(triangle.C)];
        int centerX = unchecked((short)(vertices[0].X + vertices[1].X + vertices[2].X)) / 3;
        int centerZ = unchecked((short)(vertices[0].Z + vertices[1].Z + vertices[2].Z)) / 3;
        ChocoboRouteEdge[] edges = new ChocoboRouteEdge[3];
        for (int index = 0; index < edges.Length; index++)
        {
            NativeWorldPathPosition first = vertices[index];
            NativeWorldPathPosition second = vertices[(index + 1) % 3];
            int x = (first.X + second.X) >> 1;
            int y = (first.Y + second.Y) >> 1;
            int z = (first.Z + second.Z) >> 1;
            short centerDeltaX = unchecked((short)(x - centerX));
            short centerDeltaZ = unchecked((short)(z - centerZ));
            short edgeYaw = ChocoboRouteAngle(centerDeltaX, centerDeltaZ);
            int angularError = Math.Abs(((yaw - edgeYaw + 2048) & 4095) - 2048);
            edges[index] = new(new(routeCellX + x, y, routeCellZ + z), centerDeltaX, centerDeltaZ, angularError);
        }
        return edges;

        NativeWorldPathPosition ToLocal(Vector3 vertex)
        {
            return new((int)vertex.X - cellX, -(int)vertex.Y, -(int)vertex.Z - cellZ);
        }
    }

    private static int FindChocoboRouteEdge(ChocoboRouteEdge[] edges, bool second)
    {
        int minimum = Math.Min(edges[0].AngularError, Math.Min(edges[1].AngularError, edges[2].AngularError));
        if (!second)
        {
            // 54E540 keeps the last edge attaining its minimum.
            return Array.FindLastIndex(edges, edge => edge.AngularError == minimum);
        }

        int maximum = Math.Max(edges[0].AngularError, Math.Max(edges[1].AngularError, edges[2].AngularError));
        int index = Array.FindIndex(edges, edge => edge.AngularError > minimum && edge.AngularError < maximum);
        if (index >= 0)
        {
            return index;
        }

        // NG-050: 54E540 leaves the middle edge unwritten on a tie. Keep
        // the original last-minimum ordering for the remaining two edges.
        int closest = Array.FindLastIndex(edges, edge => edge.AngularError == minimum);
        int first = (closest + 1) % edges.Length;
        int other = (closest + 2) % edges.Length;
        return edges[first].AngularError < edges[other].AngularError ? first
            : edges[first].AngularError > edges[other].AngularError ? other
            : Math.Max(first, other);
    }

    private static NativeWorldPathPosition ProbeChocoboRouteEdge(NativeWorldPathPosition midpoint, short yaw, short pitch)
    {
        // 56CD50 builds a signed fixed-point Rx*Ry matrix before the
        // 460830 transform of the 24-unit Z vector.
        int yawCosine = ChocoboRouteTrig(yaw, cosine: true);
        int pitchSine = ChocoboRouteTrig(pitch, cosine: false);
        int pitchCosine = ChocoboRouteTrig(pitch, cosine: true);
        int matrixY = NativePrecision53.RoundInteger(-(long)pitchSine * yawCosine / 4096d);
        int matrixZ = NativePrecision53.RoundInteger((long)pitchCosine * yawCosine / 4096d);
        int x = NativePrecision53.RoundInteger(24 * ChocoboRouteTrig(yaw, cosine: false) / 4096d);
        int y = NativePrecision53.RoundInteger(24 * matrixY / 4096d);
        int z = NativePrecision53.RoundInteger(24 * matrixZ / 4096d);
        return new(midpoint.X + x, midpoint.Y + y, midpoint.Z + z);
    }

    private static short ChocoboRouteAngle(int x, int z)
    {
        return unchecked((short)NativePrecision53.RoundInteger(Math.Atan2(x, z) * ChocoboUnitsPerRadian));
    }

    private static int ChocoboRouteTrig(short angle, bool cosine)
    {
        double radians = angle * ChocoboRadiansPerUnit;
        return NativePrecision53.RoundInteger((cosine ? Math.Cos(radians) : Math.Sin(radians)) * 4096);
    }
}
