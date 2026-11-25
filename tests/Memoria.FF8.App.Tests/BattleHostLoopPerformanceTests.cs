using System.Diagnostics;
using System.Reflection;
using System.Security.Cryptography;
using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.GameUi;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

[Collection("GPU rendering")]
public sealed class BattleHostLoopPerformanceTests
{
    [Fact]
    public void Automatic_Odin_at_six_times_speed_completes_in_the_graphics_host()
    {
        string? source = Environment.GetEnvironmentVariable("MEMORIA_FF8_AUTOMATIC_GUARDIAN_SAVE");
        Assert.SkipUnless(Environment.GetEnvironmentVariable("MEMORIA_FF8_BATTLE_HOST_PERFORMANCE") == "1" && source is not null && File.Exists(source), "A standalone world save and interactive graphics diagnostic opt-in are required.");
        WithOdinEncounter(source!, (directory, session, world, battle) => MeasureOdinHost(source!, directory, session, world, battle));
    }

    [Fact]
    public void Automatic_Odin_rain_retains_the_next_native_callback_after_fork()
    {
        string? source = Environment.GetEnvironmentVariable("MEMORIA_FF8_AUTOMATIC_GUARDIAN_SAVE");
        Assert.SkipUnless(source is not null && File.Exists(source), "A standalone world save for the automatic summon is required.");
        WithOdinEncounter(source!, (_, _, _, battle) =>
        {
            BattlePresentationProcess process = battle.Presentation!;
            for (int callback = 0; callback < 600; callback++)
            {
                battle.Tick();
                NativeBattleModulePoolTask? rain = process.Graphics.OriginalEffect?.Pools.SelectMany(pool => pool.Slots).FirstOrDefault(task => task.Callback == 0x647F80 && (task.Flags & 1) != 0);
                if (rain is not null && rain.ReadWord(12) >= 40)
                {
                    break;
                }
            }
            Assert.Equal((ushort)187, battle.PreparedPresentation?.PilotEffect);
            Assert.Contains(process.Graphics.OriginalEffect!.Pools.SelectMany(pool => pool.Slots), task => task.Callback == 0x647F80 && (task.Flags & 1) != 0 && task.ReadWord(12) >= 40);
            long previousCallback = process.Graphics.Callback;
            BattleSession resumed = battle.ForkForContinuation();
            battle.Tick();
            resumed.Tick();
            BattlePresentationProcess continued = resumed.Presentation!;
            Assert.Equal(previousCallback + 1, process.Graphics.Callback);
            Assert.Equal(process.Graphics.Callback, continued.Graphics.Callback);
            Assert.Equal(battle.Capture().NativeCrtRandom, resumed.Capture().NativeCrtRandom);
            Assert.Equal(process.Memory.Effect187!.CommonPacketStart, continued.Memory.Effect187!.CommonPacketStart);
            Assert.Equal(process.Memory.Effect187.CommonPacketEnd, continued.Memory.Effect187.CommonPacketEnd);
            Assert.NotEmpty(process.Graphics.OriginalEffect!.PresentationPackets);
            Assert.Equal(process.Graphics.OriginalEffect!.PresentationPackets.AsEnumerable(), continued.Graphics.OriginalEffect!.PresentationPackets.AsEnumerable());
        });
    }

