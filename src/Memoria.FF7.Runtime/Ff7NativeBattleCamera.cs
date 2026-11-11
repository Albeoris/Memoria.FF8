using System.Buffers.Binary;
using Memoria.FF7.Formats;

namespace Memoria.FF7.Runtime;

public readonly record struct Ff7BattleActionCameras(ushort SingleTarget, ushort MultipleTargets);

public static class Ff7NativeBattleCamera
{
    public static sbyte SelectTrack(sbyte current, byte presentationCounter)
    {
        if ((uint)current >= 4)
        {
            throw new InvalidDataException("FFVII camera requires a valid current presentation track.");
        }
        int candidate = presentationCounter & 3;
        return current == 3 || candidate == 3 ? current : (sbyte)candidate;
    }

    public static int? InitialTarget(IReadOnlyList<int> targetRows)
    {
        ArgumentNullException.ThrowIfNull(targetRows);
        if (targetRows.Any(slot => (uint)slot >= 10))
        {
            throw new InvalidDataException("FFVII presentation target rows require native physical actor slots.");
        }
        return targetRows.Count switch
        {
            0 => null,
            1 => targetRows[0],
            _ => 15
        };
    }

    public static Ff7BattleActionCameras SelectWeapon(ReadOnlySpan<byte> weapon)
    {
        if (weapon.Length != 44)
        {
            throw new InvalidDataException("FFVII weapon camera selection requires its complete native record.");
        }
        ushort camera = BinaryPrimitives.ReadUInt16LittleEndian(weapon[12..]);
        return new(camera, camera);
    }

    public static Ff7BattleActionCameras SelectItem(ReadOnlySpan<byte> item)
    {
        if (item.Length != 28)
        {
            throw new InvalidDataException("FFVII item camera selection requires its complete native record.");
        }
        ushort camera = BinaryPrimitives.ReadUInt16LittleEndian(item[8..]);
        return new(camera, camera);
    }

    public static Ff7BattleActionCameras SelectAction(Ff7BattleAttack attack, ushort enemyOverride, bool limit, bool multipleTargets)
    {
        ArgumentNullException.ThrowIfNull(attack);
        ushort selected = limit ? multipleTargets ? attack.MultipleTargetCamera : attack.SingleTargetCamera : enemyOverride;
        return selected == ushort.MaxValue
            ? new(attack.SingleTargetCamera, attack.MultipleTargetCamera)
            : new(selected, selected);
    }
}
