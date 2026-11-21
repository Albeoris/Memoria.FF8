using System.Numerics;
using Memoria.FF7.Resources;

namespace Memoria.FF7.Runtime;

public sealed record Ff7SnowboardSurfaceSample(Vector3 Normal, float FloorHeightQ12, short CourseDirection)
{
    public short TurnAngle { get; init; }
}

public static class Ff7SnowboardSurface
{
    public static Ff7SnowboardSurfaceSample Sample(Ff7SnowboardNativeData data, Ff7SnowboardCourseNavigation navigation)
    {
        ArgumentNullException.ThrowIfNull(data);
        ArgumentNullException.ThrowIfNull(navigation);
        Ff7SnowboardCourseData course = ReadCourse(data, navigation.CurrentCourse, navigation.CurrentSegment);
        int segment = navigation.CurrentSegment;
        int tile = course.Tiles[segment];
        int triangle = navigation.CurrentTriangle;
        if ((uint)triangle >= Ff7SnowboardTerrain.TriangleCount)
        {
            throw new InvalidDataException("The snowboard surface requires an admitted current triangle.");
        }
        int record = tile * Ff7SnowboardTerrain.TileSize + 0x254 + triangle * 4;
        int ia = data.TileGeometry[record];
        int ib = data.TileGeometry[record + 1];
        int ic = data.TileGeometry[record + 2];
        var a = Vertex(data, tile, ia);
        var b = Vertex(data, tile, ib);
        var c = Vertex(data, tile, ic);
        Vector3 normal = Normal(a, b, c, course.Angles[segment]);
        int previousTile = segment > 0 ? course.Tiles[segment - 1] : tile;
        int upperRow = segment > 0 ? 0x158 : 8;
        short AdjustedY(int index, short y)
        {
            return index < 7 ? unchecked((short)(Word(data, previousTile, upperRow + index * 8 + 2) - Word(data, previousTile, upperRow + 2))) : y;
        }
        a.Y = AdjustedY(ia, a.Y);
        b.Y = AdjustedY(ib, b.Y);
        c.Y = AdjustedY(ic, c.Y);
        int height = PlaneHeight(navigation.LocalX, navigation.LocalZ, a, b, c);
        float heightQ12 = unchecked((height + course.Positions[segment * 3 + 1]) << 12);
        int curvature = Word(data, tile, 4);
        short offset = 0;
        if (curvature > 0)
        {
            offset = unchecked((short)-(short)Ff7SnowboardCollisionResponse.Direction(navigation.LocalZ, curvature + navigation.LocalX));
        }
        else if (curvature < 0)
        {
            offset = unchecked((short)Ff7SnowboardCollisionResponse.Direction(navigation.LocalZ, -curvature + Word(data, tile, 0x38) - navigation.LocalX));
        }
        short direction = (short)((course.Angles[segment] + offset) & 4095);
        return new(normal, heightQ12, direction) { TurnAngle = offset };
    }

    public static Vector3 InitialPosition(Ff7SnowboardNativeData data, int course = 0, int segment = 0)
    {
        Ff7SnowboardCourseData selected = ReadCourse(data, course, segment);
        int tile = selected.Tiles[segment];
        short x = unchecked((short)((Word(data, tile, 0x0E) + Word(data, tile, 0x1E)) >> 1));
        short y = unchecked((short)((Word(data, tile, 0x22) + Word(data, tile, 0x172)) >> 1));
        short z = unchecked((short)((Word(data, tile, 0x26) + Word(data, tile, 0x36)) >> 1));
        (x, z) = Ff7SnowboardCourseNavigation.Rotate(x, z, unchecked((short)-selected.Angles[segment]));
        return new(unchecked((selected.Positions[segment * 3] + x) << 12), unchecked((selected.Positions[segment * 3 + 1] + y) << 12), unchecked((selected.Positions[segment * 3 + 2] + z) << 12));
    }

    private static Vector3 Normal((short X, short Y, short Z) a, (short X, short Y, short Z) b, (short X, short Y, short Z) c, short angle)
    {
        unchecked
        {
            int ux = b.X - a.X;
            int uy = b.Y - a.Y;
            int uz = b.Z - a.Z;
            int vx = c.X - b.X;
            int vy = c.Y - b.Y;
            int vz = c.Z - b.Z;
            short x = (short)(-(uy * vz - uz * vy) >> 3);
            short y = (short)((uz * vx - ux * vz) >> 3);
            short z = (short)(-(ux * vy - uy * vx) >> 3);
            (x, z) = Ff7SnowboardCourseNavigation.Rotate(x, z, angle);
            return new(x, y, z);
        }
    }

    private static int PlaneHeight(short x, short z, (short X, short Y, short Z) a, (short X, short Y, short Z) b, (short X, short Y, short Z) c)
    {
        unchecked
        {
            int nx = ((a.Y - b.Y) * (a.Z - c.Z) - (a.Y - c.Y) * (a.Z - b.Z)) * (a.X - x);
            int divisor = (a.Z - b.Z) * (a.X - c.X) - (a.Z - c.Z) * (a.X - b.X);
            int nz = ((a.X - b.X) * (a.Y - c.Y) - (a.X - c.X) * (a.Y - b.Y)) * (a.Z - z);
            int delta = divisor == 0 ? 0 : (nx + nz) / divisor;
            return delta + a.Y;
        }
    }

    private static (short X, short Y, short Z) Vertex(Ff7SnowboardNativeData data, int tile, int index)
    {
        int offset = 8 + index * 8;
        if (offset + 6 > 0x254)
        {
            throw new InvalidDataException("The snowboard surface references a vertex outside its geometry records.");
        }
        return (Word(data, tile, offset), Word(data, tile, offset + 2), Word(data, tile, offset + 4));
    }

    private static short Word(Ff7SnowboardNativeData data, int tile, int offset)
    {
        int address = tile * Ff7SnowboardTerrain.TileSize + offset;
        if ((uint)tile >= 76 || data.TileGeometry.Count != 76 * Ff7SnowboardTerrain.TileSize || offset < 0 || offset + 2 > Ff7SnowboardTerrain.TileSize)
        {
            throw new InvalidDataException("The snowboard surface requires a complete native geometry tile.");
        }
        return unchecked((short)(data.TileGeometry[address] | data.TileGeometry[address + 1] << 8));
    }

    private static Ff7SnowboardCourseData ReadCourse(Ff7SnowboardNativeData data, int course, int segment)
    {
        ArgumentNullException.ThrowIfNull(data);
        if ((uint)course >= data.Courses.Count || data.TileGeometry.Count != 76 * Ff7SnowboardTerrain.TileSize)
        {
            throw new InvalidDataException("The snowboard surface references a missing course.");
        }
        Ff7SnowboardCourseData selected = data.Courses[course];
        if ((uint)segment >= selected.Tiles.Count || selected.Angles.Count != selected.Tiles.Count || selected.Positions.Count != selected.Tiles.Count * 3 || selected.Tiles[segment] >= 76)
        {
            throw new InvalidDataException("The snowboard surface references an incomplete course segment.");
        }
        return selected;
    }
}
