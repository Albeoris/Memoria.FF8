using System.Text.Json;
using Memoria.FF8.GameUi;
using Memoria.FF8.Runtime;
using Memoria.FF8.Ui;
using Microsoft.Xna.Framework.Input;

namespace Memoria.FF8.App.Tests;

public sealed class DebugRoomSettingsTests
{
    [Fact]
    public void Additional_enemies_slider_keeps_party_bonus_out_of_its_hardcore_minimum()
    {
        UiPreferences preferences = new() { BattleAllyCount = 8, Difficulty = BattleDifficulty.Hardcore };
        MenuController menus = new(() =>
        {
            MenuData data = new();
            data.Settings["Additional enemies"] = preferences.AdditionalEnemies.ToString(System.Globalization.CultureInfo.InvariantCulture);
            data.SupportedActions.Add("config:Additional enemies");
            return data;
        }, action => preferences = preferences.WithSetting(action.SubjectId, action.Value));
        menus.ConnectPreferences(() => preferences, action => preferences = preferences.WithSetting(action.SubjectId, action.Value));
        menus.Open("config-combat");
        menus.SelectEntry("config-combat", "Additional enemies");

        UiNumericSlider slider = Assert.IsType<UiNumericSlider>(menus.BuildView().FocusedElement);
        Assert.Equal(0, slider.Value);
        Assert.Equal(1f, slider.ForcedMinimum);
        Assert.True(string.IsNullOrEmpty(slider.Caption));
        Assert.Equal(10, slider.Max);
        Assert.Equal("Adds enemies where the encounter and area allow it.\n+1 enemy per party member beyond three. Hardcore: minimum 1.", menus.SelectedEntry!.Help);
        menus.Navigate(MenuInput.Confirm);
        menus.Navigate(MenuInput.Left);
        Assert.Equal(0, preferences.AdditionalEnemies);
        menus.Navigate(MenuInput.Right);
        Assert.Equal(1, preferences.AdditionalEnemies);
        Assert.Equal(1f, Assert.IsType<UiNumericSlider>(menus.BuildView().FocusedElement).ForcedMinimum);

        UiView view = menus.BuildView();
        slider = Assert.IsType<UiNumericSlider>(view.FocusedElement);
        float trackRight = slider.ValueBounds.Right - slider.ValueWidth - slider.ValueGap;
        view.Pointer(trackRight - 1, slider.Bounds.Y + slider.Bounds.Height / 2, pressed: true);
        Assert.Equal(10, preferences.AdditionalEnemies);
        Assert.Equal(1f, Assert.IsType<UiNumericSlider>(menus.BuildView().FocusedElement).ForcedMinimum);
        Assert.Equal(10, preferences.WithInstallationSetting("Additional enemies", "2", activeGame: true).AdditionalEnemies);
        Assert.Equal(2, preferences.WithInstallationSetting("Additional enemies", "2", activeGame: false).AdditionalEnemies);
    }

    [Theory]
    [InlineData(BattleDifficulty.Normal, 3, 0)]
    [InlineData(BattleDifficulty.Normal, 5, 0)]
    [InlineData(BattleDifficulty.Hardcore, 3, 1)]
    [InlineData(BattleDifficulty.Hardcore, 5, 1)]
    public void Additional_enemies_help_and_minimum_follow_difficulty_without_party_scaling(BattleDifficulty difficulty, int allies, float minimum)
    {
        UiPreferences preferences = new() { BattleAllyCount = allies, Difficulty = difficulty, AdditionalEnemies = 5 };
        MenuController menus = new(() =>
        {
            MenuData data = new();
            data.Settings["Additional enemies"] = "5";
            data.SupportedActions.Add("config:Additional enemies");
            return data;
        }, _ => { });
        menus.ConnectPreferences(() => preferences, _ => { });
        menus.Open("config-combat");
        menus.SelectEntry("config-combat", "Additional enemies");
        UiNumericSlider slider = Assert.IsType<UiNumericSlider>(menus.BuildView().FocusedElement);
        Assert.Equal(minimum, slider.ForcedMinimum);
        Assert.True(string.IsNullOrEmpty(slider.Caption));
        string help = menus.SelectedEntry!.Help;
        Assert.StartsWith("Adds enemies where the encounter and area allow it.\n+1 enemy per party member beyond three.", help);
        Assert.Equal(difficulty == BattleDifficulty.Hardcore, help.Contains("Hardcore: minimum 1.", StringComparison.Ordinal));
    }

