namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    public BattleActionDefinition? GetActionDefinition(BattleAbility ability, int id)
    {
        foreach (BattleActionDefinition action in definition.ActionDefinitions)
        {
            if (action.Ability == ability && action.Id == id)
            {
                return action with { Statuses = action.Statuses?.ToArray() };
            }
        }
        return null;
    }
}
