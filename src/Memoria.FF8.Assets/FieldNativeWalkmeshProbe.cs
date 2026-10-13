using System.Numerics;

namespace Memoria.FF8.Assets;

public readonly record struct FieldNativeWalkmeshPosition(int FixedX, int FixedY, int NativeHeight, int Triangle);

/// <summary>Original 0x47A3E0 adjacency probes, with a bounded flat-ground 0x479C60 radius envelope.</summary>
public static class FieldNativeWalkmeshProbe
{
    private static readonly short[] QuarterSine = [0, 100, 200, 301, 401, 501, 601, 700, 799, 897, 995, 1092, 1189, 1284, 1379, 1474, 1567, 1659, 1751, 1841, 1930, 2018, 2105, 2191, 2275, 2358, 2439, 2519, 2598, 2675, 2750, 2824, 2896, 2966, 3034, 3101, 3166, 3229, 3289, 3348, 3405, 3460, 3513, 3563, 3612, 3658, 3702, 3744, 3784, 3821, 3856, 3889, 3919, 3947, 3973, 3996, 4017, 4035, 4051, 4065, 4076, 4084, 4091, 4094, 4096];

    private readonly record struct Point(int X, int Y, int Z);

    public static int Sine(int direction)
    {
        int angle = unchecked((byte)direction);
        return angle switch
        {
            <= 64 => QuarterSine[angle],
            <= 128 => QuarterSine[128 - angle],
            <= 192 => -QuarterSine[angle - 128],
            _ => -QuarterSine[256 - angle]
        };
    }

    public static FieldNativeWalkmeshPosition? Resolve(FieldWalkmesh mesh, int currentTriangle, int fixedX, int fixedY, IReadOnlySet<int>? lockedTriangles = null)
    {
        return ResolveCore(mesh, currentTriangle, fixedX, fixedY, lockedTriangles, null);
    }

    /// <summary>Requires a clear native actor-contact envelope at the caller; rejects nonflat paths and blocked fan probes rather than inventing native steering.</summary>
    public static FieldNativeWalkmeshPosition? ResolveFlatStep(FieldWalkmesh mesh, int currentTriangle, int proposedFixedX, int proposedFixedY, int pushRadius, int direction, IReadOnlySet<int>? lockedTriangles = null)
    {
        ArgumentNullException.ThrowIfNull(mesh);
        if ((uint)currentTriangle >= mesh.Triangles.Count || pushRadius is < 0 or > ushort.MaxValue)
        {
            return null;
        }
        WalkmeshTriangle original = mesh.Triangles[currentTriangle];
        int height = OriginalPoint(original.A).Z;
        if (OriginalPoint(original.B).Z != height || OriginalPoint(original.C).Z != height)
        {
            return null;
        }
        foreach (int offset in new[] { 32, -32, 0 })
        {
            int angle = unchecked((byte)(direction + offset));
            int x = unchecked(proposedFixedX + pushRadius * Sine(angle));
            int y = unchecked(proposedFixedY - pushRadius * Sine(angle + 64));
            if (ResolveCore(mesh, currentTriangle, x, y, lockedTriangles, height) is null)
            {
                return null;
            }
        }
        return ResolveCore(mesh, currentTriangle, proposedFixedX, proposedFixedY, lockedTriangles, height);
    }

    private static FieldNativeWalkmeshPosition? ResolveCore(FieldWalkmesh mesh, int currentTriangle, int fixedX, int fixedY, IReadOnlySet<int>? lockedTriangles, int? requiredFlatHeight)
    {
        ArgumentNullException.ThrowIfNull(mesh);
        int x = fixedX / 4096;
        int y = fixedY / 4096;
        int edgeX = unchecked((short)x);
        int edgeY = unchecked((short)y);
        HashSet<int> visited = [];
        while ((uint)currentTriangle < mesh.Triangles.Count && currentTriangle <= short.MaxValue && visited.Add(currentTriangle))
        {
            WalkmeshTriangle triangle = mesh.Triangles[currentTriangle];
            Point a = OriginalPoint(triangle.A);
            Point b = OriginalPoint(triangle.B);
            Point c = OriginalPoint(triangle.C);
            if (requiredFlatHeight is int flat && (a.Z != flat || b.Z != flat || c.Z != flat))
            {
                return null;
            }
            int neighbor;
            if (Edge(a, b, edgeX, edgeY) < 0)
            {
                neighbor = triangle.NeighborA;
            }
            else if (Edge(b, c, edgeX, edgeY) < 0)
            {
                neighbor = triangle.NeighborB;
            }
            else if (Edge(c, a, edgeX, edgeY) < 0)
            {
                neighbor = triangle.NeighborC;
            }
            else
            {
                int height = NativeHeight(a, b, c, x, y);
                if (requiredFlatHeight is int expected && height != expected)
                {
                    return null;
                }
                return new FieldNativeWalkmeshPosition(fixedX, fixedY, height, currentTriangle);
            }
            if (neighbor < 0 || lockedTriangles?.Contains(neighbor) == true)
            {
                return null;
            }
            currentTriangle = neighbor;
        }
        // Original malformed cyclic adjacency can loop forever; no geometry result is fabricated here.
        return null;
    }

    private static int Edge(Point start, Point end, int x, int y)
    {
        return unchecked(x * (end.Y - start.Y) + end.X * (start.Y - y) + start.X * (y - end.Y));
    }

    private static int NativeHeight(Point a, Point b, Point c, int x, int y)
    {
        unchecked
        {
            int ux = b.X - a.X;
            int uy = b.Y - a.Y;
            int uz = b.Z - a.Z;
            int vx = c.X - b.X;
            int vy = c.Y - b.Y;
            int vz = c.Z - b.Z;
            int nx = uz * vy - uy * vz;
            int ny = vz * ux - uz * vx;
            int nz = uy * vx - vy * ux;
            if (nz == 0)
            {
                return 0;
            }
            return (a.Y * ny + nz * a.Z + nx * a.X - ny * y - nx * x) / nz;
        }
    }

    private static Point OriginalPoint(Vector3 value)
    {
        static int Word(float coordinate)
        {
            if (!float.IsFinite(coordinate) || coordinate != MathF.Truncate(coordinate) || coordinate is < short.MinValue or > short.MaxValue)
            {
                throw new InvalidDataException("Native walkmesh probes require original signed-WORD vertices.");
            }
            return (short)coordinate;
        }
        return new Point(Word(value.X), Word(value.Y), Word(value.Z));
    }
}
