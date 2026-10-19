using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Battle.Tests;

public sealed class NativeBattleLimitSelectionTests
{
    [Fact]
    public void Explicit_actor_switches_and_same_actor_reselection_each_consume_one_crisis_draw()
    {
        BattleSession battle = CreateBattle();
        NativeBattleRandomState expected = battle.Capture().Random!;

        foreach (string actorId in new[] { "quistis", "squall", "quistis", "quistis" })
        {
            expected = expected.Draw(out byte roll);
            BattleCombatant actor = battle.Combatants.Single(candidate => candidate.Id == actorId);

            battle.SelectPlayerCommandActor(actorId);

            Assert.True(battle.HasNativeLimitSelection(actorId));
            Assert.Equal(NativeBattleLimitRules.CrisisLevel(actor.NativeLimit!, actor.CurrentHp, actor.MaximumHp, 0, 0, 0, roll), battle.GetNativeLimitLevel(actorId));
            Assert.Equal(JsonSerializer.Serialize(expected), JsonSerializer.Serialize(battle.Capture().Random));
        }
    }

    [Theory]
    [InlineData(true, 0u)]
    [InlineData(false, 0x200u)]
    public void Prohibition_and_curse_select_zero_crisis_without_drawing_random(bool disabled, uint statuses)
    {
        BattleSession battle = CreateBattle(disabled, statuses);
        string random = JsonSerializer.Serialize(battle.Capture().Random);

        battle.SelectPlayerCommandActor("quistis");
        battle.SelectPlayerCommandActor("quistis");

        Assert.True(battle.HasNativeLimitSelection("quistis"));
        Assert.Equal((byte)0, battle.GetNativeLimitLevel("quistis"));
        Assert.Equal(random, JsonSerializer.Serialize(battle.Capture().Random));
    }

    [Fact]
    public void Json_restore_and_command_inspection_preserve_saved_crisis_and_random_state()
    {
        BattleSession battle = CreateBattle();
        battle.SelectPlayerCommandActor("quistis");
        battle.SelectPlayerCommandActor("squall");
        string snapshot = JsonSerializer.Serialize(battle.Capture());

        BattleSession restored = BattleSession.Restore(JsonSerializer.Deserialize<BattleSnapshot>(snapshot)!);
        for (int inspection = 0; inspection < 2; inspection++)
        {
            foreach (string actorId in new[] { "quistis", "squall" })
            {
                Assert.True(restored.HasNativeLimitSelection(actorId));
                Assert.Equal(battle.GetNativeLimitLevel(actorId), restored.GetNativeLimitLevel(actorId));
                Assert.True(restored.CanChoosePlayerCommand(actorId));
            }
            Assert.Equal(2, restored.ReadyPlayerCommandOrder.Count);
            _ = restored.SelectedPlayerCommandActorId;
        }

        Assert.Equal(snapshot, JsonSerializer.Serialize(restored.Capture()));
    }

    [Theory]
    [InlineData("quistis")]
    [InlineData("enemy")]
    public void Ineligible_actor_selection_refresh_and_limit_queue_reject_without_mutation(string actorId)
    {
        BattleSession battle = CreateBattle(ready: false);
        battle = BattleSession.Restore(battle.Capture() with { NativeLimits = [new("quistis", 4)] });
        string before = JsonSerializer.Serialize(battle.Capture());

        Assert.Throws<InvalidOperationException>(() => battle.SelectPlayerCommandActor(actorId));
        Assert.Equal(before, JsonSerializer.Serialize(battle.Capture()));
        Assert.Throws<InvalidOperationException>(() => battle.RefreshNativeLimit(actorId));
        Assert.Equal(before, JsonSerializer.Serialize(battle.Capture()));
        Assert.Throws<InvalidOperationException>(() => battle.QueueNativePlayerCommand(actorId, "enemy", 15, 0));
        Assert.Equal(before, JsonSerializer.Serialize(battle.Capture()));
    }

    private static BattleSession CreateBattle(bool disabled = false, uint statuses = 0, bool ready = true)
    {
        ImmutableArray<byte> weights = Enumerable.Repeat((byte)0, 32).ToImmutableArray();
        BattleNativeLimitDefinition blueMagic = new(3, 15, 0, 250, weights);
        BattleNativeLimitDefinition renzokuken = new(0, 5, 0, 250, weights);
        return new(new BattleDefinition
        {
            NativeLimitsDisabled = disabled,
            Combatants =
            [
                new() { Id = "quistis", Name = "Quistis", IsPlayer = true, ModelId = 3, MaximumHp = 100, CurrentHp = 10, InitialAtb = ready ? BattleSession.AtbMaximum : 0, NativeStatus1 = statuses, NativeLimit = blueMagic },
                new() { Id = "squall", Name = "Squall", IsPlayer = true, ModelId = 0, MaximumHp = 100, CurrentHp = 20, InitialAtb = BattleSession.AtbMaximum, NativeLimit = renzokuken },
                new() { Id = "enemy", Name = "Enemy" }
            ]
        }, BattleAtbMode.TurnBased);
    }
}