    private static void WithOdinEncounter(string source, Action<string, GameSession, GameWorld, BattleSession> run)
    {
        byte[] sourceHash = SHA256.HashData(File.ReadAllBytes(source));
        string gameDirectory = TestResources.GameDirectory;
        using GameAssets assets = new(gameDirectory);
        GameState saved = PlayerStateLoader.Load(source, gameDirectory, sharedAssets: assets);
        Assert.Equal(SceneKind.WorldMap, saved.Scene);
        for (int seed = 9; seed <= byte.MaxValue; seed++)
        {
            GameSession session = new(saved);
            session.PrepareDiagnosticAutomaticGuardian(odin: true);
            using GameWorld world = new(session, gameDirectory, sharedAssets: assets);
            world.EnsureField();
            world.ConfigureGameplayPreferences(session.State.GameplayPreferences with { BattleAllyCount = 3, AdditionalEnemies = 0 });
            WorldMapScene scene = Assert.IsType<WorldMapScene>(world.WorldScene);
            RandomEncounterSessionState random = Assert.IsType<RandomEncounterSessionState>(session.State.RandomEncounters);
            session.SetRandomEncounters(random with { Pending = new(SceneKind.WorldMap, session.State.FieldId, scene.State.Frame, ExpandedBattleInstalledEntry.Formation, scene.AllocationProgramSha256) });
            world.ApplyBattleInput(new(BattleInputKind.StartEncounter, Mode: BattleAtbMode.TurnBased, NativeSeed: (byte)seed));
            BattleSession battle = Assert.IsType<BattleSession>(world.Battle);
            ExpandedBattleInstalledEntry.WaitForEntry(world, battle);
            if (!battle.QueuedActions.Any(command => command is { NativeQueuedCommand: 245, ActionId: 0 }))
            {
                continue;
            }
            TestContext.Current.TestOutputHelper!.WriteLine($"Odin entered with seed {seed}.");
            run(gameDirectory, session, world, battle);
            Assert.Equal(sourceHash, SHA256.HashData(File.ReadAllBytes(source)));
            return;
        }
        Assert.Fail("No original entry roll admitted Odin.");
    }

    private static void MeasureOdinHost(string source, string gameDirectory, GameSession session, GameWorld world, BattleSession battle)
    {
        string directory = TestResources.ArtifactFile("odin-host-loop");
        Directory.CreateDirectory(directory);
        GlobalSettingsPaths settings = new(isolatedDirectory: directory);
        UiPreferences preferences = new() { WindowWidth = 960, WindowHeight = 720, BattleFps = 60, VSync = true, FastForwardMultiplier = 6 };
        preferences.Save(settings.UiPreferencesPath);
        LaunchOptions options = new() { GameDirectory = gameDirectory, InitialState = session.State, StatePath = source, SavePath = Path.Combine(directory, "unused.json"), SettingsDirectory = directory, MuteAudio = true, ExitAfterSeconds = 25, ResolutionSpecified = true, Width = 960, Height = 720 };
        using RecreateGame game = new(options, session);
        Set(game, "world", world);
        Set(game, "globalSettings", settings);
        Set(game, "uiPreferences", preferences);
        Set(game, "diagnosticHostPhaseTiming", new DiagnosticHostPhaseTiming(true));
        GameplayClock clock = Get<GameplayClock>(game, "gameplayClock");
        clock.Configure(6, 100);
        clock.ToggleSpeed();
        List<double> frameTimes = [];
        List<double> updateTimes = [];
        long previousPresent = 0;
        bool observed = false;
        bool completed = false;
        game.PresentedFrameObserver = (_, end, _, phases) =>
        {
            if (battle.PreparedPresentation?.PilotEffect == 187)
            {
                Assert.Equal(6, clock.SpeedMultiplier);
                if (previousPresent != 0)
                {
                    frameTimes.Add(Stopwatch.GetElapsedTime(previousPresent, end).TotalMilliseconds);
                }
                updateTimes.Add(Stopwatch.GetElapsedTime(0, phases!.Value.BattlePresentationUpdate).TotalMilliseconds);
                previousPresent = end;
                observed = true;
            }
            if ((observed && battle.CanReturnToField) || Get<bool>(game, "fieldFaulted"))
            {
                completed = battle.CanReturnToField;
                Set(game, "windowCloseApproved", true);
                Set(game, "authoringExitAllowed", true);
                game.Exit();
            }
        };
        game.Run();
        Assert.False(Get<bool>(game, "fieldFaulted"), Get<string?>(game, "error"));
        Assert.True(observed && completed);
        Assert.Equal(BattleOutcome.Victory, battle.Outcome);
        Assert.NotEmpty(frameTimes);
        frameTimes.Sort();
        updateTimes.Sort();
        TestContext.Current.TestOutputHelper!.WriteLine($"Odin ×6, {frameTimes.Count} present intervals, mean {frameTimes.Average():F3} ms, p95 {frameTimes[frameTimes.Count * 95 / 100]:F3} ms, max {frameTimes[^1]:F3} ms. Update p95 {updateTimes[updateTimes.Count * 95 / 100]:F3} ms, max {updateTimes[^1]:F3} ms.");
    }

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