    [Fact]
    public void LaboratoryContainsOnlyScenarioControlsAndChangingItsAtbModeIsLocal()
    {
        BattleDebugController debug = Controller();
        Assert.DoesNotContain(debug.Rows, row => row.Label.Contains("policy", StringComparison.OrdinalIgnoreCase) || row.Label.Contains("random", StringComparison.OrdinalIgnoreCase));
        Activate(debug, "Scenario ATB mode");
        Assert.Equal(BattleAtbMode.TurnBased, debug.Mode);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Debug_outcome_buttons_request_the_selected_result_when_enabled(bool victory)
    {
        bool active = false;
        bool? requestedVictory = null;
        MenuController menus = new();
        menus.ConnectBattleSettings(() => active, won =>
        {
            requestedVictory = won;
            active = false;
        });
        menus.Open("debug-room", true);
        Assert.DoesNotContain(menus.Entries, row => row.Id is "instant-victory" or "instant-defeat");
        active = true;
        Assert.Equal(new[] { "instant-victory", "instant-defeat" }, menus.Entries.Take(2).Select(row => row.Id));
        Assert.True(menus.Entries.Single(row => row.Id == "instant-victory").Enabled);
        Assert.True(menus.Entries.Single(row => row.Id == "instant-defeat").Enabled);
        menus.Execute(new MenuAction("battle-debug-outcome", Value: victory ? "Victory" : "Defeat"));
        Assert.Equal(victory, requestedVictory);
        Assert.DoesNotContain(menus.Entries, row => row.Id is "instant-victory" or "instant-defeat");
        menus.Open("debug-battle", true);
        Assert.DoesNotContain(menus.Entries, row => row.Id is "instant-victory" or "instant-defeat");
    }

    [Fact]
    public void InstantOutcomesStayVisibleButCannotExecuteDuringOpponentTurn()
    {
        bool playerTurn = false;
        bool active = true;
        int completions = 0;
        MenuController menus = new();
        menus.ConnectInstantOutcomes(() => active, () => active && playerTurn, _ => completions++);
        menus.Open("debug-room", true);
        Assert.All(menus.Entries.Take(2), row => Assert.False(row.Enabled));
        menus.Execute(new("battle-debug-outcome", Value: "Victory"));
        Assert.Equal(0, completions);
        playerTurn = true;
        Assert.All(menus.Entries.Take(2), row => Assert.True(row.Enabled));
        menus.Execute(new("battle-debug-outcome", Value: "Defeat"));
        Assert.Equal(1, completions);
        active = false;
        Assert.DoesNotContain(menus.Entries, row => row.Id is "instant-victory" or "instant-defeat");
    }

    [Fact]
    public void Instant_battle_outcomes_require_an_entered_process_even_if_a_player_is_ready()
    {
        Assert.False(RecreateGame.CanCompleteBattleInstantly(null));
        BattleDefinition definition = Scenario();
        BattleSession waiting = new(definition, BattleAtbMode.Normal);
        Assert.False(RecreateGame.CanCompleteBattleInstantly(waiting));
        BattleSession ready = new(definition with { Combatants = [definition.Combatants[0] with { InitialAtb = BattleSession.AtbMaximum }, definition.Combatants[1]] }, BattleAtbMode.Normal);
        Assert.False(RecreateGame.CanCompleteBattleInstantly(ready));
    }

    [Fact]
    public void Actor_details_open_local_groups_without_changing_scenario()
    {
        BattleDebugController debug = Controller();
        JsonElement before = JsonSerializer.SerializeToElement(debug.Definition);
        Activate(debug, "Editor panel");
        Assert.Contains(debug.Rows, row => row.Label == "Enemy 1");
        Assert.DoesNotContain(debug.Rows, row => row.Label == "Enemy 1 · Attack");
        Activate(debug, "Enemy 1");
        Assert.Contains(debug.Rows, row => row.Label == "Enemy 1 · Character / model");
        Activate(debug, "Actor details");
        Assert.Contains(debug.Rows, row => row.Label == "Enemy 1 · Attack");
        debug.Navigate(MenuInput.Cancel);
        Assert.DoesNotContain(debug.Rows, row => row.Label == "Enemy 1 · Attack");
        Assert.Equal("Enemy 1", debug.Rows[debug.SelectedIndex].Label);
        Assert.True(JsonElement.DeepEquals(before, JsonSerializer.SerializeToElement(debug.Definition)));
        Assert.DoesNotContain(debug.Rows, row => row.Label == "Party 1 · Attack");
    }

    [Fact]
    public void Explicit_presets_round_trip_and_restore_edited_scenario_in_isolation()
    {
        string directory = Path.Combine(Path.GetTempPath(), "ff8-debug-presets-" + Guid.NewGuid().ToString("N"));
        string path = Path.Combine(directory, "battle-presets.json");
        try
        {
            BattleDebugController editor = Controller(new BattleDebugPresets(path));
            Activate(editor, "Enemy 1");
            Activate(editor, "Enemy 1 · Attack");
            Activate(editor, "Scenario ATB mode");
            Activate(editor, "Save preset");
            BattleDebugController restored = Controller(new BattleDebugPresets(path));
            Assert.Equal(20, restored.Definition.Combatants[1].Attack);
            Activate(restored, "Load preset");
            Assert.Equal(25, restored.Definition.Combatants[1].Attack);
            Assert.Equal(BattleAtbMode.TurnBased, restored.Mode);
            Activate(restored, "Current party + last enemy");
            Assert.Equal(20, restored.Definition.Combatants[1].Attack);
            BattleDefinition isolated = new BattleDebugPresets(path).Read(0)!.Definition;
            isolated.Combatants[0] = isolated.Combatants[0] with { Name = "Changed" };
            Assert.Equal("Squall", new BattleDebugPresets(path).Read(0)!.Definition.Combatants[0].Name);
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
                Directory.Delete(directory);
            }
        }
    }

