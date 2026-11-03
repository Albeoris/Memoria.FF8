using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    public BattleNativeExperienceReceipt CaptureNativeExperienceResult()
    {
        if (NativeGraphics is null || !NativeExperienceDistributed || NativeExperienceBanks is not { } banks || Outcome is not (BattleOutcome.Victory or BattleOutcome.Escaped))
        {
            throw new NotSupportedException("Native battle results require their executed experience/AP bank distributor; migrate the named historical snapshot explicitly.");
        }
        byte[] rows = Enumerable.Repeat((byte)255, 3).ToArray();
        foreach (BattleCombatant player in combatants.Where(actor => actor.IsPlayer))
        {
            if (player.NativeIndex is < 0 or >= 3)
            {
                throw new InvalidDataException("Native result recipients require their physical party slots.");
            }
            rows[player.NativeIndex] = checked((byte)(player.MagicRecordId ?? throw new NotSupportedException("Native result banks require the saved character identity for each physical party slot.")));
        }
        return new(banks, rows.ToImmutableArray());
    }
}
