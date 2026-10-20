using System.Buffers.Binary;
using static Memoria.FF8.Runtime.NativeBattleWaterExecutionFields;

namespace Memoria.FF8.Runtime;

/// <summary>Copies selected cached pose components into the current node's fixed-point motion fields.</summary>
public static class NativeBattleCinematicCopyPose91
{
    public static NativeBattleWaterExecutionFrame Execute(NativeBattleWaterExecutionFrame frame, NativeBattleWaterScriptProgram program, Func<NativeBattleWaterExecutionFrame, short, uint> resolveNode, Func<NativeBattleWaterExecutionFrame, byte, NativeBattleWaterExecutionFrame> updateSlot)
    {
        short selector = BinaryPrimitives.ReadInt16LittleEndian(NativeBattleScriptedCommandReads.Program(frame, program, 4, 2));
        uint reference = resolveNode(frame, selector);
        short mask = BinaryPrimitives.ReadInt16LittleEndian(NativeBattleScriptedCommandReads.Program(frame, program, 2, 2));
        for (int lane = 0; lane < 6; lane++)
        {
            if ((mask & (1 << lane)) != 0)
            {
                int offset = 0x8C + lane * 2 + (lane >= 3 ? 2 : 0);
                short component = BinaryPrimitives.ReadInt16LittleEndian(frame.Arena.Read(ArenaOffset(unchecked(reference + (uint)offset), 2), 2));
                frame = WriteSlotDword(frame, 0x50 + lane * 4, unchecked((uint)(component << 16)));
            }
        }
        for (int lane = 0; lane < 3; lane++)
        {
            int component = BinaryPrimitives.ReadInt32LittleEndian(ReadSlot(frame, 0x50 + lane * 4, 4)) >> 16;
            frame = WriteSlotWord(frame, 0x8C + lane * 2, unchecked((ushort)component));
        }
        frame = updateSlot(frame, ReadSlot(frame, 0x18, 1)[0]);
        return NativeBattleScriptedCommandReads.Advance(frame, 6);
    }
}
