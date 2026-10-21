using System.Buffers.Binary;
using static Memoria.FF8.Runtime.NativeBattleWaterExecutionFields;

namespace Memoria.FF8.Runtime;

public static class NativeBattleCinematicContextCommandAF
{
    public static NativeBattleWaterExecutionFrame Execute(NativeBattleWaterExecutionFrame frame, NativeBattleWaterScriptProgram program)
    {
        if (BinaryPrimitives.ReadUInt16LittleEndian(NativeBattleScriptedCommandReads.Program(frame, program, 2, 2)) != 0)
        {
            return frame;
        }
        uint payload = unchecked(CoreDword(frame, 0x2797450) + 4);
        frame = WriteSlotDword(frame, 0xB8, payload);
        uint context = BinaryPrimitives.ReadUInt32LittleEndian(ReadSlot(frame, 0xC0, 4));
        for (int index = 0; index < 3; index++)
        {
            short relative = BinaryPrimitives.ReadInt16LittleEndian(frame.Arena.Read(ArenaOffset(payload + (uint)(2 * index), 2), 2));
            uint value = unchecked(payload + (uint)(int)relative);
            frame = frame with { Arena = frame.Arena.Write(ArenaOffset(context + (uint)(8 + 4 * index), 4), BitConverter.GetBytes(value)) };
        }
        return NativeBattleScriptedCommandReads.Advance(frame, 0x50);
    }
}
