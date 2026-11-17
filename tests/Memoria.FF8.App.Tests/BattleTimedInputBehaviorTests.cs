using System.Reflection;
using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.GameUi;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class BattleTimedInputBehaviorTests
{
    [Fact]
    public void Closed_Duel_widget_releases_HUD_input_while_finisher_state_remains()
    {
        BattleSession battle = new(new BattleDefinition { Combatants = [new() { Id = "party", Name = "Party", IsPlayer = true }, new() { Id = "other", Name = "Other", IsPlayer = true, InitialAtb = BattleSession.AtbMaximum }, new() { Id = "enemy", Name = "Enemy" }] }, BattleAtbMode.TurnBased);
        typeof(BattleCombatant).GetProperty(nameof(BattleCombatant.IsBusy))!.SetValue(battle.Combatants[0], true);
        var duels = (Dictionary<string, BattleNativeDuelState>)typeof(BattleSession).GetField("nativeDuelProcesses", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(battle)!;
        duels.Add("party", new(0, new BattleTargetRequest(new BattleTargetSelection.Explicit([new BattleEnemyActorReference("enemy")]), BattleTargetEligibility.SavedStatus, false, BattleTargetReplacement.None), 400, 100, BattleNativeDuelPhase.Finisher) { WidgetPhase = 11, Selection1D28DEF = 255, Ui = new(BattleTimedInputUiPhase.Closing) });
        BattleHudController hud = new(battle, battle.QueueAttack, () => { }, () => { }, readPresentationUi: BattleHudTestPresentation.Visible);
        Assert.Empty(hud.Commands);
        Assert.True(battle.GetDuelUi("party")!.OwnsInput);
        for (int poll = 0; poll < 40 && !duels["party"].WidgetClosed; poll++)
        {
            duels["party"] = NativeBattleDuelRules.AdvanceWidget(duels["party"], poll % BattlePresentationTiming.NativeUiServicePollsPerCallback == 0, 0, false, [], static () => throw new InvalidOperationException());
        }
        Assert.NotNull(battle.GetNativeDuel("party"));
        Assert.False(battle.GetDuelUi("party")!.OwnsInput);
        Assert.Null(hud.NativeDuelState);
        Assert.Empty(battle.QueuedActions);
        Assert.Equal(0, battle.Frame);
    }

    [Fact]
    public void Closed_native_UI_projection_round_trips_without_any_model_resources()
    {
        BattleTimedInputUi original = new(BattleTimedInputUiPhase.Closed);
        string json = JsonSerializer.Serialize(original);
        BattleTimedInputUi restored = JsonSerializer.Deserialize<BattleTimedInputUi>(json)!;
        Assert.Equal(original, restored);
        Assert.False(restored.OwnsInput);
        Assert.DoesNotContain("Native", json);
        Assert.DoesNotContain("Resource", json);
        Assert.DoesNotContain("OwnsInput", json);
    }

}
