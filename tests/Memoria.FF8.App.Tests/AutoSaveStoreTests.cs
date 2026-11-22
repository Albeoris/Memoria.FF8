using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class AutoSaveStoreTests : IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), "Memoria.FF8-autosaves-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public void Rotation_retains_each_category_independently_and_preserves_manual_and_quick_saves()
    {
        PlayerSaveStore store = CreateStore();
        store.Save(1, Session(50));
        store.Save(0, Session(60));
        for (int index = 1; index <= 5; index++)
        {
            Save(store, AutoSaveKind.Location, 3, index);
            Save(store, AutoSaveKind.Interval, 3, 10 + index);
        }

        PlayerSaveStore reopened = CreateStore();
        Assert.Equal(new[] { 5f, 4f, 3f }, ReadPosition(reopened, AutoSaveKind.Location));
        Assert.Equal(new[] { 15f, 14f, 13f }, ReadPosition(reopened, AutoSaveKind.Interval));
        Assert.Equal(50, reopened.Load(1).X);
        Assert.Equal(60, reopened.Load(0).X);
        Assert.Single(reopened.ReadQuickEntries());
        Assert.Equal(8, Directory.GetFiles(directory).Length);

        Save(reopened, AutoSaveKind.Location, 1, 6);
        Assert.Equal(new[] { 6f }, ReadPosition(reopened, AutoSaveKind.Location));
        Assert.Equal(new[] { 15f, 14f, 13f }, ReadPosition(reopened, AutoSaveKind.Interval));
        Assert.Equal(6, Directory.GetFiles(directory).Length);
    }

    [Fact]
    public void A_failed_write_preserves_older_saves_and_never_publishes_the_staging_file()
    {
        PlayerSaveStore store = CreateStore();
        Save(store, AutoSaveKind.Location, 2, 1);
        Save(store, AutoSaveKind.Location, 2, 2);
        string[] before = Directory.GetFiles(directory).Order().ToArray();

        Assert.Throws<InvalidDataException>(() => store.SaveAuto(AutoSaveKind.Location, 1, Session(3), null, (path, _, _) =>
        {
            File.WriteAllText(path, "Interrupted save");
            throw new InvalidDataException("Rejected continuation.");
        }));

        Assert.Equal(before, Directory.GetFiles(directory).Order().ToArray());
        Assert.Equal(new[] { 2f, 1f }, ReadPosition(store, AutoSaveKind.Location));
    }

    [Fact]
    public void Rotation_does_not_expose_or_delete_a_configured_checkpoint()
    {
        PlayerSaveStore store = CreateStore();
        Save(store, AutoSaveKind.Location, 1, 1);
        string checkpoint = store.GetPath(Assert.Single(store.ReadRecentEntries()).Slot);
        byte[] original = File.ReadAllBytes(checkpoint);
        PlayerSaveStore protectedStore = CreateStore(checkpoint);

        Assert.Empty(protectedStore.ReadRecentEntries());
        Save(protectedStore, AutoSaveKind.Location, 1, 2);
        Save(protectedStore, AutoSaveKind.Location, 1, 3);

        Assert.Equal(original, File.ReadAllBytes(checkpoint));
        Assert.Equal(new[] { 3f }, ReadPosition(protectedStore, AutoSaveKind.Location));
        Assert.Equal(2, Directory.GetFiles(directory).Length);
    }

    [Fact]
    public void Quick_load_and_load_show_labelled_autosaves_and_the_save_menu_uses_manual_slots()
    {
        PlayerSaveStore store = CreateStore();
        Save(store, AutoSaveKind.Location, 3, 1);
        Save(store, AutoSaveKind.Interval, 3, 2);
        store.Save(0, Session(3));
        store.Save(1, Session(4));
        PlayerSaveMenu menu = new(store);

        menu.Open(PlayerSaveMenuMode.QuickLoad);
        Assert.Equal(new[] { "Interval Autosave", "Location Autosave", "Quick Save" }, menu.VisibleEntries.Select(entry => entry.Label).Order(StringComparer.Ordinal));
        Assert.Equal(menu.Entries.Select(entry => entry.SavedAtUtc).OrderDescending(), menu.Entries.Select(entry => entry.SavedAtUtc));
        int intervalSlot = Assert.Single(menu.Entries, entry => PlayerSaveStore.GetAutoSaveKind(entry.Slot) == AutoSaveKind.Interval).Slot;
        menu.SelectSlot(intervalSlot);
        Assert.Equal(intervalSlot, menu.SelectedSlot);
        float? loaded = null;
        menu.Update(new InputFrame(0, 0, true, false, false, false), _ => Assert.Fail("Unexpected save."), slot => loaded = store.Load(slot).X);
        Assert.Equal(2f, loaded);

        menu.Open(PlayerSaveMenuMode.Load);
        Assert.Contains(menu.Entries, entry => entry.Slot == intervalSlot && entry.CanLoad);
        menu.Open(PlayerSaveMenuMode.Save);
        Assert.Equal(Enumerable.Range(1, PlayerSaveStore.MemoriaSlotCount), menu.Entries.Select(entry => entry.Slot));
        Assert.Throws<ArgumentOutOfRangeException>(() => store.Save(intervalSlot, Session(5)));
    }

    private PlayerSaveStore CreateStore(string? checkpoint = null)
    {
        return new(checkpoint ?? Path.Combine(directory, "checkpoint.ffsave"), directory, discoverOriginalSaves: false);
    }

    private static float[] ReadPosition(PlayerSaveStore store, AutoSaveKind kind)
    {
        return store.ReadRecentEntries().Where(entry => PlayerSaveStore.GetAutoSaveKind(entry.Slot) == kind).Select(entry => store.Load(entry.Slot).X).ToArray();
    }

    private static GameSession Session(float x)
    {
        return new(new GameState { Scene = SceneKind.Field, X = x });
    }

    private static void Save(PlayerSaveStore store, AutoSaveKind kind, int count, float x)
    {
        GameSession session = Session(x);
        store.SaveAuto(kind, count, session, null, (path, validate, savedAt) =>
        {
            session.Save(path, compact: true, validate, savedAt);
            return session.State;
        });
    }

    public void Dispose()
    {
        if (Directory.Exists(directory))
        {
            Directory.Delete(directory, recursive: true);
        }
    }
}
