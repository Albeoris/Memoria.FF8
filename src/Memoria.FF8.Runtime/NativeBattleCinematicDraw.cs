using static Memoria.FF8.Runtime.NativeBattleWaterExecutionFields;

namespace Memoria.FF8.Runtime;

/// <summary>Selected lit or unlit cinematic traversal preserves current list and alternate packet cursor order.</summary>
public static class NativeBattleCinematicDraw
{
    public static NativeBattleWaterExecutionFrame Run(NativeBattleCinematicProfile profile, NativeBattleWaterExecutionFrame frame, Func<NativeBattleWaterExecutionFrame, sbyte, NativeBattleWaterExecutionFrame>? prepareLighting, Func<NativeBattleWaterExecutionFrame, byte, NativeBattleWaterExecutionFrame> drawSlot, int traversalBudget = 256)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(drawSlot);
        if (profile != NativeBattleCinematicProfile.ForEffect(profile.Effect))
        {
            throw new InvalidDataException("Native cinematic drawing requires its actual selected profile.");
        }
        if (!profile.Lit)
        {
            return NativeBattleWaterCallback.RunDrawSlots(frame, current => current, drawSlot, traversalBudget);
        }
        ArgumentNullException.ThrowIfNull(prepareLighting);
        return NativeBattleWaterCallback.RunDrawSlots(frame, current =>
        {
            sbyte light = unchecked((sbyte)ReadSlot(current, 0xE1, 1)[0]);
            return light == 0 ? current : prepareLighting(current, light);
        }, drawSlot, traversalBudget);
    }
}
