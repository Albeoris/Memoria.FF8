using Memoria.FF8.Ui;

namespace Memoria.FF8.GameUi;

public sealed partial class MenuController
{
    private int _magazineIndex;

    private void RegisterMagazineScreens()
    {
        Register(new MenuScreenDefinition
        {
            Id = "magazine",
            Title = "Magazine",
            Help = "Turn the page or return to the inventory.",
            Entries = c => [new("previous", "Previous page", "Turn to the previous original page.", Action: new MenuAction("magazine-page", Value: "-1"), Enabled: c.Item?.NativeMagazinePages.Length > 1), new("next", "Next page", "Turn to the next original page.", Action: new MenuAction("magazine-page", Value: "1"), Enabled: c.Item?.NativeMagazinePages.Length > 1), new("close", "Close", "Return to the inventory.", Action: new MenuAction("menu-back"))],
            BuildContent = (c, panel) => panel.Children.Add(c.BuildMagazinePage())
        });
    }

    private void ShowMagazinePage(int index)
    {
        var pages = Item?.NativeMagazinePages ?? [];
        if (pages.Length == 0 || _liveAction is null)
        {
            throw new InvalidOperationException("Magazine viewer requires its owned original pages and live action consumer.");
        }
        int selected = (index % pages.Length + pages.Length) % pages.Length;
        _liveAction(new MenuAction("magazine-read", Item!.Id, Value: pages[selected].PageId.ToString(System.Globalization.CultureInfo.InvariantCulture)));
        _magazineIndex = selected;
        _liveData = null;
        _view = null;
    }

    private UiElement BuildMagazinePage()
    {
        var pages = Item?.NativeMagazinePages ?? [];
        var page = Grid([1, 7, 1], [1], 8);
        Place(page, Panel(Item?.Name ?? "Magazine", Label($"Page {_magazineIndex + 1}/{pages.Length}", 24)), 0);
        string[] text = pages.Length == 0 ? [] : pages[Math.Clamp(_magazineIndex, 0, pages.Length - 1)].Text.SelectMany(line => line.Split('\n')).ToArray();
        Place(page, Panel(null, Lines(text, 21)), 1);
        Place(page, Commands(38, 22), 2);
        return page;
    }
}
