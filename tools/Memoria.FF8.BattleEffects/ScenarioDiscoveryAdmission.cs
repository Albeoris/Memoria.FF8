using Memoria.FF8.Runtime;

namespace Memoria.FF8.BattleEffects;

public static partial class ScenarioDiscovery
{
    public static bool SupportsOrdinaryStockMagicRecipe(BattleNativeAction action)
    {
        return action is { Command: 2, KernelSection: 1, ActionId: >= 1 and <= 63, MagicId: >= 1 and <= 400 } &&
            ExpandedBattleActionRules.SupportsPlayerAction(BattleAbility.Magic, action);
    }

    public static string[] UnsupportedStockMagicCaseIds(InstalledScenarioInventory catalog)
    {
        return catalog.Actions.Where(action => action.Native is { Command: 2, KernelSection: 1 } && !SupportsOrdinaryStockMagicRecipe(action.Native))
            .Select(action => "action-" + action.Id).Order(StringComparer.Ordinal).ToArray();
    }
}
