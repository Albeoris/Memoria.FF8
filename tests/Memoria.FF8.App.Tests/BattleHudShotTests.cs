using System.Reflection;
using Memoria.FF8.Assets;
using Memoria.FF8.GameUi;
using Memoria.FF8.Runtime;
using Memoria.FF8.Ui;

namespace Memoria.FF8.App.Tests;

public sealed class BattleHudShotTests
{
    [Fact]
    public void Shot_menu_dispatches_the_selected_ammunition_to_the_native_opening()
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
        Assert.Equal(BattleNativeShotPhase.Opening, battle.GetNativeShot("irvine")!.Phase);
    }

    [Theory]
    [InlineData(BattleNativeShotPhase.Opening)]
    [InlineData(BattleNativeShotPhase.AwaitingEffect)]
    [InlineData(BattleNativeShotPhase.Ending)]
    public void Nonready_Shot_panels_render_without_admitting_a_fire_press(BattleNativeShotPhase phase)
    {
        BattleSession battle = new(new BattleDefinition { Combatants = [new() { Id = "irvine", Name = "Irvine", IsPlayer = true }, new() { Id = "enemy", Name = "Shot target" }] }, BattleAtbMode.TurnBased);
        Dictionary<string, BattleNativeShotState> shots = (Dictionary<string, BattleNativeShotState>)typeof(BattleSession).GetField("nativeShotProcesses", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(battle)!;
        BattleTargetRequest target = new(new BattleTargetSelection.Explicit([new BattleEnemyActorReference("enemy")]), BattleTargetEligibility.Living, false, BattleTargetReplacement.None);
        shots.Add("irvine", new(0, target, 240, 180, 2, phase) { Widget = new(180, phase == BattleNativeShotPhase.Ending ? (byte)8 : (byte)4, 3, 0, false) });
        List<BattleInput> inputs = [];
        BattleHudController hud = CreateHud(battle, inputs.Add);
        UiView view = hud.BuildView();
        view.Resize(960, 672);
        BattleUiTests.TestCanvas canvas = new();
        view.Draw(canvas, new UiTheme());
        Assert.Contains(canvas.Texts, text => text.Contains("TARGET", StringComparison.Ordinal));
        Assert.Contains(canvas.Texts, text => text.Contains("Ammo: 2", StringComparison.Ordinal));
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
        BattleTargetRequest target = new(new BattleTargetSelection.Explicit([new BattleEnemyActorReference("enemy")]), BattleTargetEligibility.Living, false, BattleTargetReplacement.None);
        shots.Add("irvine", new(0, target, 240, 179, 2, BattleNativeShotPhase.Ready) { Widget = new(179, 5, 3, 0, false) });
        BattleHudController hud = CreateHud(battle);
        UiView view = hud.BuildView();
        BattleUiTests.TestCanvas canvas = new();
        view.Draw(canvas, new UiTheme());
        Assert.Contains(canvas.Texts, text => text.Contains("TARGET", StringComparison.Ordinal));
        Assert.Contains(canvas.Texts, text => text.Contains("FireKey", StringComparison.Ordinal) && text.Contains("Fire", StringComparison.Ordinal));
        Assert.Contains(canvas.Texts, text => text.Contains("Key0010", StringComparison.Ordinal) && text.Contains("Finish", StringComparison.Ordinal));
        Assert.Contains("Ammo: 2", canvas.Texts);
        Assert.Empty(hud.Commands);
    }

    private static BattleHudController CreateHud(BattleSession battle, Action<BattleInput>? execute = null)
    {
        return new(battle, battle.QueueAttack, () => { }, () => { }, execute: execute, readPresentationUi: BattleHudTestPresentation.Visible)
        {
            GunbladeBindingHint = "FireKey",
            NativeButtonBindingHint = buttons => $"Key{buttons:X4}"
        };
    }
}
