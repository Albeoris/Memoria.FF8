using System.Buffers.Binary;
using static Memoria.FF8.Runtime.NativeBattleWaterExecutionFields;

namespace Memoria.FF8.Runtime;

/// <summary>Copies the selected actor's three rotation WORDs into the slot's fixed-point scale fields.</summary>
public static class NativeBattleCinematicActorRotation72
{
    public static NativeBattleWaterExecutionFrame Execute(NativeBattleWaterExecutionFrame frame, Func<BattleModelOwner, NativeBattleGraphicsModel> readActor)
    {
        NativeBattleGraphicsModel actor = readActor(NativeBattleCinematicActorAccess.ReadSlotActor(frame));
        for (int lane = 0; lane < 3; lane++)
        {
            short value = (lane switch { 0 => actor.Rotation12, 1 => actor.Rotation14, _ => actor.Rotation16 }) ?? throw new NotSupportedException("Native cinematic actor rotation is unknown.");
            frame = WriteSlotDword(frame, 0x50 + lane * 4, unchecked((uint)(value << 16)));
        }
        for (int lane = 0; lane < 3; lane++)
        {
            int value = unchecked((int)BinaryPrimitives.ReadUInt32LittleEndian(ReadSlot(frame, 0x50 + lane * 4, 4))) >> 16;
            frame = WriteSlotWord(frame, 0x8C + lane * 2, unchecked((ushort)value));
        }
        return NativeBattleScriptedCommandReads.Advance(frame, 2);
    }
}
