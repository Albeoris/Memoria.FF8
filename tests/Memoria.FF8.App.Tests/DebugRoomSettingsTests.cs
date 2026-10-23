using System.Text.Json;
using Memoria.FF8.GameUi;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class DebugRoomSettingsTests
{
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
    public void DebugOutcomeButtonsFollowActiveGameOrLaboratoryBattleAndApplyImmediately(bool victory)
    {
        BattleSession? battle = null;
        MenuController menus = new();
        menus.ConnectBattleSettings(() => battle?.Outcome == BattleOutcome.Active, won =>
        {
            if (won)
            {
                battle!.InstantVictory();
            }
            else
            {
                battle!.InstantDefeat();
            }
        });
        menus.Open("debug-battle", true);
        Assert.False(menus.Entries.Single(row => row.Id == "instant-victory").Enabled);
        Assert.False(menus.Entries.Single(row => row.Id == "instant-defeat").Enabled);
        battle = new BattleSession(Scenario(), BattleAtbMode.TurnBased);
        Assert.True(menus.Entries.Single(row => row.Id == "instant-victory").Enabled);
        Assert.True(menus.Entries.Single(row => row.Id == "instant-defeat").Enabled);
        menus.Execute(new MenuAction("battle-debug-outcome", Value: victory ? "Victory" : "Defeat"));
        Assert.Equal(victory ? BattleOutcome.Victory : BattleOutcome.Defeat, battle.Outcome);
        Assert.False(menus.Entries.Single(row => row.Id == "instant-victory").Enabled);
        Assert.False(menus.Entries.Single(row => row.Id == "instant-defeat").Enabled);
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
    public void Default_uses_current_party_hp_and_last_enemy_or_explicit_dummy()
    {
        BattleDefinition source = Scenario();
        BattleSession current = new(source, BattleAtbMode.Normal);
        BattleDefinition defaults = BattleDebugDefaults.Create([source.Combatants[0] with { Name = "Current party" }], source.Combatants[1], 9, current);
        Assert.Equal("Current party", defaults.Combatants[0].Name);
        Assert.Equal(70, defaults.Combatants[0].CurrentHp);
        Assert.Equal("Soldier", defaults.Combatants[1].Name);
        Assert.Equal(100, defaults.Combatants[1].CurrentHp);
        Assert.Equal(9, defaults.StageId);
        BattleDefinition dummy = BattleDebugDefaults.Create([source.Combatants[0]], null, 7);
        Assert.Equal("Dummy", dummy.Combatants[1].Name);
        Assert.Equal(-1, dummy.Combatants[1].ModelId);
        _ = new BattleSession(dummy, BattleAtbMode.Normal);
    }

    [Fact]
    public void Debug_room_has_only_developer_tools_and_no_player_preference_copies()
    {
        UiPreferences preferences = new();
        int writes = 0;
        MenuController menus = new();
        menus.ConnectPreferences(() => preferences, _ => writes++);
        menus.Open("debug-room", true);
        Assert.Equal(new[] { "Models", "Battle", "Scene", "UI tools", "Session", "Automation", "Shortcuts" }, menus.Entries.Select(entry => entry.Label));
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
    public void Menu_and_editor_emit_sounds_for_selection_activation_and_cancel()
    {
        List<MenuSound> sounds = [];
        MenuController menus = new();
        menus.SoundRequested += sounds.Add;
        menus.Open("debug-room", true);
        menus.Navigate(MenuInput.Down);
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
