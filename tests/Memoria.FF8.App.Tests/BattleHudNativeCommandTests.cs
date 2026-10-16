using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.GameUi;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class BattleHudNativeCommandTests
{
    [Fact]
    public void Restored_Limit_selection_is_visible_and_queueable_without_an_extra_rng_draw_from_rendering()
    {
        string directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR") ?? @"C:\Steam\steamapps\common\FINAL FANTASY VIII";
        Assert.SkipUnless(Directory.Exists(directory), "Installed battle resources are required.");
        using BattleAssets assets = new(directory);
        NativeLimitProgress progress = new(new string('A', 64), 1, 0, 0, 0, Enumerable.Repeat((byte)0, 8).ToImmutableArray());
        BattleSession session = new(new BattleDefinition { NativeActions = assets.NativeActions.ToArray(), LimitProgress = progress, NativeLimitsDisabled = false, Combatants = [new() { Id = "quistis", Name = "Quistis", IsPlayer = true, ModelId = 3, InitialAtb = BattleSession.AtbMaximum, NativeLimit = assets.ReadNativeLimitDefinition(3) }, new() { Id = "enemy", Name = "Enemy" }] }, BattleAtbMode.TurnBased);
        session = BattleSession.Restore(session.Capture() with { NativeLimits = [new("quistis", 4)] });
        BattleHudController hud = new(session, session.QueueAttack, () => { }, () => { }, execute: input => session.QueueAction(input.ActorId!, input.TargetId!, input.Ability, input.ActionId));
        string before = JsonSerializer.Serialize(session.Capture());

        hud.BuildView();
        hud.BuildView();

        Assert.Equal(before, JsonSerializer.Serialize(session.Capture()));
        Assert.Contains(BattleAbility.LimitBreak, hud.Commands);
        hud.Choose(1);
        Assert.True(hud.SelectingAction);
        Assert.Single(hud.ActionChoices);
        hud.Choose(0);
        hud.Attack();

        Assert.Equal((byte)15, Assert.Single(session.QueuedActions).NativeAction!.Command);
        Assert.False(hud.SelectingAction);
    }
}
