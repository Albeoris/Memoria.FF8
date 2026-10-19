using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;
using Microsoft.Xna.Framework.Input;

namespace Memoria.FF8.App.Tests;

public sealed class PlayerSaveTests : IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), "Memoria.FF8-player-saves-" + Guid.NewGuid().ToString("N"));

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public void Failed_continuation_validation_preserves_existing_save_and_rejects_new_slot(int slot)
    {
        PlayerSaveStore store = CreateStore();
        GameSession session = new(new GameState { Scene = SceneKind.Field, X = 10 });
        store.Save(slot, session);
        byte[] original = File.ReadAllBytes(store.GetPath(slot));
        session.SetPosition(20, 0);
        int validations = 0;
        void Reject(GameState candidate)
        {
            validations++;
            Assert.Equal(20, candidate.X);
            Assert.NotSame(session.State, candidate);
            Assert.Equal(original, File.ReadAllBytes(store.GetPath(slot)));
            throw new InvalidDataException("Unsupported continuation");
        }
        Assert.Throws<InvalidDataException>(() => store.Save(slot, session, Reject));
        Assert.Equal(1, validations);
        Assert.Equal(original, File.ReadAllBytes(store.GetPath(slot)));
        Assert.False(File.Exists(store.GetPath(slot) + ".tmp"));
        int emptySlot = slot + 2;
        Assert.Throws<NotSupportedException>(() => store.Save(emptySlot, session, _ => throw new NotSupportedException("Unsupported continuation")));
        Assert.False(File.Exists(store.GetPath(emptySlot)));
        Assert.False(File.Exists(store.GetPath(emptySlot) + ".tmp"));
        store.Save(slot, session, candidate => Assert.Equal(20, candidate.X));
        Assert.Equal(20, store.Load(slot).X);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public void Player_saves_compact_retained_memory_without_changing_roundtrip_state(int slot)
    {
        PlayerSaveStore store = CreateStore();
        byte[] bytes = Enumerable.Range(0, 16384).Select(index => (byte)(index % 256)).ToArray();
        NativeBattleRetainedMemory memory = NativeBattleRetainedMemory.FromColdProcess() with { FileStaging = NativeBattleFileStaging.Unknown().Write(NativeBattleFileStaging.Address, bytes) };
        GameSession session = new(new GameState { Scene = SceneKind.Field, FieldId = 180, FieldName = "bghoke_2", X = 12.5f, Y = 41, NativeBattleMemory = memory });
        string indentedPath = Path.Combine(directory, "indented.json");
        session.Save(indentedPath);
        int validations = 0;
        store.Save(slot, session, candidate =>
        {
            validations++;
            Assert.Equal(JsonSerializer.Serialize(session.State), JsonSerializer.Serialize(candidate));
        });
        GameState restored = store.Load(slot);
        Assert.Equal(1, validations);
        Assert.Equal(JsonSerializer.Serialize(session.State), JsonSerializer.Serialize(restored));
        Assert.Equal(bytes, restored.NativeBattleMemory!.FileStaging!.Read(NativeBattleFileStaging.Address, bytes.Length));
        Assert.True(new FileInfo(store.GetPath(slot)).Length < new FileInfo(indentedPath).Length / 2);
        Assert.False(File.Exists(store.GetPath(slot) + ".tmp"));
    }

    [Fact]
    public void Save_rejects_state_that_passes_live_validation_but_cannot_load()
    {
        PlayerSaveStore store = CreateStore();
        GameSession session = new(new GameState { Scene = SceneKind.Field, X = 10 });
        store.Save(0, session);
        byte[] original = File.ReadAllBytes(store.GetPath(0));
        session.SetEventState(System.Text.Json.JsonSerializer.SerializeToElement(new { }));
        Assert.Throws<InvalidDataException>(() => store.Save(0, session));
        Assert.Equal(original, File.ReadAllBytes(store.GetPath(0)));
        Assert.False(File.Exists(store.GetPath(0) + ".tmp"));
    }

    [Fact]
    public void Player_slots_and_quick_save_do_not_replace_research_checkpoint()
    {
        string research = Path.Combine(directory, "research.json");
        Directory.CreateDirectory(directory);
        File.WriteAllText(research, "research evidence");
        var store = new PlayerSaveStore(research, Path.Combine(directory, "player-saves"));
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
        Assert.Equal(101, entries.Count);
        Assert.True(entries[0].CanLoad);
        Assert.Equal("Unavailable", entries[1].Details);
        Assert.NotNull(entries[1].SavedAt);
        Assert.False(entries[2].CanLoad);
    }

    [Theory]
    [InlineData(0u, "00:00:00")]
    [InlineData(90061u, "25:01:01")]
    public void Slot_metadata_uses_saved_native_time_and_resolves_each_saved_location(uint seconds, string expectedTime)
    {
        var resolvedFields = new List<int>();
        var store = new PlayerSaveStore(Path.Combine(directory, "research.json"), Path.Combine(directory, "player-saves"), saved =>
        {
            resolvedFields.Add(saved.FieldId);
            return saved.FieldId == 180 ? "Balamb Garden- Infirmary" : "Balamb Garden- Hall";
        });
        var session = new GameSession(new GameState { Scene = SceneKind.Field, FieldId = 180, Playtime = new NativePlaytimeState(seconds, 0, 0), ExamTimer = NativeExamTimerState.FromColdProcess() });
        store.Save(1, session);
        store.Save(2, new GameSession(new GameState { Scene = SceneKind.Field, FieldId = 181 }));
        session.SetPosition(99, 48);
        IReadOnlyList<PlayerSaveEntry> entries = store.ReadEntries(false);
        Assert.Equal(new[] { 180, 181 }, resolvedFields);
        Assert.Equal("Balamb Garden- Infirmary", entries[0].Details);
        Assert.Equal("Balamb Garden- Hall", entries[1].Details);
        Assert.Equal(TimeSpan.FromSeconds(seconds), entries[0].PlayTime);
        Assert.Equal(expectedTime, entries[0].PlayTimeText);
        Assert.Null(entries[1].PlayTime);
        Assert.Equal("--:--:--", entries[1].PlayTimeText);
        Assert.Empty(entries[0].PartyPortraits);
        Assert.True(entries[1].CanLoad);
    }

    [Fact]
    public void Slot_portraits_preserve_saved_party_order_and_skip_empty_slots()
    {
        Assert.SkipUnless(Directory.Exists(TestResources.GameDirectory), "Installed character resources are required.");
        using GameAssets assets = new(TestResources.GameDirectory);
        NativeCharacterVitals initial = assets.ReadNewGameCharacterVitals();
        NativeCharacterVitals vitals = initial with { CombatParty = [1, 255, 0] };
        PlayerSaveStore store = CreateStore();
        store.Save(1, new GameSession(new GameState { Scene = SceneKind.Field, CharacterVitals = vitals }));
        PlayerSaveEntry entry = store.ReadEntries(false)[0];
        Assert.True(entry.CanLoad);
        Assert.Equal(new[] { (int)vitals.Records[1].CharacterId, vitals.Records[0].CharacterId }, entry.PartyPortraits);
        Assert.Equal(vitals.CombatParty, store.Load(1).CharacterVitals!.CombatParty);
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
        var conflicting = new PlayerSaveStore(store.GetPath(1), Path.GetDirectoryName(store.GetPath(1)));
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
    public void One_hundred_Memoria_slots_preserve_existing_paths_and_keep_original_banks_separate()
    {
        PlayerSaveStore store = CreateStore();
        Assert.Equal((3, 30, 100), (PlayerSaveStore.BankCount, PlayerSaveStore.OriginalSlotsPerBank, PlayerSaveStore.SlotCount));
        Assert.Equal("slot-01.json", Path.GetFileName(store.GetPath(1)));
        Assert.Equal("slot-02.json", Path.GetFileName(store.GetPath(2)));
        Assert.Equal("slot-03.json", Path.GetFileName(store.GetPath(3)));
        Assert.Equal("quick.json", Path.GetFileName(store.GetPath(0)));
        Assert.Equal("Memoria, Slot 30", PlayerSaveStore.GetLabel(30));
        Assert.Equal("Memoria, Slot 31", PlayerSaveStore.GetLabel(31));
        Assert.Equal("Memoria, Slot 60", PlayerSaveStore.GetLabel(60));
        Assert.Equal("Memoria, Slot 100", PlayerSaveStore.GetLabel(100));
        Assert.Equal("ORIGINAL FF8 I, Slot 01", PlayerSaveStore.GetLabel(101));
        Assert.Equal("ORIGINAL FF8 II, Slot 30", PlayerSaveStore.GetLabel(160));
        store.Save(0, new GameSession(new GameState { Scene = SceneKind.Field, X = 5 }));
        store.Save(30, new GameSession(new GameState { Scene = SceneKind.Field, X = 30 }));
        store.Save(31, new GameSession(new GameState { Scene = SceneKind.Field, X = 31 }));
        store.Save(60, new GameSession(new GameState { Scene = SceneKind.Field, X = 60 }));
        store.Save(100, new GameSession(new GameState { Scene = SceneKind.Field, X = 100 }));
        Assert.Equal(5, store.Load(0).X);
        Assert.Equal(30, store.Load(30).X);
        Assert.Equal(31, store.Load(31).X);
        Assert.Equal(60, store.Load(60).X);
        Assert.Equal(100, store.Load(100).X);
        Assert.Throws<ArgumentOutOfRangeException>(() => store.GetPath(161));
        IReadOnlyList<PlayerSaveEntry> entries = store.ReadEntries(false);
        Assert.Equal(100, entries.Count);
        Assert.All(entries, entry => Assert.InRange(entry.Slot, 1, 100));
    }

    [Fact]
    public void Bank_and_page_navigation_reaches_last_slot_and_remembers_each_mode_independently()
    {
        var menu = new PlayerSaveMenu(CreateStore());
        menu.Open(PlayerSaveMenuMode.Save);
        Assert.Equal(new[] { 1, 2, 3, 4, 5 }, menu.VisibleEntries.Select(entry => entry.Slot));
        for (int page = 0; page < 19; page++)
        {
            Navigate(menu, new InputFrame(0, 0, false, false, false, false, MenuPage: 1));
        }
        for (int row = 0; row < 4; row++)
        {
            Navigate(menu, new InputFrame(0, 0, false, false, false, false, MenuVertical: 1));
        }
        Assert.Equal((0, 19, 100), (menu.Bank, menu.Page, menu.SelectedSlot));
        Navigate(menu, new InputFrame(0, 0, false, false, false, false, MenuHorizontal: 1));
        Assert.Equal((1, 5, 130), (menu.Bank, menu.Page, menu.SelectedSlot));
        Assert.Equal(new[] { 126, 127, 128, 129, 130 }, menu.VisibleEntries.Select(entry => entry.Slot));
        Navigate(menu, new InputFrame(0, 0, false, false, false, false, MenuPage: 1, MenuVertical: 1));
        Assert.Equal((1, 5, 130), (menu.Bank, menu.Page, menu.SelectedSlot));
        menu.Close();

        menu.Open(PlayerSaveMenuMode.Load);
        Assert.Equal(0, menu.SelectedSlot);
        Assert.Equal(0, menu.QuickEntry!.Slot);
        Navigate(menu, new InputFrame(0, 0, false, false, false, false, MenuHorizontal: 1));
        Navigate(menu, new InputFrame(0, 0, false, false, false, false, MenuPage: 1));
        Navigate(menu, new InputFrame(0, 0, false, false, false, false, MenuVertical: 1));
        Assert.Equal((1, 1, 107), (menu.Bank, menu.Page, menu.SelectedSlot));
        menu.Open(PlayerSaveMenuMode.Save);
        Assert.Equal((1, 5, 130), (menu.Bank, menu.Page, menu.SelectedSlot));
        Assert.Null(menu.QuickEntry);
        menu.Open(PlayerSaveMenuMode.Load);
        Assert.Equal((1, 1, 107), (menu.Bank, menu.Page, menu.SelectedSlot));
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
        Assert.Equal(PlayerSaveMenuMode.QuickLoad, menu.Mode);
        menu.Update(Confirm(), _ => throw new InvalidOperationException(), _ => throw new InvalidDataException("Invalid VM continuation"));
        Assert.Contains("Invalid VM continuation", menu.Message);
        int loaded = -1;
        menu.Update(Confirm(), _ => throw new InvalidOperationException(), slot => loaded = slot);
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

    private static void Navigate(PlayerSaveMenu menu, InputFrame input)
    {
        menu.Update(input, _ => throw new InvalidOperationException("Navigation must not save."), _ => throw new InvalidOperationException("Navigation must not load."));
    }

    private PlayerSaveStore CreateStore()
    {
        return new PlayerSaveStore(Path.Combine(directory, "research.json"), Path.Combine(directory, "player-saves"));
    }

    private static InputFrame Confirm()
    {
        return new InputFrame(0, 0, true, false, false, false);
    }

    private static InputFrame Cancel()
    {
        return new InputFrame(0, 0, false, true, true, false);
    }

    public void Dispose()
    {
        if (Directory.Exists(directory))
        {
            Directory.Delete(directory, true);
        }
    }
}
