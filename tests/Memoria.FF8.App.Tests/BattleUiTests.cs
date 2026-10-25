using System.Text.Json;
using Memoria.FF8.GameUi;
using Memoria.FF8.Runtime;
using Memoria.FF8.Ui;

namespace Memoria.FF8.App.Tests;

public sealed class BattleUiTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void GuardianListExtendsNamePanelWithRightAlignedCurrentHp(bool guardianFormsEnabled)
    {
        BattleSession battle = new(new BattleDefinition
        {
            GuardianFormsEnabled = guardianFormsEnabled,
            GuardianForces = [new(1, 7, 9999), new(2, 1234, 9999)],
            Combatants =
            [
                new() { Id = "caster", Name = "Caster", IsPlayer = true, InitialAtb = BattleSession.AtbMaximum, Abilities = [BattleAbility.GuardianForce], EquipmentIds = [1, 2] },
                new() { Id = "enemy", Name = "Enemy" }
            ],
            ActionDefinitions = [new(BattleAbility.GuardianForce, 1, "Shiva", BattleActionEffect.Damage, 10, false), new(BattleAbility.GuardianForce, 2, "Ifrit", BattleActionEffect.Damage, 10, false)]
        }, BattleAtbMode.TurnBased);
        BattleHudController hud = new(battle, (_, _) => { }, () => { }, () => { });
        hud.Choose(0);
        TestCanvas canvas = new();
        UiView view = hud.BuildView();
        view.Resize(960, 672);
        view.Draw(canvas, new UiTheme());

        var name = Assert.Single(canvas.TextDraws, draw => draw.Text == "Shiva");
        UiRect firstHp = Assert.Single(canvas.Clips, clip => clip.Y == name.Bounds.Y && clip.X > 216);
        UiRect secondHp = Assert.Single(canvas.Clips, clip => clip.Y == name.Bounds.Y + 28 && clip.X > 216);
        Assert.Contains(canvas.TextDraws, draw => draw.Text == "7" && draw.Bounds.X == firstHp.X);
        Assert.Contains(canvas.TextDraws, draw => draw.Text == "1234" && draw.Bounds.X == secondHp.X);
        Assert.Equal(firstHp.Right, secondHp.Right);
        Assert.Contains(canvas.Clips, clip => clip.X == 60 && clip.Y == name.Bounds.Y && clip.Width == (guardianFormsEnabled ? 134 : 156));
        float panelRight = canvas.Fills.Where(fill => fill.Bounds.Y == 516).Max(fill => fill.Bounds.Right);
        Assert.Equal(276, panelRight);
        Assert.Equal(panelRight - 8, firstHp.Right);
        if (guardianFormsEnabled)
        {
            var arrow = Assert.Single(canvas.Images, image => image.Source == new UiImageSource(UiImageKind.Icon, 6, 5));
            Assert.True(arrow.Bounds.Right < secondHp.X);
        }
    }

    [Theory]
    [InlineData(0)]
    [InlineData(99)]
    [InlineData(100)]
    public void DrawListReservesRightAlignedStockColumnWithoutReducingNameSpace(int count)
    {
        BattleSession battle = new(new BattleDefinition
        {
            Combatants =
            [
                new() { Id = "caster", Name = "Caster", IsPlayer = true, InitialAtb = BattleSession.AtbMaximum, Abilities = [BattleAbility.Draw], MagicStocks = count == 0 ? [] : [new(2, count)] },
                new() { Id = "source", Name = "Source", NativeDrawSlots = [new(2, 0), new(3, 0), new(0, 0), new(0, 0)] }
            ],
            ActionDefinitions = [new(BattleAbility.Magic, 2, "Blizzard", BattleActionEffect.Damage, 10, false), new(BattleAbility.Magic, 3, "Sleep", BattleActionEffect.Damage, 10, false)]
        }, BattleAtbMode.TurnBased);
        BattleHudController hud = new(battle, (_, _) => { }, () => { }, () => { });
        hud.Choose(0);
        hud.Navigate(MenuInput.Confirm);
        TestCanvas canvas = new();
        UiTheme theme = new();
        UiView view = hud.BuildView();
        view.Resize(960, 672);
        view.Draw(canvas, theme);

        var name = Assert.Single(canvas.TextDraws, draw => draw.Text == "Blizzard");
        var quantity = Assert.Single(canvas.TextDraws, draw => draw.Text == count.ToString() && draw.Bounds.Y == name.Bounds.Y);
        var emptyQuantity = Assert.Single(canvas.TextDraws, draw => draw.Text == "0" && draw.Bounds.Y == name.Bounds.Y + 28);
        Assert.Equal(count == 100 ? theme.Muted : theme.Foreground, name.Color);
        Assert.Equal(name.Color, quantity.Color);
        Assert.Equal(theme.Foreground, emptyQuantity.Color);
        UiRect quantityClip = Assert.Single(canvas.Clips, clip => clip.Y == name.Bounds.Y && clip.X > 216);
        UiRect emptyQuantityClip = Assert.Single(canvas.Clips, clip => clip.Y == name.Bounds.Y + 28 && clip.X > 216);
        Assert.Equal(quantityClip.Right, emptyQuantityClip.Right);
        Assert.Contains(canvas.Clips, clip => clip.X == 60 && clip.Y == name.Bounds.Y && clip.Width == 156);
        Assert.Equal(266, canvas.Fills.Where(fill => fill.Bounds.Y == 516).Max(fill => fill.Bounds.Right));
    }

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
        UiColor expected = new((byte)Math.Min(255, 238 * shade / 128), (byte)Math.Min(255, 238 * shade / 128), (byte)Math.Min(255, 238 * shade / 128));
        UiColor expectedHp = new((byte)Math.Min(255, 255 * shade / 128), (byte)Math.Min(255, 255 * shade / 128), (byte)Math.Min(255, 255 * shade / 128));
        Assert.Equal(expected, Assert.Single(canvas.TextDraws, draw => draw.Text == "Squall").Color);
        Assert.Equal(expectedHp, Assert.Single(canvas.TextDraws, draw => draw.Bounds.X == 674 && draw.Bounds.Y == 528).Color);
        Assert.Equal(new UiColor(238, 238, 238), Assert.Single(canvas.TextDraws, draw => draw.Text == "Quistis").Color);
        Assert.Equal(new UiColor(255, 255, 255), Assert.Single(canvas.TextDraws, draw => draw.Bounds.X == 674 && draw.Bounds.Y == 566).Color);
        Assert.DoesNotContain(canvas.Pointers, bounds => bounds.X == 496);
        Assert.Contains(canvas.Pointers, bounds => bounds.X == 38);
        Assert.Equal(0, session.Frame);
    }

    [Theory]
    [InlineData(0, 255, 41, 24)]
    [InlineData(1, 255, 255, 123)]
    [InlineData(24, 255, 255, 123)]
    [InlineData(25, 255, 255, 255)]
    [InlineData(26, 255, 255, 255)]
    [InlineData(100, 255, 255, 255)]
    public void Native_name_and_hp_palettes_keep_critical_and_ko_separate(int hp, byte red, byte green, byte blue)
    {
        BattleDefinition definition = Definition();
        definition = definition with { Combatants = [definition.Combatants[0] with { CurrentHp = hp, MaximumHp = 100, InitialAtb = 0, NativeStatus0 = hp == 0 ? (ushort)1 : (ushort)0 }, new() { Id = "party-1", Name = "Quistis", IsPlayer = true, InitialAtb = BattleSession.AtbMaximum }, .. definition.Combatants.Skip(1)] };
        BattleSession session = new(definition, BattleAtbMode.TurnBased);
        BattleHudController hud = new(session, session.QueueAttack, () => { }, () => { });
        TestCanvas canvas = new();
        hud.BuildView().Draw(canvas, new UiTheme());
        Assert.Equal(hp == 0 ? new UiColor(148, 148, 164) : new UiColor(238, 238, 238), Assert.Single(canvas.TextDraws, draw => draw.Text == "Squall").Color);
        Assert.Equal(new UiColor(red, green, blue), Assert.Single(canvas.TextDraws, draw => draw.Bounds.X == 674 && draw.Bounds.Y == 528).Color);
        Assert.Equal(new UiColor(255, 255, 255), Assert.Single(canvas.TextDraws, draw => draw.Text == " /  100" && draw.Bounds.Y == 528).Color);
    }

    [Fact]
    public void Active_actor_pulse_modulates_critical_hp_palette_without_yellowing_the_name()
    {
        BattleDefinition definition = Definition();
        definition = definition with { Combatants = [definition.Combatants[0] with { CurrentHp = 24 }, .. definition.Combatants.Skip(1)] };
        BattleSession session = new(definition, BattleAtbMode.TurnBased);
        BattleHudController hud = new(session, session.QueueAttack, () => { }, () => { });
        hud.AdvancePresentation(TimeSpan.FromSeconds(3.125 / BattleSession.FramesPerSecond));
        TestCanvas canvas = new();
        hud.BuildView().Draw(canvas, new UiTheme());
        Assert.Equal(new UiColor(148, 148, 148), Assert.Single(canvas.TextDraws, draw => draw.Text == "Squall").Color);
        Assert.Equal(new UiColor(159, 159, 76), Assert.Single(canvas.TextDraws, draw => draw.Bounds.X == 674 && draw.Bounds.Y == 528).Color);
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
        Assert.Equal(new UiColor(238, 238, 238), Assert.Single(canvas.TextDraws, draw => draw.Text == "Squall").Color);
        Assert.NotEqual(theme.Foreground, Assert.Single(canvas.TextDraws, draw => draw.Text == "Quistis").Color);
        Assert.Equal(new UiColor(59, 59, 59), Assert.Single(canvas.TextDraws, draw => draw.Text == "Quistis").Color);
        Assert.Equal(new UiColor(63, 63, 63), Assert.Single(canvas.TextDraws, draw => draw.Bounds.X == 674 && draw.Bounds.Y == 566).Color);
        hud.Navigate(MenuInput.Confirm);
        Assert.Equal("party-0", hud.SelectedActor!.Id);
        canvas = new();
        hud.BuildView().Draw(canvas, theme);
        Assert.NotEqual(theme.Foreground, Assert.Single(canvas.TextDraws, draw => draw.Text == "Squall").Color);
        Assert.Equal(new UiColor(238, 238, 238), Assert.Single(canvas.TextDraws, draw => draw.Text == "Quistis").Color);
    }

    [Fact]
    public void Restored_target_menu_uses_saved_participant_availability()
    {
        GameState state = GameSession.Load(TestResources.FixturePath("battle-reserved-target"));
        BattleSession session = BattleSession.Restore(state.Battle!);
        BattleCombatant reserve = Assert.Single(session.Combatants, actor => actor.IsActive && actor.Hidden && actor.Untargetable);
        BattleHudController hud = new(session, (_, _) => { }, () => { }, () => { }, execute: _ => { });
        hud.SelectParty(session.Combatants.First(actor => actor.IsPlayer && actor.IsReady && actor.Abilities.Contains(BattleAbility.Magic)).Id);
        hud.Choose(hud.Commands.ToList().IndexOf(BattleAbility.Magic));
        hud.Navigate(MenuInput.Confirm);
        Assert.True(hud.SelectingTarget);
        Assert.DoesNotContain(reserve, hud.Targets);
        Assert.NotEmpty(hud.Targets);
        Assert.All(hud.Targets, actor => Assert.True(actor.IsActive && !actor.Untargetable));
        long frame = session.Frame;
        hud.Navigate(MenuInput.Down);
        Assert.NotEqual(reserve.Id, hud.SelectedTarget!.Id);
        Assert.Equal(frame, session.Frame);
    }

    [Theory]
    [InlineData(BattleAbility.Attack, false)]
    [InlineData(BattleAbility.Magic, false)]
    [InlineData(BattleAbility.Attack, true)]
    [InlineData(BattleAbility.Magic, true)]
    public void Target_menu_excludes_reserved_and_untargetable_actors(BattleAbility ability, bool restore)
    {
        BattleDefinition definition = new()
        {
            Combatants =
            [
                new() { Id = "party", Name = "Party", IsPlayer = true, InitialAtb = BattleSession.AtbMaximum, Abilities = [ability], MagicStocks = [new(1, 10)] },
                new() { Id = "present", Name = "Present" },
                new() { Id = "reserve", Name = "Reserve", InitialHidden = true, InitialUntargetable = true },
                new() { Id = "untargetable", Name = "Untargetable", InitialUntargetable = true },
                new() { Id = "unallocated", Name = "Unallocated", InitialActive = false, SceneSlot = 3 },
                new() { Id = "hidden", Name = "Hidden", InitialHidden = true }
            ],
            ActionDefinitions = [new(ability, 1, "Action", BattleActionEffect.Damage, 10, false)]
        };
        BattleSession session = new(definition, BattleAtbMode.TurnBased);
        if (restore)
        {
            session = BattleSession.Restore(session.Capture());
        }
        BattleHudController hud = new(session, (_, _) => { }, () => { }, () => { }, execute: _ => { });
        hud.Choose(0);
        if (ability != BattleAbility.Attack)
        {
            hud.Navigate(MenuInput.Confirm);
        }
        Assert.True(hud.SelectingTarget);
        Assert.Equal(new[] { "present", "hidden" }, hud.Targets.Select(actor => actor.Id));
        hud.SelectTarget("reserve", false);
        Assert.Equal("present", hud.SelectedTarget!.Id);
        TestCanvas canvas = new();
        hud.BuildView().Draw(canvas, new UiTheme());
        Assert.DoesNotContain("Reserve", canvas.Texts);
        Assert.DoesNotContain("Untargetable", canvas.Texts);
        Assert.DoesNotContain("Unallocated", canvas.Texts);
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
        SelectRow(debug, "Actor details");
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
        Assert.Equal(BattleResultPage.GuardianForce, hud.ResultPage);
        Assert.Equal(0, completions);
        canvas.Texts.Clear();
        SettleResultFade(hud);
        view.Draw(canvas, new UiTheme());
        Assert.Contains("Received 3 AP!", canvas.Texts);
        Assert.Contains("Lv. 2", canvas.Texts);
        Assert.Contains("↑", canvas.Texts);
        Assert.Contains("Ability 5", canvas.Texts);
        Assert.DoesNotContain(canvas.Texts, text => text.Contains("Guardian 0", StringComparison.Ordinal));
        Assert.Equal(0, completions);
        Assert.Equal(before, JsonSerializer.Serialize(session.Capture()));
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
        SelectRow(debug, "Actor details");
        SelectRow(debug, "Actor details");
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
        menus.SelectEntry("debug-room", "debug-battle");
        menus.Navigate(MenuInput.Confirm);
        Assert.Equal("debug-battle", menus.ScreenId);
        const string laboratoryId = "battle-laboratory";
        Assert.False(menus.Entries.Single(entry => entry.Id == laboratoryId).Enabled);
        int requests = 0;
        menus.BattleDebugRequested += () => requests++;
        MenuData fixture = menus.Data;
        Assert.StartsWith("Battle laboratory", menus.Entries.Single(entry => entry.Id == laboratoryId).Label, StringComparison.Ordinal);
        Assert.True(menus.Entries.Single(entry => entry.Id == laboratoryId).Enabled);
        menus.SelectEntry("debug-battle", laboratoryId);
        menus.Navigate(MenuInput.Confirm);
        Assert.Equal(1, requests);
        Assert.Same(fixture, menus.Data);
        Assert.Equal("debug-battle", menus.ScreenId);
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
        if (label.StartsWith("Party ", StringComparison.Ordinal) && !debug.Rows.Any(row => row.Label == label))
        {
            debug.Select(debug.Rows.Select((row, index) => (row, index)).Single(pair => pair.row.Label == "Editor panel").index, true);
        }
        debug.Select(debug.Rows.Select((row, index) => (row, index)).Single(pair => pair.row.Label == label).index, true);
    }

    internal sealed class TestCanvas : IUiCanvas
    {
        public List<(UiImageSource Source, UiRect Bounds)> Images { get; } = [];
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
            Images.Add((source, bounds));
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
