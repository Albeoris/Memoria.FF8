using Memoria.FF8.Assets;

namespace Memoria.FF8.BattleEffects;

public static partial class ScenarioDiscovery
{
    public static bool TryValidateFreshEnemyEntry(BattleEncounter formation, IEnumerable<EnemyScenarioScriptedAction> scripted, out string? blocker, IReadOnlyList<EnemyScenarioStartup>? startups = null)
    {
        ArgumentNullException.ThrowIfNull(formation);
        ArgumentNullException.ThrowIfNull(scripted);
        blocker = null;
        if (startups is null || formation.Slots.Where(slot => slot.Enabled && !slot.InitiallySuspended).Any(slot => !startups.Any(startup => startup.ModelId == slot.ModelId && startup.AllocationAbsenceProved)))
        {
            // Another initializer may allocate the target, or its allocation behavior is unknown.
            return true;
        }
        foreach (EnemyScenarioScriptedAction action in scripted)
        {
            if (!action.UnconditionalStartupPrefix || action.Callback != 0 || action.Command is not (2 or 8) || action.ActionId is not > 0 || action.TargetSelector is not (>= 16 and < 200))
            {
                continue;
            }
            // Initialization is queued for allocated, nonsuspended actors, including hidden actors.
            // Fixed enemy selectors scan allocated actors by their native model identity.
            if (!formation.Slots.Any(slot => slot.Enabled && !slot.InitiallySuspended && slot.ModelId == action.ModelId) || formation.Slots.Any(slot => slot.Enabled && slot.NativeModelId == action.TargetSelector.Value))
            {
                continue;
            }
            blocker = $"ENEMY_FRESH_ENTRY_DEPENDENCY: formation {formation.Id} initializes model {action.ModelId} callback0 offset {action.Offset}, command {action.Command}, action {action.ActionId} with fixed native model selector {action.TargetSelector.Value}, but no enabled original actor has that identity. Preserve the original preceding phase allocation/activation; this phase record is not a cold fresh-start carrier.";
            return false;
        }
        return true;
    }
}
