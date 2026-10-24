using System.Diagnostics;
using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class BattleSavedDuelPerformanceTests
{
    [Fact]
    public void Standalone_Duel_moves_report_callback_allocation_and_restore_mid_attack()
    {
        string? input = Environment.GetEnvironmentVariable("MEMORIA_FF8_DUEL_PERFORMANCE_SNAPSHOT");
        string? output = Environment.GetEnvironmentVariable("MEMORIA_FF8_DUEL_OUTPUT");
        Assert.SkipUnless(input is not null && output is not null, "Select a standalone Duel fixture and diagnostic output directory.");
        GameSession session = new(GameSession.Load(input!));
        using GameWorld world = new(session, TestResources.GameDirectory);
        world.EnsureField();
        BattleSession battle = Assert.IsType<BattleSession>(world.Battle);
        BattleCombatant actor = battle.Combatants.Single(candidate => candidate.IsPlayer && candidate.NativeLimit?.Command == 11);
        BattleCombatant target = battle.Combatants.First(candidate => !candidate.IsPlayer && candidate.IsAlive && candidate.IsActive);
        TimeSpan elapsed = TimeSpan.FromTicks((TimeSpan.TicksPerSecond + BattleSession.FramesPerSecond - 1) / BattleSession.FramesPerSecond);
        List<object> samples = [];
        int moves = 0;
        bool invoked = battle.GetNativeDuel(actor.Id) is not null;
        int initialHp = target.CurrentHp;
        long maximumMoveAllocation = 0;
        int firstMoveCallbacks = 0;
        bool restoredMidAttack = false;
        int ordinaryTurns = 0;
        Directory.CreateDirectory(output!);
        for (int callback = 0; callback < 2000; callback++)
        {
            if (!invoked && battle.ActiveAction is null && battle.QueuedActions.Count == 0 && battle.GuardianForceCasts.Count == 0)
            {
                if (!battle.CanChoosePlayerCommand(actor.Id))
                {
                    battle.AdvanceTurn();
                }
                if (battle.CanChoosePlayerCommand(actor.Id))
                {
                    if (battle.GetNativeLimitLevel(actor.Id) != 0 || battle.RefreshNativeLimit(actor.Id) != 0)
                    {
                        initialHp = target.CurrentHp;
                        battle.QueueAction(actor.Id, target.Id, BattleAbility.LimitBreak, 11 << 16);
                        invoked = true;
                    }
                    else if (ordinaryTurns < 5)
                    {
                        battle.QueueAttack(actor.Id, target.Id);
                        ordinaryTurns++;
                    }
                }
            }
            if (battle.GetNativeDuel(actor.Id) is { Phase: BattleNativeDuelPhase.Ready } && moves < 2)
            {
                BattleNativeDuelChoice choice = battle.GetNativeDuelChoices(actor.Id).First(candidate => candidate.Learned && !candidate.Finisher);
                foreach (ushort button in choice.Buttons)
                {
                    battle.AdvanceNativeDuelPresentation(elapsed, 0);
                    battle.AdvanceNativeDuelPresentation(elapsed, button);
                }
                moves++;
            }
            long allocation = GC.GetAllocatedBytesForCurrentThread();
            long started = Stopwatch.GetTimestamp();
            battle.AdvanceNativeGraphicsPresentation(elapsed);
            double update = Stopwatch.GetElapsedTime(started).TotalMilliseconds;
            long updateBytes = GC.GetAllocatedBytesForCurrentThread() - allocation;
            if (moves > 0 && battle.GetNativeDuel(actor.Id) is not null)
            {
                maximumMoveAllocation = Math.Max(maximumMoveAllocation, updateBytes);
            }
            allocation = GC.GetAllocatedBytesForCurrentThread();
            started = Stopwatch.GetTimestamp();
            world.SynchronizeBattlePresentation();
            double synchronize = Stopwatch.GetElapsedTime(started).TotalMilliseconds;
            long synchronizeBytes = GC.GetAllocatedBytesForCurrentThread() - allocation;
            NativeBattlePrimitiveFrame? primitiveFrame = battle.NativeGraphics?.PrimitiveFrame;
            samples.Add(new { Callback = callback, battle.Frame, Phase = battle.GetNativeDuel(actor.Id)?.Phase.ToString(), Moves = moves, Update = update, UpdateBytes = updateBytes, Synchronize = synchronize, SynchronizeBytes = synchronizeBytes, Commands = primitiveFrame?.LastCommands.Length ?? 0, ModuleDraws = primitiveFrame?.LastCommands.Where(command => command.Command.ModuleDraw.HasValue).Select(command => command.Command.ModuleDraw).Distinct().Count() ?? 0, PendingPackets = primitiveFrame?.Pending.Packets.Length ?? 0 });
            battle.AdvanceNativeDuelPresentation(elapsed, 0);
            if (moves == 1 && ++firstMoveCallbacks == 16)
            {
                Assert.Equal(BattleNativeDuelPhase.AwaitingEffect, battle.GetNativeDuel(actor.Id)!.Phase);
                world.CaptureBattleState();
                string midpoint = Path.Combine(output!, "duel-performance-midpoint.json");
                session.Save(midpoint, compact: true);
                Receipt("duel-performance-midpoint");
                using GameWorld independent = new(new GameSession(GameSession.Load(midpoint)), TestResources.GameDirectory);
                independent.EnsureField();
                BattleSession restored = Assert.IsType<BattleSession>(independent.Battle);
                battle.AdvanceNativeGraphicsPresentation(elapsed);
                restored.AdvanceNativeGraphicsPresentation(elapsed);
                world.SynchronizeBattlePresentation();
                independent.SynchronizeBattlePresentation();
                battle.AdvanceNativeDuelPresentation(elapsed, 0);
                restored.AdvanceNativeDuelPresentation(elapsed, 0);
                Assert.Equal(JsonSerializer.Serialize(battle.Capture()), JsonSerializer.Serialize(restored.Capture()));
                restoredMidAttack = true;
            }
            if (invoked && moves == 2 && battle.GetNativeDuel(actor.Id) is null && battle.ActiveAction is null && battle.QueuedActions.Count == 0)
            {
                File.WriteAllText(Path.Combine(output!, "duel-performance.json"), JsonSerializer.Serialize(samples));
                Assert.True(target.CurrentHp < initialHp);
                Assert.True(restoredMidAttack);
                Assert.True(maximumMoveAllocation < 32 * 1024 * 1024, $"A Duel callback allocated {maximumMoveAllocation} bytes.");
                world.CaptureBattleState();
                session.Save(Path.Combine(output!, "duel-performance-completed.json"), compact: true);
                Receipt("duel-performance-completed");
                return;
            }
        }
        File.WriteAllText(Path.Combine(output!, "duel-performance.json"), JsonSerializer.Serialize(samples));
        Assert.Fail("The standalone fixture must complete two Duel attacks.");

        void Receipt(string name)
        {
            File.WriteAllText(Path.Combine(output!, name + ".receipt.json"), JsonSerializer.Serialize(new { session.State.Scene, session.State.FieldId, FieldFrame = world.Events.Frame, WorldFrame = session.State.World?.Frame, BattleFrame = battle.Frame, Moves = moves, OrdinaryTurns = ordinaryTurns, InitialHp = initialHp, CurrentHp = target.CurrentHp, MaximumMoveAllocation = maximumMoveAllocation }));
        }
    }
}
