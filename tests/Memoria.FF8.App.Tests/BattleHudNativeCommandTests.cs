using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.GameUi;
using Memoria.FF8.Runtime;
using Memoria.FF8.Ui;

namespace Memoria.FF8.App.Tests;

public sealed class BattleHudNativeCommandTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Slots_displays_do_over_and_cast_and_submits_the_accepted_roll_without_target_selection(bool pointer)
    {
        using BattleAssets assets = new(TestResources.GameDirectory);
        BattleSession session = new(new BattleDefinition { NativeCommandSequences = NativeBattleCommandSequenceFixture.Values, NativeRetainedMemory = NativeBattleRetainedMemory.FromColdProcess(), NativeActions = assets.NativeActions.ToArray(), NativeLimitsDisabled = false, Combatants = [new() { Id = "selphie", Name = "Selphie", IsPlayer = true, ModelId = 5, Level = 20, InitialAtb = BattleSession.AtbMaximum, NativeLimit = assets.ReadNativeLimitDefinition(5) }, new() { Id = "enemy", Name = "Enemy", Speed = 1 }] }, BattleAtbMode.TurnBased);
        session = BattleSession.Restore(session.Capture() with { NativeLimits = [new("selphie", 4)] });
        BattleHudController hud = new(session, session.QueueAttack, () => { }, () => { }, execute: input => session.QueueAction(input.ActorId!, input.TargetId!, input.Ability, input.ActionId));
        hud.Navigate(MenuInput.Right);
        hud.Choose(0);
        Assert.True(hud.SelectingNativeSlots);
        Assert.Equal(new[] { "Do over", "Cast" }, hud.ChoiceLabels);
        Assert.Equal(0, hud.ChoiceIndex);
        string before = JsonSerializer.Serialize(session.Capture());
        BattleUiTests.TestCanvas canvas = new();
        UiView view = hud.BuildView();
        view.Draw(canvas, new UiTheme());
        Assert.Contains("Do over", canvas.Texts);
        Assert.Contains("Cast", canvas.Texts);
        Assert.Contains(hud.NativeSlotsLabel!, canvas.Texts);
        Assert.Equal(before, JsonSerializer.Serialize(session.Capture()));
        hud.Navigate(MenuInput.Up);
        Assert.Equal(1, hud.ChoiceIndex);
        hud.Navigate(MenuInput.Down);
        Assert.Equal(0, hud.ChoiceIndex);
        NativeBattleRandomState expected = session.Capture().Random!;
        expected = expected.Draw(out _).Draw(out _).Draw(out _).Draw(out _);
        hud.Navigate(MenuInput.Confirm);
        Assert.Equal(JsonSerializer.Serialize(expected), JsonSerializer.Serialize(session.Capture().Random));
        Assert.True(hud.SelectingNativeSlots);
        Assert.Empty(session.QueuedActions);
        expected = expected.Draw(out _).Draw(out _).Draw(out _).Draw(out _);
        if (pointer)
        {
            UiRect row = Assert.Single(canvas.TextDraws, draw => draw.Text == "Do over").Bounds;
            view.Pointer(row.X + 5, row.Y + 5, false);
            view.Pointer(row.X + 5, row.Y + 5, true);
        }
        else
        {
            hud.Choose(0);
        }
        Assert.Equal(JsonSerializer.Serialize(expected), JsonSerializer.Serialize(session.Capture().Random));
        BattleNativeSlotsRoll accepted = session.GetNativeSlotsRoll("selphie")!;
        if (pointer)
        {
            UiRect row = Assert.Single(canvas.TextDraws, draw => draw.Text == "Cast").Bounds;
            view.Pointer(row.X + 5, row.Y + 5, false);
            view.Pointer(row.X + 5, row.Y + 5, true);
        }
        else
        {
            hud.Navigate(MenuInput.Down);
            hud.Navigate(MenuInput.Confirm);
        }
        Assert.False(hud.SelectingTarget);
        Assert.False(hud.SelectingAction);
        Assert.Equal(accepted, Assert.Single(session.QueuedActions).NativeSlots);
        string queued = JsonSerializer.Serialize(session.Capture());
        Assert.Equal(queued, JsonSerializer.Serialize(BattleSession.Restore(JsonSerializer.Deserialize<BattleSnapshot>(queued)!).Capture()));
    }

    [Fact]
    public void Duel_displays_current_native_bindings_and_accepts_raw_edges_without_advancing_gameplay()
    {
        using GameAssets game = new(TestResources.GameDirectory);
        using BattleAssets assets = new(game.GameDirectory);
        NativeLimitProgress progress = game.ReadNewGameLimitProgress() with { DuelFlags = 0x3FF, Options978 = 0 };
        BattleSession session = new(new BattleDefinition { NativeCommandSequences = NativeBattleCommandSequenceFixture.Values, NativeRetainedMemory = NativeBattleRetainedMemory.FromColdProcess(), NativeActions = assets.NativeActions.ToArray(), LimitProgress = progress, NativeLimitsDisabled = false, Combatants = [new() { Id = "zell", Name = "Zell", IsPlayer = true, ModelId = 1, InitialAtb = BattleSession.AtbMaximum, NativeLimit = assets.ReadNativeLimitDefinition(1) }, new() { Id = "enemy", Name = "Enemy", Speed = 1, MaximumHp = 50000, CurrentHp = 50000 }] }, BattleAtbMode.TurnBased);
        session = BattleSession.Restore(session.Capture() with { NativeLimits = [new("zell", 1)] });
        BattleHudController hud = new(session, session.QueueAttack, () => { }, () => { }, execute: input => session.QueueAction(input.ActorId!, input.TargetId!, input.Ability, input.ActionId)) { NativeButtonBindingHint = buttons => $"Key{buttons:X4}" };
        hud.BuildView();
        hud.Navigate(MenuInput.Right);
        hud.Choose(0);
        hud.Attack();
        for (int frame = 0; frame < 100 && session.GetNativeDuel("zell")?.Phase != BattleNativeDuelPhase.Ready; frame++)
        {
            session.Tick();
        }
        Assert.Equal(BattleNativeDuelPhase.Ready, session.GetNativeDuel("zell")!.Phase);
        BattleNativeDuelChoice choice = session.GetNativeDuelChoices("zell").First(candidate => candidate.Learned);
        string before = JsonSerializer.Serialize(session.Capture());

        hud.BuildView();
        hud.BuildView();

        Assert.Equal(before, JsonSerializer.Serialize(session.Capture()));
        Assert.Empty(hud.Commands);
        Assert.Contains(choice.Name + "  " + string.Join(" ", choice.Buttons.Select(button => ButtonPromptText.Mark($"Key{button:X4}"))), hud.ChoiceLabels);
        long gameplayFrame = session.Frame;
        foreach (ushort button in choice.Buttons)
        {
            session.AdvanceNativeDuelPresentation(TimeSpan.FromMilliseconds(34), button);
            session.AdvanceNativeDuelPresentation(TimeSpan.FromMilliseconds(34), 0);
        }

        Assert.Equal(gameplayFrame, session.Frame);
        Assert.Equal((ushort)choice.ActionId, Assert.Single(session.QueuedActions).NativeAction!.ActionId);
        Assert.Equal(BattleNativeDuelPhase.AwaitingEffect, session.GetNativeDuel("zell")!.Phase);
    }

    [Fact]
    public void Shot_process_holds_command_selection_and_cancel_queues_the_native_end_control()
    {
        using GameAssets game = new(TestResources.GameDirectory);
        using BattleAssets assets = new(game.GameDirectory);
        BattleSession session = new(new BattleDefinition { NativeCommandSequences = NativeBattleCommandSequenceFixture.Values, NativeRetainedMemory = NativeBattleRetainedMemory.FromColdProcess(), NativeActions = assets.NativeActions.ToArray(), NativeShotAvailability = game.ReadNewGameShotAvailability() with { Flags770 = 0 }, Items = [new(101, 2)], NativeLimitsDisabled = false, Combatants = [new() { Id = "irvine", Name = "Irvine", IsPlayer = true, ModelId = 2, InitialAtb = BattleSession.AtbMaximum, NativeLimit = assets.ReadNativeLimitDefinition(2) }, new() { Id = "ally", Name = "Ally", IsPlayer = true, InitialAtb = BattleSession.AtbMaximum }, new() { Id = "enemy", Name = "Enemy", Speed = 1 }] }, BattleAtbMode.TurnBased);
        session = BattleSession.Restore(session.Capture() with { NativeLimits = [new("irvine", 4)] });
        BattleHudController hud = new(session, session.QueueAttack, () => { }, () => { }, execute: input => session.QueueAction(input.ActorId!, input.TargetId!, input.Ability, input.ActionId));
        hud.BuildView();
        hud.Navigate(MenuInput.Right);
        hud.Choose(0);
        hud.Choose(0);
        hud.Attack();

        Assert.Equal((byte)14, Assert.Single(session.QueuedActions).NativeQueuedCommand);
        Assert.Empty(hud.Commands);
        hud.Attack();
        Assert.Single(session.QueuedActions);
        for (int frame = 0; frame < 25; frame++)
        {
            session.Tick();
        }
        Assert.Equal(BattleNativeShotPhase.Ready, session.GetNativeShot("irvine")!.Phase);

        hud.Navigate(MenuInput.Cancel);

        Assert.Equal((byte)238, Assert.Single(session.QueuedActions).NativeQueuedCommand);
    }

    [Fact]
    public void Restored_Limit_selection_is_visible_and_queueable_without_an_extra_rng_draw_from_rendering()
    {
        string directory = TestResources.GameDirectory;
        Assert.SkipUnless(Directory.Exists(directory), "Installed battle resources are required.");
        using BattleAssets assets = new(directory);
        NativeLimitProgress progress = new(new string('A', 64), 1, 0, 0, 0, Enumerable.Repeat((byte)0, 8).ToImmutableArray());
        BattleSession session = new(new BattleDefinition { NativeCommandSequences = NativeBattleCommandSequenceFixture.Values, NativeRetainedMemory = NativeBattleRetainedMemory.FromColdProcess(), NativeActions = assets.NativeActions.ToArray(), LimitProgress = progress, NativeLimitsDisabled = false, Combatants = [new() { Id = "quistis", Name = "Quistis", IsPlayer = true, ModelId = 3, InitialAtb = BattleSession.AtbMaximum, NativeLimit = assets.ReadNativeLimitDefinition(3) }, new() { Id = "enemy", Name = "Enemy" }] }, BattleAtbMode.TurnBased);
        session = BattleSession.Restore(session.Capture() with { NativeLimits = [new("quistis", 4)] });
        BattleHudController hud = new(session, session.QueueAttack, () => { }, () => { }, execute: input => session.QueueAction(input.ActorId!, input.TargetId!, input.Ability, input.ActionId));
        string before = JsonSerializer.Serialize(session.Capture());

        hud.BuildView();
        hud.BuildView();

        Assert.Equal(before, JsonSerializer.Serialize(session.Capture()));
        Assert.True(hud.NativeLimitAvailable);
        Assert.DoesNotContain(BattleAbility.LimitBreak, hud.Commands);
        hud.Navigate(MenuInput.Right);
        hud.Choose(0);
        Assert.True(hud.SelectingAction);
        Assert.Single(hud.ActionChoices);
        hud.Choose(0);
        hud.Attack();

        Assert.Equal((byte)15, Assert.Single(session.QueuedActions).NativeAction!.Command);
        Assert.False(hud.SelectingAction);
    }
}
