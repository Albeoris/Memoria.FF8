using Memoria.FF8.Runtime;
using Memoria.FF8.Ui;

namespace Memoria.FF8.App.Tests;

public sealed class QuickSaveMenuTests : IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), "Memoria-quick-menu-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public void PickerShowsOnlyQuickSavesNewestFirstAndReopensAtNewest()
    {
        PlayerSaveStore store = new(Path.Combine(directory, "checkpoint.json"), directory);
        GameSession session = new(new GameState { Scene = SceneKind.Field });
        store.Save(1, session);
        for (int index = 0; index < 9; index++)
        {
            session.SetPosition(index, 0);
            store.Save(0, session);
            File.SetLastWriteTimeUtc(store.GetPath(0), DateTime.UtcNow.AddMinutes(index - 10));
        }
        PlayerSaveMenu menu = new(store);
        menu.Open(PlayerSaveMenuMode.QuickLoad);
        Assert.Equal(9, menu.Entries.Count);
        Assert.All(menu.Entries, entry => Assert.True(entry.Slot <= 0));
        Assert.Equal(menu.Entries.OrderByDescending(entry => entry.SavedAt), menu.Entries);
        Assert.Equal(8, store.Load(menu.SelectedSlot).X);
        Assert.True(menu.IsQuick);
        Assert.False(menu.IsSaving);
        Assert.Equal(2, menu.PageCount);
        Assert.Equal(5, menu.VisibleEntries.Count);
        for (int index = 0; index < 5; index++)
        {
            menu.Update(new InputFrame(0, 0, false, false, false, false, MenuVertical: 1), _ => Assert.Fail(), _ => Assert.Fail());
        }
        Assert.Equal(1, menu.Page);
        Assert.Equal(4, menu.VisibleEntries.Count);
        Assert.Equal(3, store.Load(menu.SelectedSlot).X);
        int selected = menu.SelectedSlot;
        int? loaded = null;
        menu.Update(new InputFrame(0, 0, true, false, false, false), _ => Assert.Fail(), slot => loaded = slot);
        Assert.Equal(selected, loaded);
        Assert.False(menu.IsOpen);
        menu.Open(PlayerSaveMenuMode.QuickLoad);
        Assert.Equal(0, menu.Selection);
        Assert.Equal(0, menu.Page);
        Assert.Equal(8, store.Load(menu.SelectedSlot).X);
    }

    [Fact]
    public void EmptyPickerCanNavigateRejectConfirmAndCancel()
    {
        PlayerSaveStore store = new(Path.Combine(directory, "checkpoint.json"), directory);
        PlayerSaveMenu menu = new(store);
        menu.Open(PlayerSaveMenuMode.QuickLoad);
        Assert.Empty(menu.Entries);
        Assert.Equal((0, 0, 1), (menu.Selection, menu.Page, menu.PageCount));
        Assert.Empty(menu.VisibleEntries);
        menu.Update(new InputFrame(0, 0, false, false, false, false, MenuVertical: 1, MenuPage: 1), _ => Assert.Fail(), _ => Assert.Fail());
        menu.Update(new InputFrame(0, 0, true, false, false, false), _ => Assert.Fail(), _ => Assert.Fail());
        Assert.True(menu.IsOpen);
        Assert.Equal("No quick saves.", menu.Message);
        Assert.Equal((0, 0), (menu.Selection, menu.Page));
        menu.Update(new InputFrame(0, 0, false, true, false, false), _ => Assert.Fail(), _ => Assert.Fail());
        Assert.False(menu.IsOpen);
    }

    [Fact]
    public void QuickSaveUsesTheSharedControlWithoutAnOverwriteOrTransferDestination()
    {
        PlayerSaveStore store = new(Path.Combine(directory, "checkpoint.json"), directory);
        store.Save(0, new GameSession(new GameState { Scene = SceneKind.Field, X = 10 }));
        PlayerSaveMenu menu = new(store);
        menu.Open(PlayerSaveMenuMode.QuickSave);
        Assert.True(menu.IsQuick);
        Assert.True(menu.IsSaving);
        Assert.Equal((0, 0, 1), (menu.Bank, menu.Page, menu.PageCount));
        Assert.Equal(0, Assert.Single(menu.Entries).Slot);
        Assert.Single(menu.VisibleEntries);
        Assert.False(menu.SelectBank(1));
        menu.RequestTransfer();
        Assert.False(menu.IsTransferring);
        int? saved = null;
        menu.Update(new InputFrame(0, 0, true, false, false, false), slot => saved = slot, _ => Assert.Fail());
        Assert.Equal(0, saved);
        Assert.False(menu.ConfirmOverwrite);
        Assert.False(menu.IsOpen);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void QuickViewsShowOnlyMemoriaAndExposeOnlyAvailableActions(bool saving)
    {
        PlayerSaveStore store = new(Path.Combine(directory, "checkpoint.json"), directory);
        PlayerSaveMenu menu = new(store);
        PlayerSaveMenuMode mode = saving ? PlayerSaveMenuMode.QuickSave : PlayerSaveMenuMode.QuickLoad;
        menu.Open(mode);
        UiView view = PlayerSaveView.Build(menu, "Transfer", "Navigate");
        Assert.IsType<PlayerSaveView>(view.Root);
        UiButton[] buttons = PlayerSaveView.FindButtons(view.Root).ToArray();
        Assert.Equal("save-bank-0", Assert.Single(buttons, button => button.Id!.StartsWith("save-bank-", StringComparison.Ordinal)).Id);
        Assert.DoesNotContain(buttons, button => button.Id == "save-transfer");
        Assert.False(Assert.Single(buttons, button => button.Id == "save-previous").Enabled);
        Assert.False(Assert.Single(buttons, button => button.Id == "save-next").Enabled);
        UiButton confirm = Assert.Single(buttons, button => button.Id == "save-confirm");
        Assert.Equal(mode == PlayerSaveMenuMode.QuickSave, confirm.Enabled);
        view.Pointer(confirm.Bounds.X + confirm.Bounds.Width / 2, confirm.Bounds.Y + confirm.Bounds.Height / 2, true);
        int? saved = null;
        menu.Update(default, slot => saved = slot, _ => Assert.Fail("An empty quick-load view cannot load."));
        if (mode == PlayerSaveMenuMode.QuickSave)
        {
            Assert.Equal(0, saved);
            Assert.False(menu.IsOpen);
        }
        else
        {
            Assert.Null(saved);
            Assert.True(menu.IsOpen);
            UiButton back = Assert.Single(buttons, button => button.Id == "save-back");
            view.Pointer(back.Bounds.X + back.Bounds.Width / 2, back.Bounds.Y + back.Bounds.Height / 2, false);
            view.Pointer(back.Bounds.X + back.Bounds.Width / 2, back.Bounds.Y + back.Bounds.Height / 2, true);
            menu.Update(default, _ => Assert.Fail(), _ => Assert.Fail());
            Assert.False(menu.IsOpen);
        }
    }

    public void Dispose()
    {
        if (Directory.Exists(directory))
        {
            Directory.Delete(directory, true);
        }
    }
}
