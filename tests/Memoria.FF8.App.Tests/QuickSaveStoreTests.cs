using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class QuickSaveStoreTests : IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), "Memoria.FF8-quick-saves-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public void Default_rotation_retains_nine_newest_saves_and_latest_alias()
    {
        PlayerSaveStore store = CreateStore();
        Assert.Empty(store.ReadQuickEntries());
        Assert.Equal("quick.json", Path.GetFileName(store.GetPath(0)));
        Assert.Equal("Empty", store.ReadEntries(true)[0].Details);
        Assert.Equal(Enumerable.Range(1, 100), store.ReadEntries(true).Select(entry => entry.Slot));
        for (int value = 1; value <= 11; value++)
        {
            Save(store, value);
        }
        IReadOnlyList<PlayerSaveEntry> entries = store.ReadQuickEntries();
        Assert.Equal(9, entries.Count);
        Assert.Equal(0, entries[0].Slot);
        Assert.Equal(Enumerable.Range(3, 9).Reverse().Select(value => (float)value), entries.Select(entry => store.Load(entry.Slot).X));
        Assert.Equal(11, store.Load(0).X);
        Assert.All(entries.Skip(1), entry => Assert.True(entry.Slot < 0));
        Assert.Equal(entries.Select(entry => entry.SavedAt).OrderByDescending(savedAt => savedAt), entries.Select(entry => entry.SavedAt));
        Assert.Equal(109, store.ReadEntries(true).Count);
        Assert.Equal(entries[0], store.ReadEntries(true)[0]);
        Assert.Equal(100, store.ReadEntries(false).Count);
        Assert.Equal(9, Directory.GetFiles(Path.Combine(directory, "saves"), "*.json").Length);
    }

    [Fact]
    public void Accepted_shrinking_removes_oldest_and_one_save_capacity_replaces_latest()
    {
        PlayerSaveStore store = CreateStore();
        for (int value = 1; value <= 4; value++)
        {
            Save(store, value);
        }
        string[] paths = store.ReadQuickEntries().Select(entry => store.GetPath(entry.Slot)).ToArray();
        store.CommitQuickSaveCount(2, 2, () => { });
        Assert.Equal(new[] { 4f, 3f }, store.ReadQuickEntries().Select(entry => store.Load(entry.Slot).X));
        Assert.All(paths.Take(2), path => Assert.True(File.Exists(path)));
        Assert.All(paths.Skip(2), path => Assert.False(File.Exists(path)));
        store.SetQuickSaveCount(1);
        Save(store, 5);
        Assert.Single(store.ReadQuickEntries());
        Assert.Equal(5, store.Load(0).X);
        Assert.False(File.Exists(paths[0]));
    }

    [Fact]
    public void Older_quick_ids_continue_to_identify_the_same_save_after_rotation()
    {
        PlayerSaveStore store = CreateStore();
        Save(store, 1);
        Save(store, 2);
        int olderSlot = store.ReadQuickEntries()[1].Slot;
        string olderPath = store.GetPath(olderSlot);
        Save(store, 3);
        Assert.Equal(olderPath, store.GetPath(olderSlot));
        Assert.Equal(1, store.Load(olderSlot).X);
        Assert.Equal(3, store.Load(0).X);
        var reopened = CreateStore();
        Assert.Equal(3, reopened.Load(0).X);
        Assert.Equal(1, reopened.Load(olderSlot).X);
    }

    [Fact]
    public void Legacy_save_remains_readable_then_participates_in_retention()
    {
        PlayerSaveStore store = CreateStore();
        string legacyPath = store.GetPath(0);
        new GameSession(new GameState { Scene = SceneKind.Field, X = 7 }).Save(legacyPath);
        byte[] original = File.ReadAllBytes(legacyPath);
        Assert.Equal(7, store.Load(0).X);
        Save(store, 8);
        IReadOnlyList<PlayerSaveEntry> entries = store.ReadQuickEntries();
        Assert.Equal(2, entries.Count);
        Assert.Equal(8, store.Load(0).X);
        Assert.Equal(legacyPath, store.GetPath(entries[1].Slot));
        Assert.Equal(7, store.Load(entries[1].Slot).X);
        Assert.Equal(original, File.ReadAllBytes(legacyPath));
        store.CommitQuickSaveCount(1, 1, () => { });
        Assert.False(File.Exists(legacyPath));
        Assert.Equal(8, store.Load(0).X);
    }

    [Fact]
    public void Corrupt_quick_saves_keep_their_timestamp_and_newest_position()
    {
        PlayerSaveStore store = CreateStore();
        Save(store, 1);
        string firstPath = store.GetPath(0);
        Save(store, 2);
        string latestPath = store.GetPath(0);
        DateTime savedAt = File.GetLastWriteTime(latestPath);
        File.WriteAllText(latestPath, "{invalid");
        File.SetLastWriteTime(latestPath, savedAt);
        IReadOnlyList<PlayerSaveEntry> entries = store.ReadQuickEntries();
        Assert.Equal(2, entries.Count);
        Assert.False(entries[0].CanLoad);
        Assert.Equal(savedAt, entries[0].SavedAt);
        Assert.StartsWith("Unavailable:", entries[0].Details);
        Assert.Equal(firstPath, store.GetPath(entries[1].Slot));
        Assert.True(entries[1].CanLoad);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(int.MinValue)]
    public void Invalid_retention_counts_do_not_change_existing_saves(int count)
    {
        PlayerSaveStore store = CreateStore();
        Save(store, 1);
        Save(store, 2);
        Assert.Throws<ArgumentOutOfRangeException>(() => store.SetQuickSaveCount(count));
        Assert.Equal(2, store.ReadQuickEntries().Count);
        Assert.Equal(2, store.Load(0).X);
    }

    [Fact]
    public void Failed_validation_preserves_all_retained_saves_and_leaves_no_new_file()
    {
        PlayerSaveStore store = CreateStore();
        store.SetQuickSaveCount(2);
        Save(store, 1);
        Save(store, 2);
        Dictionary<string, byte[]> originals = store.ReadQuickEntries().ToDictionary(entry => store.GetPath(entry.Slot), entry => File.ReadAllBytes(store.GetPath(entry.Slot)));
        var session = new GameSession(new GameState { Scene = SceneKind.Field, X = 3 });
        Assert.Throws<InvalidDataException>(() => store.Save(0, session, _ => throw new InvalidDataException("Rejected continuation")));
        Assert.Equal(2, store.ReadQuickEntries().Count);
        Assert.Equal(2, store.Load(0).X);
        Assert.All(originals, original => Assert.Equal(original.Value, File.ReadAllBytes(original.Key)));
        Assert.Equal(originals.Keys.OrderBy(path => path), Directory.GetFiles(Path.Combine(directory, "saves")).OrderBy(path => path));
    }

    [Fact]
    public void Checkpoint_conflicts_block_shrinking_and_rotation_before_any_deletion()
    {
        PlayerSaveStore initial = CreateStore();
        Save(initial, 1);
        string checkpoint = initial.GetPath(0);
        Save(initial, 2);
        Save(initial, 3);
        byte[] original = File.ReadAllBytes(checkpoint);
        var protectedStore = new PlayerSaveStore(checkpoint, Path.Combine(directory, "saves"));
        Assert.Throws<InvalidOperationException>(() => protectedStore.CommitQuickSaveCount(1, 2, () => { }));
        Assert.Equal(3, protectedStore.ReadQuickEntries().Count);
        protectedStore.SetQuickSaveCount(3);
        Assert.Throws<InvalidOperationException>(() => Save(protectedStore, 4));
        Assert.Equal(3, protectedStore.ReadQuickEntries().Count);
        Assert.Equal(original, File.ReadAllBytes(checkpoint));
        Assert.Equal(3, initial.Load(0).X);
    }

    [Fact]
    public void New_quick_save_and_temporary_file_cannot_overwrite_checkpoint()
    {
        string saves = Path.Combine(directory, "saves");
        Directory.CreateDirectory(saves);
        foreach (string suffix in new[] { "", ".tmp" })
        {
            string checkpoint = Path.Combine(saves, "quick-0000000001.json" + suffix);
            File.WriteAllText(checkpoint, "Protected checkpoint");
            var store = new PlayerSaveStore(checkpoint, saves);
            if (suffix.Length == 0)
            {
                store.SetQuickSaveCount(1);
            }
            Assert.Throws<InvalidOperationException>(() => Save(store, 1));
            Assert.Equal("Protected checkpoint", File.ReadAllText(checkpoint));
            File.Delete(checkpoint);
        }
    }

    [Fact]
    public void Preview_and_limit_adoption_do_not_delete_files()
    {
        PlayerSaveStore store = CreateStore();
        Save(store, 1);
        Save(store, 2);
        Assert.Equal(1, store.PreviewQuickSaveCount(1));
        Assert.Equal(2, store.ReadQuickEntries().Count);
        store.SetQuickSaveCount(1);
        Assert.Equal(1, store.QuickSaveCount);
        Assert.Equal(2, store.ReadQuickEntries().Count);
    }

    [Fact]
    public void Failed_persistence_and_stale_confirmation_preserve_limit_and_files()
    {
        PlayerSaveStore store = CreateStore();
        Save(store, 1);
        Save(store, 2);
        bool persisted = false;
        Assert.Throws<InvalidOperationException>(() => store.CommitQuickSaveCount(1, 0, () => persisted = true));
        Assert.False(persisted);
        Assert.Throws<IOException>(() => store.CommitQuickSaveCount(1, 1, () => throw new IOException("Persistence failed")));
        Assert.Equal(9, store.QuickSaveCount);
        Assert.Equal(2, store.ReadQuickEntries().Count);
    }

    [Fact]
    public void Increasing_limit_with_pending_cleanup_does_not_delete_or_require_confirmation()
    {
        PlayerSaveStore store = CreateStore();
        Save(store, 1);
        Save(store, 2);
        Save(store, 3);
        store.SetQuickSaveCount(1);
        Assert.Equal(0, store.PreviewQuickSaveCount(2));
        QuickSaveCleanupResult result = store.CommitQuickSaveCount(2, 0, () => { });
        Assert.Equal(0, result.Removed);
        Assert.Equal(3, store.ReadQuickEntries().Count);
        Assert.Equal(2, store.QuickSaveCount);
    }

    [Fact]
    public void Cleanup_failure_keeps_accepted_limit_and_reports_partial_deletion()
    {
        PlayerSaveStore store = CreateStore();
        for (int value = 1; value <= 4; value++)
        {
            Save(store, value);
        }
        string lockedPath = store.GetPath(store.ReadQuickEntries()[2].Slot);
        string slot = store.GetPath(1);
        File.WriteAllText(slot, "Ordinary save");
        string checkpoint = Path.Combine(directory, "checkpoint.json");
        File.WriteAllText(checkpoint, "Checkpoint");
        bool persisted = false;
        using (new FileStream(lockedPath, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            QuickSaveCleanupResult result = store.CommitQuickSaveCount(1, 3, () =>
            {
                Assert.Equal(4, store.ReadQuickEntries().Count);
                Assert.Equal(9, store.QuickSaveCount);
                persisted = true;
            });
            Assert.True(persisted);
            Assert.Equal(1, store.QuickSaveCount);
            Assert.Equal(1, result.Removed);
            Assert.Equal(2, result.Remaining);
            Assert.NotNull(result.Error);
            Assert.Contains("limit saved", result.Message);
            Assert.Equal(3, store.ReadQuickEntries().Count);
        }
        Assert.Equal("Ordinary save", File.ReadAllText(slot));
        Assert.Equal("Checkpoint", File.ReadAllText(checkpoint));
        Save(store, 5);
        Assert.Single(store.ReadQuickEntries());
    }

    private PlayerSaveStore CreateStore()
    {
        return new PlayerSaveStore(Path.Combine(directory, "checkpoint.json"), Path.Combine(directory, "saves"));
    }

    private static void Save(PlayerSaveStore store, int value)
    {
        store.Save(0, new GameSession(new GameState { Scene = SceneKind.Field, X = value }));
    }

    public void Dispose()
    {
        if (Directory.Exists(directory))
        {
            Directory.Delete(directory, true);
        }
    }
}
