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
    public void Pending_encounter_advances_inside_the_complete_graphics_host_loop(int speed)
    {
        string? source = Environment.GetEnvironmentVariable("MEMORIA_FF8_BATTLE_PERFORMANCE_SNAPSHOT");
        Assert.SkipUnless(Environment.GetEnvironmentVariable("MEMORIA_FF8_BATTLE_HOST_PERFORMANCE") == "1" && source is not null && File.Exists(source), "A pre-battle encounter save and interactive graphics diagnostic opt-in are required.");
        string? selectedSpeed = Environment.GetEnvironmentVariable("MEMORIA_FF8_BATTLE_PERFORMANCE_SPEED");
        Assert.SkipUnless(selectedSpeed is null || selectedSpeed == speed.ToString(System.Globalization.CultureInfo.InvariantCulture), "A different optional diagnostic speed was selected.");
        GameSession session = new(GameSession.Load(source!));
        GameWorld world = RestorePerformanceWorld(session);
        BattleSession battle = Assert.IsType<BattleSession>(world.Battle);
        long initialFrame = battle.Frame;
        long initialCallback = battle.Presentation!.Graphics.Callback;
        string directory = TestResources.ArtifactFile("battle-host-loop");
        Directory.CreateDirectory(directory);
        GlobalSettingsPaths settings = new(directory);
        ControlsProfile controls = ControlsProfile.CreateDefault();
        controls = controls.WithBinding(ControlAction.Confirm, ControlDevice.Keyboard, new([Microsoft.Xna.Framework.Input.Keys.F22], []));
        controls = controls.WithBinding(ControlAction.Cancel, ControlDevice.Keyboard, new([Microsoft.Xna.Framework.Input.Keys.F23], []));
        foreach (ControlAction action in Enum.GetValues<ControlAction>())
        {
            if (action is not (ControlAction.Confirm or ControlAction.Cancel))
            {
                controls = controls.WithoutBinding(action, ControlDevice.Keyboard).WithoutBinding(action, ControlDevice.Controller);
            }
        }
        controls.Save(settings.ControlsPreferencesPath);
        LaunchOptions options = new() { GameDirectory = TestResources.GameDirectory, InitialState = session.State, StatePath = source, SavePath = Path.Combine(directory, "unused.json"), MuteAudio = true, ExitAfterSeconds = 12, ResolutionSpecified = true, Width = 960, Height = 720 };
        using RecreateGame game = new(options, session);
        Set(game, "world", world);
        Set(game, "globalSettings", settings);
        string? requestedFps = Environment.GetEnvironmentVariable("MEMORIA_FF8_BATTLE_PERFORMANCE_FPS");
        int fps = requestedFps is null ? 60 : int.Parse(requestedFps, System.Globalization.CultureInfo.InvariantCulture);
        bool vSync = Environment.GetEnvironmentVariable("MEMORIA_FF8_BATTLE_PERFORMANCE_VSYNC") == "1";
        Set(game, "uiPreferences", new UiPreferences { WindowWidth = 960, WindowHeight = 720, VSync = vSync, BattleFps = fps });
        if (speed == 3)
        {
            Get<GameplayClock>(game, "gameplayClock").ToggleSpeed();
        }
        Stopwatch timer = Stopwatch.StartNew();
        using System.Threading.Timer deadline = new(_ => game.Exit(), null, TimeSpan.FromSeconds(25), System.Threading.Timeout.InfiniteTimeSpan);
        game.Run();
        double seconds = timer.Elapsed.TotalSeconds;
        GameLoopSample sample = Get<GameLoopMetrics>(game, "metrics").Latest!;
        Assert.NotNull(sample);
        string? evidence = Environment.GetEnvironmentVariable("MEMORIA_FF8_NATIVE_PRODUCTION_EFFECT_EVIDENCE");
        NativeBattleProductionEffectAdmissionTests.Publish(evidence, $"host-loop-x{speed}-measurement", new { Speed = speed, FramesPerSecondLimit = fps, VSync = vSync, Seconds = seconds, sample, PresentedScenes = Get<GpuTriangleRenderer?>(game, "battleGpu")?.PresentationVersion, InitialFrame = initialFrame, FinalFrame = battle.Frame, InitialCallback = initialCallback, FinalCallback = battle.Presentation!.Graphics.Callback });
        session.SetLiveBattle(battle);
        session.SetEventState(world.Events.Capture());
        NativeBattleProductionEffectAdmissionTests.Publish(evidence, $"host-loop-x{speed}-after", session.State);
        Assert.False(Get<bool>(game, "fieldFaulted"), Get<string?>(game, "error"));
        Assert.True(battle.Mode == BattleAtbMode.TurnBased || battle.Frame > initialFrame + 200 * speed, $"Frame {initialFrame} -> {battle.Frame}; callback {initialCallback} -> {battle.Presentation!.Graphics.Callback}; mode {battle.Mode}; outcome {battle.Outcome}");
        Assert.True(battle.Presentation!.Graphics.Callback > initialCallback + 200 * speed);
    }

    internal static GameWorld RestorePerformanceWorld(GameSession session)
    {
        Assert.Null(session.State.Battle);
        Assert.True(session.State.Scene is SceneKind.Field or SceneKind.WorldMap);
        GameWorld world = NativeBattleProductionEffectAdmissionTests.RestoreWorld(session, TestResources.GameDirectory);
        try
        {
            world.ApplyBattleInput(new(BattleInputKind.StartEncounter, NativeSeed: 4));
            ExpandedBattleInstalledEntry.WaitForEntry(world, Assert.IsType<BattleSession>(world.Battle));
            return world;
        }
        catch
        {
            world.Dispose();
            throw;
        }
    }

    private static T Get<T>(RecreateGame game, string name) => (T)typeof(RecreateGame).GetField(name, BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(game)!;

    private static void Set(RecreateGame game, string name, object value)
    {
        typeof(RecreateGame).GetField(name, BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(game, value);
    }
}
