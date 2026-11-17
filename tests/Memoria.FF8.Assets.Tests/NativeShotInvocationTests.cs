using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeShotInvocationTests
{
    [Theory]
    [InlineData(0, 1)]
    [InlineData(1, 11)]
    [InlineData(7, 1)]
    public void ExpandedShotRetainsItsOriginalControlPhasesAndSelectedRecipientsAcrossJson(int shot, int expectedContacts)
    {
        using GameAssets game = new(TestResources.GameDirectory);
        using BattleAssets assets = new(game.GameDirectory);
        BattleDefinition definition = CreateBattle(game, assets, 2).ReadDefinition();
        BattleCombatantDefinition irvine = definition.Combatants[0];
        BattleCombatantDefinition enemy = definition.Combatants[1];
        BattleSession battle = new(definition with
        {
            Items = [new(shot + 101, 2)],
            Combatants =
            [
                .. Enumerable.Range(0, 10).Select(index => new BattleCombatantDefinition { Id = $"party-{index}", Name = $"Party {index}", ModelId = index, IsPlayer = true }),
                irvine,
                .. Enumerable.Range(0, 11).Select(index => enemy with { Id = $"enemy-{index}" })
            ]
        }, BattleAtbMode.TurnBased);
        battle = BattleSession.Restore(battle.Capture() with { NativeLimits = [new("irvine", 4)] });
        battle.QueueNativePlayerCommand("irvine", "enemy-10", 14, (ushort)shot);
        var shotTargets = Assert.IsType<BattleTargetSelection.Explicit>(battle.GetNativeShot("irvine")!.TargetRequest.Selection);
        Assert.Equal("enemy-10", Assert.Single(shotTargets.Actors).ActorId);
        Assert.Equal(2, Assert.Single(battle.Items).Count);
        Until(battle, () => battle.GetNativeShot("irvine")?.Phase == BattleNativeShotPhase.Ready);
        Assert.DoesNotContain(battle.Events, entry => entry.Kind == "EffectContact");
        BattleSession restored = BattleSession.Restore(battle.Capture());
        Assert.True(battle.TriggerNativeShot("irvine"));
        Assert.True(restored.TriggerNativeShot("irvine"));
        Assert.False(battle.TriggerNativeShot("irvine"));
        for (int frame = 0; frame < BattleSession.AttackWindupFrames + 2; frame++)
        {
            battle.Tick();
            restored.Tick();
        }
        Assert.Equal(BattleNativeShotPhase.Ready, battle.GetNativeShot("irvine")!.Phase);
        Assert.Equal((byte)(1 << shot), battle.NativeScriptState.ShotFlagsSetMask);
        Assert.Equal(1, Assert.Single(battle.Items).Count);
        BattleEvent[] contacts = battle.Events.Where(entry => entry.Kind == "EffectContact" && entry.ActorId == "irvine").ToArray();
        Assert.Equal(expectedContacts, contacts.Length);
        Assert.Contains(contacts, contact => contact.TargetId == "enemy-10");
        Assert.Equal(JsonSerializer.Serialize(battle.Capture()), JsonSerializer.Serialize(restored.Capture()));
        battle.EndNativeShot("irvine");
        Until(battle, () => battle.GetNativeShot("irvine") is null);
        Assert.Contains(battle.Events, entry => entry.Kind == "NativeLimitControl" && entry.Message == "Shot ended.");
        Assert.False(Assert.Single(battle.Combatants, actor => actor.Id == "irvine").IsBusy);
    }

    [Fact]
    public void Shot_timeout_queues_its_zero_damage_end_control_and_preserves_fractional_presentation_time()
    {
        using GameAssets game = new(TestResources.GameDirectory);
        using BattleAssets assets = new(game.GameDirectory);
        BattleSession battle = CreateBattle(game, assets, 2);
        battle.QueueNativePlayerCommand("irvine", "enemy", 14, 0);
        Until(battle, () => battle.GetNativeShot("irvine")?.Phase == BattleNativeShotPhase.Ready);
        long frame = battle.Frame;
        int hp = battle.Combatants[1].CurrentHp;

        battle.AdvanceNativeShotPresentation(TimeSpan.FromTicks(1));

        Assert.Equal(30, battle.GetNativeShot("irvine")!.UiSubframeTicks);
        BattleSnapshot snapshot = battle.Capture();
        battle = BattleSession.Restore(NativeBattleStorageSnapshots.Capture(snapshot));
        Assert.Equal(JsonSerializer.Serialize(snapshot), JsonSerializer.Serialize(battle.Capture()));
        battle.AdvanceNativeShotUi(battle.GetNativeShot("irvine")!.RemainingTimer);
        Assert.Equal(frame, battle.Frame);
        Assert.Equal(BattleNativeShotPhase.Ending, battle.GetNativeShot("irvine")!.Phase);
        Assert.False(battle.TriggerNativeShot("irvine"));
        Assert.Equal((byte)238, Assert.Single(battle.QueuedActions).NativeQueuedCommand);
        Until(battle, () => battle.GetNativeShot("irvine") is null);
        Assert.Equal(hp, battle.Combatants[1].CurrentHp);
        Assert.Equal(2, battle.Items.Single(item => item.Id == 101).Count);
    }

    [Fact]
    public void Shot_control_commands_do_not_damage_and_each_accepted_trigger_consumes_one_ammunition()
    {
        using GameAssets game = new(TestResources.GameDirectory);
        using BattleAssets assets = new(game.GameDirectory);
        BattleSession battle = CreateBattle(game, assets, 2);
        int initialHp = battle.Combatants[1].CurrentHp;

        battle.QueueNativePlayerCommand("irvine", "enemy", 14, 0);
        Assert.Equal(BattleNativeShotPhase.Opening, battle.GetNativeShot("irvine")!.Phase);
        Assert.False(battle.TriggerNativeShot("irvine"));
        Until(battle, () => battle.GetNativeShot("irvine")?.Phase == BattleNativeShotPhase.Ready);

        Assert.Equal(initialHp, battle.Combatants[1].CurrentHp);
        Assert.Equal(2, battle.Items.Single(item => item.Id == 101).Count);
        Assert.True(battle.TriggerNativeShot("irvine"));
        Assert.False(battle.TriggerNativeShot("irvine"));
        Assert.Equal(1, battle.Items.Single(item => item.Id == 101).Count);
        Until(battle, () => battle.GetNativeShot("irvine")?.Phase == BattleNativeShotPhase.Ready);
        Assert.True(battle.Combatants[1].CurrentHp < initialHp);
        Assert.Equal((byte)1, battle.NativeScriptState.ShotFlagsSetMask);
        Assert.Equal((byte)1, battle.NativeScriptState.ShotFlagsAfter);
        int afterShotHp = battle.Combatants[1].CurrentHp;
        battle.EndNativeShot("irvine");
        Assert.Equal((byte)238, Assert.Single(battle.QueuedActions).NativeQueuedCommand);
        Until(battle, () => battle.GetNativeShot("irvine") is null);
        Assert.Equal(afterShotHp, battle.Combatants[1].CurrentHp);
        Assert.False(battle.Combatants[0].IsBusy);
    }

    [Fact]
    public void Shot_ui_time_and_ready_callback_restore_without_advancing_the_battle_gameplay_clock()
    {
        using GameAssets game = new(TestResources.GameDirectory);
        using BattleAssets assets = new(game.GameDirectory);
        BattleSession battle = CreateBattle(game, assets, 1);
        battle.QueueNativePlayerCommand("irvine", "enemy", 14, 0);
        Until(battle, () => battle.GetNativeShot("irvine")?.Phase == BattleNativeShotPhase.Ready);
        BattleSnapshot before = battle.Capture();

        battle.AdvanceNativeShotUi(3);

        BattleSnapshot after = battle.Capture();
        Assert.Equal(before.Frame, after.Frame);
        Assert.Equal(JsonSerializer.Serialize(before.Random), JsonSerializer.Serialize(after.Random));
        Assert.Equal(JsonSerializer.Serialize(before.Combatants), JsonSerializer.Serialize(after.Combatants));
        Assert.Equal(before.NativeLimits[0].Shot!.RemainingTimer - 3, after.NativeLimits[0].Shot!.RemainingTimer);
        string snapshot = JsonSerializer.Serialize(after);
        BattleSession restored = BattleSession.Restore(JsonSerializer.Deserialize<BattleSnapshot>(snapshot)!);
        Assert.Equal(snapshot, JsonSerializer.Serialize(restored.Capture()));
        Assert.True(restored.TriggerNativeShot("irvine"));
        Until(restored, () => restored.GetNativeShot("irvine") is null);
        Assert.Empty(restored.Items);
        Assert.Contains(restored.Events, item => item.Kind == "NativeLimitControl" && item.Message == "Shot ended.");
    }

    private static BattleSession CreateBattle(GameAssets game, BattleAssets assets, byte ammoCount)
    {
        BattleSession battle = new(new BattleDefinition { NativeCommandSequences = NativeBattleCommandSequenceFixture.Values, NativeRetainedMemory = NativeBattleRetainedMemory.FromColdProcess(), NativeActions = assets.NativeActions.ToArray(), NativeShotAvailability = game.ReadNewGameShotAvailability() with { Flags770 = 0 }, NativeLimitsDisabled = false, Items = [new(101, ammoCount)], Combatants = [new() { Id = "irvine", Name = "Irvine", IsPlayer = true, ModelId = 2, Attack = 80, Hit = 255, InitialAtb = BattleSession.AtbMaximum, NativeLimit = assets.ReadNativeLimitDefinition(2) }, new() { Id = "enemy", Name = "Enemy", MaximumHp = 50000, CurrentHp = 50000, Speed = 1, Defense = 1 }] }, BattleAtbMode.TurnBased);
        return BattleSession.Restore(battle.Capture() with { NativeLimits = [new("irvine", 4)] });
    }

    private static void Until(BattleSession battle, Func<bool> complete)
    {
        for (int frame = 0; frame < 100 && !complete(); frame++)
        {
            battle.Tick();
        }
        Assert.True(complete());
    }
}
