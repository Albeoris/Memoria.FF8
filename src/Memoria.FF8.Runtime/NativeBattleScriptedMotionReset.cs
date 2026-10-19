using static Memoria.FF8.Runtime.NativeBattleWaterExecutionFields;

namespace Memoria.FF8.Runtime;

/// <summary>Reset the six current motion lanes, update the selected node and republish its cached coordinates.</summary>
public static class NativeBattleScriptedMotionReset
{
    public static NativeBattleWaterExecutionFrame Execute(NativeBattleWaterExecutionFrame frame, NativeBattleWaterSlotBank bank, Func<NativeBattleWaterExecutionFrame, byte, NativeBattleWaterExecutionFrame> update)
    {
        if ((frame.ReadTemporaryWord(0x14A) & 0x1FF) != 0x37)
        {
            throw new InvalidDataException("Native motion reset requires its selected instruction.");
        }
        frame = frame with { Arena = frame.Arena.Write(SlotOffset(frame, 0x50, 24), new byte[24]) };
        frame = update(frame, ReadSlot(frame, 0x18, 1)[0]);
        frame = frame with { Arena = NativeBattleWaterSlots.CachePositionAA17D0(frame.Arena, bank, NativeBattleWaterInstructionDispatcher.CurrentIndex(frame, bank)) };
        return NativeBattleScriptedCommandReads.Advance(frame, 2);
    }
}
