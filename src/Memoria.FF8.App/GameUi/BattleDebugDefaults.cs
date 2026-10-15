using Memoria.FF8.Runtime;

namespace Memoria.FF8.GameUi;

public static class BattleDebugDefaults
{
    /// <summary>The procedural practice target has no original FF8 model identity.</summary>
    public static BattleCombatantDefinition Dummy => new() { Id = "enemy-0", Name = "Dummy", ModelId = -1, MaximumHp = 1000, CurrentHp = 1000, Speed = 50, Attack = 10, Defense = 5 };

    public static BattleDefinition Create(IReadOnlyList<BattleCombatantDefinition> currentParty, BattleCombatantDefinition? lastEnemy, int stageId, BattleSession? currentBattle = null, IReadOnlyList<BattleActionDefinition>? actionDefinitions = null, IReadOnlyList<BattleStock>? items = null)
    {
        BattleCombatantDefinition enemy = lastEnemy is null ? Dummy : lastEnemy with { Id = "enemy-0", IsPlayer = false, CurrentHp = lastEnemy.MaximumHp };
        return BattleDebugController.InitializeParty(new BattleDefinition { StageId = stageId, Combatants = [enemy], ActionDefinitions = (actionDefinitions ?? currentBattle?.ActionDefinitions ?? []).ToArray(), Items = (currentBattle?.Items ?? items ?? []).ToArray() }, currentParty, currentBattle);
    }
}
