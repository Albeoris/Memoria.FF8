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
    public void Initiative_draws_all_living_actor_heads_and_can_be_hidden()
    {
        BattleSession session = new(Definition(), BattleAtbMode.Normal);
        BattleHudController hud = new(session, (_, _) => { }, () => { }, () => { });
        List<string> heads = [];
        hud.HeadRenderer = (actor, _, _) => heads.Add(actor.Id);
        UiView view = hud.BuildView();
        view.Resize(1280, 720);
        view.Draw(new TestCanvas(), new UiTheme());
        Assert.Equal(session.Combatants.Select(actor => actor.Id).Order(), heads.Order());
        heads.Clear();
        hud.ShowInitiative = false;
        view.Draw(new TestCanvas(), new UiTheme());
        Assert.Empty(heads);
    }

    [Fact]
    public void Initiative_places_equal_gauges_by_time_to_ready_when_actor_speeds_differ()
    {
        BattleDefinition definition = Definition();
        definition = definition with { Combatants = [definition.Combatants[0] with { InitialAtb = 0, Speed = 200 }, definition.Combatants[1] with { Speed = 50 }, definition.Combatants[2] with { Speed = 100 }] };
        BattleSession session = new(definition, BattleAtbMode.Normal);
        BattleHudController hud = new(session, (_, _) => { }, () => { }, () => { });
        Dictionary<string, float> positions = [];
        hud.HeadRenderer = (actor, _, bounds) => positions[actor.Id] = bounds.X;
        hud.BuildView().Draw(new TestCanvas(), new UiTheme());
        Assert.True(positions["party-0"] > positions["enemy-1"]);
        Assert.True(positions["enemy-1"] > positions["enemy-0"]);
    }

    [Fact]
    public void Debug_scenario_changes_do_not_mutate_source_definition_and_preserve_stage_identity()
    {
        BattleDefinition original = Definition() with { StageId = 7, EncounterId = 42 };
        BattleDefinition? started = null;
        BattleDebugController debug = new(original, new BattleDebugCatalog(), BattleAtbMode.Normal, "ResearchInstantVictory", true, (definition, _) => started = definition, (_, _, _) => { }, () => { });
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
        Assert.Equal(2, completions);
        Assert.Equal(frame, session.Frame);
        Assert.Equal(BattleOutcome.Victory, session.Outcome);
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
        BattleDebugController debug = new(original, new BattleDebugCatalog(), BattleAtbMode.Normal, "ResearchInstantVictory", true, (_, _) => throw failure switch
        {
            "unsupported" => new NotSupportedException("Unsupported costume"),
            "data" => new InvalidDataException("Invalid resource"),
            "argument" => new ArgumentException("Invalid definition"),
            _ => new IOException("Cannot read model")
        }, (_, _, _) => { }, () => { });
        debug.Start();
        Assert.StartsWith("Cannot start scenario: ", debug.Message);
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
        BattleDebugController debug = new(initial, catalog, BattleAtbMode.Normal, "ResearchInstantVictory", true, (_, _) => { }, (_, _, _) => { }, () => { });
        int costumeRow = debug.Rows.Select((row, index) => (row, index)).Single(pair => pair.row.Label == "Party 1 · Costume").index;
        debug.Select(costumeRow, true);
        Assert.Equal(2, debug.Definition.Combatants[0].Costume);
        Assert.Equal(0, initial.Combatants[0].Costume);
    }

    [Fact]
    public void Timeline_draws_each_living_participants_name()
    {
        BattleDefinition definition = Definition();
        definition = definition with { Combatants = [definition.Combatants[0], definition.Combatants[1] with { Name = "Soldier" }, definition.Combatants[2] with { Name = "Goblin" }] };
        BattleHudController hud = new(new BattleSession(definition, BattleAtbMode.Normal), (_, _) => { }, () => { }, () => { });
        TestCanvas canvas = new();
        hud.BuildView().Draw(canvas, new UiTheme());
        foreach (BattleCombatantDefinition actor in definition.Combatants)
        {
            Assert.Contains(actor.Name, canvas.Texts);
        }
    }

    [Fact]
    public void Debug_weapons_are_filtered_by_character_and_enemy_rows_cannot_select_them()
    {
        BattleDebugCatalog catalog = new()
        {
            Weapons = [new BattleDebugEquipment(7, "Compatible", 0), new BattleDebugEquipment(9, "Other character", 1)]
        };
        BattleDebugController debug = new(Definition(), catalog, BattleAtbMode.Normal, "ResearchInstantVictory", true, (_, _) => { }, (_, _, _) => { }, () => { });
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
            UiPreferences preferences = new UiPreferences().WithSetting("Battle ATB mode", "TurnBased").WithSetting("Battle policy", "NewGameFullCombat").WithSetting("Initiative bar", "Off");
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