    [Fact]
    public void Default_uses_current_party_hp_and_selected_enemy()
    {
        BattleDefinition source = Scenario();
        BattleSession current = new(source, BattleAtbMode.Normal);
        BattleDefinition defaults = BattleDebugDefaults.Create([source.Combatants[0] with { Name = "Current party" }], source.Combatants[1], 9, current);
        Assert.Equal("Current party", defaults.Combatants[0].Name);
        Assert.Equal(70, defaults.Combatants[0].CurrentHp);
        Assert.Equal("Soldier", defaults.Combatants[1].Name);
        Assert.Equal(100, defaults.Combatants[1].CurrentHp);
        Assert.Equal(9, defaults.StageId);
    }

    [Fact]
    public void Debug_room_exposes_gameplay_rules_and_keeps_gallery_preferences_isolated()
    {
        UiPreferences preferences = new();
        int writes = 0;
        MenuController menus = new();
        menus.ConnectPreferences(() => preferences, _ => writes++);
        menus.Open("debug-room", true);
        Assert.Equal(new[] { "Start FPS trace", "Mods", "Localization", "Gameplay rules", "Textures", "Models", "Battle", "3D field scenes", "Scene", "UI tools", "Session", "Automation", "Shortcuts", "Switch world: FFVII", "Quit" }, menus.Entries.Select(entry => entry.Label));
        menus.SelectEntry("debug-room", "debug-gallery");
        menus.Navigate(MenuInput.Confirm);
        Assert.Equal("debug-gallery", menus.ScreenId);
        Assert.False(menus.IsFixtureContext);
        menus.Push("config-text");
        Assert.True(menus.IsFixtureContext);
        menus.Execute(new("config", "Text scale", Value: "150%"));
        Assert.Equal(0, writes);
        Assert.Equal(100, preferences.TextScale);
    }

