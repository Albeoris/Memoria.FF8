using System.Text.Json;
using Memoria.FF8.GameUi;
using Memoria.FF8.Runtime;
using Memoria.FF8.Ui;

namespace Memoria.FF8.App.Tests;

public sealed class BattleUiTests
{
    [Fact]
    public void Attack_requires_target_confirmation_and_uses_host_command_callback()
    {
        BattleSession session = new(Definition(), BattleAtbMode.TurnBased);
        List<BattleAttackCommand> commands = [];
        BattleHudController hud = new(session, (actor, target) => commands.Add(new(actor, target)), () => { }, () => { });
        hud.Navigate(MenuInput.Confirm);
        Assert.True(hud.SelectingTarget);
        Assert.Empty(commands);
        hud.Navigate(MenuInput.Down);
        hud.Navigate(MenuInput.Confirm);
        Assert.Equal(new BattleAttackCommand("party-0", "enemy-1"), Assert.Single(commands));
        Assert.False(hud.SelectingTarget);
        Assert.Empty(session.QueuedActions);
        Assert.Equal(BattleSession.AtbMaximum, session.Combatants[0].Atb);
    }

    [Fact]
    public void Ready_sleeping_player_waits_for_automatic_status_expiry_before_command_selection()
    {
        BattleDefinition definition = Definition();
        definition.Combatants[0] = definition.Combatants[0] with { StatusEffects = [new BattleStatusEffectDefinition { Kind = BattleStatusKind.Sleep, DurationFrames = 1 }] };
        BattleSession session = new(definition, BattleAtbMode.TurnBased);
        BattleHudController hud = new(session, session.QueueAttack, () => { }, () => session.AdvanceTurn());
        Assert.Null(hud.SelectedActor);
        Assert.Empty(hud.Commands);
        hud.SelectParty("party-0");
        Assert.Null(hud.SelectedActor);
        session.Tick();
        Assert.Equal(1, session.Frame);
        Assert.Equal("party-0", hud.SelectedActor!.Id);
        hud.Navigate(MenuInput.Confirm);
        Assert.True(hud.SelectingTarget);
    }

    [Fact]
    public void Cancel_returns_to_commands_and_advance_only_dispatches_in_turn_based_mode()
    {
        BattleSession session = new(Definition(), BattleAtbMode.TurnBased);
        int advances = 0;
        BattleHudController hud = new(session, (_, _) => { }, () => { }, () => advances++);
        hud.Navigate(MenuInput.Confirm);
        hud.Navigate(MenuInput.Cancel);
        Assert.False(hud.SelectingTarget);
        hud.AdvanceTurn();
        Assert.Equal(1, advances);
        session.SetMode(BattleAtbMode.Normal);
        hud.AdvanceTurn();
        Assert.Equal(1, advances);
    }

    [Fact]
    public void Debug_scenario_changes_do_not_mutate_source_definition_and_preserve_stage_identity()
    {
        BattleDefinition original = Definition() with { StageId = 7, EncounterId = 42 };
        BattleDefinition? started = null;
        BattleDebugController debug = new(original, new BattleDebugCatalog(), BattleAtbMode.Normal, (definition, _) => started = definition, () => { });
        SelectRow(debug, "Party 1");
        int attackRow = debug.Rows.Select((row, index) => (row, index)).Single(pair => pair.row.Label == "Party 1 · Attack").index;
        debug.Select(attackRow, true);
        debug.Start();
        Assert.NotNull(started);
        Assert.Equal(original.Combatants[0].Attack + 5, started.Combatants[0].Attack);
        Assert.Equal(20, original.Combatants[0].Attack);
        Assert.Equal(7, started.StageId);
        Assert.Equal(42, started.EncounterId);
        _ = new BattleSession(started, BattleAtbMode.Normal);
    }

    [Fact]
    public void Terminal_confirm_and_pointer_use_completion_callback_without_battle_mutation()
    {
        BattleSession session = new(Definition(), BattleAtbMode.Normal);
        int completions = 0;
        BattleHudController hud = new(session, (_, _) => { }, () => { }, () => { }, () => completions++);
        hud.Complete();
        Assert.Equal(0, completions);
        session.InstantVictory();
        long frame = session.Frame;
        hud.Navigate(MenuInput.Confirm);
        hud.BuildView().Pointer(100, 560, true);
        Assert.Equal(1, completions);
        Assert.Equal(frame, session.Frame);
        Assert.Equal(BattleOutcome.Victory, session.Outcome);
    }

