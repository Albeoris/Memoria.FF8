using System.Buffers.Binary;
using System.Reflection.PortableExecutable;

namespace Memoria.FF7.Resources;

public sealed record Ff7SnowboardCourseData(uint Descriptor, uint Connections, IReadOnlyList<byte> Tiles, IReadOnlyList<byte> Attributes, IReadOnlyList<byte> Auxiliary, IReadOnlyList<int> Parameters, IReadOnlyList<short> Angles, IReadOnlyList<int> Positions, short EndDistance)
{
    public short StartDistance { get; init; }
}

public sealed record Ff7SnowboardNativeData(IReadOnlyList<Ff7SnowboardCourseData> Courses, IReadOnlyList<byte> TileGeometry, IReadOnlyList<byte> GridClassifications, IReadOnlyList<byte> Results)
{
    public IReadOnlyList<byte> ActionScripts { get; init; } = Array.Empty<byte>();
    public IReadOnlyList<ushort> InputMasks { get; init; } = Array.Empty<ushort>();
    public IReadOnlyList<byte> CollisionActions { get; init; } = Array.Empty<byte>();
    public IReadOnlyList<int> ActionPoses { get; init; } = Array.Empty<int>();
}

public sealed partial class Ff7PcExecutableData
{
    public static Ff7SnowboardNativeData LoadSnowboardData(Ff7Installation installation)
    {
        using FileStream stream = OpenSupportedImage(installation);
        using PEReader pe = new(stream, PEStreamOptions.LeaveOpen);
        byte[] headers = ReadVirtual(stream, pe, 0x939CE0, 7 * 32);
        byte[] ends = ReadVirtual(stream, pe, 0x9262B8, 7 * 2);
        byte[] starts = ReadVirtual(stream, pe, 0x926298, 7 * 2);
        Ff7SnowboardCourseData[] courses = new Ff7SnowboardCourseData[7];
        for (int index = 0; index < courses.Length; index++)
        {
            ReadOnlySpan<byte> header = headers.AsSpan(index * 32, 32);
            uint descriptor = BinaryPrimitives.ReadUInt32LittleEndian(header);
            int count = (ushort)descriptor;
            if (count == 0)
            {
                throw new InvalidDataException("The FFVII snowboard course has no native segments.");
            }
            byte[] tiles = ReadVirtual(stream, pe, BinaryPrimitives.ReadUInt32LittleEndian(header[8..]), count);
            if (tiles.Any(tile => tile >= 76))
            {
                throw new InvalidDataException("The FFVII snowboard course references an unknown native tile.");
            }
            byte[] parameters = ReadVirtual(stream, pe, BinaryPrimitives.ReadUInt32LittleEndian(header[20..]), checked(count * 4));
            byte[] angles = ReadVirtual(stream, pe, BinaryPrimitives.ReadUInt32LittleEndian(header[24..]), checked(count * 2));
            byte[] positions = ReadVirtual(stream, pe, BinaryPrimitives.ReadUInt32LittleEndian(header[28..]), checked(count * 12));
            courses[index] = new(descriptor, BinaryPrimitives.ReadUInt32LittleEndian(header[4..]), Array.AsReadOnly(tiles), Array.AsReadOnly(ReadVirtual(stream, pe, BinaryPrimitives.ReadUInt32LittleEndian(header[12..]), count)), Array.AsReadOnly(ReadVirtual(stream, pe, BinaryPrimitives.ReadUInt32LittleEndian(header[16..]), count)), Array.AsReadOnly(ReadIntegers(parameters)), Array.AsReadOnly(ReadShorts(angles)), Array.AsReadOnly(ReadIntegers(positions)), BinaryPrimitives.ReadInt16LittleEndian(ends.AsSpan(index * 2)))
            {
                StartDistance = BinaryPrimitives.ReadInt16LittleEndian(starts.AsSpan(index * 2))
            };
        }
        byte[] masks = ReadVirtual(stream, pe, 0x956400, 8 * 2);
        ushort[] inputMasks = new ushort[8];
        for (int index = 0; index < inputMasks.Length; index++)
        {
            inputMasks[index] = BinaryPrimitives.ReadUInt16LittleEndian(masks.AsSpan(index * 2));
        }
        return new(Array.AsReadOnly(courses), Array.AsReadOnly(ReadVirtual(stream, pe, 0x926500, 76 * 0x374)), Array.AsReadOnly(ReadVirtual(stream, pe, 0x9263E0, 64)), Array.AsReadOnly(ReadVirtual(stream, pe, 0x926380, 7)))
        {
            ActionScripts = Array.AsReadOnly(ReadVirtual(stream, pe, 0x94A340, 0x4000)),
            InputMasks = Array.AsReadOnly(inputMasks),
            CollisionActions = Array.AsReadOnly(ReadVirtual(stream, pe, 0x9564D8, 32 * 9)),
            ActionPoses = Array.AsReadOnly(ReadIntegers(ReadVirtual(stream, pe, 0x956344, 5 * 4)))
        };

        static int[] ReadIntegers(ReadOnlySpan<byte> source)
        {
            int[] values = new int[source.Length / 4];
            for (int index = 0; index < values.Length; index++)
            {
                values[index] = BinaryPrimitives.ReadInt32LittleEndian(source[(index * 4)..]);
            }
            return values;
        }

        static short[] ReadShorts(ReadOnlySpan<byte> source)
        {
            short[] values = new short[source.Length / 2];
            for (int index = 0; index < values.Length; index++)
            {
                values[index] = BinaryPrimitives.ReadInt16LittleEndian(source[(index * 2)..]);
            }
            return values;
        }
    }
}
