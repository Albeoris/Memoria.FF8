using System.Buffers.Binary;
using static Memoria.FF8.Runtime.NativeBattleWaterExecutionFields;

namespace Memoria.FF8.Runtime;

/// <summary>Solve the selected acceleration lanes over the original signed interval and target positions.</summary>
public static class NativeBattleCinematicAccelerationAC
{
    public static NativeBattleWaterExecutionFrame Execute(NativeBattleWaterExecutionFrame frame, NativeBattleWaterScriptProgram program, NativeBattleWaterSlotBank bank, Func<NativeBattleWaterExecutionFrame, short, uint> resolveSlot)
    {
        ushort instruction = frame.ReadTemporaryWord(0x14A);
        int duration = BinaryPrimitives.ReadInt16LittleEndian(NativeBattleScriptedCommandReads.Program(frame, program, 2, 2));
        int divisor = unchecked(duration * (duration + 1) << 1) / 2;
        bool immediate = (instruction & 0x200) != 0;
        uint target = 0;
        if (immediate)
        {
            frame = NativeBattleScriptedCommandReads.Advance(frame, 4);
        }
        else
        {
            short selector = BinaryPrimitives.ReadInt16LittleEndian(NativeBattleScriptedCommandReads.Program(frame, program, 4, 2));
            target = unchecked(resolveSlot(frame, selector) + 0x50);
            frame = NativeBattleScriptedCommandReads.Advance(frame, 6);
        }
        for (int lane = 0; lane < 6; lane++)
        {
            if ((instruction & (0x8000 >> lane)) == 0)
            {
                continue;
            }
            int velocity = SlotDword(0x68 + lane * 4);
            int acceleration;
            if (immediate)
            {
                int endpoint = BinaryPrimitives.ReadInt16LittleEndian(NativeBattleScriptedCommandReads.Program(frame, program, 0, 2)) << 16;
                acceleration = unchecked(endpoint - unchecked(velocity * duration)) / divisor;
                acceleration >>= 7;
                frame = NativeBattleScriptedCommandReads.Advance(frame, 2);
            }
            else
            {
                int endpoint = BinaryPrimitives.ReadInt32LittleEndian(frame.Arena.Read(ArenaOffset(unchecked(target + (uint)(lane * 4)), 4), 4)) >> 4;
                int current = SlotDword(0x50 + lane * 4) >> 4;
                int travel = unchecked((velocity >> 4) * duration);
                acceleration = unchecked(unchecked(endpoint - travel) - current) / divisor;
                acceleration >>= 3;
            }
            frame = WriteSlotWord(frame, 0x80 + lane * 2, unchecked((ushort)(acceleration >> 4)));
        }
        return frame with { Arena = NativeBattleWaterSlots.RefreshAccelerationAA27F0(frame.Arena, bank, NativeBattleWaterInstructionDispatcher.CurrentIndex(frame, bank)) };

        int SlotDword(int offset) => BinaryPrimitives.ReadInt32LittleEndian(ReadSlot(frame, offset, 4));
    }
}