    [Fact]
    public void Victory_rewards_preview_is_cached_and_paged_without_applying_until_confirm()
    {
        BattleSession session = new(Definition(), BattleAtbMode.TurnBased);
        BattleRewardSummary rewards = new(new Dictionary<byte, ushort> { [0] = 123 }, Enumerable.Range(0, 9).ToDictionary(id => (byte)id, id => (ushort)(id + 10)), Enumerable.Range(0, 9).ToDictionary(id => id, _ => (ushort)3), [new BattleStock(7, 2)], [4]);
        int previews = 0;
        int completions = 0;
        BattleHudController hud = new(session, session.QueueAttack, () => { }, () => session.AdvanceTurn(), () => completions++, previewRewards: () =>
        {
            previews++;
            return rewards;
        }, guardianForceName: id => $"Guardian {id}", itemName: _ => "Potion", cardName: _ => "Bite Bug");
        Assert.Empty(hud.RewardLines);
        Assert.Equal(0, previews);
        session.InstantVictory();
        string before = JsonSerializer.Serialize(session.Capture());
        Assert.Contains("Squall   EXP +123", hud.VisibleRewardLines);
        Assert.Contains("Guardian 0   EXP +10   AP +3", hud.VisibleRewardLines);
        Assert.Equal(2, hud.RewardPageCount);
        Assert.Equal(8, hud.VisibleRewardLines.Count);
        hud.Navigate(MenuInput.Right);
        Assert.Equal(1, hud.RewardPage);
        Assert.Contains("Guardian 8   EXP +18   AP +3", hud.VisibleRewardLines);
        Assert.Contains("Potion   x2", hud.VisibleRewardLines);
        Assert.Contains("Bite Bug   x1", hud.VisibleRewardLines);
        TestCanvas canvas = new();
        hud.BuildView().Draw(canvas, new UiTheme());
        Assert.Contains("VICTORY · REWARDS", canvas.Texts);
        Assert.Contains("Potion   x2", canvas.Texts);
        Assert.Equal(1, previews);
        Assert.Equal(0, completions);
        Assert.Equal(before, JsonSerializer.Serialize(session.Capture()));
        hud.Navigate(MenuInput.Left);
        Assert.Equal(0, hud.RewardPage);
        hud.Navigate(MenuInput.Confirm);
        hud.Complete();
        Assert.Equal(1, completions);
        Assert.Equal(1, previews);
    }

    [Fact]
    public void Standalone_victory_explains_rewards_are_not_saved()
    {
        BattleSession session = new(Definition(), BattleAtbMode.Normal);
        BattleHudController hud = new(session, session.QueueAttack, () => { }, () => { });
        session.InstantVictory();
        Assert.Equal("Standalone battle: rewards are not saved.", Assert.Single(hud.RewardLines));
        Assert.Equal(1, hud.RewardPageCount);
        hud.ChangeRewardPage(1);
        Assert.Equal(0, hud.RewardPage);
    }

    [Theory]
    [InlineData("unsupported")]
    [InlineData("data")]
    [InlineData("argument")]
    [InlineData("io")]
    public void Rejected_debug_scenario_displays_error_and_preserves_existing_battle(string failure)
    {
        BattleDefinition original = Definition();
        JsonElement originalBefore = JsonSerializer.SerializeToElement(original);
        BattleSession session = new(original, BattleAtbMode.Normal);
        BattleSnapshot before = session.Capture();
        BattleDebugController debug = new(original, new BattleDebugCatalog(), BattleAtbMode.Normal, (_, _) => throw failure switch
        {
            "unsupported" => new NotSupportedException("Unsupported costume"),
            "data" => new InvalidDataException("Invalid resource"),
            "argument" => new ArgumentException("Invalid definition"),
            _ => new IOException("Cannot read model")
        }, () => { });
        List<MenuSound> sounds = [];
        debug.SoundRequested += sounds.Add;
        debug.Start();
        Assert.StartsWith("Cannot start scenario: ", debug.Message);
        Assert.Empty(sounds);
        Assert.Equal(before.Frame, session.Frame);
        Assert.Equal(before.Outcome, session.Outcome);
        Assert.True(JsonElement.DeepEquals(JsonSerializer.SerializeToElement(before), JsonSerializer.SerializeToElement(session.Capture())));
        Assert.True(JsonElement.DeepEquals(originalBefore, JsonSerializer.SerializeToElement(original)));
        Assert.True(JsonElement.DeepEquals(originalBefore, JsonSerializer.SerializeToElement(debug.Definition)));
    }

