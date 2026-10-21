using System.Text.Json;
using Memoria.FF8.GameUi;
using Memoria.FF8.Runtime;
using Memoria.FF8.Ui;

namespace Memoria.FF8.App.Tests;

public sealed class BattleUiTests
{
    [Theory]
    [InlineData(0, 32)]
    [InlineData(3, 80)]
    [InlineData(7, 144)]
    [InlineData(8, 144)]
    [InlineData(12, 80)]
    [InlineData(15, 32)]
    [InlineData(16, 32)]
    public void Selected_ready_actor_name_and_hp_pulse_together_without_a_party_pointer(int frame, int shade)
    {
        BattleDefinition definition = Definition();
        definition = definition with { Combatants = [.. definition.Combatants, new() { Id = "party-1", Name = "Quistis", IsPlayer = true, InitialAtb = BattleSession.AtbMaximum }] };
        BattleSession session = new(definition, BattleAtbMode.TurnBased);
        BattleHudController hud = new(session, session.QueueAttack, () => { }, () => session.AdvanceTurn());
        UiTheme theme = new();
        UiView view = hud.BuildView();
        view.Resize(960, 672);
        hud.AdvancePresentation(TimeSpan.FromSeconds((frame + 0.125) / BattleSession.FramesPerSecond));
        TestCanvas canvas = new();
        view.Draw(canvas, theme);
        UiColor expected = new((byte)Math.Min(255, theme.Foreground.R * shade / 128), (byte)Math.Min(255, theme.Foreground.G * shade / 128), (byte)Math.Min(255, theme.Foreground.B * shade / 128));
        Assert.Equal(expected, Assert.Single(canvas.TextDraws, draw => draw.Text == "Squall").Color);
        Assert.Equal(expected, Assert.Single(canvas.TextDraws, draw => draw.Bounds.X == 674 && draw.Bounds.Y == 528).Color);
        Assert.Equal(theme.Foreground, Assert.Single(canvas.TextDraws, draw => draw.Text == "Quistis").Color);
        Assert.Equal(theme.Foreground, Assert.Single(canvas.TextDraws, draw => draw.Bounds.X == 674 && draw.Bounds.Y == 566).Color);
        Assert.DoesNotContain(canvas.Pointers, bounds => bounds.X == 496);
        Assert.Contains(canvas.Pointers, bounds => bounds.X == 38);
        Assert.Equal(0, session.Frame);
    }

