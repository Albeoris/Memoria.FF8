using System.Buffers.Binary;
using Memoria.FF7.Formats;

namespace Memoria.FF7.Resources;

public sealed record Ff7CondorEnemyWave(byte RowType, byte X, byte Y, byte Path, ushort SpawnTime, byte Extra)
{
    public byte Reserved { get; init; }
}
public sealed record Ff7CondorPathPoint(byte X, byte Y);
public sealed record Ff7CondorUnitType(byte Behavior, byte Interval, ReadOnlyMemory<byte> Data);

public sealed class Ff7CondorResources
{
    public const int AttackCount = 7;
    private readonly byte[] data;
    private readonly int pathDirectory;

    private Ff7CondorResources(byte[] data, int pathDirectory, int pathCount, IReadOnlyList<ReadOnlyMemory<byte>> terrainCells)
    {
        this.data = data;
        this.pathDirectory = pathDirectory;
        PathCount = pathCount;
        TerrainCells = terrainCells;
    }

    public int PathCount { get; }
    public IReadOnlyList<ReadOnlyMemory<byte>> TerrainCells { get; }

    public static Ff7CondorResources Load(Ff7Installation installation)
    {
        ArgumentNullException.ThrowIfNull(installation);
        using LgpArchive archive = LgpArchive.Open(Path.Combine(installation.DataDirectory, "minigame", "condor.lgp"));
        return Read(ReadMember(archive, "data.bin"), ReadMember(archive, "id.bin"), ReadMember(archive, "vert.bin"));
    }

    private static byte[] ReadMember(LgpArchive archive, string name)
    {
        IReadOnlyList<LgpEntry> entries = archive.Find(name);
        if (entries.Count != 1)
        {
            throw new InvalidDataException("The FFVII Condor archive requires an unambiguous native data member.");
        }
        return archive.ReadEntry(entries[0]);
    }

    public static Ff7CondorResources Read(ReadOnlySpan<byte> source, ReadOnlySpan<byte> identifiers = default, ReadOnlySpan<byte> vertices = default)
    {
        if (source.Length < 10 + AttackCount * 2)
        {
            throw new InvalidDataException("The FFVII Condor native data header is truncated.");
        }
        int directory = BinaryPrimitives.ReadUInt16LittleEndian(source[4..]);
        if (directory < 10 + AttackCount * 2 || directory > source.Length - 2)
        {
            throw new InvalidDataException("The FFVII Condor path directory lies outside its allocation.");
        }
        int firstPath = BinaryPrimitives.ReadUInt16LittleEndian(source[directory..]);
        int directoryBytes = firstPath - directory;
        if (directoryBytes < 2 || (directoryBytes & 1) != 0 || firstPath >= source.Length)
        {
            throw new InvalidDataException("The FFVII Condor path directory has invalid native offsets.");
        }
        List<ReadOnlyMemory<byte>> cells = [];
        if (!identifiers.IsEmpty || !vertices.IsEmpty)
        {
            if (identifiers.Length < 2)
            {
                throw new InvalidDataException("The FFVII Condor terrain cell count is truncated.");
            }
            int count = BinaryPrimitives.ReadUInt16LittleEndian(identifiers);
            if (vertices.Length != count * 0x4C)
            {
                throw new InvalidDataException("The FFVII Condor terrain allocation does not match its native cell count.");
            }
            byte[] geometry = vertices.ToArray();
            for (int index = 0; index < count; index++)
            {
                cells.Add(geometry.AsMemory(index * 0x4C, 0x4C));
            }
        }
        return new(source.ToArray(), directory, directoryBytes / 2, cells.AsReadOnly());
    }

    public Ff7CondorUnitType ReadUnitType(int type)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(type);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(type, byte.MaxValue);
        int directory = BinaryPrimitives.ReadUInt16LittleEndian(data);
        int offset = directory + type * 32;
        if (directory < 10 + AttackCount * 2 || offset > data.Length - 32)
        {
            throw new InvalidDataException("The FFVII Condor unit type record lies outside its allocation.");
        }
        return new(data[offset + 6], data[offset + 0x0C], data.AsMemory(offset, 32));
    }

    public IReadOnlyList<Ff7CondorEnemyWave> ReadWave(int attack)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(attack);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(attack, AttackCount);
        int cursor = BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(10 + attack * 2));
        if (cursor < 10 + AttackCount * 2 || cursor >= data.Length)
        {
            throw new InvalidDataException("The FFVII Condor enemy wave lies outside its allocation.");
        }
        List<Ff7CondorEnemyWave> rows = [];
        while (cursor < data.Length)
        {
            if (data[cursor] == 0xFF)
            {
                return rows.AsReadOnly();
            }
            if (cursor > data.Length - 8 || data[cursor + 3] >= PathCount)
            {
                throw new InvalidDataException("The FFVII Condor enemy wave has a truncated row or invalid path.");
            }
            rows.Add(new(data[cursor], data[cursor + 1], data[cursor + 2], data[cursor + 3], BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(cursor + 4)), data[cursor + 6]) { Reserved = data[cursor + 7] });
            cursor += 8;
        }
        throw new InvalidDataException("The FFVII Condor enemy wave has no native terminator.");
    }

    public IReadOnlyList<Ff7CondorPathPoint> ReadPath(int path)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(path);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(path, PathCount);
        int cursor = BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(pathDirectory + path * 2));
        if (cursor < pathDirectory + PathCount * 2 || cursor >= data.Length)
        {
            throw new InvalidDataException("The FFVII Condor path lies outside its allocation.");
        }
        List<Ff7CondorPathPoint> points = [];
        while (cursor < data.Length)
        {
            if (data[cursor] == 0xFF)
            {
                return points.AsReadOnly();
            }
            if (cursor > data.Length - 2)
            {
                throw new InvalidDataException("The FFVII Condor path point is truncated.");
            }
            points.Add(new(data[cursor], data[cursor + 1]));
            cursor += 2;
        }
        throw new InvalidDataException("The FFVII Condor path has no native terminator.");
    }
}
