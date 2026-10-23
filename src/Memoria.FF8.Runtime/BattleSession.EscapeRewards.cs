namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    public BattleEnemyRemainingExperience[] CaptureEscapeRemainingExperience()
    {
        if (Outcome != BattleOutcome.Escaped)
        {
            throw new InvalidOperationException("Remaining enemy experience requires a completed escape.");
        }
        return combatants.Where(actor => !actor.IsPlayer && actor.NativeIndex is >= 3 and <= 6 && (NativeSavedStatusBits(actor) & 1) == 0).OrderBy(actor => actor.NativeIndex).Select(actor => new BattleEnemyRemainingExperience(actor.SourceDefinition.RewardSlot ?? throw new InvalidDataException("Escape experience requires the current enemy formation slot."), checked((byte)actor.Level), actor.MaximumHp, actor.CurrentHp, actor.NativeIndex)).ToArray();
    }
}
