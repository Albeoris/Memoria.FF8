using System.Numerics;

namespace Memoria.FF8.Assets;

/// <summary>Native 0x477B00 party-trace triangle selection, separate from movement and IDLOCK filtering.</summary>
public static class FieldPartyTraceGeometry
{
    private readonly record struct Point(int X, int Y, int Z);

    public static Vector3? ResolveTriangleCentroid(FieldWalkmesh mesh, int triangleIndex)
    {
        ArgumentNullException.ThrowIfNull(mesh);
        if ((uint)triangleIndex >= mesh.Triangles.Count || triangleIndex > short.MaxValue)
        {
            return null;
        }
        WalkmeshTriangle triangle = mesh.Triangles[triangleIndex];
        Point a = OriginalPoint(triangle.A);
        Point b = OriginalPoint(triangle.B);
        Point c = OriginalPoint(triangle.C);
        // Native477C90 divides each signed vertex sum by3 toward zero before shifting12.
        return new Vector3((a.X + b.X + c.X) / 3, (a.Y + b.Y + c.Y) / 3, (a.Z + b.Z + c.Z) / 3);
    }

    /// <summary>Native 0x47A680 plane evaluation at an explicitly selected triangle, without containment or coordinate narrowing.</summary>
    public static int? ResolveTriangleHeight(FieldWalkmesh mesh, int triangleIndex, int x, int y)
    {
        ArgumentNullException.ThrowIfNull(mesh);
        if ((uint)triangleIndex >= mesh.Triangles.Count || triangleIndex > short.MaxValue)
        {
            return null;
        }
        WalkmeshTriangle triangle = mesh.Triangles[triangleIndex];
        return NativeHeight(OriginalPoint(triangle.A), OriginalPoint(triangle.B), OriginalPoint(triangle.C), x, y);
    }

    public static int? Resolve(FieldWalkmesh mesh, int x, int y, int z)
    {
        ArgumentNullException.ThrowIfNull(mesh);
        if (mesh.Triangles.Count > short.MaxValue)
        {
            throw new InvalidDataException("Party-trace triangle indices require the bounded native signed-WORD range.");
        }
        x = unchecked((short)x);
        y = unchecked((short)y);
        z = unchecked((short)z);
        int? selected = null;
        int bestDifference = int.MaxValue;
        for (int index = 0; index < mesh.Triangles.Count; index++)
        {
            WalkmeshTriangle triangle = mesh.Triangles[index];
            Point a = OriginalPoint(triangle.A);
            Point b = OriginalPoint(triangle.B);
            Point c = OriginalPoint(triangle.C);
            if (!ContainsEdge(a, b, x, y) || !ContainsEdge(b, c, x, y) || !ContainsEdge(c, a, x, y))
            {
                continue;
            }
            int height = NativeHeight(a, b, c, x, y);
            int difference = unchecked(height <= z ? z - height : height - z);
            if (difference < bestDifference)
            {
                selected = index;
                bestDifference = difference;
            }
        }
        // Native returns an uninitialized WORD when no candidate wins. Keep that unsupported result explicit.
        return selected;
    }

    private static bool ContainsEdge(Point start, Point end, int x, int y)
    {
        return unchecked((end.Y - start.Y) * (x - start.X) - (end.X - start.X) * (y - start.Y)) >= 0;
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
            int numerator = a.Y * ny + nz * a.Z + nx * a.X - ny * y - nx * x;
            return numerator / nz;
        }
    }

    private static Point OriginalPoint(Vector3 value)
    {
        static int Word(float coordinate)
        {
            if (!float.IsFinite(coordinate) || coordinate != MathF.Truncate(coordinate) || coordinate is < short.MinValue or > short.MaxValue)
            {
                throw new InvalidDataException("Party-trace geometry requires original signed-WORD vertices.");
            }
            return (short)coordinate;
        }
        return new Point(Word(value.X), Word(value.Y), Word(value.Z));
    }
}
