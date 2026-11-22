using System.Buffers.Binary;
using System.Collections.ObjectModel;

namespace Memoria.FF7.Resources;

public sealed record Ff7SubmarineRouteNode(int X, int Y, int Z, int NextIndex, ReadOnlyMemory<byte> Data)
{
    public int Radius { get; init; }
    public int TargetSpeed { get; init; }
}
public sealed record Ff7SubmarineFleetEntry(int X, int Y, int Z, int HitPoints, short Pitch, short Yaw, short Roll, int Speed, int TargetSpeed, int Flags, int BehaviorIndex, int RouteIndex, ReadOnlyMemory<byte> Data);
public sealed record Ff7SubmarinePlayerEntry(int X, int Y, int Z, int HitPoints, short Pitch, short Yaw, short Roll, int ActionType, int BehaviorIndex, ReadOnlyMemory<byte> Data);

public sealed class Ff7SubmarineResources
{
    private Ff7SubmarineResources(byte[] terrainAscii, Ff7SubmarineRouteNode[] routeNodes, Ff7SubmarineFleetEntry[] fleet, Ff7SubmarinePlayerEntry player)
    {
        TerrainAscii = Array.AsReadOnly(terrainAscii);
        RouteNodes = Array.AsReadOnly(routeNodes);
        Fleet = Array.AsReadOnly(fleet);
        Player = player;
    }

    public IReadOnlyList<byte> TerrainAscii { get; }
    public IReadOnlyList<Ff7SubmarineRouteNode> RouteNodes { get; }
    public IReadOnlyList<Ff7SubmarineFleetEntry> Fleet { get; }
    public Ff7SubmarinePlayerEntry Player { get; }
    public IReadOnlyList<int> SpeedTable { get; private init; } = Array.Empty<int>();
    public IReadOnlyList<IReadOnlyList<short>> MotionTables { get; private init; } = Array.Empty<IReadOnlyList<short>>();
    public IReadOnlyList<ReadOnlyMemory<byte>> PlayerTypes { get; private init; } = Array.Empty<ReadOnlyMemory<byte>>();
    public IReadOnlyList<ushort> NativeRoots { get; private init; } = Array.Empty<ushort>();
    public IReadOnlyList<ushort> NativeReciprocals { get; private init; } = Array.Empty<ushort>();
    public IReadOnlyDictionary<int, int> InitialGlobals { get; private init; } = new ReadOnlyDictionary<int, int>(new Dictionary<int, int>());

    public static Ff7SubmarineResources Load(Ff7Installation installation)
    {
        ArgumentNullException.ThrowIfNull(installation);
        return Ff7PcExecutableData.LoadSubmarineResources(installation);
    }

