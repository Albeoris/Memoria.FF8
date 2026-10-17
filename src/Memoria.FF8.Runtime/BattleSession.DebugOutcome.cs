namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    public BattleOutcome? DebugOutcome { get; private set; }

    private void RestoreDebugOutcome(BattleSnapshot snapshot)
    {
        if (snapshot.DebugOutcome is not null and not (BattleOutcome.Victory or BattleOutcome.Defeat))
        {
            throw new InvalidDataException("An explicit debug outcome must select Victory or Defeat.");
        }
        if (snapshot.DebugOutcome is BattleOutcome requested && (snapshot.Outcome != requested || combatants.Any(actor => (requested == BattleOutcome.Defeat ? actor.IsPlayer : !actor.IsPlayer) && actor.CurrentHp != 0)))
        {
            throw new InvalidDataException("The saved debug outcome disagrees with its terminal HP and outcome owners.");
        }
        DebugOutcome = snapshot.DebugOutcome;
    }
}
