using System.Reflection;
using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.GameUi;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class BattleTimedInputBehaviorTests
{
    [Fact]
    public void Closing_releases_other_party_commands_while_the_finisher_is_still_running()
    {
        BattleSession battle = new(new BattleDefinition { Combatants = [new() { Id = "party", Name = "Party", IsPlayer = true }, new() { Id = "other", Name = "Other", IsPlayer = true, InitialAtb = BattleSession.AtbMaximum }, new() { Id = "enemy", Name = "Enemy" }] }, BattleAtbMode.TurnBased);
        typeof(BattleCombatant).GetProperty(nameof(BattleCombatant.IsBusy))!.SetValue(battle.Combatants[0], true);
        var duels = (Dictionary<string, BattleNativeDuelState>)typeof(BattleSession).GetField("nativeDuelProcesses", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(battle)!;
        //An older finisher state has no optional UI fields. Its already submitted action must not be replayed.
        duels.Add("party", new(0, new BattleTargetRequest(new BattleTargetSelection.Explicit([new BattleEnemyActorReference("enemy")]), BattleTargetEligibility.SavedStatus, false, BattleTargetReplacement.None), 400, 100, BattleNativeDuelPhase.Finisher));
        BattleHudController hud = new(battle, battle.QueueAttack, () => { }, () => { });
        Assert.Empty(hud.Commands);
        Assert.True(battle.GetDuelUi("party")!.OwnsInput);
        string before = JsonSerializer.Serialize(duels["party"]);
        battle.AdvanceNativeDuelPresentation(TimeSpan.Zero, 1);
        Assert.Equal(before, JsonSerializer.Serialize(duels["party"]));
        battle.AdvanceNativeDuelPresentation(TimeSpan.FromSeconds(0.25), 0);
        Assert.NotNull(battle.GetNativeDuel("party"));
        Assert.False(battle.GetDuelUi("party")!.OwnsInput);
        Assert.Null(hud.NativeDuelState);
        Assert.Equal("other", hud.SelectedActor!.Id);
        Assert.Contains(BattleAbility.Attack, hud.Commands);
        Assert.Empty(battle.QueuedActions);
        Assert.Equal(0, battle.Frame);
    }

    [Fact]
    public void Closing_ui_serializes_compactly_and_progresses_without_any_model_or_native_memory()
    {
        BattleTimedInputUi original = new(BattleTimedInputUiPhase.Closing, 3);
        string json = JsonSerializer.Serialize(original);
        BattleTimedInputUi restored = JsonSerializer.Deserialize<BattleTimedInputUi>(json)!;
        for (int tick = 0; tick < 12; tick++)
        {
            original = original.Advance(60);
            restored = restored.Advance(60);
        }
        Assert.Equal(original, restored);
        Assert.False(restored.OwnsInput);
        Assert.DoesNotContain("Native", json);
        Assert.DoesNotContain("Resource", json);
        Assert.DoesNotContain("OwnsInput", json);
    }

}
