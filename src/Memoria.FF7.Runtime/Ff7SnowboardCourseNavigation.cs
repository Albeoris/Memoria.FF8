using System.Buffers.Binary;
using System.Numerics;
using Memoria.FF7.Resources;

namespace Memoria.FF7.Runtime;

public sealed record Ff7SnowboardCourseCollision(int Kind, int Course, int Segment, int Tile, short LocalX, short LocalZ, int Triangle);

public sealed record Ff7SnowboardCourseReconciliation(Vector3 PositionQ12, Vector3 PreviousQ12, bool Accepted, bool CourseEnded, Ff7SnowboardCourseCollision? Collision)
{
    public bool RequiresVelocityResponse => Collision is not null;
}

public sealed class Ff7SnowboardCourseNavigation
{
    private readonly Ff7SnowboardNativeData data;
    private readonly byte[][] tiles;

    public Ff7SnowboardCourseNavigation(Ff7SnowboardNativeData data)
    {
        ArgumentNullException.ThrowIfNull(data);
        if (data.Courses.Count != 7 || data.TileGeometry.Count != 76 * Ff7SnowboardTerrain.TileSize || data.GridClassifications.Count != 64)
        {
            throw new InvalidDataException("Snowboard course navigation requires complete native course and tile data.");
        }
        this.data = data;
        tiles = new byte[76][];
        for (int tile = 0; tile < tiles.Length; tile++)
        {
            tiles[tile] = new byte[Ff7SnowboardTerrain.TileSize];
            for (int index = 0; index < tiles[tile].Length; index++)
            {
                tiles[tile][index] = data.TileGeometry[tile * Ff7SnowboardTerrain.TileSize + index];
            }
        }
        Reset(0, 0);
    }

    public int CurrentCourse { get; private set; }
    public int CurrentSegment { get; private set; }
    public int CurrentTriangle { get; private set; }
    public byte SurfaceFlag { get; private set; }
    public short LocalX { get; private set; }
    public short LocalZ { get; private set; }
    public int LogicalDistance => unchecked((short)(Course(CurrentCourse).StartDistance + CurrentSegment));

    public void Reset(int course, int segment)
    {
        ValidateSegment(course, segment);
        CurrentCourse = course;
        CurrentSegment = segment;
        CurrentTriangle = 0;
        SurfaceFlag = 0;
        LocalX = 0;
        LocalZ = 0;
    }

    internal void RestorePreviousSegment(int segment, int triangle)
    {
        ValidateSegment(CurrentCourse, segment);
        if ((uint)triangle >= Ff7SnowboardTerrain.TriangleCount)
        {
            throw new InvalidDataException("The snowboard motion rollback has an invalid previous triangle.");
        }
        CurrentSegment = segment;
        CurrentTriangle = triangle;
    }

