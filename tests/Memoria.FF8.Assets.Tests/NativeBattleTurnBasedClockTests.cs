using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeBattleTurnBasedClockTests
{
    private static readonly TimeSpan PresentationFrame = TimeSpan.FromTicks((TimeSpan.TicksPerSecond + BattleSession.FramesPerSecond - 1) / BattleSession.FramesPerSecond);

    [Fact]
    public void Zero_work_budget_reaches_readiness_with_one_graphics_callback_and_resumes_exactly()
    {
        using GameAssets game = new(TestResources.GameDirectory);
        using BattleAssets assets = new(game.GameDirectory);
        BattleSession battle = CreateClockBattle(game, assets, nextActor: 0);
        BattleSession restored = Resume(battle);
        BattleSnapshot before = battle.Capture();

        battle.AdvanceNativeGraphicsPresentation(PresentationFrame, TimeSpan.Zero);
        restored.AdvanceNativeGraphicsPresentation(PresentationFrame, TimeSpan.Zero);

        Assert.True(battle.Combatants[0].IsReady);
        Assert.True(battle.Frame > before.Frame + 1);
        Assert.Equal(before.NativeGraphics!.Callback + 1, battle.NativeGraphics!.Callback);
        long logicalSteps = battle.Frame - before.Frame;
        Assert.Equal(before.NativeHostCallbacks!.Value + logicalSteps + 1, battle.NativeHostCallbacks!.Value);
        Assert.Equal(before.NativeExamTimer!.ClockCalls + 4 * (logicalSteps + 1), battle.NativeExamTimer!.ClockCalls);
        Assert.Equal(JsonSerializer.Serialize(battle.Capture()), JsonSerializer.Serialize(restored.Capture()));

        long heldFrame = battle.Frame;
        int heldGauge = battle.Combatants[1].NativeAtb!.Current;
        long callbacks = battle.NativeHostCallbacks.Value;
        long timerCalls = battle.NativeExamTimer.ClockCalls;
        battle.AdvanceNativeGraphicsPresentation(PresentationFrame, TimeSpan.Zero);
        restored.AdvanceNativeGraphicsPresentation(PresentationFrame, TimeSpan.Zero);
        Assert.Equal(heldFrame, battle.Frame);
        Assert.Equal(heldGauge, battle.Combatants[1].NativeAtb!.Current);
        Assert.Equal(callbacks + 1, battle.NativeHostCallbacks.Value);
        Assert.Equal(timerCalls + 4, battle.NativeExamTimer.ClockCalls);
        Assert.Equal(JsonSerializer.Serialize(battle.Capture()), JsonSerializer.Serialize(restored.Capture()));
        SaveOptionalSnapshot(battle, nameof(Zero_work_budget_reaches_readiness_with_one_graphics_callback_and_resumes_exactly));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    public void Explicit_advance_reaches_next_ally_or_enemy_without_advancing_presentation(int nextActor)
    {
        using GameAssets game = new(TestResources.GameDirectory);
        using BattleAssets assets = new(game.GameDirectory);
        BattleSession battle = CreateClockBattle(game, assets, nextActor, readyPlayer: true);
        BattleSession restored = Resume(battle);
        BattleSnapshot before = battle.Capture();

        int advanced = battle.AdvanceTurn();
        Assert.Equal(advanced, restored.AdvanceTurn());

        Assert.True(advanced > 1);
        Assert.True(battle.Combatants[0].IsReady);
        Assert.Equal(before.Combatants[0].NativeAtb, battle.Combatants[0].NativeAtb);
        Assert.True(battle.Combatants[nextActor].IsReady);
        Assert.Null(battle.ActiveAction);
        BattleSnapshot after = battle.Capture();
        Assert.Equal(before.Frame + advanced, after.Frame);
        Assert.Equal(before.NativeGraphics!.Callback, after.NativeGraphics!.Callback);
        // Full-gauge registration may enqueue UI readiness; all presentation owners stay at their saved boundary.
        Assert.Equal(JsonSerializer.Serialize(before.NativeGraphics), JsonSerializer.Serialize(after.NativeGraphics with { UiServices = before.NativeGraphics.UiServices }));
        Assert.Equal(JsonSerializer.Serialize(before.NativeActionPackets), JsonSerializer.Serialize(after.NativeActionPackets));
        Assert.Equal(JsonSerializer.Serialize(before.ActiveAction), JsonSerializer.Serialize(after.ActiveAction));
        Assert.Equal(before.NativeHostCallbacks!.Value + advanced, after.NativeHostCallbacks!.Value);
        Assert.Equal(before.NativeExamTimer!.ClockCalls + 4L * advanced, after.NativeExamTimer!.ClockCalls);
        Assert.Equal(JsonSerializer.Serialize(after), JsonSerializer.Serialize(restored.Capture()));
        SaveOptionalSnapshot(battle, $"ExplicitAdvance{nextActor}");
    }

    [Fact]
    public void Ready_player_without_menu_holds_gauges_and_statuses_during_original_action()
    {
        using GameAssets game = new(TestResources.GameDirectory);
        using BattleAssets assets = new(game.GameDirectory);
        NativeBattleAiDefinition original = assets.ReadEnemyAi(61, 1);
        NativeBattleAiDefinition ai = original with { Scripts = [[4, 200, 5, 14, 7, 89, 6, 0], [0], [0], [0], [0]] };
        BattleDefinition seed = NativeBattleGraphicsEntryTests.Create(game, assets, 61, ai: ai).Capture().Definition;
        BattleSession battle = new(seed with { NativeClockRules = new(10), NativeBattleSpeed = 2, Combatants = seed.Combatants.Select(actor => actor with { Speed = 1 }).ToArray() }, BattleAtbMode.TurnBased);
        for (int callback = 0; callback < 200 && battle.ActiveAction is null; callback++)
        {
            battle.Tick();
        }
        Assert.NotNull(battle.ActiveAction);
        Assert.True(battle.Combatants[0].IsReady);
        BattleSnapshot active = battle.Capture();
        BattleCombatantSnapshot player = active.Combatants[0];
        short[] timers = player.NativeStatus!.Timers.ToArray();
        timers[5] = 600;
        BattleCombatantSnapshot[] actors = active.Combatants.ToArray();
        actors[0] = player with
        {
            NativeStatus = player.NativeStatus with { Battle = player.NativeStatus.Battle | 0x20u, Timers = timers },
            StatusEffects = [new(BattleStatusKind.Regen, 600, 60, new() { Kind = BattleStatusKind.Regen, DurationFrames = 600, TickIntervalFrames = 60 })]
        };
        battle = BattleSession.Restore(active with { Combatants = actors, NativeGraphics = active.NativeGraphics! with { UiServices = active.NativeGraphics.UiServices! with { Menu957 = -1 } } });
        BattleSession restored = Resume(battle);
        BattleSnapshot before = battle.Capture();
        Assert.True(before.NativeGraphics!.UiServices!.AllowsAtb(seed.NativeSavedOptions is uint options ? unchecked((ushort)options) : null));

        battle.Tick();
        restored.Tick();

        BattleSnapshot after = battle.Capture();
        Assert.Equal(before.Frame + 1, after.Frame);
        Assert.Equal(before.Combatants.Select(actor => actor.NativeAtb!.Current), after.Combatants.Select(actor => actor.NativeAtb!.Current));
        Assert.Equal(before.Combatants[0].NativeStatus!.Timers, after.Combatants[0].NativeStatus!.Timers);
        Assert.Equal(before.Combatants[0].StatusEffects, after.Combatants[0].StatusEffects);
        Assert.Equal(before.NativeGraphics.Callback + 1, after.NativeGraphics!.Callback);
        Assert.NotEqual(JsonSerializer.Serialize(before.NativeGraphics), JsonSerializer.Serialize(after.NativeGraphics));
        Assert.Equal(JsonSerializer.Serialize(after), JsonSerializer.Serialize(restored.Capture()));

        BattleCombatantSnapshot ally = after.Combatants[1];
        BattleCombatantSnapshot[] unready = after.Combatants.ToArray();
        unready[1] = ally with { Atb = 0, NativeAtb = ally.NativeAtb! with { Current = 0 }, NativeParticipant = ally.NativeParticipant! with { NativeCombatantFlags = ally.NativeParticipant.NativeCombatantFlags!.Value & ~0xCu } };
        battle = BattleSession.Restore(after with { Combatants = unready, NativeGraphics = after.NativeGraphics with { UiServices = after.NativeGraphics.UiServices! with { Commands = after.NativeGraphics.UiServices.Commands.Enqueue(1, 0x12) } } });
        restored = Resume(battle);
        BattleSnapshot beforePass = battle.Capture();
        Assert.Equal(1u, beforePass.NativeClock!.ActionPause);

        int advanced = battle.AdvanceTurn();
        Assert.Equal(advanced, restored.AdvanceTurn());

        Assert.True(advanced > 1);
        Assert.True(battle.Combatants[0].IsReady);
        Assert.True(battle.Combatants[1].IsReady);
        BattleSnapshot passed = battle.Capture();
        Assert.Equal(beforePass.ActiveAction, passed.ActiveAction);
        Assert.Equal(beforePass.NativeClock.ActionPause, passed.NativeClock!.ActionPause);
        Assert.Equal(JsonSerializer.Serialize(beforePass.NativeGraphics), JsonSerializer.Serialize(passed.NativeGraphics! with { UiServices = beforePass.NativeGraphics!.UiServices }));
        Assert.Equal(JsonSerializer.Serialize(beforePass.NativeActionPackets), JsonSerializer.Serialize(passed.NativeActionPackets));
        Assert.Equal(JsonSerializer.Serialize(passed), JsonSerializer.Serialize(restored.Capture()));
        SaveOptionalSnapshot(battle, nameof(Ready_player_without_menu_holds_gauges_and_statuses_during_original_action));
    }

    private static BattleSession CreateClockBattle(GameAssets game, BattleAssets assets, int nextActor, bool readyPlayer = false)
    {
        BattleDefinition seed = NativeBattleGraphicsEntryTests.Create(game, assets, 61, ready: false).Capture().Definition;
        BattleDefinition definition = seed with
        {
            NativeClockRules = new(10),
            NativeBattleSpeed = 2,
            NativeExamTimerAtEntry = NativeExamTimerState.FromReachedSet(300),
            Combatants = seed.Combatants.Select((actor, index) => actor with { Speed = index == nextActor ? 100 : 1, InitialAtb = readyPlayer && index == 0 ? BattleSession.AtbMaximum : 0, NativeCombatantFlags = actor.NativeCombatantFlags | (actor.IsPlayer ? 0u : 0x10u) }).ToArray()
        };
        BattleSession battle = new(definition, BattleAtbMode.TurnBased);
        for (int callback = 0; callback < 200 && !battle.NativeEntryReady; callback++)
        {
            battle.Tick();
        }
        Assert.True(battle.NativeEntryReady);
        for (int callback = 0; callback < 200 && (battle.QueuedActions.Count != 0 || !battle.NativeGraphics!.Descriptors.Queue.IsEmpty || !battle.NativeGraphics.Requests.IsEmpty); callback++)
        {
            battle.Tick();
        }
        Assert.Empty(battle.QueuedActions);
        Assert.True(battle.NativeGraphics!.Descriptors.Queue.IsEmpty);
        Assert.True(battle.NativeGraphics.Requests.IsEmpty);
        return battle;
    }

    private static BattleSession Resume(BattleSession battle)
    {
        return BattleSession.Restore(JsonSerializer.Deserialize<BattleSnapshot>(JsonSerializer.Serialize(battle.Capture()))!);
    }

    private static void SaveOptionalSnapshot(BattleSession battle, string name)
    {
        if (Environment.GetEnvironmentVariable("MEMORIA_FF8_TURN_BASED_CLOCK_OUTPUT_DIRECTORY") is not string directory)
        {
            return;
        }
        directory = Path.GetFullPath(directory);
        Directory.CreateDirectory(directory);
        using FileStream stream = new(Path.Combine(directory, $"{name}.json"), FileMode.CreateNew, FileAccess.Write);
        JsonSerializer.Serialize(stream, battle.Capture());
    }
}
