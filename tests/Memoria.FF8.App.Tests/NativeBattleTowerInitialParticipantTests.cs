using System.Text.Json;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeBattleTowerInitialParticipantTests
{
    [Fact]
    public void Saved_tower_approach_does_not_run_suspended_Wedge_initialization()
    {
        string source = TestResources.FixturePath("App.Tests.TowerEntry.1");
        byte[] original = File.ReadAllBytes(source);
        GameSession session = new(GameSession.Load(source));
        using GameWorld world = new(session, TestResources.GameDirectory);
        world.EnsureField();
        for (int frame = 0; frame < 3000 && !world.HasPendingBattle; frame++)
        {
            PlayerAction input = world.CurrentDialogue?.CanConfirm == true && world.CurrentDialogue.FirstChoice is int first ? new(PlayerActionKind.Choose, first) : new(world.CurrentDialogue?.CanConfirm == true ? PlayerActionKind.Confirm : PlayerActionKind.Wait);
            Assert.True(world.Apply(input).Accepted);
        }
        Assert.True(world.HasPendingBattle);
        world.ApplyBattleInput(new(BattleInputKind.StartEncounter, Mode: session.State.GameplayPreferences.AtbMode, NativeSeed: 0));
        BattleSession battle = world.Battle!;
        Assert.Equal(29, battle.EncounterId);
        for (int frame = 0; frame < 240; frame++)
        {
            world.ApplyBattleInput(new(BattleInputKind.Tick, Frames: 1));
            AssertReserveBeforeFourthTurn(battle);
            Assert.DoesNotContain(battle.Events, item => item.ActorId == "enemy-1" && item.Kind is "Queued" or "Windup" or "NativeText");
        }
        string output = Environment.GetEnvironmentVariable("MEMORIA_FF8_TOWER_ENTRY_OUTPUT") ?? TestResources.ArtifactDirectory;
        Directory.CreateDirectory(output);
        string entry = Path.Combine(output, "tower-before-fourth-turn.json");
        session.SetEventState(world.Events.Capture());
        session.Save(entry, compact: true);
        BattleSession restored = BattleSession.Restore(GameSession.Load(entry).Battle!);
        Assert.Equal(JsonSerializer.Serialize(battle.Capture()), JsonSerializer.Serialize(restored.Capture()));
        world.ApplyBattleInput(new(BattleInputKind.Tick, Frames: 1));
        restored.Tick();
        AssertReserveBeforeFourthTurn(battle);
        Assert.Equal(JsonSerializer.Serialize(battle.Capture()), JsonSerializer.Serialize(restored.Capture()));
        session.SetEventState(world.Events.Capture());
        session.Save(Path.Combine(output, "tower-entry-next-callback.json"), compact: true);
        Assert.Equal(original, File.ReadAllBytes(source));
    }

    [Fact]
    public void Saved_tower_entry_resumes_without_initializing_suspended_participants()
    {
        GameSession session = new(GameSession.Load(TestResources.FixturePath("App.Tests.TowerEntry.2")));
        using GameWorld world = new(session, TestResources.GameDirectory);
        world.EnsureField();
        BattleSession battle = Assert.IsType<BattleSession>(world.Battle);
        Assert.Equal(29, battle.EncounterId);
        AssertReserveBeforeFourthTurn(battle);
        world.ApplyBattleInput(new(BattleInputKind.Tick, Frames: 1));
        AssertReserveBeforeFourthTurn(battle);
        Assert.DoesNotContain(battle.Events, item => item.ActorId == "enemy-1" && item.Kind is "Queued" or "Windup" or "NativeText");
    }

    private static void AssertReserveBeforeFourthTurn(BattleSession battle)
    {
        if (battle.Capture().Combatants.Single(actor => actor.Id == "enemy-0").NativeParticipant!.TurnCount < 4)
        {
            BattleCombatant wedge = battle.Combatants.Single(actor => actor.Id == "enemy-1");
            Assert.True(wedge.Suspended);
            Assert.True(wedge.Hidden);
        }
    }
}
