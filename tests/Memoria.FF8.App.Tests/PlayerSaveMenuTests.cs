using Memoria.FF8.GameUi;
using Memoria.FF8.Runtime;
using Memoria.FF8.Ui;

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
        Navigate(menu, horizontal: 1);
        Assert.Equal(4, sounds.Count);
        Assert.Equal(137, menu.SelectedSlot);
        menu.Open(PlayerSaveMenuMode.Load);
        sounds.Clear();
        Navigate(menu, horizontal: 1);
        Assert.Equal(101, menu.SelectedSlot);
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
        menu.SelectSlot(0);
        sounds.Clear();
        menu.Update(Confirm(), _ => Assert.Fail("Unexpected save."), _ => throw new InvalidDataException("Unavailable test save."));
        Assert.True(menu.IsOpen);
        Assert.Equal(new[] { MenuSound.Cancel }, sounds);
        int loaded = -1;
        menu.Update(Confirm(), _ => Assert.Fail("Unexpected save."), slot => loaded = slot);
        Assert.Equal(0, loaded);
        Assert.False(menu.IsOpen);
        Assert.Equal(new[] { MenuSound.Cancel, MenuSound.Confirm }, sounds);
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

    [Fact]
    public void BankCapacitiesAndCursorMemoriesRespectAllThreeBanks()
    {
        PlayerSaveMenu menu = CreateMenu(out _);
        menu.Open(PlayerSaveMenuMode.Save);
        Assert.Equal((100, 20), (menu.BankSlotCount, menu.PageCount));
        menu.SelectSlot(100);
        Assert.True(menu.SelectBank(1));
        Assert.Equal((30, 6, 130, 5), (menu.BankSlotCount, menu.PageCount, menu.SelectedSlot, menu.Page));
        menu.SelectSlot(107);
        menu.SelectBank(2);
        Assert.Equal(137, menu.SelectedSlot);
        menu.SelectSlot(152);
        menu.SelectBank(0);
        Assert.Equal(100, menu.SelectedSlot);
        menu.SelectBank(1);
        Assert.Equal(107, menu.SelectedSlot);
        menu.Open(PlayerSaveMenuMode.Load);
        Assert.Equal(1, menu.SelectedSlot);
        Assert.Equal(100, menu.Entries.Count);
        menu.SelectBank(1);
        Assert.All(menu.Entries, entry => Assert.True(entry.Slot > 100));
        Assert.Equal(101, menu.SelectedSlot);
        menu.Open(PlayerSaveMenuMode.Save);
        Assert.Equal((1, 107), (menu.Bank, menu.SelectedSlot));
    }

    [Fact]
    public void FirstVisitToShorterBankPreservesRowAndClampsPage()
    {
        PlayerSaveMenu menu = CreateMenu(out _);
        menu.Open(PlayerSaveMenuMode.Save);
        menu.SelectSlot(98);
        menu.SelectBank(1);
        Assert.Equal((5, 128), (menu.Page, menu.SelectedSlot));
        menu.SelectBank(2);
        Assert.Equal((5, 158), (menu.Page, menu.SelectedSlot));
        menu.SelectBank(0);
        Assert.Equal((19, 98), (menu.Page, menu.SelectedSlot));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CopyUsesSelectedSavedSnapshotAndRestoresSourceInEitherMenu(bool loading)
    {
        PlayerSaveMenu menu = CreateMenu(out PlayerSaveStore store, originalDirectory: true);
        store.Save(1, new GameSession(new GameState { Scene = SceneKind.Field, X = 10 }));
        byte[] source = File.ReadAllBytes(store.GetPath(1));
        menu.Open(loading ? PlayerSaveMenuMode.Load : PlayerSaveMenuMode.Save);
        menu.SelectSlot(1);
        sounds.Clear();
        menu.Update(Card(), UnexpectedSave, UnexpectedLoad);
        Assert.True(menu.IsTransferring);
        Assert.Equal(1, menu.TransferSource);
        Assert.Equal((1, 101), (menu.Bank, menu.SelectedSlot));
        Navigate(menu, page: 1);
        Navigate(menu, vertical: 1);
        (int Source, int Destination)? copied = null;
        menu.Update(Confirm(), UnexpectedSave, UnexpectedLoad, (from, to) => copied = (from, to));
        Assert.Equal((1, 107), copied);
        Assert.True(menu.IsOpen);
        Assert.False(menu.IsTransferring);
        Assert.Equal((0, 1), (menu.Bank, menu.SelectedSlot));
        Assert.Contains("Copied", menu.Message);
        Assert.Equal(source, File.ReadAllBytes(store.GetPath(1)));
        Assert.Equal(new[] { MenuSound.Confirm, MenuSound.Move, MenuSound.Move, MenuSound.Confirm }, sounds);
    }

    [Fact]
    public void SelectedQuickSnapshotCanCopyToSecondOriginalBankWithoutBeingReplacedByQuickSave()
    {
        PlayerSaveMenu menu = CreateMenu(out PlayerSaveStore store, originalDirectory: true);
        store.Save(0, new GameSession(new GameState { Scene = SceneKind.Field, X = 10 }));
        byte[] source = File.ReadAllBytes(store.GetPath(0));
        menu.Open(PlayerSaveMenuMode.Load);
        menu.Update(Card(), UnexpectedSave, UnexpectedLoad);
        Assert.Equal(0, menu.TransferSource);
        menu.Update(new InputFrame(0, 0, false, false, false, true), UnexpectedSave, UnexpectedLoad);
        Assert.True(menu.IsTransferring);
        Assert.Contains("before quick saving", menu.Message);
        Assert.True(menu.SelectBank(2));
        menu.SelectSlot(160);
        (int Source, int Destination)? copied = null;
        menu.Update(Confirm(), UnexpectedSave, UnexpectedLoad, (from, to) => copied = (from, to));
        Assert.Equal((0, 160), copied);
        Assert.Equal((0, 0), (menu.Bank, menu.SelectedSlot));
        Assert.Contains(menu.Entries, entry => entry.Slot == 0 && entry.CanLoad);
        Assert.Equal(source, File.ReadAllBytes(store.GetPath(0)));
    }

    [Fact]
    public void CopyCancelUnwindsOverwriteThenDestinationThenMenu()
    {
        PlayerSaveMenu menu = CreateMenu(out PlayerSaveStore store, originalDirectory: true);
        store.Save(7, new GameSession(new GameState { Scene = SceneKind.Field }));
        File.WriteAllText(store.GetPath(107), "occupied destination");
        menu.Open(PlayerSaveMenuMode.Load);
        menu.SelectSlot(7);
        sounds.Clear();
        menu.Update(Card(), UnexpectedSave, UnexpectedLoad);
        menu.SelectSlot(107);
        sounds.Clear();
        menu.Update(Confirm(), UnexpectedSave, UnexpectedLoad, (_, _) => Assert.Fail("Overwrite requires acceptance."));
        Assert.True(menu.ConfirmOverwrite);
        menu.Update(Cancel(), UnexpectedSave, UnexpectedLoad);
        Assert.False(menu.ConfirmOverwrite);
        Assert.True(menu.IsTransferring);
        menu.Update(Cancel(), UnexpectedSave, UnexpectedLoad);
        Assert.True(menu.IsOpen);
        Assert.False(menu.IsTransferring);
        Assert.Equal((0, 0, 7), (menu.Bank, menu.Page, menu.SelectedSlot));
        menu.Update(Cancel(), UnexpectedSave, UnexpectedLoad);
        Assert.False(menu.IsOpen);
        Assert.Equal("occupied destination", File.ReadAllText(store.GetPath(107)));
        Assert.Equal(new[] { MenuSound.Confirm, MenuSound.Cancel, MenuSound.Cancel, MenuSound.Cancel }, sounds);
    }

    [Fact]
    public void FailedCopyKeepsSourceAndDestinationAndCanBeRetriedAfterOverwriteAcceptance()
    {
        PlayerSaveMenu menu = CreateMenu(out PlayerSaveStore store, originalDirectory: true);
        store.Save(1, new GameSession(new GameState { Scene = SceneKind.Field }));
        File.WriteAllText(store.GetPath(101), "occupied destination");
        menu.Open(PlayerSaveMenuMode.Save);
        menu.Update(Card(), UnexpectedSave, UnexpectedLoad);
        menu.Update(Confirm(), UnexpectedSave, UnexpectedLoad);
        menu.Update(Confirm(), UnexpectedSave, UnexpectedLoad, (_, _) => throw new NotSupportedException("Unsupported continuation."));
        Assert.True(menu.IsOpen);
        Assert.True(menu.IsTransferring);
        Assert.False(menu.ConfirmOverwrite);
        Assert.Contains("Unsupported continuation", menu.Message);
        Assert.Equal("occupied destination", File.ReadAllText(store.GetPath(101)));
        int copies = 0;
        menu.Update(Confirm(), UnexpectedSave, UnexpectedLoad, (_, _) => copies++);
        Assert.True(menu.ConfirmOverwrite);
        Assert.Equal(0, copies);
        menu.Update(Confirm(), UnexpectedSave, UnexpectedLoad, (_, _) => copies++);
        Assert.Equal(1, copies);
        Assert.False(menu.IsTransferring);
    }

    [Fact]
    public void CopyRejectsEmptyAndCorruptSourcesAndCancelledBankSelection()
    {
        PlayerSaveMenu menu = CreateMenu(out PlayerSaveStore store);
        Directory.CreateDirectory(Path.GetDirectoryName(store.GetPath(2))!);
        File.WriteAllText(store.GetPath(2), "invalid save");
        store.Save(3, new GameSession(new GameState { Scene = SceneKind.Field }));
        menu.Open(PlayerSaveMenuMode.Save);
        menu.Update(Card(), UnexpectedSave, UnexpectedLoad);
        Assert.False(menu.IsTransferring);
        Assert.Equal("This slot is empty.", menu.Message);
        menu.SelectSlot(2);
        menu.Update(Card(), UnexpectedSave, UnexpectedLoad);
        Assert.False(menu.IsTransferring);
        Assert.StartsWith("Unavailable:", menu.Message);
        menu.SelectSlot(3);
        menu.BeforeBankChange = _ => false;
        menu.Update(Card(), UnexpectedSave, UnexpectedLoad);
        Assert.False(menu.IsTransferring);
        Assert.Equal((0, 3), (menu.Bank, menu.SelectedSlot));
        Assert.Equal("Bank selection cancelled.", menu.Message);
    }

    [Fact]
    public void OriginalCopyTargetsMemoriaAndRestoresExactOriginalBankOnCancel()
    {
        PlayerSaveMenu menu = CreateMenu(out PlayerSaveStore store, originalDirectory: true);
        byte[] block = new byte[NativePcSaveData.BlockSize];
        block[0] = (byte)'S';
        block[1] = (byte)'C';
        block[386] = 255;
        block[387] = 8;
        File.WriteAllBytes(store.GetPath(145), NativePcSaveCodec.Encode(new NativePcSaveData(block)));
        menu.Open(PlayerSaveMenuMode.Load);
        menu.SelectBank(2);
        menu.SelectSlot(145);
        menu.Update(Card(), UnexpectedSave, UnexpectedLoad);
        Assert.True(menu.IsTransferring);
        Assert.Equal(145, menu.TransferSource);
        Assert.Equal(0, menu.Bank);
        Assert.Equal(100, menu.Entries.Count);
        Assert.All(menu.Entries, entry => Assert.True(entry.Slot > 0));
        Assert.NotEqual(0, menu.SelectedSlot);
        Assert.False(menu.SelectBank(1));
        menu.Update(Cancel(), UnexpectedSave, UnexpectedLoad);
        Assert.Equal((2, 2, 145), (menu.Bank, menu.Page, menu.SelectedSlot));
    }

    [Fact]
    public void QuickLoadPickerDoesNotStartCopyOrSwitchBanks()
    {
        PlayerSaveMenu menu = CreateMenu(out PlayerSaveStore store);
        store.Save(0, new GameSession(new GameState { Scene = SceneKind.Field }));
        menu.Open(PlayerSaveMenuMode.QuickLoad);
        menu.Update(Card(), UnexpectedSave, UnexpectedLoad);
        Assert.False(menu.IsTransferring);
        Assert.False(menu.SelectBank(1));
        Assert.Equal(0, menu.SelectedSlot);
    }

    [Fact]
    public void LoadPagesMixEveryQuickSnapshotWithManualSavesInDescendingTimeOrder()
    {
        PlayerSaveMenu menu = CreateMenu(out PlayerSaveStore store);
        DateTime savedAt = new(2025, 1, 1, 12, 0, 0, DateTimeKind.Utc);
        for (int index = 0; index < 6; index++)
        {
            store.Save(0, new GameSession(new GameState { Scene = SceneKind.Field, X = index }));
            File.SetLastWriteTimeUtc(store.GetPath(0), savedAt.AddMinutes(index * 2));
        }
        store.Save(10, new GameSession(new GameState { Scene = SceneKind.Field, X = 101 }));
        File.SetLastWriteTimeUtc(store.GetPath(10), savedAt.AddMinutes(3));
        store.Save(20, new GameSession(new GameState { Scene = SceneKind.Field, X = 102 }));
        File.SetLastWriteTimeUtc(store.GetPath(20), savedAt.AddMinutes(9));
        menu.Open(PlayerSaveMenuMode.Load);
        Assert.Equal(106, menu.Entries.Count);
        Assert.Equal(22, menu.PageCount);
        Assert.Equal(new[] { 5f, 102f, 4f, 3f, 2f, 101f, 1f, 0f }, menu.Entries.Take(8).Select(entry => store.Load(entry.Slot).X));
        Assert.All(menu.Entries.Skip(8), entry => Assert.Null(entry.SavedAt));
        Navigate(menu, page: 1);
        Navigate(menu, vertical: 1);
        int selected = menu.SelectedSlot;
        Assert.True(selected < 0);
        Assert.Equal((1, 6), (menu.Page, menu.Selection));
        Assert.Equal(1, store.Load(selected).X);
        float? loadedX = null;
        menu.Update(Confirm(), UnexpectedSave, slot => loadedX = store.Load(slot).X);
        Assert.Equal(1f, loadedX);
        Assert.False(menu.IsOpen);
        menu.Open(PlayerSaveMenuMode.Load);
        Assert.Equal((1, selected), (menu.Page, menu.SelectedSlot));
        for (int page = 1; page < 21; page++)
        {
            menu.RequestPage(1);
        }
        Assert.Equal(21, menu.Page);
        Assert.Single(menu.VisibleEntries);
        Navigate(menu, vertical: 1);
        Assert.Equal(105, menu.Selection);
    }

    [Fact]
    public void TransferButtonsRestoreTheExactOlderQuickSnapshotAfterCancellation()
    {
        PlayerSaveMenu menu = CreateMenu(out PlayerSaveStore store, originalDirectory: true);
        DateTime savedAt = new(2025, 1, 1, 12, 0, 0, DateTimeKind.Utc);
        for (int index = 0; index < 8; index++)
        {
            store.Save(0, new GameSession(new GameState { Scene = SceneKind.Field, X = index }));
            File.SetLastWriteTimeUtc(store.GetPath(0), savedAt.AddMinutes(index));
        }
        menu.Open(PlayerSaveMenuMode.Load);
        Click(menu, "save-next");
        Click(menu, "save-slot-" + menu.Entries[6].Slot);
        int selected = menu.SelectedSlot;
        Assert.True(selected < 0);
        Assert.Equal(1, store.Load(selected).X);
        Click(menu, "save-transfer");
        Assert.True(menu.IsTransferring);
        Assert.Equal(selected, menu.TransferSource);
        Assert.Equal(1, menu.Bank);
        menu.SelectBank(2);
        menu.SelectSlot(160);
        Click(menu, "save-transfer");
        Assert.False(menu.IsTransferring);
        Assert.Equal((0, 1, 6, selected), (menu.Bank, menu.Page, menu.Selection, menu.SelectedSlot));
        Click(menu, "save-transfer");
        (int Source, int Destination)? copied = null;
        menu.SelectBank(2);
        menu.SelectSlot(160);
        Click(menu, "save-confirm");
        menu.Update(default, UnexpectedSave, UnexpectedLoad, (source, destination) => copied = (source, destination));
        Assert.Equal((selected, 160), copied);
        Assert.False(menu.IsTransferring);
        Assert.Equal((0, 1, selected), (menu.Bank, menu.Page, menu.SelectedSlot));
    }

    [Fact]
    public void ClickingTheActiveBankRetainsTheSelectedSavePointer()
    {
        PlayerSaveMenu menu = CreateMenu(out _);
        menu.Open(PlayerSaveMenuMode.Save);
        menu.SelectSlot(3);
        UiView view = PlayerSaveView.Build(menu, "Transfer", "Navigate");
        UiButton bank = Assert.Single(PlayerSaveView.FindButtons(view.Root), button => button.Id == "save-bank-0");
        view.Pointer(bank.Bounds.X + bank.Bounds.Width / 2, bank.Bounds.Y + bank.Bounds.Height / 2, true);
        Assert.Equal(3, menu.SelectedSlot);
        Assert.Equal("save-slot-3", view.FocusedElement?.Id);
    }

    private static void Click(PlayerSaveMenu menu, string id)
    {
        UiView view = PlayerSaveView.Build(menu, "Transfer", "Navigate");
        Assert.IsType<PlayerSaveView>(view.Root);
        UiButton button = Assert.Single(PlayerSaveView.FindButtons(view.Root), button => button.Id == id);
        Assert.True(button.Enabled);
        view.Pointer(button.Bounds.X + button.Bounds.Width / 2, button.Bounds.Y + button.Bounds.Height / 2, true);
    }

    private static void UnexpectedSave(int slot)
    {
        Assert.Fail("Unexpected save.");
    }

    private static void UnexpectedLoad(int slot)
    {
        Assert.Fail("Unexpected load.");
    }

    private static InputFrame Card()
    {
        return new InputFrame(0, 0, false, false, false, false, CardChallenge: true);
    }

    private PlayerSaveMenu CreateMenu(out PlayerSaveStore store, bool originalDirectory = false)
    {
        store = new PlayerSaveStore(Path.Combine(directory, "research.json"), Path.Combine(directory, "saves"));
        if (originalDirectory)
        {
            string originals = Path.Combine(directory, "originals");
            Directory.CreateDirectory(originals);
            store.SetOriginalDirectory(originals);
        }
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
