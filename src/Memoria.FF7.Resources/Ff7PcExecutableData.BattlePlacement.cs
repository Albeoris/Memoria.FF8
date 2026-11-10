using System.Buffers.Binary;
using System.Numerics;
using System.Reflection.PortableExecutable;

namespace Memoria.FF7.Resources;

public readonly record struct Ff7BattlePlacement(Vector3 Position, int Yaw);

public sealed class Ff7BattlePlacementTables
{
    private readonly byte[] threePositions;
    private readonly byte[] twoPositions;
    private readonly byte[] threeAngles;
    private readonly byte[] twoAngles;

    internal Ff7BattlePlacementTables(byte[] threePositions, byte[] twoPositions, byte[] threeAngles, byte[] twoAngles)
    {
        this.threePositions = threePositions;
        this.twoPositions = twoPositions;
        this.threeAngles = threeAngles;
        this.twoAngles = twoAngles;
    }

    public Ff7BattlePlacement Party(int layout, int count, int physicalSlot, int compactSlot, bool backRow)
    {
        ArgumentOutOfRangeException.ThrowIfGreaterThan((uint)layout, 8u);
        bool pair = count == 2;
        int slot = pair ? compactSlot : physicalSlot;
        int rowLength = pair ? 2 : 3;
        int index = layout * rowLength + slot;
        ReadOnlySpan<byte> position = (pair ? twoPositions : threePositions).AsSpan(index * 6, 6);
        int yaw = BinaryPrimitives.ReadInt16LittleEndian((pair ? twoAngles : threeAngles).AsSpan(index * 2));
        int x = count == 1 ? 0 : BinaryPrimitives.ReadInt16LittleEndian(position);
        int y = BinaryPrimitives.ReadInt16LittleEndian(position[2..]);
        int z = BinaryPrimitives.ReadInt16LittleEndian(position[4..]);
        if (backRow)
        {
            z += yaw == 0 ? 516 : -516;
        }
        return new(new Vector3(x, y, z), yaw);
    }

    public static int EnemyYaw(int layout, Vector3 position)
    {
        return layout switch
        {
            0 or 2 or 4 or 8 => position.Z < 0 ? 2048 : 0,
            1 => 0,
            3 or 5 or 6 or 7 => position.Z >= 0 ? 2048 : 0,
            _ => throw new ArgumentOutOfRangeException(nameof(layout))
        };
    }
}

public sealed partial class Ff7PcExecutableData
{
    public static Ff7BattlePlacementTables LoadBattlePlacement(Ff7Installation installation)
    {
        using FileStream stream = OpenSupportedImage(installation);
        using PEReader pe = new(stream, PEStreamOptions.LeaveOpen);
        return new(ReadVirtual(stream, pe, 0x7C21F0, 9 * 18), ReadVirtual(stream, pe, 0x7C2340, 9 * 12), ReadVirtual(stream, pe, 0x7C2420, 9 * 6), ReadVirtual(stream, pe, 0x7C2490, 9 * 4));
    }
}
