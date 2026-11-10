using System.Buffers.Binary;

namespace Memoria.FF7.Formats;

public enum Ff7BattleCameraComponent
{
    Position,
    Focus
}

public sealed class Ff7BattleCameraArchive
{
    private const uint PointerBase = 0x801A0000;
    private readonly byte[] data;
    private readonly int[][] offsets;
    private readonly int[] boundaries;

    private Ff7BattleCameraArchive(byte[] data, int[][] offsets, int[] boundaries, int cameraCount)
    {
        this.data = data;
        this.offsets = offsets;
        this.boundaries = boundaries;
        CameraCount = cameraCount;
    }

    public int CameraCount { get; }

    public static Ff7BattleCameraArchive Read(ReadOnlySpan<byte> source)
    {
        if (source.Length < 40)
        {
            throw new InvalidDataException("FFVII battle camera archive is truncated.");
        }

        int[] tables = new int[4];
        for (int table = 0; table < tables.Length; table++)
        {
            tables[table] = Relocate(source, table * sizeof(uint));
            if (tables[table] < 16 || tables[table] > source.Length - 12 || (tables[table] & 3) != 0 || table > 0 && tables[table] <= tables[table - 1])
            {
                throw new InvalidDataException("FFVII battle camera table pointer is invalid.");
            }
        }

        int tableBytes = tables[1] - tables[0];
        if (tableBytes % 12 != 0 || tables[1] + tableBytes > tables[2] || tables[2] + 12 != tables[3] || tables[3] + 12 != source.Length)
        {
            throw new InvalidDataException("FFVII battle camera table sizes are invalid.");
        }

        int cameraCount = tableBytes / 12;
        int[][] offsets = new int[4][];
        SortedSet<int> boundaries = new(tables) { source.Length };
        for (int table = 0; table < offsets.Length; table++)
        {
            int count = table < 2 ? cameraCount * 3 : 3;
            offsets[table] = new int[count];
            for (int entry = 0; entry < count; entry++)
            {
                int offset = Relocate(source, tables[table] + entry * sizeof(uint));
                if (offset < 16 || offset >= source.Length || offset >= tables[0] && offset < tables[1] + tableBytes || offset >= tables[2])
                {
                    throw new InvalidDataException("FFVII battle camera script pointer crosses a pointer table.");
                }
                offsets[table][entry] = offset;
                boundaries.Add(offset);
            }
        }

        return new(source.ToArray(), offsets, boundaries.ToArray(), cameraCount);
    }

    public ReadOnlyMemory<byte> Script(int camera, int track, Ff7BattleCameraComponent component)
    {
        if ((uint)camera >= CameraCount || (uint)track >= 3 || (uint)component > 1)
        {
            throw new ArgumentOutOfRangeException(nameof(camera), "FFVII camera script requires a valid camera, component and one of three tracks.");
        }
        return At(offsets[(int)component][camera * 3 + track]);
    }

    public ReadOnlyMemory<byte> SpecialScript(int track, Ff7BattleCameraComponent component)
    {
        if ((uint)track >= 3 || (uint)component > 1)
        {
            throw new ArgumentOutOfRangeException(nameof(track));
        }
        return At(offsets[2 + (int)component][track]);
    }

    private ReadOnlyMemory<byte> At(int offset)
    {
        int index = Array.BinarySearch(boundaries, offset);
        return data.AsMemory(offset, boundaries[index + 1] - offset);
    }

    private static int Relocate(ReadOnlySpan<byte> source, int offset)
    {
        uint pointer = BinaryPrimitives.ReadUInt32LittleEndian(source.Slice(offset, sizeof(uint)));
        if (pointer < PointerBase || pointer - PointerBase >= source.Length)
        {
            throw new InvalidDataException("FFVII battle camera pointer is outside the archive.");
        }
        return (int)(pointer - PointerBase);
    }
}