    // A rejected position still requires the actor's separate velocity-response stage.
    public Ff7SnowboardCourseReconciliation Reconcile(Vector3 currentQ12, Vector3 previousQ12)
    {
        Vector3 current = Truncate(currentQ12);
        Vector3 previous = Truncate(previousQ12);
        int course = CurrentCourse;
        int segment = CurrentSegment;
        int tile = 0;
        short localX = 0;
        short localZ = 0;
        int triangle = -1;
        Resolve();
        byte[] geometry = ReadTile(tile);
        int type = Word(geometry, 0);
        if ((uint)type >= 4)
        {
            throw new NotSupportedException("The snowboard tile uses an unsupported navigation grid type.");
        }
        int xBin = 0;
        while (xBin < 3 && localX >= Word(geometry, 0x0E + xBin * 8))
        {
            xBin++;
        }
        int zBin = 0;
        while (zBin < 3 && localZ <= Word(geometry, 0x26 + zBin * 8))
        {
            zBin++;
        }
        int handler = data.GridClassifications[type * 16 + xBin + zBin * 4];
        bool ended = false;
        int collision = handler switch
        {
            0 => QueryAndCommit(),
            1 => Next(),
            2 => Previous(),
            3 => QueryOrNext(),
            4 => Branch(0, false),
            5 => Branch(1, false),
            6 => QueryOrBranch(0, false),
            7 => QueryOrBranch(1, true),
            8 => 1,
            _ => throw new NotSupportedException("The snowboard navigation grid references an unsupported handler.")
        };
        if (collision != 0)
        {
            return new(previous, previous, false, false, new(collision, course, segment, tile, localX, localZ, triangle));
        }
        LocalX = localX;
        LocalZ = localZ;
        return new(current, previous, true, ended, null);

        void Resolve()
        {
            ValidateSegment(course, segment);
            Ff7SnowboardCourseData selected = Course(course);
            tile = selected.Tiles[segment];
            if ((uint)tile >= 76)
            {
                throw new InvalidDataException("A snowboard course references a missing geometry tile.");
            }
            int x = unchecked((TruncateInt(current.X) >> 12) - selected.Positions[segment * 3]);
            int z = unchecked((TruncateInt(current.Z) >> 12) - selected.Positions[segment * 3 + 2]);
            (localX, localZ) = Rotate(unchecked((short)x), unchecked((short)z), unchecked((short)-selected.Angles[segment]));
        }

        int Query()
        {
            return triangle = Ff7SnowboardTerrain.FindTriangle(ReadTile(tile), localX, localZ);
        }

        int Commit()
        {
            CurrentCourse = course;
            CurrentSegment = segment;
            CurrentTriangle = triangle;
            SurfaceFlag = Ff7SnowboardTerrain.SurfaceFlag(ReadTile(tile), triangle);
            return 0;
        }

        int QueryAndCommit()
        {
            return Query() < 0 ? 1 : Commit();
        }

        int Next()
        {
            segment = unchecked((short)(segment + 1));
            if (segment >= Course(course).Tiles.Count)
            {
                ended = true;
                return 0;
            }
            Resolve();
            if (localZ > 0)
            {
                localZ = 0;
            }
            return QueryAndCommit();
        }

        int Previous()
        {
            segment = unchecked((short)(segment - 1));
            if (segment < 0)
            {
                int predecessor = (byte)(Course(course).Descriptor >> 24);
                if (predecessor == byte.MaxValue)
                {
                    return 1;
                }
                course = predecessor;
                segment = Course(course).Tiles.Count - 1;
            }
            Resolve();
            return QueryAndCommit();
        }

        int Branch(int branch, bool checkAvailability)
        {
            int destination = unchecked((sbyte)(Course(course).Connections >> (branch * 8)));
            Ff7SnowboardCourseData selected = Course(destination);
            if (checkAvailability && (byte)(selected.Descriptor >> 16) != 0)
            {
                return branch == 0 ? 3 : 4;
            }
            course = destination;
            segment = 0;
            Resolve();
            return QueryAndCommit();
        }

        int QueryOrNext()
        {
            return Query() < 0 ? Next() : Commit();
        }

        int QueryOrBranch(int branch, bool checkAvailability)
        {
            return Query() < 0 ? Branch(branch, checkAvailability) : Commit();
        }
    }

    private Ff7SnowboardCourseData Course(int index)
    {
        if ((uint)index >= data.Courses.Count)
        {
            throw new NotSupportedException("A snowboard connection references an unsupported course.");
        }
        return data.Courses[index];
    }

    private void ValidateSegment(int course, int segment)
    {
        Ff7SnowboardCourseData selected = Course(course);
        if ((uint)segment >= selected.Tiles.Count || selected.Angles.Count != selected.Tiles.Count || selected.Positions.Count != selected.Tiles.Count * 3 || (ushort)selected.Descriptor != selected.Tiles.Count)
        {
            throw new InvalidDataException("The snowboard course has an invalid segment or incomplete geometry transforms.");
        }
    }

    private byte[] ReadTile(int tile)
    {
        return tiles[tile];
    }

    private static short Word(byte[] data, int offset) => BinaryPrimitives.ReadInt16LittleEndian(data.AsSpan(offset));

    internal static (short X, short Z) Rotate(short x, short z, short angle)
    {
        double radians = angle * (double)(float)Math.PI / 2048.0;
        short sine = unchecked((short)(int)(Math.Sin(radians) * 4096.0));
        short cosine = unchecked((short)(int)(Math.Cos(radians) * 4096.0));
        int rotatedX = unchecked(cosine * x + sine * z) / 4096;
        int rotatedZ = unchecked(-sine * x + cosine * z) / 4096;
        return ((short)Math.Clamp(rotatedX, short.MinValue, short.MaxValue), (short)Math.Clamp(rotatedZ, short.MinValue, short.MaxValue));
    }

    private static Vector3 Truncate(Vector3 value) => new(TruncateInt(value.X), TruncateInt(value.Y), TruncateInt(value.Z));

    private static int TruncateInt(float value) => !float.IsFinite(value) || value >= 2147483648f || value < -2147483648f ? int.MinValue : (int)value;
}
