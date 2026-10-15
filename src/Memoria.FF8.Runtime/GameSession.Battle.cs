namespace Memoria.FF8.Runtime;

public sealed partial class GameSession
{
    public void SetRandomEncounters(RandomEncounterSessionState state)
    {
        state.Validate();
        State = State with { RandomEncounters = state };
    }
    public void SetFullBattlePlaythrough(bool enabled)
    {
        if (State.Battle is not null)
        {
            throw new InvalidOperationException("Battle policy cannot change during combat.");
        }
        State = State with { FullBattlePlaythrough = enabled ? true : null };
    }

    public void SetBattle(BattleSnapshot? snapshot)
    {
        BattleSnapshot? owned = snapshot is null ? null : BattleSession.Restore(snapshot).Capture();
        State = State with { Battle = owned };
    }
}
