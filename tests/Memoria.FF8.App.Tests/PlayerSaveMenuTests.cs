using Memoria.FF8.GameUi;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class PlayerSaveMenuTests : IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), "Memoria.FF8-save-menu-sound-" + Guid.NewGuid().ToString("N"));
    private readonly List<MenuSound> sounds = [];

    [Fact]
    public void CursorSoundsRequireAChangedSlotPageOrBank()
    {
        PlayerSaveMenu menu = CreateMenu(out _);
        menu.Open(PlayerSaveMenuMode.Save);
        Navigate(menu, vertical: -1);
        Navigate(menu, page: -1);
        Navigate(menu, horizontal: -1);
        Assert.Empty(sounds);
        Navigate(menu, vertical: 1);
        Navigate(menu, page: 1);
        Navigate(menu, horizontal: 1);
        Assert.Equal(new[] { MenuSound.Move, MenuSound.Move, MenuSound.Move }, sounds);
        Navigate(menu, horizontal: 1);
        Assert.Equal(3, sounds.Count);
        Assert.Equal(37, menu.SelectedSlot);
        menu.Open(PlayerSaveMenuMode.Load);
        sounds.Clear();
        Navigate(menu, horizontal: 1);
        Assert.Equal(0, menu.SelectedSlot);
        Assert.Equal(1, menu.Bank);
        Assert.Equal(new[] { MenuSound.Move }, sounds);
    }

    [Fact]
    public void OverwritePromptAcceptanceAndBothCancellationPathsRequestTheirOwnSound()
    {
        PlayerSaveMenu menu = CreateMenu(out PlayerSaveStore store);
        store.Save(1, new GameSession(new GameState { Scene = SceneKind.Field }));
        menu.Open(PlayerSaveMenuMode.Save);
        int saves = 0;
        void Save(int slot)
        {
            Assert.Equal(1, slot);
            saves++;
        }
        menu.Update(Confirm(), Save, _ => Assert.Fail("Unexpected load."));
        Assert.True(menu.ConfirmOverwrite);
        menu.Update(Cancel(), Save, _ => Assert.Fail("Unexpected load."));
        Assert.True(menu.IsOpen);
        menu.Update(Cancel(), Save, _ => Assert.Fail("Unexpected load."));
        Assert.False(menu.IsOpen);
        Assert.Equal(new[] { MenuSound.Confirm, MenuSound.Cancel, MenuSound.Cancel }, sounds);
        menu.Open(PlayerSaveMenuMode.Save);
        sounds.Clear();
        menu.Update(Confirm(), Save, _ => Assert.Fail("Unexpected load."));
        menu.Update(Confirm(), Save, _ => Assert.Fail("Unexpected load."));
        Assert.Equal(1, saves);
        Assert.Equal(new[] { MenuSound.Confirm, MenuSound.Confirm }, sounds);
        Assert.False(menu.IsOpen);
    }

    [Fact]
    public void RejectedAndFailedLoadsUseCancelAndSuccessfulLoadUsesConfirm()
    {
        PlayerSaveMenu menu = CreateMenu(out PlayerSaveStore store);
        menu.Open(PlayerSaveMenuMode.Load);
        menu.Update(Confirm(), _ => Assert.Fail("Unexpected save."), _ => Assert.Fail("Empty slot was loaded."));
        Assert.Equal(new[] { MenuSound.Cancel }, sounds);
        store.Save(0, new GameSession(new GameState { Scene = SceneKind.Field }));
        menu.Open(PlayerSaveMenuMode.Load);
        menu.Update(Confirm(), _ => Assert.Fail("Unexpected save."), _ => throw new InvalidDataException("Unavailable test save."));
        Assert.True(menu.IsOpen);
        Assert.Equal(new[] { MenuSound.Cancel, MenuSound.Cancel }, sounds);
        int loaded = -1;
        menu.Update(Confirm(), _ => Assert.Fail("Unexpected save."), slot => loaded = slot);
        Assert.Equal(0, loaded);
        Assert.False(menu.IsOpen);
        Assert.Equal(new[] { MenuSound.Cancel, MenuSound.Cancel, MenuSound.Confirm }, sounds);
    }

    [Fact]
    public void QuickActionsUseConfirmationOnSuccessAndCancellationOnFailure()
    {
        PlayerSaveMenu menu = CreateMenu(out _);
        menu.Open(PlayerSaveMenuMode.Save);
        menu.Update(new InputFrame(0, 0, false, false, false, true), slot => Assert.Equal(0, slot), _ => Assert.Fail("Unexpected load."));
        Assert.Equal(new[] { MenuSound.Confirm }, sounds);
        menu.Update(new InputFrame(0, 0, false, false, false, false, QuickLoad: true), _ => Assert.Fail("Unexpected save."), _ => Assert.Fail("Quick load must open the picker."));
        Assert.True(menu.IsOpen);
        Assert.Equal(PlayerSaveMenuMode.QuickLoad, menu.Mode);
        Assert.Equal(new[] { MenuSound.Confirm, MenuSound.Confirm }, sounds);
        menu.Update(Confirm(), _ => Assert.Fail("Unexpected save."), _ => Assert.Fail("Empty picker must not load."));
        Assert.Equal(new[] { MenuSound.Confirm, MenuSound.Confirm, MenuSound.Cancel }, sounds);

    }

    private PlayerSaveMenu CreateMenu(out PlayerSaveStore store)
    {
        store = new PlayerSaveStore(Path.Combine(directory, "research.json"), Path.Combine(directory, "saves"));
        PlayerSaveMenu menu = new(store);
        menu.SoundRequested += sounds.Add;
        return menu;
    }

    private static InputFrame Confirm()
    {
        return new InputFrame(0, 0, true, false, false, false);
    }

    private static InputFrame Cancel()
    {
        return new InputFrame(0, 0, false, true, false, false);
    }

    private static void Navigate(PlayerSaveMenu menu, int horizontal = 0, int vertical = 0, int page = 0)
    {
        menu.Update(new InputFrame(0, 0, false, false, false, false, MenuHorizontal: horizontal, MenuVertical: vertical, MenuPage: page), _ => Assert.Fail("Unexpected save."), _ => Assert.Fail("Unexpected load."));
    }

    public void Dispose()
    {
        if (Directory.Exists(directory))
        {
            Directory.Delete(directory, true);
        }
    }
}
