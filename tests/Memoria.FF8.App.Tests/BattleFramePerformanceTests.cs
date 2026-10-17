using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class BattleFramePerformanceTests
{
    [Fact]
    public void Named_standalone_continues_with_the_live_display_and_simulation_clocks()
    {
        string? source = Environment.GetEnvironmentVariable("MEMORIA_FF8_BATTLE_PERFORMANCE_SNAPSHOT");
        Assert.SkipUnless(source is not null && File.Exists(source), "An optional standalone battle fixture is required.");
        GameSession session = new(GameSession.Load(source!));
        using GameWorld world = NativeBattleProductionEffectAdmissionTests.RestoreWorld(session, TestResources.GameDirectory);
        BattleSession battle = world.Battle!;
        BattleScenePresentation scene = new(world.BattleResources, battle.Capture().Definition, true);
        long initialFrame = battle.Frame;
        long initialCallback = battle.NativeGraphics!.Callback;
        TimeSpan elapsed = TimeSpan.FromSeconds(1d / 60);
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
        Assert.InRange(battle.Frame - initialFrame, 89, 90);
        Assert.InRange(battle.NativeGraphics.Callback - initialCallback, 89, 90);
        session.SetLiveBattle(battle);
        session.SetEventState(world.Events.Capture());
        GameState saved = session.State;
        string? evidence = Environment.GetEnvironmentVariable("MEMORIA_FF8_NATIVE_PRODUCTION_EFFECT_EVIDENCE");
        string id = "performance-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fffffff");
        NativeBattleProductionEffectAdmissionTests.Publish(evidence, id + "-after", saved);
        using GameWorld restored = NativeBattleProductionEffectAdmissionTests.RestoreWorld(new GameSession(saved), TestResources.GameDirectory);
        long savedCallback = battle.NativeGraphics.Callback;
        battle.AdvanceNativeGraphicsPresentation(TimeSpan.FromSeconds(1d / 30));
        restored.Battle!.AdvanceNativeGraphicsPresentation(TimeSpan.FromSeconds(1d / 30));
        world.ApplyBattleInput(new(BattleInputKind.Tick));
        restored.ApplyBattleInput(new(BattleInputKind.Tick));
        Assert.True(battle.NativeGraphics.Callback > savedCallback);
        Assert.Equal(JsonSerializer.Serialize(battle.Capture()), JsonSerializer.Serialize(restored.Battle!.Capture()));
        Assert.Equal(scene.Render(battle, 960, 720), new BattleScenePresentation(restored.BattleResources, restored.Battle.Capture().Definition, true).Render(restored.Battle, 960, 720));
        NativeBattleProductionEffectAdmissionTests.Publish(evidence, id + "-measurement", new { WarmupDisplayFrames = 60, DisplayFrames = 120, Width = 960, Height = 720, TotalMilliseconds = totalMilliseconds, UpdateMilliseconds = updateMilliseconds, RenderMilliseconds = renderMilliseconds, InitialFrame = initialFrame, SavedFrame = saved.Battle!.Frame, SavedCallback = saved.Battle.NativeGraphics!.Callback, ValidatedNextFrame = battle.Frame, ValidatedNextCallback = battle.NativeGraphics.Callback, Sha256 = Convert.ToHexString(SHA256.HashData(scene.Render(battle, 960, 720))) });
    }
}
