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
            Help = "Use the arrows to turn pages or Cancel to return to the inventory.",
            BuildContent = (c, panel) => panel.Children.Add(c.BuildMagazinePage())
        });
    }

    private bool TryNavigateMagazine(MenuInput input)
    {
        if (ScreenId != "magazine")
        {
            return false;
        }
        if (input == MenuInput.Cancel)
        {
            Back();
            return true;
        }
        int delta = input switch
        {
            MenuInput.Left or MenuInput.Up or MenuInput.PageLeft or MenuInput.PageUp => -1,
            MenuInput.Right or MenuInput.Down or MenuInput.PageRight or MenuInput.PageDown => 1,
            _ => 0
        };
        if (delta != 0 && Item?.NativeMagazinePages.Length > 1)
        {
            int previous = _magazineIndex;
            ShowMagazinePage(_magazineIndex + delta);
            if (_magazineIndex != previous)
            {
                SoundRequested?.Invoke(MenuSound.Move);
            }
        }
        return true;
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
            return SystemText("ui.magazinepagereason.details.magazine.page.metadata.is.unavailable", "Magazine page metadata is unavailable.");
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
        string? fontFamily = _readPreferences?.Invoke().UiFontFamily;
        return new MagazinePageElement(layout, fontFamily) { Id = "magazine-page" };
    }
}
