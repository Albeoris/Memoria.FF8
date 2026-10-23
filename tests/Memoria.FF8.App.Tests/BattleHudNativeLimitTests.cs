using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.GameUi;
using Memoria.FF8.Runtime;
using Memoria.FF8.Ui;

namespace Memoria.FF8.App.Tests;

public sealed class BattleHudNativeLimitTests
{
    [Fact]
    public void Limit_arrow_uses_native_artwork_and_blinks_without_advancing_battle_or_rerolling()
    {
        BattleSession battle = Create();
        BattleHudController hud = Hud(battle);
        string before = JsonSerializer.Serialize(battle.Capture());
        UiView view = hud.BuildView();
        view.Resize(960, 672);
        BattleUiTests.TestCanvas first = new();
        view.Draw(first, new UiTheme());
        var arrow = Assert.Single(first.Images, image => image.Source.Id == 6);
        Assert.Equal(new UiImageSource(UiImageKind.Icon, 6, 5), arrow.Source);
        Assert.DoesNotContain(first.Texts, text => text.Contains('>'));
        Assert.Equal("Attack", hud.ChoiceLabels[0]);
        hud.AdvancePresentation(TimeSpan.FromSeconds(2.125 / BattleSession.FramesPerSecond));
        BattleUiTests.TestCanvas next = new();
        view.Draw(next, new UiTheme());
        Assert.Equal(new UiImageSource(UiImageKind.Icon, 6, 0), Assert.Single(next.Images).Source);
        Assert.Equal(arrow.Bounds, Assert.Single(next.Images).Bounds);
        Assert.Equal(before, JsonSerializer.Serialize(battle.Capture()));
        hud.Navigate(MenuInput.Right);
        BattleUiTests.TestCanvas selected = new();
        view.Draw(selected, new UiTheme());
        Assert.Empty(selected.Images);
        Assert.Equal("Renzokuken", hud.ChoiceLabels[0]);
    }

    [Fact]
    public void Named_standalone_continues_command_selection_and_restores_without_an_extra_draw()
    {
        string? source = Environment.GetEnvironmentVariable("MEMORIA_FF8_LIMIT_SELECTION_SNAPSHOT");
        string? output = Environment.GetEnvironmentVariable("MEMORIA_FF8_LIMIT_SELECTION_OUTPUT");
        Assert.SkipUnless(source is not null && File.Exists(source) && output is not null, "An optional standalone battle and distinct output directory are required.");
        Assert.False(Directory.Exists(output), "Preserve previous standalone checkpoints by choosing a new output directory.");
        Directory.CreateDirectory(output!);
        BattleSession battle = BattleSession.Restore(JsonSerializer.Deserialize<BattleSnapshot>(File.ReadAllText(source!))!);
        File.WriteAllText(Path.Combine(output!, "loaded.battle.json"), JsonSerializer.Serialize(battle.Capture()));
        BattleCombatant actor = battle.ReadyPlayerCommandOrder.First(candidate => candidate.NativeLimit is not null);
        battle.SelectPlayerCommandActor(actor.Id);
        string selected = JsonSerializer.Serialize(battle.Capture());
        File.WriteAllText(Path.Combine(output!, "selected.battle.json"), selected);
        BattleSession restored = BattleSession.Restore(JsonSerializer.Deserialize<BattleSnapshot>(selected)!);
        BattleHudController hud = Hud(restored);
        hud.BuildView();
        hud.BuildView();
        Assert.Equal(selected, JsonSerializer.Serialize(restored.Capture()));

        hud.CycleCommandActor();
        Hud(battle).CycleCommandActor();
        string continued = JsonSerializer.Serialize(battle.Capture());
        Assert.Equal(continued, JsonSerializer.Serialize(restored.Capture()));
        File.WriteAllText(Path.Combine(output!, "reselected.battle.json"), continued);
    }

    [Fact]
    public void Terminal_HUD_does_not_select_or_reroll_a_ready_native_character()
    {
        BattleSession battle = Create();
        battle.InstantVictory();
        string before = JsonSerializer.Serialize(battle.Capture());
        BattleHudController hud = Hud(battle);
        hud.BuildView();
        hud.CycleCommandActor();
        Assert.Empty(hud.Commands);
        Assert.Equal(before, JsonSerializer.Serialize(battle.Capture()));
    }