    [Fact]
    public void Debug_costume_selection_uses_character_specific_installed_choices()
    {
        BattleDefinition initial = Definition();
        BattleDebugCatalog catalog = new() { PlayerCostumes = new Dictionary<int, int[]> { [initial.Combatants[0].ModelId] = [0, 2, 4] } };
        BattleDebugController debug = new(initial, catalog, BattleAtbMode.Normal, (_, _) => { }, () => { });
        SelectRow(debug, "Party 1");
        int costumeRow = debug.Rows.Select((row, index) => (row, index)).Single(pair => pair.row.Label == "Party 1 · Costume").index;
        debug.Select(costumeRow, true);
        Assert.Equal(2, debug.Definition.Combatants[0].Costume);
        Assert.Equal(0, initial.Combatants[0].Costume);
    }

    [Fact]
    public void Debug_weapons_are_filtered_by_character_and_enemy_rows_cannot_select_them()
    {
        BattleDebugCatalog catalog = new()
        {
            Weapons = [new BattleDebugEquipment(7, "Compatible", 0), new BattleDebugEquipment(9, "Other character", 1)]
        };
        BattleDebugController debug = new(Definition(), catalog, BattleAtbMode.Normal, (_, _) => { }, () => { });
        SelectRow(debug, "Party 1");
        Assert.DoesNotContain(debug.Rows, row => row.Label == "Enemy 1 · Weapon");
        int weaponRow = debug.Rows.Select((row, index) => (row, index)).Single(pair => pair.row.Label == "Party 1 · Weapon").index;
        debug.Select(weaponRow, true);
        Assert.Equal(7, debug.Definition.Combatants[0].WeaponId);
        debug.Select(weaponRow, true);
        Assert.Null(debug.Definition.Combatants[0].WeaponId);
    }

    [Fact]
    public void Debug_room_opens_host_battle_laboratory_without_changing_menu_fixture()
    {
        MenuController menus = new();
        menus.Open("debug-room", true);
        Assert.False(menus.Entries[0].Enabled);
        int requests = 0;
        menus.BattleDebugRequested += () => requests++;
        MenuData fixture = menus.Data;
        Assert.Equal("Battle laboratory", menus.Entries[0].Label);
        Assert.True(menus.Entries[0].Enabled);
        menus.Navigate(MenuInput.Confirm);
        Assert.Equal(1, requests);
        Assert.Same(fixture, menus.Data);
        Assert.Equal("debug-room", menus.ScreenId);
    }

    [Fact]
    public void Preferences_validate_and_persist_battle_configuration()
    {
        string path = Path.Combine(Path.GetTempPath(), "ff8-battle-ui-" + Guid.NewGuid().ToString("N"), "ui-settings.json");
        try
        {
            UiPreferences preferences = new UiPreferences().WithSetting("Battle ATB mode", "TurnBased").WithSetting("Battle policy", "NewGameFullCombat");
            preferences.Save(path);
            Assert.Equal(preferences, UiPreferences.Load(path));
            Assert.Throws<ArgumentException>(() => preferences.WithSetting("Battle ATB mode", "Wait"));
            Assert.Throws<ArgumentException>(() => preferences.WithSetting("Battle policy", "Unknown"));
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
                Directory.Delete(Path.GetDirectoryName(path)!);
            }
        }
    }

    private static BattleDefinition Definition()
    {
        return new BattleDefinition
        {
            Combatants =
            [
                new() { Id = "party-0", Name = "Squall", IsPlayer = true, InitialAtb = BattleSession.AtbMaximum },
                new() { Id = "enemy-0", Name = "Bite Bug A" },
                new() { Id = "enemy-1", Name = "Bite Bug B" }
            ]
        };
    }

    private static void SelectRow(BattleDebugController debug, string label)
    {
        debug.Select(debug.Rows.Select((row, index) => (row, index)).Single(pair => pair.row.Label == label).index, true);
    }

    private sealed class TestCanvas : IUiCanvas
    {
        public List<string> Texts { get; } = [];
        public void Fill(UiRect bounds, UiColor color)
        {
        }

        public void Text(string text, float fontSize, UiRect bounds, UiColor color)
        {
            Texts.Add(text);
        }

        public void Text(string text, float fontSize, UiRect bounds, UiColor color, UiAlignment verticalAlignment)
        {
            Text(text, fontSize, bounds, color);
        }

        public void Image(UiImageSource source, UiRect bounds)
        {
        }

        public void MenuPointer(UiRect bounds)
        {
        }

        public void Line(float x1, float y1, float x2, float y2, UiColor color, float thickness)
        {
        }

        public void PushClip(UiRect bounds)
        {
        }

        public void PopClip()
        {
        }
    }
}