    [Fact]
    public void FpsTraceStartsOnceAfterConfirmClosesTheDebugRoom()
    {
        MenuController menu = new();
        KeyboardInput keyboard = new();
        int starts = 0;
        menu.PerformanceTraceRequested += () =>
        {
            Assert.False(menu.IsOpen);
            starts++;
        };

        keyboard.Read(new KeyboardState(Keys.F2), true);
        Assert.True(keyboard.DebugRoomRequested);
        menu.Open("debug-room", true);
        menu.SelectEntry("debug-room", "start-fps-trace");
        Assert.True(menu.SelectedEntry!.Enabled);
        Assert.Equal(0, starts);

        KeyboardState confirm = new(Keys.X);
        keyboard.Read(confirm, true);
        Assert.True(keyboard.UiConfirmRequested);
        menu.Navigate(MenuInput.Confirm);
        Assert.Equal(1, starts);

        keyboard.Read(confirm, true);
        Assert.False(keyboard.UiConfirmRequested);
        menu.Navigate(MenuInput.Confirm);
        Assert.Equal(1, starts);
    }

    [Fact]
    public void FpsTraceIsDisabledWithoutARecordingHost()
    {
        MenuController menu = new();
        menu.Open("debug-room", true);
        menu.SelectEntry("debug-room", "start-fps-trace", allowDisabled: true);
        Assert.False(menu.SelectedEntry!.Enabled);
        menu.Navigate(MenuInput.Confirm);
        Assert.True(menu.IsOpen);
        Assert.Equal("debug-room", menu.ScreenId);
    }

    [Fact]
    public void QuitIsLastAndOffersTitleOrExitBeforeRequestingExitFromTheHost()
    {
        int requests = 0;
        MenuController menus = new();
        menus.QuitRequested += () => requests++;
        menus.Open("debug-room", true);

        Assert.Equal("quit", menus.Entries[^1].Id);
        Assert.True(menus.Entries[^1].Enabled);
        menus.SelectEntry("debug-room", "quit");
        Assert.Equal(0, requests);
        menus.Navigate(MenuInput.Confirm);

        Assert.Equal(0, requests);
        Assert.Equal("debug-quit", menus.ScreenId);
        Assert.Equal(new[] { "title", "exit" }, menus.Entries.Select(entry => entry.Id));
        Assert.False(menus.Entries[0].Enabled);
        menus.SelectEntry("debug-quit", "exit");
        menus.Navigate(MenuInput.Confirm);
        Assert.Equal(1, requests);
    }

    [Fact]
    public void QuitChoiceCanBeCancelledOrReturnToTitleWithoutExiting()
    {
        int titles = 0;
        int exits = 0;
        MenuController menus = new();
        menus.ReturnToTitleRequested += () => titles++;
        menus.QuitRequested += () => exits++;
        menus.Open("debug-room", true);
        menus.SelectEntry("debug-room", "quit");
        menus.Navigate(MenuInput.Confirm);
        Assert.Equal("title", menus.SelectedEntry?.Id);
        menus.Navigate(MenuInput.Cancel);
        Assert.Equal("debug-room", menus.ScreenId);
        Assert.Equal("quit", menus.SelectedEntry?.Id);
        Assert.Equal(0, titles);
        Assert.Equal(0, exits);
        menus.Navigate(MenuInput.Confirm);
        menus.Navigate(MenuInput.Confirm);
        Assert.Equal(1, titles);
        Assert.Equal(0, exits);
    }

