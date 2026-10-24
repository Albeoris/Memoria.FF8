using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class BattleSavedRepeatedDuelTests
{
    [Fact]
    public void StandaloneSecondDuelReinitializesAliasedRootAndContinuesAfterJsonRestore()
    {
        string? input = Environment.GetEnvironmentVariable("MEMORIA_FF8_DUEL_PERFORMANCE_SNAPSHOT");
        string? output = Environment.GetEnvironmentVariable("MEMORIA_FF8_DUEL_OUTPUT");
        Assert.SkipUnless(input is not null && output is not null, "Select a standalone Duel fixture and diagnostic output directory.");
        GameSession session = new(GameSession.Load(input!));
        using GameWorld world = new(session, TestResources.GameDirectory);
        world.EnsureField();
        BattleSession battle = Assert.IsType<BattleSession>(world.Battle);
        BattleCombatant actor = battle.Combatants.Single(candidate => candidate.IsPlayer && candidate.NativeLimit?.Command == 11);
        TimeSpan elapsed = TimeSpan.FromTicks((TimeSpan.TicksPerSecond + BattleSession.FramesPerSecond - 1) / BattleSession.FramesPerSecond);
        int invocations = battle.GetNativeDuel(actor.Id) is null ? 0 : 1;
        int completed = 0;
        bool acceptedMove = false;
        bool restored = false;
        int ordinaryTurns = 0;
        Directory.CreateDirectory(output!);
        for (int callback = 0; callback < 4000; callback++)
        {
            if (invocations == completed && invocations < 2 && battle.ActiveAction is null && battle.QueuedActions.Count == 0)
            {
                if (!battle.CanChoosePlayerCommand(actor.Id) && battle.ActiveAction is null && battle.QueuedActions.Count == 0)
                {
                    battle.AdvanceTurn();
                }
                if (battle.CanChoosePlayerCommand(actor.Id))
                {
                    BattleCombatant target = battle.Combatants.First(candidate => !candidate.IsPlayer && candidate.IsAlive && candidate.IsActive);
                    if (battle.GetNativeLimitLevel(actor.Id) != 0 || battle.RefreshNativeLimit(actor.Id) != 0)
                    {
                        battle.QueueAction(actor.Id, target.Id, BattleAbility.LimitBreak, 11 << 16);
                        invocations++;
                        acceptedMove = false;
                    }
                    else
                    {
                        Assert.True(++ordinaryTurns <= 5, "The selected fixture must offer another Limit within five ordinary turns.");
                        battle.QueueAttack(actor.Id, target.Id);
                    }
                }
            }
            if (battle.GetNativeDuel(actor.Id) is { Phase: BattleNativeDuelPhase.Ready } && !acceptedMove)
            {
                if (invocations == 2)
                {
                    world.SynchronizeBattlePresentation();
                    string path = Path.Combine(output!, "second-duel-ready.json");
                    world.CaptureBattleState();
                    session.Save(path, compact: true);
                    Receipt("second-duel-ready");
                    GameSession resumed = new(GameSession.Load(path));
                    using GameWorld restoredWorld = new(resumed, TestResources.GameDirectory);
                    restoredWorld.EnsureField();
                    BattleSession restoredBattle = Assert.IsType<BattleSession>(restoredWorld.Battle);
                    battle.AdvanceNativeGraphicsPresentation(elapsed);
                    restoredBattle.AdvanceNativeGraphicsPresentation(elapsed);
                    Assert.True(JsonElement.DeepEquals(JsonSerializer.SerializeToElement(battle.Capture()), JsonSerializer.SerializeToElement(restoredBattle.Capture())));
                    restored = true;
                }
                BattleNativeDuelChoice choice = battle.GetNativeDuelChoices(actor.Id).First(candidate => candidate.Learned && !candidate.Finisher);
                foreach (ushort button in choice.Buttons)
                {
                    battle.AdvanceNativeDuelPresentation(elapsed, 0);
                    battle.AdvanceNativeDuelPresentation(elapsed, button);
                }
                acceptedMove = true;
            }
            battle.AdvanceNativeGraphicsPresentation(elapsed);
            world.SynchronizeBattlePresentation();
            battle.AdvanceNativeDuelPresentation(elapsed, 0);
            if (invocations > completed && acceptedMove && battle.GetNativeDuel(actor.Id) is null && battle.ActiveAction is null && battle.QueuedActions.Count == 0)
            {
                completed++;
                world.CaptureBattleState();
                session.Save(Path.Combine(output!, $"duel-{completed}-completed.json"), compact: true);
                Receipt($"duel-{completed}-completed");
                if (completed == 2)
                {
                    Assert.True(restored);
                    world.CaptureBattleState();
                    session.Save(Path.Combine(output!, "second-duel-completed.json"), compact: true);
                    Receipt("second-duel-completed");
                    return;
                }
            }
        }
        File.WriteAllText(Path.Combine(output!, "repeated-duel-boundary.json"), JsonSerializer.Serialize(battle.Capture()));
        Assert.Fail($"Repeated Duel continuation stopped after {invocations} invocations and {completed} completions.");

        void Receipt(string name)
        {
            File.WriteAllText(Path.Combine(output!, name + ".receipt.json"), JsonSerializer.Serialize(new { session.State.Scene, session.State.FieldName, FieldFrame = world.Events.Frame, WorldFrame = session.State.World?.Frame, BattleFrame = battle.Frame, Invocations = invocations, Completed = completed, OrdinaryTurns = ordinaryTurns }));
        }
    }
}
