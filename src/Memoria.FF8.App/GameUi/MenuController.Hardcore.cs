using Memoria.FF8.Runtime;
using Memoria.FF8.Ui;

namespace Memoria.FF8.GameUi;

public sealed partial class MenuController
{
    private bool ExecuteHardcoreConfirmation(ref MenuAction action)
    {
        if (action.Kind == "hardcore-confirm")
        {
            if (ScreenId != "hardcore-confirm")
            {
                return true;
            }
            Back();
            _liveData = null;
            if (!IsConfigSettingsScreen || !ConfigEntries().Any(entry => entry.Id == "Difficulty" && entry.Enabled))
            {
                return true;
            }
            action = new MenuAction("config", "Difficulty", Value: "Hardcore");
            return false;
        }
        if (action.Kind != "config" || action.SubjectId != "Difficulty" || action.Value != "Hardcore" || ConfigValue("Difficulty") == "Hardcore")
        {
            return false;
        }
        string Warning() => SystemText("ui.executehardcoreconfirmation.warning.are.you.sure.this.change.is.permanent.hardcore", "Are you sure? This change is permanent. Hardcore cannot be disabled, and these rules will be enabled and locked On:");
        string[] Rules() => GameplayPreferences.NewGameOptions.Select(option => LocalizeSettingLabel(option.Label, MagicUiText.SettingLabel(option.Label))).ToArray();
        Register(new MenuScreenDefinition
        {
            Id = "hardcore-confirm",
            Title = "Enable Hardcore?",
            Help = Warning(),
            HelpText = _ => Warning(),
            Entries = _ =>
            [
                new MenuEntry("no", SystemText("ui.executehardcoreconfirmation.entry.label.no", "No"), SystemText("ui.executehardcoreconfirmation.entry.help.keep.the.current.difficulty.and.rules", "Keep the current difficulty and rules."), Action: new MenuAction("menu-back")),
                new MenuEntry("yes", SystemText("ui.executehardcoreconfirmation.entry.label.yes.enable.hardcore", "Yes, enable Hardcore"), Warning(), Action: new MenuAction("hardcore-confirm"))
            ],
            Details = _ => [Warning(), .. Rules()],
            BuildContent = (menus, root) =>
            {
                var anchor = new UiAnchorPanel();
                var content = Grid([3, 5, 3], [1], 8);
                UiLabel prompt = menus.Label(Warning(), 20);
                prompt.TextFlow = UiTextFlow.Wrap;
                Place(content, prompt, 0);
                int rowCount = (Rules().Length + 1) / 2;
                var options = Grid(Enumerable.Repeat(1f, rowCount).ToArray(), [1, 1], 6);
                for (int index = 0; index < Rules().Length; index++)
                {
                    Place(options, menus.Label(Rules()[index], 15, UiTextFlow.Wrap), index % rowCount, index / rowCount);
                }
                Place(content, options, 1);
                Place(content, menus.Commands(30, 22), 2);
                UiPanel panel = menus.Panel(SystemText("ui.executehardcoreconfirmation.panel.enable.hardcore", "Enable Hardcore?"), content);
                panel.Anchors = new UiAnchors(0.06f, 0.04f, 0.94f, 0.96f);
                anchor.Children.Add(panel);
                root.Children.Add(anchor);
            }
        });
        Push("hardcore-confirm");
        Select(0, Entries.Count);
        return true;
    }
}
