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
        BattleHudController hud = new(session, session.QueueAttack, () => { }, () => { }, execute: input => session.QueueAction(input.ActorId!, input.TargetId!, input.Ability, input.ActionId), readPresentationUi: BattleHudTestPresentation.Visible);
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
        BattleNativeSlotsRoll submitted = Assert.IsType<BattleNativeSlotsRoll>(Assert.Single(session.QueuedActions).NativeSlots);
        Assert.Equal(accepted.MagicId, submitted.MagicId);
        Assert.Equal(accepted.Repetitions, submitted.Repetitions);
        Assert.NotNull(submitted.PreflightSelection);
        BattleSnapshot queued = session.Capture();
        Assert.Equal(JsonSerializer.Serialize(queued), JsonSerializer.Serialize(BattleSession.Restore(queued).Capture()));
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void Duel_draws_published_native_choices_and_hides_them_after_a_move_without_advancing_combat(bool vector, bool hiddenCommands)
    {
        using GameAssets game = new(TestResources.GameDirectory);
        using BattleAssets assets = new(game.GameDirectory);
        NativeLimitProgress progress = game.ReadNewGameLimitProgress() with { DuelFlags = 0x3FF, Options978 = 0 };
        BattleSession session = new(new BattleDefinition { NativeCommandSequences = NativeBattleCommandSequenceFixture.Values, NativeRetainedMemory = NativeBattleRetainedMemory.FromColdProcess(), NativeActions = assets.NativeActions.ToArray(), LimitProgress = progress, NativeLimitsDisabled = false, Combatants = [new() { Id = "zell", Name = "Zell", IsPlayer = true, ModelId = 1, InitialAtb = BattleSession.AtbMaximum, NativeLimit = assets.ReadNativeLimitDefinition(1) }, new() { Id = "enemy", Name = "Enemy", Speed = 1, MaximumHp = 50000, CurrentHp = 50000 }] }, BattleAtbMode.TurnBased);
        BattleTargetRequest target = new(new BattleTargetSelection.Explicit([new BattleEnemyActorReference("enemy")]), BattleTargetEligibility.SavedStatus, false, BattleTargetReplacement.None);
        var states = (Dictionary<string, BattleNativeDuelState>)typeof(BattleSession).GetField("nativeDuelProcesses", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.GetValue(session)!;
        short timer = NativeBattleDuelRules.Timer(session.Combatants[0].NativeLimit!, 1);
        BattleNativeDuelState initial = new(0, target, timer, timer, BattleNativeDuelPhase.Ready) { Selection1D28DEF = 254, WidgetPhase = 4, WidgetRevealQ12 = 4096, PublicationLatch = 4 };
        states.Add("zell", initial);
        BattleHudController hud = new(session, session.QueueAttack, () => { }, () => { }, readPresentationUi: () => new(hiddenCommands, true, false, false)) { NativeButtonBindingHint = buttons => $"Key{buttons:X4}" };
        BattleNativeDuelChoice choice = session.GetNativeDuelChoices("zell").First(candidate => candidate.Learned);
        hud.SetNativeCommandButtons(hiddenCommands ? (ushort)0x100 : (ushort)0);
        Assert.Equal(hiddenCommands, hud.CommandMenuHidden);
        Assert.Null(hud.SelectedActor);
        string before = JsonSerializer.Serialize(session.Capture());

        UiView view = hud.BuildView();
        hud.BuildView();
        BattleUiTests.TestCanvas canvas = new();
        UiTheme theme = vector ? new VectorUiTheme() : new UiTheme();
        view.Draw(canvas, theme);

        Assert.Equal(before, JsonSerializer.Serialize(session.Capture()));
        Assert.Empty(hud.Commands);
        Assert.Contains(choice.Name + "  " + string.Join(" ", choice.Buttons.Select(button => ButtonPromptText.Mark($"Key{button:X4}"))), hud.ChoiceLabels);
        Assert.Contains(choice.Name, canvas.Texts);
        foreach (ushort button in choice.Buttons)
        {
            Assert.Contains(ButtonPromptText.Mark($"Key{button:X4}"), canvas.Texts);
        }
        int remaining = session.GetNativeDuel("zell")!.RemainingTimer;
        AssertDuelTimer(canvas, remaining);
        Assert.Empty(canvas.Pointers);
        string? output = Environment.GetEnvironmentVariable("MEMORIA_FF8_RESULT_UI_OUTPUT");
        if (output is not null)
        {
            Directory.CreateDirectory(output);
            using SkiaSharp.SKBitmap bitmap = new(1920, 1080);
            using (BattlePreviewCanvas preview = new(bitmap, game.GameDirectory))
            {
                view.Resize(bitmap.Width, bitmap.Height);
                view.Draw(preview, theme);
            }
            bitmap.SavePng(Path.Combine(output, $"duel-{(vector ? "vector" : "classic")}-{hiddenCommands}.png"));
        }
        view.Resize(960, 672);
        BattleNativeDuelState selected = initial;
        foreach (ushort button in choice.Buttons)
        {
            selected = NativeBattleDuelRules.AdvanceWidget(selected, true, button, false, [choice], static () => throw new InvalidOperationException());
        }
        states["zell"] = selected;

        Assert.Equal(0, session.Frame);
        Assert.Empty(session.QueuedActions);
        Assert.Equal(BattleNativeDuelPhase.AwaitingEffect, selected.Phase);
        before = JsonSerializer.Serialize(session.Capture());
        canvas = new();
        view.Draw(canvas, theme);
        AssertDuelTimer(canvas, session.GetNativeDuel("zell")!.RemainingTimer);
        Assert.DoesNotContain(choice.Name, canvas.Texts);
        Assert.Equal(before, JsonSerializer.Serialize(session.Capture()));
    }

    private static void AssertDuelTimer(BattleUiTests.TestCanvas canvas, int remaining)
    {
        int seconds = remaining / 60;
        int hundredths = 100 * (remaining % 60) / 60;
        (UiImageSource Source, UiRect Bounds)[] expected =
        [
            (new(UiImageKind.Icon, 111), new(777, 525, 72, 24)),
            (new(UiImageKind.SystemTimerDigit, seconds / 10), new(777, 558, 45, 96)),
            (new(UiImageKind.SystemTimerDigit, seconds % 10), new(824.25f, 558, 45, 96)),
            (new(UiImageKind.SystemTimerDigit, hundredths / 10), new(873.75f, 588, 24, 48)),
            (new(UiImageKind.SystemTimerDigit, hundredths % 10), new(897.75f, 588, 24, 48))
        ];
        Assert.Equal(expected, canvas.Images);
    }

    [Fact]
    public void Shot_process_holds_command_selection_and_cancel_queues_the_native_end_control()
    {
        using GameAssets game = new(TestResources.GameDirectory);
        using BattleAssets assets = new(game.GameDirectory);
        BattleDefinition definition = new()
        {
            StageId = assets.StageIds.First(),
            NativeShotAvailability = game.ReadNewGameShotAvailability() with { Flags770 = 0 },
            Items = [new(101, 2)],
            NativeLimitsDisabled = false,
            Combatants =
            [
                new() { Id = "irvine", Name = "Irvine", IsPlayer = true, ModelId = 2, WeaponId = assets.WeaponDefinitions.First(weapon => weapon.CharacterId == 2).Id, MaximumHp = 1000, CurrentHp = 1, InitialAtb = BattleSession.AtbMaximum },
                new() { Id = "ally", Name = "Ally", IsPlayer = true, ModelId = 0, MaximumHp = 1000, CurrentHp = 1, InitialAtb = BattleSession.AtbMaximum },
                new() { Id = "enemy", Name = "Enemy", ModelId = 1, MaximumHp = 10000, CurrentHp = 10000, Speed = 1 }
            ]
        };
        BattleSession session = InstalledBattlePractice.Create(definition, BattleAtbMode.TurnBased, game, assets);
        session.ConfigureDiagnosticScheduling(holdEnemyCommands: true, readinessActorId: "irvine");
        for (int frame = 0; frame < 1800 && !session.CanChoosePlayerCommand("irvine"); frame++)
        {
            session.Tick();
        }
        Assert.True(session.CanChoosePlayerCommand("irvine"));
        for (int selection = 0; selection < 256 && session.GetNativeLimitLevel("irvine") == 0; selection++)
        {
            session.RefreshNativeLimit("irvine");
        }
        Assert.InRange(session.GetNativeLimitLevel("irvine"), (byte)1, (byte)4);
        BattleHudController hud = new(session, session.QueueAttack, () => { }, () => { }, execute: input => session.QueueAction(input.ActorId!, input.TargetId!, input.Ability, input.ActionId), readPresentationUi: BattleHudTestPresentation.Visible);
        hud.BuildView();
        hud.Navigate(MenuInput.Right);
        hud.Choose(0);
        hud.Choose(0);
        hud.Attack();

        Assert.Equal((byte)14, Assert.Single(session.QueuedActions, command => command.ActorId == "irvine").NativeQueuedCommand);
        Assert.Empty(hud.Commands);
        hud.Attack();
        Assert.Single(session.QueuedActions, command => command.ActorId == "irvine");
        for (int frame = 0; frame < 1800 && session.GetNativeShot("irvine")?.Phase != BattleNativeShotPhase.Ready; frame++)
        {
            session.Tick();
        }
        Assert.Equal(BattleNativeShotPhase.Ready, session.GetNativeShot("irvine")!.Phase);

        hud.Navigate(MenuInput.Cancel);

        Assert.Equal((byte)238, Assert.Single(session.QueuedActions, command => command.ActorId == "irvine").NativeQueuedCommand);
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
        BattleHudController hud = new(session, session.QueueAttack, () => { }, () => { }, execute: input => session.QueueAction(input.ActorId!, input.TargetId!, input.Ability, input.ActionId), readPresentationUi: BattleHudTestPresentation.Visible);
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
