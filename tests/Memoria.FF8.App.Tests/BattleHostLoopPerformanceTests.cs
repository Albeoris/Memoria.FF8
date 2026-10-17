using System.Diagnostics;
using System.Reflection;
using Memoria.FF8.GameUi;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

[Collection("GPU rendering")]
public sealed class BattleHostLoopPerformanceTests
{
    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    public void Named_standalone_advances_inside_the_complete_graphics_host_loop(int speed)
    {
        string? source = Environment.GetEnvironmentVariable("MEMORIA_FF8_BATTLE_PERFORMANCE_SNAPSHOT");
        Assert.SkipUnless(Environment.GetEnvironmentVariable("MEMORIA_FF8_BATTLE_HOST_PERFORMANCE") == "1" && source is not null && File.Exists(source), "An optional battle fixture and interactive graphics diagnostic opt-in are required.");
        GameSession session = new(GameSession.Load(source!));
        GameWorld world = NativeBattleProductionEffectAdmissionTests.RestoreWorld(session, TestResources.GameDirectory);
        BattleSession battle = world.Battle!;
        long initialFrame = battle.Frame;
        long initialCallback = battle.NativeGraphics!.Callback;
        string directory = TestResources.ArtifactFile("battle-host-loop");
        Directory.CreateDirectory(directory);
        LaunchOptions options = new() { GameDirectory = TestResources.GameDirectory, InitialState = session.State, StatePath = source, SavePath = Path.Combine(directory, "unused.json"), MuteAudio = true, ExitAfterSeconds = 12, ResolutionSpecified = true, Width = 960, Height = 720 };
        using RecreateGame game = new(options, session);
        Set(game, "world", world);
        Set(game, "globalSettings", new GlobalSettingsPaths(directory));
        Set(game, "uiPreferences", new UiPreferences { WindowWidth = 960, WindowHeight = 720, VSync = false });
        if (speed == 3)
        {
            Get<GameplayClock>(game, "gameplayClock").ToggleSpeed();
        }
        Stopwatch timer = Stopwatch.StartNew();
        game.Run();
        double seconds = timer.Elapsed.TotalSeconds;
        GameLoopSample sample = Get<GameLoopMetrics>(game, "metrics").Latest!;
        Assert.NotNull(sample);
        string? evidence = Environment.GetEnvironmentVariable("MEMORIA_FF8_NATIVE_PRODUCTION_EFFECT_EVIDENCE");
        NativeBattleProductionEffectAdmissionTests.Publish(evidence, $"host-loop-x{speed}-measurement", new { Speed = speed, Seconds = seconds, sample, InitialFrame = initialFrame, FinalFrame = battle.Frame, InitialCallback = initialCallback, FinalCallback = battle.NativeGraphics.Callback });
        session.SetLiveBattle(battle);
        session.SetEventState(world.Events.Capture());
        NativeBattleProductionEffectAdmissionTests.Publish(evidence, $"host-loop-x{speed}-after", session.State);
        Assert.False(Get<bool>(game, "fieldFaulted"), Get<string?>(game, "error"));
        Assert.True(battle.Frame > initialFrame + 200 * speed, $"Frame {initialFrame} -> {battle.Frame}; callback {initialCallback} -> {battle.NativeGraphics.Callback}; mode {battle.Mode}; outcome {battle.Outcome}");
        Assert.True(battle.NativeGraphics.Callback > initialCallback + 200 * speed);
    }

    private static T Get<T>(RecreateGame game, string name) => (T)typeof(RecreateGame).GetField(name, BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(game)!;

    private static void Set(RecreateGame game, string name, object value)
    {
        typeof(RecreateGame).GetField(name, BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(game, value);
    }
}