    [Fact]
    public void Vitals_pulse_follows_actor_selection_and_persists_during_target_selection()
    {
        BattleDefinition definition = Definition();
        definition = definition with { Combatants = [.. definition.Combatants, new() { Id = "party-1", Name = "Quistis", IsPlayer = true, InitialAtb = BattleSession.AtbMaximum }] };
        BattleSession session = new(definition, BattleAtbMode.TurnBased);
        BattleHudController hud = new(session, session.QueueAttack, () => { }, () => session.AdvanceTurn());
        hud.SelectParty("party-1");
        hud.Navigate(MenuInput.Confirm);
        Assert.True(hud.SelectingTarget);
        UiView view = hud.BuildView();
        view.Resize(960, 672);
        TestCanvas canvas = new();
        UiTheme theme = new();
        view.Draw(canvas, theme);
        Assert.Equal(theme.Foreground, Assert.Single(canvas.TextDraws, draw => draw.Text == "Squall").Color);
        Assert.NotEqual(theme.Foreground, Assert.Single(canvas.TextDraws, draw => draw.Text == "Quistis").Color);
        Assert.Equal(Assert.Single(canvas.TextDraws, draw => draw.Text == "Quistis").Color, Assert.Single(canvas.TextDraws, draw => draw.Bounds.X == 674 && draw.Bounds.Y == 566).Color);
        hud.Navigate(MenuInput.Confirm);
        Assert.Equal("party-0", hud.SelectedActor!.Id);
        canvas = new();
        hud.BuildView().Draw(canvas, theme);
        Assert.NotEqual(theme.Foreground, Assert.Single(canvas.TextDraws, draw => draw.Text == "Squall").Color);
        Assert.Equal(theme.Foreground, Assert.Single(canvas.TextDraws, draw => draw.Text == "Quistis").Color);
    }

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
    public void Failed_command_displays_a_compact_timed_error_without_closing_target_selection()
    {
        BattleSession session = new(Definition(), BattleAtbMode.TurnBased);
        BattleHudController hud = new(session, (_, _) => throw new InvalidOperationException("Rejected command"), () => { }, () => { });
        hud.Navigate(MenuInput.Confirm);
        hud.Navigate(MenuInput.Confirm);
        TestCanvas canvas = new();
        hud.BuildView().Draw(canvas, new UiTheme());
        Assert.Contains("Rejected command", canvas.Texts);
        Assert.True(hud.SelectingTarget);
        Assert.Empty(session.QueuedActions);
        hud.AdvancePresentation(TimeSpan.FromSeconds(5));
        Assert.Null(hud.BattleMessage);
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
    public void Terminal_confirm_and_pointer_advance_results_before_single_completion()
    {
        BattleSession session = new(Definition(), BattleAtbMode.Normal);
        int completions = 0;
        BattleHudController hud = new(session, (_, _) => { }, () => { }, () => { }, () => completions++);
        hud.Complete();
        Assert.Equal(0, completions);
        session.InstantVictory();
        long frame = session.Frame;
        hud.Navigate(MenuInput.Confirm);
        Assert.False(hud.ExperienceCounting);
        Assert.False(hud.ExperienceFinished);
        Assert.Equal(0, completions);
        ConfirmSettledResult(hud);
        Assert.True(hud.ExperienceFinished);
        Assert.Equal(BattleResultPage.Experience, hud.ResultPage);
        Assert.Equal(0, completions);
        UiView view = hud.BuildView();
        view.Resize(960, 672);
        SettleResultFade(hud);
        view.Pointer(100, 575, true);
        Assert.Equal(BattleResultPage.Items, hud.ResultPage);
        Assert.Equal(0, completions);
        SettleResultFade(hud);
        view.Pointer(100, 575, true);
        ConfirmSettledResult(hud);
        Assert.Equal(1, completions);
        Assert.Equal(frame, session.Frame);
        Assert.Equal(BattleOutcome.Victory, session.Outcome);
    }

    [Fact]
    public void Victory_rewards_preview_is_cached_and_confirmed_in_native_result_order()
    {
        BattleSession session = new(Definition(), BattleAtbMode.TurnBased);
        BattleRewardSummary rewards = new(new Dictionary<byte, ushort> { [0] = 123 }, Enumerable.Range(0, 9).ToDictionary(id => (byte)id, id => (ushort)(id + 10)), Enumerable.Range(0, 9).ToDictionary(id => id, _ => (ushort)3), [new BattleStock(7, 2)], [4])
        {
            AbilityPointsNotice = 3,
            CharacterGrowth = [new(0, 100, 223, 1, 2, 100, 177)],
            GuardianForceGrowth = [new(2, 1, 2, [5])]
        };
        int previews = 0;
        int completions = 0;
        BattleHudController hud = new(session, session.QueueAttack, () => { }, () => session.AdvanceTurn(), () => completions++, previewRewards: () =>
        {
            previews++;
            return rewards;
        }, guardianForceName: id => $"Guardian {id}", itemName: _ => "Potion", cardName: _ => "Bite Bug");
        Assert.Equal(0, previews);
        session.InstantVictory();
        string before = JsonSerializer.Serialize(session.Capture());
        TestCanvas canvas = new();
        UiView view = hud.BuildView();
        view.Resize(960, 672);
        SettleResultFade(hud);
        view.Draw(canvas, new UiTheme());
        Assert.Contains("EXP received", canvas.Texts);
        Assert.Contains("Squall", canvas.Texts);
        Assert.DoesNotContain("Bite Bug A", canvas.Texts);
        Assert.DoesNotContain("Attack", canvas.Texts);
        Assert.Contains(canvas.Fills, fill => fill.Color == new UiColor(0, 0, 0) && fill.Bounds.Width == 960 && fill.Bounds.Height == 672);
        foreach (float rowY in new[] { 102f, 252f, 402f })
        {
            Assert.Contains(new UiRect(75, rowY, 807.5f, 150), canvas.Clips);
        }
        Assert.Equal(1, previews);
        foreach (MenuInput input in new[] { MenuInput.Left, MenuInput.Right, MenuInput.Up, MenuInput.Down, MenuInput.Cancel })
        {
            hud.Navigate(input);
            Assert.Equal(BattleResultPage.Experience, hud.ResultPage);
        }
        ConfirmSettledResult(hud);
        Assert.True(hud.ExperienceCounting);
        hud.AdvancePresentation(TimeSpan.FromMilliseconds(10));
        SettleResultFade(hud);
        view.Draw(canvas, new UiTheme());
        ConfirmSettledResult(hud);
        Assert.True(hud.ExperienceFinished);
        Assert.Equal(BattleResultPage.Experience, hud.ResultPage);
        Assert.Equal(0, completions);
        ConfirmSettledResult(hud);
        Assert.Equal(BattleResultPage.Items, hud.ResultPage);
        canvas.Texts.Clear();
        SettleResultFade(hud);
        view.Draw(canvas, new UiTheme());
        Assert.Contains("Potion", canvas.Texts);
        hud.Navigate(MenuInput.Right);
        hud.Navigate(MenuInput.Cancel);
        Assert.Equal(BattleResultPage.Items, hud.ResultPage);
        ConfirmSettledResult(hud);
        canvas.Texts.Clear();
        SettleResultFade(hud);
        view.Draw(canvas, new UiTheme());
        Assert.Contains("Received!", canvas.Texts);
        Assert.Contains("Potion", canvas.Texts);
        ConfirmSettledResult(hud);
        canvas.Texts.Clear();
        SettleResultFade(hud);
        view.Draw(canvas, new UiTheme());
        Assert.Contains("Bite Bug Card", canvas.Texts);
        ConfirmSettledResult(hud);
        Assert.Equal(BattleResultPage.Items, hud.ResultPage);
        hud.AdvancePresentation(TimeSpan.FromTicks((long)Math.Ceiling(32d * TimeSpan.TicksPerSecond / BattleSession.FramesPerSecond)));
        Assert.Equal(BattleResultPage.AbilityPoints, hud.ResultPage);
        Assert.Equal(0, completions);
        ConfirmSettledResult(hud);
        Assert.Equal(BattleResultPage.GuardianForce, hud.ResultPage);
        canvas.Texts.Clear();
        SettleResultFade(hud);
        view.Draw(canvas, new UiTheme());
        Assert.Contains("G.F. Guardian 2 Level Up!", canvas.Texts);
        Assert.DoesNotContain(canvas.Texts, text => text.Contains("Guardian 0", StringComparison.Ordinal));
        Assert.Equal(0, completions);
        Assert.Equal(before, JsonSerializer.Serialize(session.Capture()));
        ConfirmSettledResult(hud);
        ConfirmSettledResult(hud);
        Assert.Equal(1, completions);
        Assert.Equal(1, previews);
        Assert.Equal(before, JsonSerializer.Serialize(session.Capture()));
    }

    [Fact]
    public void Standalone_victory_explains_unsaved_rewards_and_requires_no_items_acknowledgment()
    {
        BattleSession session = new(Definition(), BattleAtbMode.Normal);
        int completions = 0;
        BattleHudController hud = new(session, session.QueueAttack, () => { }, () => { }, () => completions++);
        session.InstantVictory();
        TestCanvas canvas = new();
        SettleResultFade(hud);
        hud.BuildView().Draw(canvas, new UiTheme());
        Assert.Contains("Standalone battle: rewards are not saved.", canvas.Texts);
        ConfirmSettledResult(hud);
        Assert.True(hud.ExperienceFinished);
        ConfirmSettledResult(hud);
        Assert.Equal(BattleResultPage.Items, hud.ResultPage);
        Assert.Equal(0, completions);
        canvas.Texts.Clear();
        SettleResultFade(hud);
        hud.BuildView().Draw(canvas, new UiTheme());
        Assert.Contains("No items found.", canvas.Texts);
        ConfirmSettledResult(hud);
        ConfirmSettledResult(hud);
        Assert.Equal(1, completions);
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

    [Fact]
    public void AcknowledgedItemsAutomaticallyAdvanceAfterTheirNativeWaits()
    {
        BattleSession session = new(Definition(), BattleAtbMode.Normal);
        BattleRewardSummary rewards = new(new Dictionary<byte, ushort>(), new Dictionary<byte, ushort>(), new Dictionary<int, ushort>(), [new BattleStock(1, 2), new BattleStock(2, 1)], []);
        int completions = 0;
        BattleHudController hud = new(session, (_, _) => { }, () => { }, () => { }, () => completions++, previewRewards: () => rewards);
        session.InstantVictory();
        ConfirmSettledResult(hud);
        ConfirmSettledResult(hud);
        ConfirmSettledResult(hud);
        Assert.True(hud.ItemAcknowledged);
        hud.AdvancePresentation(TimeSpan.FromTicks((long)Math.Ceiling(16d * TimeSpan.TicksPerSecond / BattleSession.FramesPerSecond)));
        Assert.Equal(1, hud.ResultItemIndex);
        Assert.False(hud.ItemAcknowledged);
        Assert.Equal(0, completions);
        ConfirmSettledResult(hud);
        hud.AdvancePresentation(TimeSpan.FromTicks((long)Math.Ceiling(16d * TimeSpan.TicksPerSecond / BattleSession.FramesPerSecond)));
        Assert.Equal(0, completions);
        hud.AdvancePresentation(TimeSpan.FromTicks((long)Math.Ceiling(16d * TimeSpan.TicksPerSecond / BattleSession.FramesPerSecond)));
        Assert.Equal(1, completions);
    }

    [Fact]
    public void ExperienceCountdownUsesElapsedTimeAtThirtyAndSixtyUpdatesPerSecond()
    {
        BattleSession slowSession = new(Definition(), BattleAtbMode.Normal);
        BattleSession fastSession = new(Definition(), BattleAtbMode.Normal);
        BattleRewardSummary rewards = new(new Dictionary<byte, ushort> { [0] = 1000 }, new Dictionary<byte, ushort>(), new Dictionary<int, ushort>(), [], []);
        BattleHudController slow = new(slowSession, (_, _) => { }, () => { }, () => { }, previewRewards: () => rewards);
        BattleHudController fast = new(fastSession, (_, _) => { }, () => { }, () => { }, previewRewards: () => rewards);
        slowSession.InstantVictory();
        fastSession.InstantVictory();
        ConfirmSettledResult(slow);
        ConfirmSettledResult(fast);
        foreach ((BattleHudController hud, int rate) in new[] { (slow, 30), (fast, 60) })
        {
            for (int index = 0; index < rate; index++)
            {
                long from = index * TimeSpan.TicksPerSecond / rate;
                long to = (index + 1) * TimeSpan.TicksPerSecond / rate;
                hud.AdvancePresentation(TimeSpan.FromTicks(to - from));
            }
        }
        Assert.Equal(slow.RemainingExperience(slow.Party[0]), fast.RemainingExperience(fast.Party[0]));
        Assert.True(slow.ExperienceCounting);
        Assert.True(fast.ExperienceCounting);
    }

    [Fact]
    public void FinalAcknowledgedItemWaitCannotBeSkippedByConfirm()
    {
        BattleSession session = new(Definition(), BattleAtbMode.Normal);
        BattleRewardSummary rewards = new(new Dictionary<byte, ushort>(), new Dictionary<byte, ushort>(), new Dictionary<int, ushort>(), [new BattleStock(1, 1)], []);
        int completions = 0;
        BattleHudController hud = new(session, (_, _) => { }, () => { }, () => { }, () => completions++, previewRewards: () => rewards);
        session.InstantVictory();
        ConfirmSettledResult(hud);
        ConfirmSettledResult(hud);
        ConfirmSettledResult(hud);
        Assert.True(hud.ItemAcknowledged);
        ConfirmSettledResult(hud);
        Assert.Equal(0, completions);
        hud.AdvancePresentation(TimeSpan.FromTicks((long)Math.Ceiling(16d * TimeSpan.TicksPerSecond / BattleSession.FramesPerSecond)));
        ConfirmSettledResult(hud);
        Assert.Equal(0, completions);
        hud.AdvancePresentation(TimeSpan.FromTicks((long)Math.Ceiling(16d * TimeSpan.TicksPerSecond / BattleSession.FramesPerSecond)));
        Assert.Equal(1, completions);
    }

    private static void SettleResultFade(BattleHudController hud)
    {
        double frames = (1 - hud.ResultOpacity) * 16;
        if (frames > 0)
        {
            hud.AdvancePresentation(TimeSpan.FromTicks((long)Math.Ceiling(frames * TimeSpan.TicksPerSecond / BattleSession.FramesPerSecond)));
        }
    }

    private static void ConfirmSettledResult(BattleHudController hud)
    {
        SettleResultFade(hud);
        hud.Navigate(MenuInput.Confirm);
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
        public List<(string Text, UiRect Bounds, UiColor Color)> TextDraws { get; } = [];
        public List<UiRect> Pointers { get; } = [];
        public List<(UiRect Bounds, UiColor Color)> Fills { get; } = [];
        public List<UiRect> Clips { get; } = [];
        public UiSize MeasureText(string text, float fontSize, float maximumWidth)
        {
            return new UiSize(Math.Min(maximumWidth, text.Length * fontSize * 0.5f), fontSize);
        }
        public void Fill(UiRect bounds, UiColor color)
        {
            Fills.Add((bounds, color));
        }

        public void Text(string text, float fontSize, UiRect bounds, UiColor color)
        {
            Texts.Add(text);
            TextDraws.Add((text, bounds, color));
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
            Pointers.Add(bounds);
        }

        public void Line(float x1, float y1, float x2, float y2, UiColor color, float thickness)
        {
        }

        public void PushClip(UiRect bounds)
        {
            Clips.Add(bounds);
        }

        public void PopClip()
        {
        }
    }
}