    [Fact]
    public void Fresh_HUD_selects_before_the_first_input_and_repeated_rendering_cannot_reroll()
    {
        BattleSession battle = Create();
        battle = BattleSession.Restore(battle.Capture() with { NativeLimits = [] });
        NativeBattleRandomState expected = battle.Capture().Random!.Draw(out _);
        BattleHudController hud = Hud(battle);
        Assert.True(battle.HasNativeLimitSelection("squall"));
        Assert.Equal(JsonSerializer.Serialize(expected), JsonSerializer.Serialize(battle.Capture().Random));
        string before = JsonSerializer.Serialize(battle.Capture());
        hud.BuildView();
        hud.BuildView();
        Assert.Equal(before, JsonSerializer.Serialize(battle.Capture()));
    }

    [Fact]
    public void Solo_Menu_closes_the_previous_target_selection_before_rerolling()
    {
        BattleHudController hud = Hud(Create());
        hud.Navigate(MenuInput.Right);
        hud.Navigate(MenuInput.Confirm);
        Assert.True(hud.SelectingTarget);
        hud.CycleCommandActor();
        Assert.False(hud.SelectingAction);
        Assert.False(hud.SelectingTarget);
        Assert.Equal(BattleAbility.Attack, hud.SelectedCommand);
    }

    [Fact]
    public void Right_replaces_first_command_with_native_Limit_name_and_release_restores_it()
    {
        BattleSession battle = Create();
        BattleHudController hud = Hud(battle);
        string before = JsonSerializer.Serialize(battle.Capture());

        Assert.Equal(new[] { BattleAbility.Attack, BattleAbility.Magic, BattleAbility.Item, BattleAbility.Draw }, hud.Commands);
        Assert.True(hud.NativeLimitAvailable);
        hud.SetNativeCommandButtons(0x2000);
        Assert.Equal(BattleAbility.LimitBreak, hud.SelectedCommand);
        Assert.Equal("Renzokuken", hud.ChoiceLabels[0]);
        Assert.Equal(4, hud.Commands.Count);
        hud.BuildView();
        hud.BuildView();
        hud.SetNativeCommandButtons(0);
        Assert.Equal(BattleAbility.Attack, hud.SelectedCommand);
        Assert.Equal(before, JsonSerializer.Serialize(battle.Capture()));
    }

    [Fact]
    public void Moving_off_first_row_drops_the_replacement_and_Right_does_not_cycle_actors()
    {
        BattleSession battle = Create(twoPlayers: true);
        BattleHudController hud = Hud(battle);
        hud.Navigate(MenuInput.Right);
        Assert.Equal(BattleAbility.LimitBreak, hud.SelectedCommand);
        hud.Navigate(MenuInput.Down);
        Assert.Equal(BattleAbility.Magic, hud.SelectedCommand);
        hud.Navigate(MenuInput.Right);
        Assert.Equal("squall", hud.SelectedActor!.Id);
        Assert.DoesNotContain(BattleAbility.LimitBreak, hud.Commands);
        hud.Navigate(MenuInput.Up);
        Assert.Equal(BattleAbility.Attack, hud.SelectedCommand);
    }

    [Fact]
    public void Menu_cycles_and_rerolls_each_actor_without_a_render_between_inputs()
    {
        BattleSession battle = Create(twoPlayers: true);
        BattleHudController hud = Hud(battle);
        NativeBattleRandomState expected = battle.Capture().Random!;
        foreach (string actorId in new[] { "quistis", "squall", "quistis" })
        {
            expected = expected.Draw(out _);
            hud.CycleCommandActor();
            Assert.Equal(actorId, hud.SelectedActor!.Id);
            Assert.Equal(JsonSerializer.Serialize(expected), JsonSerializer.Serialize(battle.Capture().Random));
        }
        string before = JsonSerializer.Serialize(battle.Capture());
        hud.BuildView();
        Assert.Equal(before, JsonSerializer.Serialize(battle.Capture()));
    }

    [Fact]
    public void Solo_Menu_rerolls_and_refreshes_before_the_next_input()
    {
        BattleSession battle = Create();
        BattleHudController hud = Hud(battle);
        NativeBattleRandomState expected = battle.Capture().Random!.Draw(out byte roll);
        hud.Navigate(MenuInput.Right);
        hud.CycleCommandActor();

        Assert.Equal("squall", hud.SelectedActor!.Id);
        Assert.Equal(BattleAbility.Attack, hud.SelectedCommand);
        Assert.Equal(NativeBattleLimitRules.CrisisLevel(battle.Combatants[0].NativeLimit!, 10, 100, 0, 0, 0, roll), battle.GetNativeLimitLevel("squall"));
        Assert.Equal(JsonSerializer.Serialize(expected), JsonSerializer.Serialize(battle.Capture().Random));
    }

