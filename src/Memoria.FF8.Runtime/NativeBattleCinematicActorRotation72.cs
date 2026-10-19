using System.Buffers.Binary;
using static Memoria.FF8.Runtime.NativeBattleWaterExecutionFields;

namespace Memoria.FF8.Runtime;

/// <summary>Copies the selected actor's three rotation WORDs into the slot's fixed-point scale fields.</summary>
public static class NativeBattleCinematicActorRotation72
{
    public static NativeBattleWaterExecutionFrame Execute(NativeBattleWaterExecutionFrame frame, Func<NativeBattleWaterExecutionFrame, uint, int, byte[]> readExternal)
    {
        ArgumentNullException.ThrowIfNull(readExternal);
        uint header = CoreDword(frame, 0x279744C);
        byte actor = ReadSlot(frame, 0x1B, 1)[0];
        uint source = CoreDword(frame, unchecked(header + 0x60 + (uint)(actor * 4)));
        for (int lane = 0; lane < 3; lane++)
        {
            short value = BinaryPrimitives.ReadInt16LittleEndian(NativeBattleScriptedCommandReads.Address(frame, unchecked(source + 0xC + (uint)(lane * 2)), 2, readExternal));
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
