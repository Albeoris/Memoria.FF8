using Memoria.FF8.Runtime;

namespace Memoria.FF8.GameUi;

public static class BattleDebugDefaults
{
    public static BattleDefinition Create(IReadOnlyList<BattleCombatantDefinition> currentParty, BattleCombatantDefinition enemy, int stageId, BattleSession? currentBattle = null, IReadOnlyList<BattleActionDefinition>? actionDefinitions = null, IReadOnlyList<BattleStock>? items = null)
    {
        enemy = enemy with { Id = "enemy-0", IsPlayer = false, CurrentHp = enemy.MaximumHp };
        return BattleDebugController.InitializeParty(new BattleDefinition { StageId = stageId, Combatants = [enemy], ActionDefinitions = (actionDefinitions ?? currentBattle?.ActionDefinitions ?? []).ToArray(), Items = (currentBattle?.Items ?? items ?? []).ToArray() }, currentParty, currentBattle);
    }
}