    [Fact]
    public void Menu_and_editor_emit_sounds_for_selection_activation_and_cancel()
    {
        List<MenuSound> sounds = [];
        MenuController menus = new();
        menus.SoundRequested += sounds.Add;
        menus.Open("debug-room", true);
        menus.SelectEntry("debug-room", "debug-gallery");
        menus.Navigate(MenuInput.Confirm);
        menus.Navigate(MenuInput.Cancel);
        Assert.Equal(new[] { MenuSound.Move, MenuSound.Confirm, MenuSound.Cancel }, sounds);
        sounds.Clear();
        BattleDebugController editor = Controller();
        editor.SoundRequested += sounds.Add;
        editor.Navigate(MenuInput.Down);
        editor.Navigate(MenuInput.Confirm);
        editor.Navigate(MenuInput.Cancel);
        Assert.Equal(new[] { MenuSound.Move, MenuSound.Confirm, MenuSound.Cancel }, sounds);
    }

    [Fact]
    public void Debug_shortcuts_write_host_profile_while_gallery_controls_stay_isolated()
    {
        ControlsProfile profile = ControlsProfile.CreateDefault();
        int writes = 0;
        MenuController menus = new();
        menus.ConnectControls(() => profile, next =>
        {
            profile = next;
            writes++;
        });
        menus.Open("debug-shortcuts", true);
        menus.Execute(new("controls-select", nameof(ControlAction.DebugRoom), Value: nameof(ControlDevice.Keyboard)));
        menus.Execute(new("controls-clear"));
        Assert.Equal(1, writes);
        Assert.Empty(profile.GetBindings(ControlAction.DebugRoom, ControlDevice.Keyboard));
        menus.Open("config-keyboard", true);
        menus.Execute(new("controls-select", nameof(ControlAction.QuickSave), Value: nameof(ControlDevice.Keyboard)));
        menus.Execute(new("controls-clear"));
        Assert.Equal(1, writes);
        Assert.NotEmpty(profile.GetBindings(ControlAction.QuickSave, ControlDevice.Keyboard));
    }

    [Fact]
    public void Missing_current_model_cycles_to_first_available_model_without_skipping_it()
    {
        BattleDebugCatalog catalog = new() { Enemies = [new() { Id = "first", Name = "First choice", ModelId = 4 }, new() { Id = "second", Name = "Second choice", ModelId = 8 }] };
        BattleDebugController debug = new(Scenario(), catalog, BattleAtbMode.Normal, (_, _) => { }, () => { });
        Activate(debug, "Enemy 1");
        Activate(debug, "Enemy 1 · Character / model");
        Assert.Equal(4, debug.Definition.Combatants[1].ModelId);
    }

    private static BattleDebugController Controller(BattleDebugPresets? presets = null)
    {
        return new BattleDebugController(Scenario(), new BattleDebugCatalog(), BattleAtbMode.Normal, (_, _) => { }, () => { }, presets);
    }

    private static void Activate(BattleDebugController debug, string label)
    {
        if (!debug.Rows.Any(row => row.Label == label))
        {
            if (label.Contains(" · ", StringComparison.Ordinal))
            {
                for (int attempt = 0; attempt < 4 && !debug.Rows.Any(row => row.Label == label); attempt++)
                {
                    Activate(debug, "Actor details");
                }
            }
            else
            {
                string panel = label is "Save preset" or "Load preset" or "Preset slot" ? "Presets" : label.StartsWith("Enemy ", StringComparison.Ordinal) || label.StartsWith("Party ", StringComparison.Ordinal) ? "Participants" : "Scenario";
                while (debug.Rows.Single(row => row.Label == "Editor panel").Value != panel)
                {
                    Activate(debug, "Editor panel");
                }
            }
        }
        debug.Select(debug.Rows.Select((row, index) => (row, index)).Single(pair => pair.row.Label == label).index, true);
    }

    private static BattleDefinition Scenario()
    {
        return new BattleDefinition { StageId = 7, Combatants = [new() { Id = "party-0", Name = "Squall", IsPlayer = true, CurrentHp = 70 }, new() { Id = "enemy-0", Name = "Soldier", CurrentHp = 30 }] };
    }
}
