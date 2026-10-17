using Memoria.FF8.Runtime;

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
        Assert.Null(menu.QuickEntry);
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
        menu.Update(new InputFrame(0, 0, false, false, false, false, MenuVertical: 1, MenuPage: 1), _ => Assert.Fail(), _ => Assert.Fail());
        menu.Update(new InputFrame(0, 0, true, false, false, false), _ => Assert.Fail(), _ => Assert.Fail());
        Assert.True(menu.IsOpen);
        Assert.Equal("No quick saves.", menu.Message);
        menu.Update(new InputFrame(0, 0, false, true, false, false), _ => Assert.Fail(), _ => Assert.Fail());
        Assert.False(menu.IsOpen);
    }

    public void Dispose()
    {
        if (Directory.Exists(directory))
        {
            Directory.Delete(directory, true);
        }
    }
}
