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
            Entries = c => [new("previous", "Previous page", c.MagazinePageReason(c._magazineIndex - 1) ?? "Turn to the previous original page.", Action: new MenuAction("magazine-page", Value: "-1"), Enabled: c.Item?.NativeMagazinePages.Length > 1 && c.MagazinePageReason(c._magazineIndex - 1) is null), new("next", "Next page", c.MagazinePageReason(c._magazineIndex + 1) ?? "Turn to the next original page.", Action: new MenuAction("magazine-page", Value: "1"), Enabled: c.Item?.NativeMagazinePages.Length > 1 && c.MagazinePageReason(c._magazineIndex + 1) is null), new("close", "Close", "Return to the inventory.", Action: new MenuAction("menu-back"))],
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
        if (MagazinePageReason(selected) is string reason)
        {
            Message = reason;
            return;
        }
        _liveAction(new MenuAction("magazine-read", Item!.Id, Value: pages[selected].PageId.ToString(System.Globalization.CultureInfo.InvariantCulture)));
        _magazineIndex = selected;
        _liveData = null;
        _view = null;
    }

    private string? MagazinePageReason(int index)
    {
        var pages = Item?.NativeMagazinePages ?? [];
        if (pages.Length == 0)
        {
            return "Magazine page metadata is unavailable.";
        }
        int selected = (index % pages.Length + pages.Length) % pages.Length;
        return Item!.NativeMagazinePageUnavailableReasons.GetValueOrDefault(pages[selected].PageId);
    }

    private UiElement BuildMagazinePage()
    {
        var pages = Item?.NativeMagazinePages ?? [];
        if (pages.Length == 0)
        {
            throw new NotSupportedException("Magazine viewing requires original page layout metadata.");
        }
        var selected = pages[Math.Clamp(_magazineIndex, 0, pages.Length - 1)];
        var layout = selected.Layout ?? throw new NotSupportedException("Magazine page lacks its original layout and artwork descriptors.");
        var page = new UiGrid { Columns = 1, Gap = 4 };
        page.RowDefinitions.Add(UiGridLength.Star());
        page.RowDefinitions.Add(UiGridLength.Pixel(34));
        string? fontFamily = _readPreferences?.Invoke().UiFontFamily;
        Place(page, new MagazinePageElement(layout, fontFamily) { Id = "magazine-page" }, 0);
        var footer = Grid([1], [1, 3], 0);
        Place(footer, Label($"Page {_magazineIndex + 1}/{pages.Length}", 16), 0);
        UiList commands = Commands(30, 16);
        commands.Orientation = UiOrientation.Horizontal;
        commands.PointerSize = 14;
        commands.TextInset = 24;
        Place(footer, commands, 0, 1);
        Place(page, footer, 1);
        return page;
    }
}
