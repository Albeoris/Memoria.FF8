using static Memoria.FF8.Runtime.NativeBattleWaterExecutionFields;

namespace Memoria.FF8.Runtime;

/// <summary>B2ABE0's slot traversal invokes the selected lighting kernel before each selected draw kernel.</summary>
public static class NativeBattleIfritDraw
{
    public static NativeBattleWaterExecutionFrame RunB2ABE0(NativeBattleWaterExecutionFrame frame, Func<NativeBattleWaterExecutionFrame, sbyte, NativeBattleWaterExecutionFrame> prepareLighting, Func<NativeBattleWaterExecutionFrame, byte, NativeBattleWaterExecutionFrame> drawSlot, int traversalBudget = 256)
    {
        ArgumentNullException.ThrowIfNull(prepareLighting);
        return NativeBattleWaterCallback.RunDrawSlots(frame, current =>
        {
            sbyte light = unchecked((sbyte)ReadSlot(current, 0xE1, 1)[0]);
            return light == 0 ? current : prepareLighting(current, light);
        }, drawSlot, traversalBudget);
    }
}
