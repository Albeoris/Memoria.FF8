namespace Memoria.FF8.Runtime;

/// <summary>B2ABE0's slot traversal invokes the selected lighting kernel before each selected draw kernel.</summary>
public static class NativeBattleIfritDraw
{
    public static NativeBattleWaterExecutionFrame RunB2ABE0(NativeBattleWaterExecutionFrame frame, Func<NativeBattleWaterExecutionFrame, sbyte, NativeBattleWaterExecutionFrame> prepareLighting, Func<NativeBattleWaterExecutionFrame, byte, NativeBattleWaterExecutionFrame> drawSlot, int traversalBudget = 256)
    {
        return NativeBattleCinematicDraw.Run(NativeBattleCinematicProfile.ForEffect(201), frame, prepareLighting, drawSlot, traversalBudget);
    }
}
