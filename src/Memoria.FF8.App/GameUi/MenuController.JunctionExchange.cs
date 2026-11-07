namespace Memoria.FF8.GameUi;

public sealed partial class MenuController
{
    private MenuAction? pendingJunctionExchange;
    private bool junctionExchangeConfirmed;

    private bool ExecuteJunctionExchangeConfirmation(MenuAction action)
    {
        if (action.Kind == "switch-junction-confirm")
        {
            MenuAction? exchange = pendingJunctionExchange;
            if (ScreenId != "switch-junction-confirm" || exchange is null)
            {
                return true;
            }
            Back();
            _liveData = null;
            if (exchange.SubjectId != ExchangeId || !Data.Characters.Any(character => character.Id == exchange.SubjectId) || !Data.Characters.Any(character => character.Id == exchange.TargetId))
            {
                return true;
            }
            junctionExchangeConfirmed = true;
            try
            {
                Execute(exchange);
            }
            finally
            {
                junctionExchangeConfirmed = false;
            }
            return true;
        }
        if (action.Kind != "junction-exchange" || IsDemo || junctionExchangeConfirmed || !Data.SupportedActions.Contains(action.Kind) || Data.JunctionExchangeLowersHp?.Invoke(ExchangeId, action.TargetId) != true)
        {
            return false;
        }
        pendingJunctionExchange = action with { SubjectId = ExchangeId };
        Register(new MenuScreenDefinition
        {
            Id = "switch-junction-confirm",
            Title = "Junction Exchange",
            Help = "Current HP will be reduced to the new maximum. Exchange junctions?",
            HelpText = c => c.SystemText("junction.exchange.hp-warning", "Current HP will be reduced to the new maximum. Exchange junctions?"),
            Entries = c =>
            [
                new MenuEntry("no", c.SystemText("junction.exchange.no", "No"), c.SystemText("junction.exchange.keep", "Keep the current junctions."), Action: new MenuAction("menu-back")),
                new MenuEntry("yes", c.SystemText("junction.exchange.yes", "Yes"), c.SystemText("junction.exchange.accept", "Exchange junctions and apply the new HP maximum."), Action: new MenuAction("switch-junction-confirm"))
            ],
            BuildContent = (menus, root) => root.Children.Add(menus.BuildJunctionExchangePrompt(menus.SystemText("junction.exchange.hp-warning", "Current HP will be reduced to the new maximum. Exchange junctions?")))
        });
        Push("switch-junction-confirm");
        Select(0, Entries.Count);
        return true;
    }
}
