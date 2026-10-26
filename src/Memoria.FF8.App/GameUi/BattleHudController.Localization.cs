using Memoria.FF8.Runtime;

namespace Memoria.FF8.GameUi;

public sealed partial class BattleHudController
{
    private Func<BattleActionDefinition, string>? localizedActionName;
    private Func<BattleCombatant, string>? localizedCombatantName;
    private Func<BattleAbility, BattleCombatant?, string, string>? localizedCommandName;
    private Func<BattleEvent, string>? localizedBattleMessage;

    internal void ConfigureLocalization(LocalizationBattlePresentation presentation)
    {
        localizedActionName = presentation.ActionName;
        localizedCombatantName = presentation.CombatantName;
        localizedCommandName = presentation.CommandName;
        localizedBattleMessage = presentation.Message;
    }

    internal string CombatantDisplayName(BattleCombatant actor) => localizedCombatantName?.Invoke(actor) ?? actor.Name;
}
