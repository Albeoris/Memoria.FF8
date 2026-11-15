using System.Buffers.Binary;
using static Memoria.FF8.Runtime.NativeBattleWaterExecutionFields;

namespace Memoria.FF8.Runtime;

public static class NativeBattleCinematicLinkedPosition70
{
    public static NativeBattleWaterExecutionFrame Draw(NativeBattleWaterExecutionFrame frame, Func<BattleModelOwner, NativeBattleLinkedModelTransform> read, Action<BattleModelOwner, NativeBattleLinkedModelTransform> write)
    {
        BattleModelOwner pointer = NativeBattleCinematicActorAccess.ReadSlotActor(frame);
        byte[] point = ReadSlot(frame, 0x94, 6);
        NativeBattleLinkedModelTransform linked = read(pointer) ?? throw new NotSupportedException("Native linked position requires the actor's actual linked object.");
        write(pointer, linked with { Position = new(BinaryPrimitives.ReadInt16LittleEndian(point), BinaryPrimitives.ReadInt16LittleEndian(point.AsSpan(2)), BinaryPrimitives.ReadInt16LittleEndian(point.AsSpan(4))) });
        return frame;
    }
}
