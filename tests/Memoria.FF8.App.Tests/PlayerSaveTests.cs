using Memoria.FF8.Runtime;
using Microsoft.Xna.Framework.Input;

namespace Memoria.FF8.App.Tests;

public sealed class PlayerSaveTests : IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), "Memoria.FF8-player-saves-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public void Player_slots_and_quick_save_do_not_replace_research_checkpoint()
    {
        string research = Path.Combine(directory, "research.json");
        Directory.CreateDirectory(directory);
        File.WriteAllText(research, "research evidence");
        var store = new PlayerSaveStore(research, Path.Combine(directory, "player-saves"), new ReplayJournalArchive(Path.Combine(directory, "journals")));
        var session = new GameSession(new GameState { Scene = SceneKind.Field, FieldId = 180, FieldName = "bghoke_2", X = 12.5f, Y = 41 });
        store.Save(0, session);
        session.SetPosition(99, 48);
        store.Save(1, session);
        Assert.Equal("research evidence", File.ReadAllText(research));
        Assert.Equal(12.5f, store.Load(0).X);
        Assert.Equal(99, store.Load(1).X);
        Assert.True(store.ContainsPath(store.GetPath(0)));
        Assert.False(store.ContainsPath(research));
        Assert.False(store.ContainsPath(Path.Combine(directory, "player-saves-other", "quick.json")));
        IReadOnlyList<PlayerSaveEntry> entries = store.ReadEntries(true);
        Assert.Equal(61, entries.Count);
        Assert.True(entries[0].CanLoad);
        Assert.Contains("bghoke_2", entries[1].Details);
        Assert.NotNull(entries[1].SavedAt);
        Assert.False(entries[2].CanLoad);
    }

    [Fact]
    public void Missing_corrupt_and_research_conflicting_saves_are_unavailable_without_replacing_current_state()
    {
        var store = CreateStore();
        Directory.CreateDirectory(Path.GetDirectoryName(store.GetPath(1))!);
        File.WriteAllText(store.GetPath(1), "{ invalid json");
        File.WriteAllText(store.GetPath(2), "{\"Version\":999}");
        IReadOnlyList<PlayerSaveEntry> entries = store.ReadEntries(true);
        Assert.Equal("Empty", entries[0].Details);
        Assert.False(entries[1].CanLoad);
        Assert.StartsWith("Unavailable:", entries[1].Details);
        Assert.False(entries[2].CanLoad);
        Assert.Throws<FileNotFoundException>(() => store.Load(0));
        Assert.Throws<System.Text.Json.JsonException>(() => store.Load(1));
        var conflicting = new PlayerSaveStore(store.GetPath(1), Path.GetDirectoryName(store.GetPath(1)), new ReplayJournalArchive(Path.Combine(directory, "journals")));
        Assert.Throws<InvalidOperationException>(() => conflicting.GetPath(1));
        Assert.Throws<InvalidOperationException>(() => conflicting.Save(1, new GameSession(new GameState { Scene = SceneKind.Field })));
        Assert.Contains("invalid json", File.ReadAllText(store.GetPath(1)));
    }

    [Fact]
    public void Standard_menu_requires_overwrite_confirmation_and_escape_preserves_existing_slot()
    {
        var store = CreateStore();
        var session = new GameSession(new GameState { Scene = SceneKind.Field, X = 10 });
        store.Save(1, session);
        session.SetPosition(20, 0);
        var menu = new PlayerSaveMenu(store);
        menu.Open(PlayerSaveMenuMode.Save);
        menu.Update(Confirm(), slot => store.Save(slot, session), _ => throw new InvalidOperationException());
        Assert.True(menu.ConfirmOverwrite);
        Assert.Equal(10, store.Load(1).X);
        menu.Update(Cancel(), slot => store.Save(slot, session), _ => throw new InvalidOperationException());
        Assert.True(menu.IsOpen);
        Assert.False(menu.ConfirmOverwrite);
        menu.Update(Cancel(), slot => store.Save(slot, session), _ => throw new InvalidOperationException());
        Assert.False(menu.IsOpen);
        Assert.Equal(10, store.Load(1).X);
        menu.Open(PlayerSaveMenuMode.Save);
        menu.Update(Confirm(), slot => store.Save(slot, session), _ => throw new InvalidOperationException());
        menu.Update(Confirm(), slot => store.Save(slot, session), _ => throw new InvalidOperationException());
        Assert.False(menu.IsOpen);
        Assert.Equal(20, store.Load(1).X);
    }

    [Fact]
    public void Load_menu_reports_empty_or_failed_load_and_remains_cancellable()
    {
        var store = CreateStore();
        var menu = new PlayerSaveMenu(store);
        int loads = 0;
        menu.Open(PlayerSaveMenuMode.Load);
        menu.Update(Confirm(), _ => throw new InvalidOperationException(), _ => loads++);
        Assert.Equal(0, loads);
        Assert.True(menu.IsOpen);
        Assert.Equal("This slot is empty.", menu.Message);
        store.Save(0, new GameSession(new GameState { Scene = SceneKind.Field }));
        menu.Open(PlayerSaveMenuMode.Load);
        menu.Update(Confirm(), _ => throw new InvalidOperationException(), _ => throw new InvalidDataException("Invalid VM continuation"));
        Assert.True(menu.IsOpen);
        Assert.Contains("Invalid VM continuation", menu.Message);
        menu.Update(Cancel(), _ => throw new InvalidOperationException(), _ => loads++);
        Assert.False(menu.IsOpen);
        Assert.Equal(0, loads);
    }

    [Fact]
    public void Save_shortcuts_are_distinct_press_edges_and_ignore_inactive_input()
    {
        var keyboard = new KeyboardInput();
        InputFrame quickSave = keyboard.Read(new KeyboardState(Keys.F5), true);
        Assert.True(quickSave.Save);
        Assert.False(quickSave.QuickLoad);
        Assert.False(quickSave.SaveMenu);
        Assert.False(quickSave.LoadMenu);
        Assert.False(keyboard.Read(new KeyboardState(Keys.F5), true).Save);
        InputFrame shortcuts = keyboard.Read(new KeyboardState(Keys.F6, Keys.F9, Keys.F10), true);
        Assert.True(shortcuts.SaveMenu);
        Assert.True(shortcuts.QuickLoad);
        Assert.True(shortcuts.LoadMenu);
        InputFrame held = keyboard.Read(new KeyboardState(Keys.F6, Keys.F9, Keys.F10), true);
        Assert.False(held.SaveMenu);
        Assert.False(held.QuickLoad);
        Assert.False(held.LoadMenu);
        keyboard.Read(new KeyboardState(), true);
        InputFrame inactive = keyboard.Read(new KeyboardState(Keys.F5, Keys.F6, Keys.F9, Keys.F10), false);
        Assert.False(inactive.Save);
        Assert.False(inactive.SaveMenu);
        Assert.False(inactive.QuickLoad);
        Assert.False(inactive.LoadMenu);
    }

    [Fact]
    public void Sixty_manual_slots_preserve_existing_paths_and_keep_banks_and_quick_save_separate()
    {
        PlayerSaveStore store = CreateStore();
        Assert.Equal((2, 30, 60), (PlayerSaveStore.BankCount, PlayerSaveStore.SlotsPerBank, PlayerSaveStore.SlotCount));
        Assert.Equal("slot-01.json", Path.GetFileName(store.GetPath(1)));
        Assert.Equal("slot-02.json", Path.GetFileName(store.GetPath(2)));
        Assert.Equal("slot-03.json", Path.GetFileName(store.GetPath(3)));
        Assert.Equal("quick.json", Path.GetFileName(store.GetPath(0)));
        Assert.Equal("Bank 1, Slot 30", PlayerSaveStore.GetLabel(30));
        Assert.Equal("Bank 2, Slot 01", PlayerSaveStore.GetLabel(31));
        Assert.Equal("Bank 2, Slot 30", PlayerSaveStore.GetLabel(60));
        store.Save(0, new GameSession(new GameState { Scene = SceneKind.Field, X = 5 }));
        store.Save(30, new GameSession(new GameState { Scene = SceneKind.Field, X = 30 }));
        store.Save(31, new GameSession(new GameState { Scene = SceneKind.Field, X = 31 }));
        store.Save(60, new GameSession(new GameState { Scene = SceneKind.Field, X = 60 }));
        Assert.Equal(5, store.Load(0).X);
        Assert.Equal(30, store.Load(30).X);
        Assert.Equal(31, store.Load(31).X);
        Assert.Equal(60, store.Load(60).X);
        Assert.Throws<ArgumentOutOfRangeException>(() => store.GetPath(61));
        IReadOnlyList<PlayerSaveEntry> entries = store.ReadEntries(false);
        Assert.Equal(60, entries.Count);
        Assert.All(entries, entry => Assert.InRange(entry.Slot, 1, 60));
    }

    [Fact]
    public void Bank_and_page_navigation_reaches_last_slot_and_remembers_each_mode_independently()
    {
        var menu = new PlayerSaveMenu(CreateStore());
        menu.Open(PlayerSaveMenuMode.Save);
        Assert.Equal(new[] { 1, 2, 3, 4, 5 }, menu.VisibleEntries.Select(entry => entry.Slot));
        for (int page = 0; page < 5; page++)
        {
            Navigate(menu, new InputFrame(0, 0, false, false, false, false, MenuPage: 1));
        }
        for (int row = 0; row < 4; row++)
        {
            Navigate(menu, new InputFrame(0, 0, false, false, false, false, MenuVertical: 1));
        }
        Assert.Equal((0, 5, 30), (menu.Bank, menu.Page, menu.SelectedSlot));
        Navigate(menu, new InputFrame(0, 0, false, false, false, false, MenuHorizontal: 1));
        Assert.Equal((1, 5, 60), (menu.Bank, menu.Page, menu.SelectedSlot));
        Assert.Equal(new[] { 56, 57, 58, 59, 60 }, menu.VisibleEntries.Select(entry => entry.Slot));
        Navigate(menu, new InputFrame(0, 0, false, false, false, false, MenuPage: 1, MenuVertical: 1));
        Assert.Equal((1, 5, 60), (menu.Bank, menu.Page, menu.SelectedSlot));
        menu.Close();

        menu.Open(PlayerSaveMenuMode.Load);
        Assert.Equal(0, menu.SelectedSlot);
        Assert.Equal(0, menu.QuickEntry!.Slot);
        Navigate(menu, new InputFrame(0, 0, false, false, false, false, MenuHorizontal: 1));
        Navigate(menu, new InputFrame(0, 0, false, false, false, false, MenuPage: 1));
        Navigate(menu, new InputFrame(0, 0, false, false, false, false, MenuVertical: 1));
        Assert.Equal((1, 1, 37), (menu.Bank, menu.Page, menu.SelectedSlot));
        menu.Open(PlayerSaveMenuMode.Save);
        Assert.Equal((1, 5, 60), (menu.Bank, menu.Page, menu.SelectedSlot));
        Assert.Null(menu.QuickEntry);
        menu.Open(PlayerSaveMenuMode.Load);
        Assert.Equal((1, 1, 37), (menu.Bank, menu.Page, menu.SelectedSlot));
    }

    [Fact]
    public void Row_navigation_crosses_pages_and_navigation_cancels_pending_overwrite()
    {
        PlayerSaveStore store = CreateStore();
        store.Save(6, new GameSession(new GameState { Scene = SceneKind.Field }));
        var menu = new PlayerSaveMenu(store);
        menu.Open(PlayerSaveMenuMode.Save);
        for (int row = 0; row < 5; row++)
        {
            Navigate(menu, new InputFrame(0, 0, false, false, false, false, MenuVertical: 1));
        }
        Assert.Equal((1, 6), (menu.Page, menu.SelectedSlot));
        menu.Update(Confirm(), _ => throw new InvalidOperationException("Overwrite has not yet been accepted."), _ => throw new InvalidOperationException());
        Assert.True(menu.ConfirmOverwrite);
        Navigate(menu, new InputFrame(0, 0, false, false, false, false, MenuVertical: -1));
        Assert.Equal((0, 5), (menu.Page, menu.SelectedSlot));
        Assert.False(menu.ConfirmOverwrite);
        Navigate(menu, new InputFrame(0, 0, false, false, false, false, MenuPage: 1));
        Assert.Equal((1, 10), (menu.Page, menu.SelectedSlot));
        Assert.Equal(new[] { 6, 7, 8, 9, 10 }, menu.VisibleEntries.Select(entry => entry.Slot));
    }

    [Fact]
    public void Quick_hotkeys_remain_available_inside_manual_menus_and_failed_load_keeps_menu_open()
    {
        PlayerSaveStore store = CreateStore();
        var menu = new PlayerSaveMenu(store);
        var session = new GameSession(new GameState { Scene = SceneKind.Field, X = 42 });
        menu.Open(PlayerSaveMenuMode.Load);
        menu.Update(new InputFrame(0, 0, false, false, false, true), slot => store.Save(slot, session), _ => throw new InvalidOperationException());
        Assert.True(menu.IsOpen);
        Assert.Equal("Quick saved.", menu.Message);
        Assert.True(menu.QuickEntry!.CanLoad);
        Assert.Equal(42, store.Load(0).X);
        menu.Update(new InputFrame(0, 0, false, false, false, false, QuickLoad: true), _ => throw new InvalidOperationException(), _ => throw new InvalidDataException("Invalid VM continuation"));
        Assert.True(menu.IsOpen);
        Assert.Contains("Invalid VM continuation", menu.Message);
        int loaded = -1;
        menu.Update(new InputFrame(0, 0, false, false, false, false, QuickLoad: true), _ => throw new InvalidOperationException(), slot => loaded = slot);
        Assert.Equal(0, loaded);
        Assert.False(menu.IsOpen);
    }

    [Fact]
    public void Bank_and_page_keys_are_press_edges_and_ignore_inactive_input()
    {
        var keyboard = new KeyboardInput();
        InputFrame first = keyboard.Read(new KeyboardState(Keys.Right, Keys.PageDown), true);
        Assert.Equal((1, 1), (first.MenuHorizontal, first.MenuPage));
        InputFrame held = keyboard.Read(new KeyboardState(Keys.Right, Keys.PageDown), true);
        Assert.Equal((0, 0), (held.MenuHorizontal, held.MenuPage));
        Assert.Equal(1, held.Horizontal);
        keyboard.Read(new KeyboardState(), true);
        InputFrame reverse = keyboard.Read(new KeyboardState(Keys.Left, Keys.PageUp), true);
        Assert.Equal((-1, -1), (reverse.MenuHorizontal, reverse.MenuPage));
        keyboard.Read(new KeyboardState(), true);
        InputFrame inactive = keyboard.Read(new KeyboardState(Keys.Right, Keys.PageDown), false);
        Assert.Equal((0, 0, 0f), (inactive.MenuHorizontal, inactive.MenuPage, inactive.Horizontal));
    }

    [Fact]
    public void Legacy_save_remains_loadable_but_recreation_cannot_fabricate_its_history()
    {
        PlayerSaveStore store = CreateStore();
        var session = new GameSession(new GameState { Scene = SceneKind.Field, X = 42 });
        store.Save(0, session);
        byte[] original = File.ReadAllBytes(store.GetPath(0));
        PlayerSaveEntry entry = store.ReadEntries(true)[0];
        Assert.True(entry.CanLoad);
        Assert.False(entry.CanRecreate);
        Assert.Contains("no recorded history", entry.RecreateUnavailableReason);
        Assert.Throws<InvalidOperationException>(() => store.Recreate(0, _ => throw new Exception("Replay must not run.")));
        var menu = new PlayerSaveMenu(store);
        menu.Open(PlayerSaveMenuMode.Load);
        menu.Update(Recreate(), _ => throw new Exception(), _ => throw new Exception(), _ => throw new Exception("Disabled recreation must not run."));
        Assert.True(menu.IsOpen);
        Assert.Contains("no recorded history", menu.Message);
        int loaded = -1;
        menu.Update(Confirm(), _ => throw new Exception(), slot => loaded = slot);
        Assert.Equal(0, loaded);
        Assert.False(menu.IsOpen);
        Assert.Equal(original, File.ReadAllBytes(store.GetPath(0)));
    }

    [Fact]
    public void Failed_recreation_preserves_original_save_and_running_session()
    {
        PlayerSaveStore store = CreateStore();
        GameSession session = NewRecordedGame();
        store.Save(0, session);
        byte[] original = File.ReadAllBytes(store.GetPath(0));
        GameState running = session.State;
        var menu = new PlayerSaveMenu(store);
        menu.Open(PlayerSaveMenuMode.Load);
        Assert.True(menu.CanRecreate);
        menu.Update(Recreate(), _ => throw new Exception(), _ => throw new Exception(), slot => store.Recreate(slot, _ => throw new NotSupportedException("Native replay boundary")));
        Assert.True(menu.IsOpen);
        Assert.Contains("Native replay boundary", menu.Message);
        Assert.Same(running, session.State);
        Assert.Equal(original, File.ReadAllBytes(store.GetPath(0)));
        Assert.Empty(Directory.GetFiles(Path.GetDirectoryName(store.GetPath(0))!, "*.recreate-*"));
    }

    [Fact]
    public void Recreation_replaces_only_selected_slot_and_loads_candidate_after_success()
    {
        PlayerSaveStore store = CreateStore();
        GameSession session = NewRecordedGame();
        store.Save(0, session);
        store.Save(1, session);
        byte[] manual = File.ReadAllBytes(store.GetPath(1));
        var menu = new PlayerSaveMenu(store);
        menu.Open(PlayerSaveMenuMode.Load);
        menu.Update(Recreate(), _ => throw new Exception(), _ => throw new Exception(), slot =>
        {
            GameState candidate = store.Recreate(slot, source => source with { X = 99 });
            session.Restore(candidate);
        });
        Assert.False(menu.IsOpen);
        Assert.Equal(99, session.State.X);
        Assert.Equal(99, store.Load(0).X);
        Assert.Equal(manual, File.ReadAllBytes(store.GetPath(1)));
        Assert.NotNull(store.Load(0).ReplayJournal);
        Assert.Empty(Directory.GetFiles(Path.GetDirectoryName(store.GetPath(0))!, "*.recreate-*"));
    }

    [Fact]
    public void Changed_save_and_invalid_recreation_candidate_are_never_replaced()
    {
        PlayerSaveStore store = CreateStore();
        GameSession session = NewRecordedGame();
        store.Save(0, session);
        byte[] original = File.ReadAllBytes(store.GetPath(0));
        Assert.Throws<InvalidDataException>(() => store.Recreate(0, source => source with { Scene = SceneKind.Title }));
        Assert.Equal(original, File.ReadAllBytes(store.GetPath(0)));
        Assert.Throws<IOException>(() => store.Recreate(0, source =>
        {
            store.Save(0, new GameSession(source with { X = 88 }));
            return source with { X = 99 };
        }));
        Assert.Equal(88, store.Load(0).X);
        Assert.Empty(Directory.GetFiles(Path.GetDirectoryName(store.GetPath(0))!, "*.recreate-*"));
    }

    [Fact]
    public void Journal_recreation_is_available_when_old_derived_state_cannot_load()
    {
        PlayerSaveStore store = CreateStore();
        GameSession session = NewRecordedGame();
        store.Save(0, session);
        var json = System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(store.GetPath(0)))!;
        json["WorldControls"]!["SchemaVersion"] = 999;
        File.WriteAllText(store.GetPath(0), json.ToJsonString());
        Assert.Throws<InvalidDataException>(() => store.Load(0));
        PlayerSaveEntry entry = store.ReadEntries(true)[0];
        Assert.False(entry.CanLoad);
        Assert.True(entry.CanRecreate);
        Assert.Contains("Normal load unavailable", entry.Details);
        store.Recreate(0, source => session.State with { ReplayJournal = source.ReplayJournal });
        Assert.True(store.ReadEntries(true)[0].CanLoad);
    }

    [Fact]
    public void Recreation_shortcut_is_a_press_edge_and_ignored_in_save_menu()
    {
        var keyboard = new KeyboardInput();
        Assert.True(keyboard.Read(new KeyboardState(Keys.R), true).RecreateSave);
        Assert.False(keyboard.Read(new KeyboardState(Keys.R), true).RecreateSave);
        keyboard.Read(new KeyboardState(), true);
        Assert.False(keyboard.Read(new KeyboardState(Keys.R), false).RecreateSave);
        var menu = new PlayerSaveMenu(CreateStore());
        menu.Open(PlayerSaveMenuMode.Save);
        menu.Update(Recreate(), _ => throw new Exception(), _ => throw new Exception(), _ => throw new Exception());
        Assert.True(menu.IsOpen);
        Assert.Null(menu.Message);
        Assert.False(menu.CanRecreate);
    }

    [Fact]
    public void Registered_player_namespace_protects_journals_for_all_sixty_one_slots()
    {
        string archiveDirectory = Path.Combine(directory, "journals");
        var archive = new ReplayJournalArchive(archiveDirectory);
        var store = new PlayerSaveStore(Path.Combine(directory, "research.json"), Path.Combine(directory, "player-saves"), archive);
        var protectedJournals = new List<string>();
        for (int slot = 0; slot <= PlayerSaveStore.SlotCount; slot++)
        {
            GameSession session = NewRecordedGame();
            session.Save(store.GetPath(slot), registerJournalReference: false);
            archive.Persist(session.State.ReplayJournal!);
            string journalPath = archive.GetPath(session.State.ReplayJournal!.SessionId);
            File.SetLastWriteTimeUtc(journalPath, DateTime.UtcNow.AddDays(-30));
            protectedJournals.Add(journalPath);
        }
        GameSession unreferenced = NewRecordedGame();
        archive.Persist(unreferenced.State.ReplayJournal!);
        string unreferencedPath = archive.GetPath(unreferenced.State.ReplayJournal!.SessionId);
        File.SetLastWriteTimeUtc(unreferencedPath, DateTime.UtcNow.AddDays(-30));
        Assert.Equal(1, archive.Prune(DateTimeOffset.UtcNow, TimeSpan.Zero, keepUnreferenced: 0));
        Assert.False(File.Exists(unreferencedPath));
        Assert.All(protectedJournals, path => Assert.True(File.Exists(path)));
    }

    [Fact]
    public void Archive_publication_failure_does_not_replace_selected_save()
    {
        PlayerSaveStore store = CreateStore();
        GameSession session = NewRecordedGame();
        store.Save(0, session);
        byte[] original = File.ReadAllBytes(store.GetPath(0));
        var archive = new ReplayJournalArchive(Path.Combine(directory, "journals"));
        string blocked = archive.GetPath(session.State.ReplayJournal!.SessionId);
        File.Delete(blocked);
        Directory.CreateDirectory(blocked);
        Exception failure = Assert.ThrowsAny<Exception>(() => store.Recreate(0, source => source with { X = 99 }));
        Assert.True(failure is IOException or UnauthorizedAccessException);
        Assert.Equal(original, File.ReadAllBytes(store.GetPath(0)));
        Assert.Equal(0, session.State.X);
        Assert.Empty(Directory.GetFiles(Path.GetDirectoryName(store.GetPath(0))!, "*.recreate-*"));
    }

    private static GameSession NewRecordedGame()
    {
        var session = new GameSession(new GameState());
        session.StartNewGame();
        return session;
    }

    private static InputFrame Recreate()
    {
        return new InputFrame(0, 0, false, false, false, false, RecreateSave: true);
    }

    private static void Navigate(PlayerSaveMenu menu, InputFrame input)
    {
        menu.Update(input, _ => throw new InvalidOperationException("Navigation must not save."), _ => throw new InvalidOperationException("Navigation must not load."));
    }

    private PlayerSaveStore CreateStore()
    {
        return new PlayerSaveStore(Path.Combine(directory, "research.json"), Path.Combine(directory, "player-saves"), new ReplayJournalArchive(Path.Combine(directory, "journals")));
    }

    private static InputFrame Confirm()
    {
        return new InputFrame(0, 0, true, false, false, false);
    }

    private static InputFrame Cancel()
    {
        return new InputFrame(0, 0, false, true, true, false);
    }

    [Fact]
    public void Optional_archive_registration_failure_does_not_block_menu_startup()
    {
        Directory.CreateDirectory(directory);
        string blocked = Path.Combine(directory, "archive-blocked");
        File.WriteAllText(blocked, "not a directory");
        var archive = new ReplayJournalArchive(Path.Combine(blocked, "archive"));
        var store = new PlayerSaveStore(Path.Combine(directory, "research.json"), Path.Combine(directory, "player-saves"), archive);
        Assert.Equal(61, store.ReadEntries(true).Count);
        var session = new GameSession(new GameState());
        session.StartNewGame();
        Assert.ThrowsAny<IOException>(() => store.Save(1, session));
        Assert.False(File.Exists(store.GetPath(1)));
        Assert.True(session.ReplayJournal!.IsComplete);
    }

    public void Dispose()
    {
        if (Directory.Exists(directory))
        {
            Directory.Delete(directory, true);
        }
    }
}
