using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class BattleFramePerformanceTests
{
    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    public void Named_standalone_continues_with_the_live_display_and_simulation_clocks(int speed)
    {
        string? source = Environment.GetEnvironmentVariable("MEMORIA_FF8_BATTLE_PERFORMANCE_SNAPSHOT");
        Assert.SkipUnless(source is not null && File.Exists(source), "An optional standalone battle fixture is required.");
        GameSession session = new(GameSession.Load(source!));
        using GameWorld world = BattleHostLoopPerformanceTests.RestorePerformanceWorld(session);
        BattleSession battle = world.Battle!;
        BattleScenePresentation scene = new(world.BattleResources, battle.Capture().Definition, true);
        long initialFrame = battle.Frame;
        long initialCallback = battle.NativeGraphics!.Callback;
        GameplayClock gameplayClock = new();
        if (speed == 3)
        {
            gameplayClock.ToggleSpeed();
        }
        TimeSpan elapsed = gameplayClock.Advance(TimeSpan.FromSeconds(1d / 60), session.State.Scene);
        double clock = 0;
        double updateMilliseconds = 0;
        double renderMilliseconds = 0;
        long started = Stopwatch.GetTimestamp();
        for (int display = 0; display < 180; display++)
        {
            if (display == 60)
            {
                started = Stopwatch.GetTimestamp();
                updateMilliseconds = 0;
                renderMilliseconds = 0;
            }
            long updateStarted = Stopwatch.GetTimestamp();
            battle.AdvanceNativeGraphicsPresentation(elapsed);
            world.SynchronizeBattlePresentation();
            scene.AdvancePresentation(elapsed);
            clock += elapsed.TotalSeconds;
            int frames = (int)(clock * BattleSession.FramesPerSecond);
            if (frames > 0)
            {
                clock -= frames / (double)BattleSession.FramesPerSecond;
                world.ApplyBattleInput(new(BattleInputKind.Tick, frames));
            }
            updateMilliseconds += Stopwatch.GetElapsedTime(updateStarted).TotalMilliseconds;
            long renderStarted = Stopwatch.GetTimestamp();
            scene.Render(battle, 960, 720);
            renderMilliseconds += Stopwatch.GetElapsedTime(renderStarted).TotalMilliseconds;
        }
        double totalMilliseconds = Stopwatch.GetElapsedTime(started).TotalMilliseconds;
        if (battle.Mode != BattleAtbMode.TurnBased)
        {
            Assert.InRange(battle.Frame - initialFrame, 90 * speed - 1, 90 * speed);
        }
        Assert.InRange(battle.NativeGraphics.Callback - initialCallback, 90 * speed - 1, 90 * speed);
        session.SetLiveBattle(battle);
        session.SetEventState(world.Events.Capture());
        GameState saved = session.State;
        string? evidence = Environment.GetEnvironmentVariable("MEMORIA_FF8_NATIVE_PRODUCTION_EFFECT_EVIDENCE");
        string id = "performance-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fffffff");
        NativeBattleProductionEffectAdmissionTests.Publish(evidence, id + "-after", saved);
        using GameWorld restored = BattleHostLoopPerformanceTests.RestorePerformanceWorld(new GameSession(saved));
        long savedCallback = battle.NativeGraphics.Callback;
        battle.AdvanceNativeGraphicsPresentation(TimeSpan.FromSeconds(1d / 30));
        restored.Battle!.AdvanceNativeGraphicsPresentation(TimeSpan.FromSeconds(1d / 30));
        world.SynchronizeBattlePresentation();
        restored.SynchronizeBattlePresentation();
        world.ApplyBattleInput(new(BattleInputKind.Tick));
        restored.ApplyBattleInput(new(BattleInputKind.Tick));
        Assert.True(battle.NativeGraphics.Callback > savedCallback);
        Assert.Equal(JsonSerializer.Serialize(battle.Capture()), JsonSerializer.Serialize(restored.Battle!.Capture()));
        Assert.Equal(scene.Render(battle, 960, 720), new BattleScenePresentation(restored.BattleResources, restored.Battle.Capture().Definition, true).Render(restored.Battle, 960, 720));
        NativeBattleProductionEffectAdmissionTests.Publish(evidence, id + "-measurement", new { Speed = speed, WarmupDisplayFrames = 60, DisplayFrames = 120, Width = 960, Height = 720, TotalMilliseconds = totalMilliseconds, UpdateMilliseconds = updateMilliseconds, RenderMilliseconds = renderMilliseconds, InitialFrame = initialFrame, SavedFrame = saved.Battle!.Frame, SavedCallback = saved.Battle.NativeGraphics!.Callback, ValidatedNextFrame = battle.Frame, ValidatedNextCallback = battle.NativeGraphics.Callback, Sha256 = Convert.ToHexString(SHA256.HashData(scene.Render(battle, 960, 720))) });
    }

    [Fact]
    public void Accelerated_queued_attack_admission_matches_ordered_callbacks_and_restores()
    {
        string? source = Environment.GetEnvironmentVariable("MEMORIA_FF8_BATTLE_PERFORMANCE_SNAPSHOT");
        Assert.SkipUnless(source is not null && File.Exists(source), "An optional standalone battle fixture is required.");
        GameSession session = new(GameSession.Load(source!));
        using GameWorld accelerated = NativeBattleProductionEffectAdmissionTests.RestoreWorld(session, TestResources.GameDirectory);
        using GameWorld ordinary = NativeBattleProductionEffectAdmissionTests.RestoreWorld(new GameSession(GameSession.Load(source!)), TestResources.GameDirectory);
        BattleSession battle = accelerated.Battle!;
        BattleCombatant actor = battle.Combatants.First(actor => actor.IsPlayer && actor.IsReady);
        BattleCombatant target = battle.Combatants.First(actor => !actor.IsPlayer && actor.IsAlive && !actor.Untargetable);
        BattleInput attack = new(BattleInputKind.Attack, ActorId: actor.Id, TargetId: target.Id);
        accelerated.ApplyBattleInput(attack);
        ordinary.ApplyBattleInput(attack);
        int completed = battle.Events.Count(item => item.Kind == "ActionComplete" && item.ActorId == target.Id);
        TimeSpan callback = TimeSpan.FromTicks((TimeSpan.TicksPerSecond + BattleSession.FramesPerSecond - 1) / BattleSession.FramesPerSecond);
        int groups = 0;
        do
        {
            battle.AdvanceNativeGraphicsPresentation(callback * 3);
            accelerated.ApplyBattleInput(new(BattleInputKind.Tick, 3));
            for (int frame = 0; frame < 3; frame++)
            {
                ordinary.Battle!.AdvanceNativeGraphicsPresentation(callback);
                ordinary.ApplyBattleInput(new(BattleInputKind.Tick));
            }
            groups++;
        }
        while (groups < 100 && battle.Events.Count(item => item.Kind == "ActionComplete" && item.ActorId == target.Id) == completed);
        Assert.True(battle.Events.Count(item => item.Kind == "ActionComplete" && item.ActorId == target.Id) > completed);
        Assert.Contains(battle.Events, item => item.Kind == "Windup" && item.ActorId == actor.Id);
        Assert.Equal(JsonSerializer.Serialize(ordinary.Battle!.Capture()), JsonSerializer.Serialize(battle.Capture()));
        Assert.Equal(NativeBattleProductionEffectAdmissionTests.RenderBattle(ordinary), NativeBattleProductionEffectAdmissionTests.RenderBattle(accelerated));
        session.SetLiveBattle(battle);
        session.SetEventState(accelerated.Events.Capture());
        GameState saved = session.State;
        string? evidence = Environment.GetEnvironmentVariable("MEMORIA_FF8_NATIVE_PRODUCTION_EFFECT_EVIDENCE");
        NativeBattleProductionEffectAdmissionTests.Publish(evidence, "accelerated-attack-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fffffff") + "-after", saved);
        using GameWorld restored = NativeBattleProductionEffectAdmissionTests.RestoreWorld(new GameSession(saved), TestResources.GameDirectory);
        restored.Battle!.AdvanceNativeGraphicsPresentation(callback);
        battle.AdvanceNativeGraphicsPresentation(callback);
        restored.ApplyBattleInput(new(BattleInputKind.Tick));
        accelerated.ApplyBattleInput(new(BattleInputKind.Tick));
        Assert.Equal(JsonSerializer.Serialize(battle.Capture()), JsonSerializer.Serialize(restored.Battle.Capture()));
    }
}
