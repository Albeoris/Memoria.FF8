using System.Buffers.Binary;
using Memoria.FF7.Formats;

namespace Memoria.FF7.Runtime;

public readonly record struct Ff7BattleActionCameras(ushort SingleTarget, ushort MultipleTargets);

public static class Ff7NativeBattleCamera
{
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
