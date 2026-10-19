using System.Buffers.Binary;
using static Memoria.FF8.Runtime.NativeBattleWaterExecutionFields;

namespace Memoria.FF8.Runtime;

/// <summary>Calculate three signed fixed-coordinate velocities toward the selected slot.</summary>
public static class NativeBattleCinematicVelocity82
{
    public static NativeBattleWaterExecutionFrame Execute(NativeBattleWaterExecutionFrame frame, NativeBattleWaterScriptProgram program, Func<NativeBattleWaterExecutionFrame, short, uint> resolveSlot)
    {
        short selector = BinaryPrimitives.ReadInt16LittleEndian(NativeBattleScriptedCommandReads.Program(frame, program, 2, 2));
        uint selected = resolveSlot(frame, selector);
        int duration = BinaryPrimitives.ReadInt16LittleEndian(NativeBattleScriptedCommandReads.Program(frame, program, 4, 2));
        for (int lane = 0; lane < 3; lane++)
        {
            uint address = unchecked(selected + 0x5Cu + (uint)(lane * 4));
            int target = BinaryPrimitives.ReadInt32LittleEndian(frame.Arena.Read(ArenaOffset(address, 4), 4));
            int current = BinaryPrimitives.ReadInt32LittleEndian(ReadSlot(frame, 0x5C + lane * 4, 4));
            int velocity = unchecked(target - current) / duration;
            frame = WriteSlotDword(frame, 0x74 + lane * 4, unchecked((uint)velocity));
        }
        return NativeBattleScriptedCommandReads.Advance(frame, 6);
    }
}
