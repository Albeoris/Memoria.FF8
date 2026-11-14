namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    public BattleEnemyRemainingExperience[] CaptureEscapeRemainingExperience()
    {
        if (Outcome != BattleOutcome.Escaped)
        {
            throw new InvalidOperationException("Remaining enemy experience requires a completed escape.");
        }
        return CurrentEscapeEnemies.Where(actor => (NativeSavedStatusBits(actor) & 1) == 0)
            .Select(actor => new BattleEnemyRemainingExperience(actor.SourceDefinition.RewardSlot ?? throw new InvalidDataException("Escape experience requires the current enemy formation slot."), checked((byte)actor.Level), actor.MaximumHp, actor.CurrentHp, UsesExpandedRoster ? null : actor.EncodeNativeSlot()))
            .OrderBy(receipt => receipt.NativeIndex ?? receipt.Slot).ToArray();
    }
}
