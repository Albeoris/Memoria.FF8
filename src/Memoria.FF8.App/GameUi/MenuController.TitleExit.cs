using Memoria.FF8.Ui;

namespace Memoria.FF8.GameUi;

public sealed partial class MenuController
{
    private Action? exitGame;

    public void ShowTitleExitConfirmation(Action exit)
    {
        Register(new MenuScreenDefinition
        {
            Id = "title-exit",
            Title = "Do you wish to exit the game?",
            Help = "Select Yes to exit or No to return to the title screen.",
            Entries = _ =>
            [
                new MenuEntry("no", "No", "Return to the title screen.", Action: new MenuAction("menu-back")),
                new MenuEntry("yes", "Yes", "Exit the game.", Action: new MenuAction("title-exit-confirm"))
            ],
            BuildContent = (menus, root) =>
            {
                var anchor = new UiAnchorPanel();
                var panel = menus.Panel("Do you wish to exit the game?", menus.Commands(40, 24));
                panel.Anchors = new UiAnchors(0.18f, 0.35f, 0.82f, 0.65f);
                anchor.Children.Add(panel);
                root.Children.Add(anchor);
            }
        });
        Open("title-exit");
        exitGame = exit;
        Select(0, Entries.Count);
    }

    private bool ExecuteTitleExitAction(MenuAction action)
    {
        if (action.Kind != "title-exit-confirm")
        {
            return false;
        }
        if (IsOpen && ScreenId == "title-exit" && exitGame is { } exit)
        {
            Close();
            exit();
        }
        return true;
    }
}