    public static Ff7SubmarineResources Read(ReadOnlySpan<byte> terrainAscii, ReadOnlySpan<byte> routes, ReadOnlySpan<byte> fleet, ReadOnlySpan<byte> player, ReadOnlySpan<byte> speeds = default, ReadOnlySpan<byte> types = default, ReadOnlySpan<byte> motion0 = default, ReadOnlySpan<byte> motion1 = default, ReadOnlySpan<byte> motion2 = default, ReadOnlySpan<byte> motion3 = default, ReadOnlySpan<byte> roots = default, ReadOnlySpan<byte> reciprocals = default, IReadOnlyDictionary<int, int>? initialGlobals = null, ReadOnlySpan<byte> routeDefaults = default)
    {
        if (terrainAscii.Length != 64 * 64 || routes.Length != 128 * 32 || fleet.Length != 12 * 48 || player.Length != 40)
        {
            throw new InvalidDataException("FFVII submarine scenario data has invalid native table lengths.");
        }
        CheckOptionalLength(speeds, 19 * 4);
        CheckOptionalLength(types, 5 * 0x38);
        CheckOptionalLength(motion0, 33 * 2);
        CheckOptionalLength(motion1, 33 * 2);
        CheckOptionalLength(motion2, 33 * 2);
        CheckOptionalLength(motion3, 33 * 2);
        CheckOptionalLength(roots, 256 * 2);
        CheckOptionalLength(reciprocals, 256 * 2);
        CheckOptionalLength(routeDefaults, 128 * 32);
        Ff7SubmarineRouteNode[] routeNodes = new Ff7SubmarineRouteNode[128];
        for (int index = 0; index < routeNodes.Length; index++)
        {
            byte[] row = routes.Slice(index * 32, 32).ToArray();
            ReadOnlySpan<byte> controls = routeDefaults.IsEmpty ? row : routeDefaults.Slice(index * 32, 32);
            routeNodes[index] = new(I32(row, 0), I32(row, 4), I32(row, 8), I32(row, 0x18), row)
            {
                Radius = I32(controls, 0x10),
                TargetSpeed = I32(controls, 0x14)
            };
        }
        Ff7SubmarineFleetEntry[] ships = new Ff7SubmarineFleetEntry[12];
        for (int index = 0; index < ships.Length; index++)
        {
            byte[] row = fleet.Slice(index * 48, 48).ToArray();
            ships[index] = new(I32(row, 0), I32(row, 4), I32(row, 8), I32(row, 0x10), I16(row, 0x14), I16(row, 0x16), I16(row, 0x18), I32(row, 0x1C), I32(row, 0x20), I32(row, 0x24), I32(row, 0x28), I32(row, 0x2C), row);
        }
        byte[] playerRow = player.ToArray();
        Ff7SubmarinePlayerEntry playerEntry = new(I32(playerRow, 0), I32(playerRow, 4), I32(playerRow, 8), I32(playerRow, 0x10), I16(playerRow, 0x14), I16(playerRow, 0x16), I16(playerRow, 0x18), I32(playerRow, 0x20), I32(playerRow, 0x24), playerRow);
        int[] speedTable = new int[speeds.Length / 4];
        for (int index = 0; index < speedTable.Length; index++)
        {
            speedTable[index] = I32(speeds, index * 4);
        }
        ReadOnlyMemory<byte>[] playerTypes = new ReadOnlyMemory<byte>[types.Length / 0x38];
        for (int index = 0; index < playerTypes.Length; index++)
        {
            playerTypes[index] = types.Slice(index * 0x38, 0x38).ToArray();
        }
        return new(terrainAscii.ToArray(), routeNodes, ships, playerEntry)
        {
            SpeedTable = Array.AsReadOnly(speedTable),
            PlayerTypes = Array.AsReadOnly(playerTypes),
            MotionTables = Array.AsReadOnly(new IReadOnlyList<short>[] { ReadSignedWords(motion0), ReadSignedWords(motion1), ReadSignedWords(motion2), ReadSignedWords(motion3) }),
            NativeRoots = ReadUnsignedWords(roots),
            NativeReciprocals = ReadUnsignedWords(reciprocals),
            InitialGlobals = new ReadOnlyDictionary<int, int>(initialGlobals?.ToDictionary(pair => pair.Key, pair => pair.Value) ?? new Dictionary<int, int>())
        };
    }

    private static void CheckOptionalLength(ReadOnlySpan<byte> source, int length)
    {
        if (!source.IsEmpty && source.Length != length)
        {
            throw new InvalidDataException("FFVII submarine physics data has an invalid native table length.");
        }
    }

    private static IReadOnlyList<short> ReadSignedWords(ReadOnlySpan<byte> source)
    {
        short[] values = new short[source.Length / 2];
        for (int index = 0; index < values.Length; index++)
        {
            values[index] = I16(source, index * 2);
        }
        return Array.AsReadOnly(values);
    }

    private static IReadOnlyList<ushort> ReadUnsignedWords(ReadOnlySpan<byte> source)
    {
        ushort[] values = new ushort[source.Length / 2];
        for (int index = 0; index < values.Length; index++)
        {
            values[index] = BinaryPrimitives.ReadUInt16LittleEndian(source[(index * 2)..]);
        }
        return Array.AsReadOnly(values);
    }

    private static int I32(ReadOnlySpan<byte> source, int offset) => BinaryPrimitives.ReadInt32LittleEndian(source[offset..]);
    private static short I16(ReadOnlySpan<byte> source, int offset) => BinaryPrimitives.ReadInt16LittleEndian(source[offset..]);
}
