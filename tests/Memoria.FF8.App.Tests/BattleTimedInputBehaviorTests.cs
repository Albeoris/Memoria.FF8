using System.Reflection;
using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.GameUi;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class BattleTimedInputBehaviorTests
{
    [Fact]
    public void Shot_single_press_consumes_one_ammunition_and_old_ready_save_needs_no_new_presentation_fields()
    {
        using GameAssets game = new(TestResources.GameDirectory);
        using BattleAssets assets = new(game.GameDirectory);
        BattleSession battle = Shot(game, assets);
        battle.QueueNativePlayerCommand("party", "enemy", 14, 0);
        Until(battle, () => battle.GetNativeShot("party")?.Phase == BattleNativeShotPhase.Ready);
        BattleSnapshot snapshot = battle.Capture();
        BattleNativeLimitState[] limits = snapshot.NativeLimits.Select(limit => limit.Shot is { } shot ? limit with { Shot = shot with { Ui = null } } : limit).ToArray();
        BattleSession restored = BattleSession.Restore(JsonSerializer.Deserialize<BattleSnapshot>(JsonSerializer.Serialize(snapshot with { NativeLimits = limits }))!);
        Assert.Equal(BattleTimedInputUiPhase.Active, restored.GetShotUi("party")!.Phase);
        long frame = restored.Frame;
        string before = JsonSerializer.Serialize(restored.Capture());
        restored.AdvanceNativeShotPresentation(TimeSpan.Zero);
        Assert.Equal(before, JsonSerializer.Serialize(restored.Capture()));
        Assert.True(restored.TriggerNativeShot("party"));
        Assert.False(restored.TriggerNativeShot("party"));
        Assert.Equal(1, restored.Items.Single(item => item.Id == 101).Count);
        Assert.Single(restored.QueuedActions, action => action.NativeQueuedCommand == 237);
        restored.EndNativeShot("party");
        restored.EndNativeShot("party");
        Assert.Single(restored.QueuedActions, action => action.NativeQueuedCommand == 238);
        Assert.Equal(1, restored.Items.Single(item => item.Id == 101).Count);
        Assert.Equal(frame, restored.Frame);
        //Headless callers may stop pumping presentation after issuing the ending command.
        for (int tick = 0; tick < 100 && restored.GetNativeShot("party") is not null; tick++)
        {
            restored.Tick();
        }
        Assert.Null(restored.GetNativeShot("party"));
        Assert.Null(restored.GetShotUi("party"));
        Assert.Equal(1, restored.Items.Single(item => item.Id == 101).Count);
    }

    [Fact]
    public void Closing_releases_other_party_commands_while_the_finisher_is_still_running()
    {
        BattleSession battle = new(new BattleDefinition { Combatants = [new() { Id = "party", Name = "Party", IsPlayer = true }, new() { Id = "other", Name = "Other", IsPlayer = true, InitialAtb = BattleSession.AtbMaximum }, new() { Id = "enemy", Name = "Enemy" }] }, BattleAtbMode.TurnBased);
        typeof(BattleCombatant).GetProperty(nameof(BattleCombatant.IsBusy))!.SetValue(battle.Combatants[0], true);
        var duels = (Dictionary<string, BattleNativeDuelState>)typeof(BattleSession).GetField("nativeDuelProcesses", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(battle)!;
        //An older finisher state has no optional UI fields. Its already submitted action must not be replayed.
        duels.Add("party", new(0, 8, 400, 100, BattleNativeDuelPhase.Finisher));
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

    private static BattleSession Shot(GameAssets game, BattleAssets assets)
    {
        BattleSession battle = new(new BattleDefinition
        {
            NativeRetainedMemory = NativeBattleRetainedMemory.FromColdProcess(),
            NativeCommandSequences = NativeBattleCommandSequenceFixture.Values,
            NativeActions = assets.NativeActions.ToArray(),
            NativeShotAvailability = game.ReadNewGameShotAvailability() with { Flags770 = 0 },
            NativeLimitsDisabled = false,
            Items = [new(101, 2)],
            Combatants = [new() { Id = "party", Name = "Irvine", IsPlayer = true, ModelId = 2, Attack = 80, Hit = 255, InitialAtb = BattleSession.AtbMaximum, NativeLimit = assets.ReadNativeLimitDefinition(2) }, new() { Id = "enemy", Name = "Enemy", MaximumHp = 50000, CurrentHp = 50000, Speed = 1 }]
        }, BattleAtbMode.TurnBased);
        return BattleSession.Restore(battle.Capture() with { NativeLimits = [new("party", 4)] });
    }

    private static void Until(BattleSession battle, Func<bool> done)
    {
        for (int tick = 0; tick < 100 && !done(); tick++)
        {
            battle.Tick();
            battle.AdvanceNativeShotPresentation(TimeSpan.FromSeconds(1d / BattleSession.FramesPerSecond));
        }
        Assert.True(done());
    }
}
