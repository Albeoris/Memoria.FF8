using System.Buffers.Binary;
using System.Text;

namespace Memoria.FF7.Formats;

public readonly record struct PcChocoboVector(short X, short Y, short Z, ushort Attributes);
public sealed record PcChocoboTrackElement(PcChocoboVector Left, PcChocoboVector Right, sbyte CameraEvent, sbyte SceneryEvent, byte CullDistance, byte TerrainFlags, uint Color);

public sealed class PcChocoboCourse
{
    private PcChocoboCourse(PcChocoboTrackElement[] track, byte[] guide, short[] mapPointers, uint[] mapTable, byte[] dome, byte[] triangles, byte[] quads, byte[] sprites)
    {
        Track = Array.AsReadOnly(track);
        Guide = guide;
        MapPointers = Array.AsReadOnly(mapPointers);
        MapTable = Array.AsReadOnly(mapTable);
        DomeTriangles = dome;
        Triangles = triangles;
        Quads = quads;
        Sprites = sprites;
    }

    public IReadOnlyList<PcChocoboTrackElement> Track { get; }
    public ReadOnlyMemory<byte> Guide { get; }
    public IReadOnlyList<short> MapPointers { get; }
    public IReadOnlyList<uint> MapTable { get; }
    public ReadOnlyMemory<byte> DomeTriangles { get; }
    public ReadOnlyMemory<byte> Triangles { get; }
    public ReadOnlyMemory<byte> Quads { get; }
    public ReadOnlyMemory<byte> Sprites { get; }

    public static PcChocoboCourse Read(ReadOnlySpan<byte> data, bool shortCourse)
    {
        CourseReader reader = new(data);
        PcChocoboCourse longCourse = ReadCourse(ref reader, 'L', 0x49D, 0x4A1, 0x3C38, 0x2E49, 0x1269, 0xFF);
        reader.Expect("SHORT");
        PcChocoboCourse shortTrack = ReadCourse(ref reader, 'S', 0x3B1, 0x3B5, 0x2F8A, 0x27A2, 0x103D, 0x5A);
        reader.Expect("END");
        return shortCourse ? shortTrack : longCourse;
    }

    private static PcChocoboCourse ReadCourse(ref CourseReader reader, char prefix, int length, int pointerCount, int mapCount, int triangleCount, int quadCount, int spriteCount)
    {
        reader.ExpectCount(length);
        PcChocoboTrackElement[] track = new PcChocoboTrackElement[length];
        for (int index = 0; index < track.Length; index++)
        {
            ReadOnlySpan<byte> record = reader.Take(24);
            track[index] = new(Vector(record), Vector(record[8..]), unchecked((sbyte)record[16]), unchecked((sbyte)record[17]), record[18], record[19], BinaryPrimitives.ReadUInt32LittleEndian(record[20..]));
        }
        reader.Expect($"{prefix}GUIDE");
        byte[] guide = reader.Take(length * 8).ToArray();
        reader.Expect($"{prefix}MAPPOINTE");
        reader.ExpectCount(pointerCount);
        short[] pointers = new short[pointerCount];
        for (int index = 0; index < pointers.Length; index++)
        {
            pointers[index] = BinaryPrimitives.ReadInt16LittleEndian(reader.Take(2));
        }
        reader.Expect($"{prefix}MAPTABLE");
        reader.ExpectCount(mapCount);
        uint[] map = new uint[mapCount];
        for (int index = 0; index < map.Length; index++)
        {
            map[index] = BinaryPrimitives.ReadUInt32LittleEndian(reader.Take(4));
        }
        byte[] dome = reader.Block($"{prefix}DOMEG3", 0x19E, 36);
        byte[] triangles = reader.Block($"{prefix}DG3", triangleCount, 28);
        byte[] quads = reader.Block($"{prefix}DG4", quadCount, 40);
        // The native allocation and next-course offset include 40 bytes per sprite, including unused trailing storage.
        byte[] sprites = reader.Block($"{prefix}SPRITE", spriteCount, 40);
        return new(track, guide, pointers, map, dome, triangles, quads, sprites);
    }

    private static PcChocoboVector Vector(ReadOnlySpan<byte> data)
    {
        return new(BinaryPrimitives.ReadInt16LittleEndian(data), BinaryPrimitives.ReadInt16LittleEndian(data[2..]), BinaryPrimitives.ReadInt16LittleEndian(data[4..]), BinaryPrimitives.ReadUInt16LittleEndian(data[6..]));
    }

    private ref struct CourseReader(ReadOnlySpan<byte> data)
    {
        private ReadOnlySpan<byte> remaining = data;

        public ReadOnlySpan<byte> Take(int length)
        {
            if (remaining.Length < length)
            {
                throw new InvalidDataException("The FFVII Chocobo course is truncated.");
            }
            ReadOnlySpan<byte> result = remaining[..length];
            remaining = remaining[length..];
            return result;
        }

        public void Expect(string marker)
        {
            if (!Take(marker.Length).SequenceEqual(Encoding.ASCII.GetBytes(marker)))
            {
                throw new InvalidDataException($"The FFVII Chocobo course is missing its {marker} marker.");
            }
        }

        public void ExpectCount(int count)
        {
            if (BinaryPrimitives.ReadUInt16LittleEndian(Take(2)) != count)
            {
                throw new InvalidDataException("The FFVII Chocobo course count differs from its native layout.");
            }
        }

        public byte[] Block(string marker, int count, int size)
        {
            Expect(marker);
            ExpectCount(count);
            return Take(checked(count * size)).ToArray();
        }
    }
}
