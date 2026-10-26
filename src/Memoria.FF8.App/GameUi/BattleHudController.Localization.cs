using Memoria.FF8.Runtime;

namespace Memoria.FF8.GameUi;

public sealed partial class BattleHudController
{
    private Func<BattleActionDefinition, string>? localizedActionName;
    private Func<BattleCombatant, string>? localizedCombatantName;
    private Func<BattleAbility, BattleCombatant?, string, string>? localizedCommandName;
    private Func<BattleEvent, string>? localizedBattleMessage;
    private Func<string, string, string>? localizeSystemText;
    private Func<int, string, string>? localizeGlobalMessage;

    internal string SystemText(string id, string original) => localizeSystemText?.Invoke(id, original) ?? original;

    internal string FormatSystemText(string id, string original, params object?[] arguments) => Memoria.FF8.Resources.Localization.SystemTextCatalog.Format(SystemText(id, original), original, arguments);

    internal void ConfigureLocalization(LocalizationBattlePresentation presentation)
    {
        localizedActionName = presentation.ActionName;
        localizedCombatantName = presentation.CombatantName;
        localizedCommandName = presentation.CommandName;
        localizedBattleMessage = presentation.Message;
        localizeSystemText = presentation.SystemText;
        localizeGlobalMessage = presentation.GlobalMessage;
    }

    internal string CombatantDisplayName(BattleCombatant actor) => localizedCombatantName?.Invoke(actor) ?? actor.Name;
}