    [Fact]
    public void Confirm_locks_selected_Limit_through_release_target_cancel_and_host_dispatch()
    {
        BattleSession battle = Create();
        List<BattleInput> inputs = [];
        BattleHudController hud = Hud(battle, inputs.Add);
        hud.SetNativeCommandButtons(0x2000);
        hud.Navigate(MenuInput.Confirm);
        hud.SetNativeCommandButtons(0);
        Assert.True(hud.SelectingAction);
        Assert.Equal(BattleAbility.LimitBreak, hud.SelectedCommand);
        Assert.True(hud.SelectingTarget);
        hud.Navigate(MenuInput.Cancel);
        Assert.False(hud.SelectingTarget);
        Assert.False(hud.SelectingAction);
        hud.SetNativeCommandButtons(0x2000);
        hud.Navigate(MenuInput.Confirm);
        hud.Navigate(MenuInput.Confirm);

        BattleInput input = Assert.Single(inputs);
        Assert.Equal(BattleAbility.LimitBreak, input.Ability);
        Assert.Equal(5 << 16, input.ActionId);
        Assert.Equal("enemy", input.TargetId);
    }

    [Fact]
    public void Simultaneous_Up_and_Right_cannot_reveal_the_Limit_on_the_newly_selected_first_row()
    {
        BattleHudController hud = Hud(Create());
        hud.Navigate(MenuInput.Down);
        hud.SetNativeCommandButtons(0x3000);
        hud.Navigate(MenuInput.Up);
        hud.Navigate(MenuInput.Right);
        Assert.Equal(BattleAbility.Attack, hud.SelectedCommand);
        hud.SetNativeCommandButtons(0x2000);
        Assert.Equal(BattleAbility.LimitBreak, hud.SelectedCommand);
    }

    [Fact]
    public void Losing_the_selected_actor_closes_its_Limit_targeting_before_selecting_the_next_actor()
    {
        BattleSession battle = Create(twoPlayers: true);
        BattleHudController hud = new(battle, (_, _) => { }, () => { }, () => { }, execute: _ => { });
        hud.Navigate(MenuInput.Right);
        hud.Navigate(MenuInput.Confirm);
        Assert.True(hud.SelectingTarget);
        battle.QueueAttack("squall", "enemy");

        hud.BuildView();

        Assert.Equal("quistis", hud.SelectedActor!.Id);
        Assert.False(hud.SelectingAction);
        Assert.False(hud.SelectingTarget);
        Assert.Equal(BattleAbility.Attack, hud.SelectedCommand);
    }

    [Fact]
    public void Zero_crisis_cannot_replace_Attack_or_consume_random_from_navigation()
    {
        BattleSession battle = Create(crisis: 0);
        BattleHudController hud = Hud(battle);
        string before = JsonSerializer.Serialize(battle.Capture());
        hud.Navigate(MenuInput.Right);
        hud.SetNativeCommandButtons(0x2000);
        Assert.False(hud.NativeLimitAvailable);
        Assert.Equal(BattleAbility.Attack, hud.SelectedCommand);
        Assert.Equal(before, JsonSerializer.Serialize(battle.Capture()));
    }

    private static BattleHudController Hud(BattleSession battle, Action<BattleInput>? execute = null)
    {
        return new(battle, (_, _) => { }, () => { }, () => { }, execute: execute);
    }

    private static BattleSession Create(bool twoPlayers = false, byte crisis = 4)
    {
        ImmutableArray<byte> weights = Enumerable.Repeat((byte)0, 32).ToImmutableArray();
        BattleNativeLimitDefinition limit = new(0, 5, 0, 250, weights) { InvocationChoices = [new(5, "Renzokuken", 0x50, 0)] };
        List<BattleCombatantDefinition> actors = [new() { Id = "squall", Name = "Squall", IsPlayer = true, MaximumHp = 100, CurrentHp = 10, InitialAtb = BattleSession.AtbMaximum, NativeLimit = limit, Abilities = [BattleAbility.Attack, BattleAbility.Magic, BattleAbility.Item, BattleAbility.Draw] }];
        if (twoPlayers)
        {
            actors.Add(new() { Id = "quistis", Name = "Quistis", IsPlayer = true, ModelId = 3, MaximumHp = 100, CurrentHp = 10, InitialAtb = BattleSession.AtbMaximum, NativeLimit = new(3, 15, 0, 250, weights) });
        }
        actors.Add(new() { Id = "enemy", Name = "Enemy" });
        BattleSession battle = new(new BattleDefinition { NativeLimitsDisabled = false, Combatants = actors.ToArray() }, BattleAtbMode.TurnBased);
        return BattleSession.Restore(battle.Capture() with { NativeLimits = [new("squall", crisis)] });
    }
}
