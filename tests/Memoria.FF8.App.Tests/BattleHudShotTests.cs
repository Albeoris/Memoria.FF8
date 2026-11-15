using System.Reflection;
using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.GameUi;
using Memoria.FF8.Runtime;
using Memoria.FF8.Ui;

namespace Memoria.FF8.App.Tests;

public sealed class BattleHudShotTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Shot_menu_dispatches_the_selected_ammunition_and_draws_a_working_fire_panel(bool directFire)
    {
        string directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR") ?? TestResources.GameDirectory;
        Assert.SkipUnless(Directory.Exists(directory), "Installed original battle resources are required.");
        using GameAssets game = new(directory);
        using BattleAssets assets = new(directory);
        BattleSession battle = new(new BattleDefinition
        {
            NativeCommandSequences = NativeBattleCommandSequenceFixture.Values,
            NativeRetainedMemory = NativeBattleRetainedMemory.FromColdProcess(),
            NativeActions = assets.NativeActions.ToArray(),
            NativeShotAvailability = game.ReadNewGameShotAvailability() with { Flags770 = 0 },
            Items = [new(101, 2)],
            NativeLimitsDisabled = false,
            Combatants =
            [
                new() { Id = "irvine", Name = "Irvine", IsPlayer = true, ModelId = 2, InitialAtb = BattleSession.AtbMaximum, NativeLimit = assets.ReadNativeLimitDefinition(2) },
                new() { Id = "enemy", Name = "Shot target", Speed = 1, MaximumHp = 50000, CurrentHp = 50000 }
            ]
        }, BattleAtbMode.TurnBased);
        battle = BattleSession.Restore(battle.Capture() with { NativeLimits = [new("irvine", 4)] });
        List<BattleInput> inputs = [];
        void Execute(BattleInput input)
        {
            inputs.Add(input);
            if (input.Kind == BattleInputKind.ShotTrigger)
            {
                Assert.True(battle.TriggerNativeShot(input.ActorId!));
            }
            else
            {
                battle.QueueAction(input.ActorId!, input.TargetId!, input.Ability, input.ActionId);
            }
        }
        BattleHudController hud = CreateHud(battle, Execute);
        hud.Navigate(MenuInput.Right);
        Assert.Equal(BattleAbility.LimitBreak, hud.SelectedCommand);
        hud.Navigate(MenuInput.Confirm);
        Assert.True(hud.SelectingAction);
        Assert.False(hud.SelectingTarget);
        Assert.Single(hud.ActionChoices);
        int ammunition = hud.SelectedAction!.Id;
        hud.Navigate(MenuInput.Confirm);
        Assert.True(hud.SelectingTarget);
        Assert.Empty(inputs);
        hud.Navigate(MenuInput.Confirm);
        BattleInput opening = Assert.Single(inputs);
        Assert.Equal(BattleInputKind.Action, opening.Kind);
        Assert.Equal(BattleAbility.LimitBreak, opening.Ability);
        Assert.Equal(ammunition, opening.ActionId);
        Assert.Equal("enemy", opening.TargetId);
        Assert.Equal((byte)14, Assert.Single(battle.QueuedActions).NativeQueuedCommand);
        for (int frame = 0; frame < 100 && battle.GetNativeShot("irvine")?.Phase != BattleNativeShotPhase.Ready; frame++)
        {
            battle.Tick();
        }
        Assert.Equal(BattleNativeShotPhase.Ready, battle.GetNativeShot("irvine")!.Phase);
        battle.AdvanceNativeShotUi(3);
        string readySnapshot = JsonSerializer.Serialize(battle.Capture());
        battle = BattleSession.Restore(JsonSerializer.Deserialize<BattleSnapshot>(readySnapshot)!);
        Assert.Equal(readySnapshot, JsonSerializer.Serialize(battle.Capture()));
        hud = CreateHud(battle, directFire ? null : Execute);
        UiView view = hud.BuildView();
        view.Resize(960, 672);
        string before = JsonSerializer.Serialize(battle.Capture());
        BattleUiTests.TestCanvas canvas = new();
        view.Draw(canvas, new UiTheme());
        Assert.Equal(before, JsonSerializer.Serialize(battle.Capture()));
        Assert.Contains(canvas.Texts, text => text.Contains("Shot target", StringComparison.Ordinal));
        Assert.Contains(canvas.Texts, text => text.Contains("Fire", StringComparison.Ordinal) && text.Contains("FireKey", StringComparison.Ordinal));
        Assert.Contains(canvas.Texts, text => text.Contains("Finish", StringComparison.Ordinal) && text.Contains("Key0010", StringComparison.Ordinal));
        Assert.Contains(canvas.Texts, text => text.Contains("2", StringComparison.Ordinal) && text.Contains("ammo", StringComparison.OrdinalIgnoreCase));
        BattleNativeShotState ready = battle.GetNativeShot("irvine")!;
        int remaining = Math.Max(0, (int)ready.RemainingTimer);
        string timer = $"{remaining / BattleSession.FramesPerSecond}.{100 * (remaining % BattleSession.FramesPerSecond) / BattleSession.FramesPerSecond:00}";
        Assert.Contains(canvas.Texts, text => text.Contains(timer, StringComparison.Ordinal));
        view.Pointer(150, 620, false);
        view.Pointer(150, 620, true);
        view.Pointer(150, 620, true);
        Assert.Equal(BattleNativeShotPhase.AwaitingEffect, battle.GetNativeShot("irvine")!.Phase);
        Assert.Equal(1, battle.Items.Single(stock => stock.Id == 101).Count);
        Assert.Equal((byte)237, Assert.Single(battle.QueuedActions).NativeQueuedCommand);
        Assert.Equal(directFire ? 0 : 1, inputs.Count(input => input.Kind == BattleInputKind.ShotTrigger));
        hud.Navigate(MenuInput.Cancel);
        hud.Navigate(MenuInput.Cancel);
        Assert.Equal(BattleNativeShotPhase.Ending, battle.GetNativeShot("irvine")!.Phase);
        Assert.Single(battle.QueuedActions, action => action.NativeQueuedCommand == 238);
    }

    [Theory]
    [InlineData(BattleNativeShotPhase.Opening)]
    [InlineData(BattleNativeShotPhase.AwaitingEffect)]
    [InlineData(BattleNativeShotPhase.Ending)]
    public void Nonready_Shot_panels_render_without_admitting_a_fire_press(BattleNativeShotPhase phase)
    {
        BattleSession battle = new(new BattleDefinition { Combatants = [new() { Id = "irvine", Name = "Irvine", IsPlayer = true }, new() { Id = "enemy", Name = "Shot target" }] }, BattleAtbMode.TurnBased);
        Dictionary<string, BattleNativeShotState> shots = (Dictionary<string, BattleNativeShotState>)typeof(BattleSession).GetField("nativeShotProcesses", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(battle)!;
        shots.Add("irvine", new(0, 8, 240, 180, 2, phase) { Ui = new(BattleTimedInputUiPhase.Active) });
        List<BattleInput> inputs = [];
        BattleHudController hud = CreateHud(battle, inputs.Add);
        UiView view = hud.BuildView();
        view.Resize(960, 672);
        BattleUiTests.TestCanvas canvas = new();
        view.Draw(canvas, new UiTheme());
        Assert.Contains(canvas.Texts, text => text.Contains("TARGET", StringComparison.Ordinal));
        Assert.Contains(canvas.Texts, text => text.Contains("Ammo: 2", StringComparison.Ordinal));
        Assert.Contains("6.00", canvas.Texts);
        view.Pointer(150, 620, false);
        view.Pointer(150, 620, true);
        Assert.Empty(inputs);
        Assert.Equal(phase, battle.GetNativeShot("irvine")!.Phase);
        Assert.Empty(battle.QueuedActions);
    }

    [Fact]
    public void Ready_Shot_draws_fire_and_finish_bindings_even_without_installed_resources()
    {
        BattleSession battle = new(new BattleDefinition { Combatants = [new() { Id = "irvine", Name = "Irvine", IsPlayer = true }, new() { Id = "enemy", Name = "Shot target" }] }, BattleAtbMode.TurnBased);
        Dictionary<string, BattleNativeShotState> shots = (Dictionary<string, BattleNativeShotState>)typeof(BattleSession).GetField("nativeShotProcesses", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(battle)!;
        shots.Add("irvine", new(0, 8, 240, 179, 2, BattleNativeShotPhase.Ready) { Ui = new(BattleTimedInputUiPhase.Active) });
        BattleHudController hud = CreateHud(battle);
        UiView view = hud.BuildView();
        BattleUiTests.TestCanvas canvas = new();
        view.Draw(canvas, new UiTheme());
        Assert.Contains(canvas.Texts, text => text.Contains("TARGET", StringComparison.Ordinal));
        Assert.Contains(canvas.Texts, text => text.Contains("FireKey", StringComparison.Ordinal) && text.Contains("Fire", StringComparison.Ordinal));
        Assert.Contains(canvas.Texts, text => text.Contains("Key0010", StringComparison.Ordinal) && text.Contains("Finish", StringComparison.Ordinal));
        Assert.Contains("Ammo: 2", canvas.Texts);
        Assert.Contains("5.96", canvas.Texts);
        Assert.Empty(hud.Commands);
    }

    private static BattleHudController CreateHud(BattleSession battle, Action<BattleInput>? execute = null)
    {
        return new(battle, battle.QueueAttack, () => { }, () => { }, execute: execute)
        {
            GunbladeBindingHint = "FireKey",
            NativeButtonBindingHint = buttons => $"Key{buttons:X4}"
        };
    }
}
