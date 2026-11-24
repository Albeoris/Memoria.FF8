using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.GameUi;
using Memoria.FF8.Platform;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

[Collection("GPU rendering")]
public sealed class BattleMeteorPartyPerformanceTests
{
    [Fact]
    public void Saved_world_party_measures_idle_and_ordinary_meteor_in_the_graphics_host()
    {
        string? requested = Environment.GetEnvironmentVariable("MEMORIA_FF8_METEOR_PERFORMANCE_REPETITIONS");
        int repetitions = requested is null ? 1 : int.Parse(requested, System.Globalization.CultureInfo.InvariantCulture);
        Assert.InRange(repetitions, 1, 3);
        for (int iteration = 0; iteration < repetitions; iteration++)
        {
            MeasureSavedWorld(iteration, repetitions);
        }
    }

    private static void MeasureSavedWorld(int iteration, int repetitions)
    {
        string? source = Environment.GetEnvironmentVariable("MEMORIA_FF8_METEOR_PERFORMANCE_SAVE");
        Assert.SkipUnless(Environment.GetEnvironmentVariable("MEMORIA_FF8_BATTLE_HOST_PERFORMANCE") == "1" && source is not null && File.Exists(source), "An optional standalone world save and interactive graphics diagnostic opt-in are required.");
        byte[] originalHash = SHA256.HashData(File.ReadAllBytes(source!));
        string gameDirectory = TestResources.GameDirectory;
        using GameAssets assets = new(gameDirectory);
        GameSession session = new(PlayerStateLoader.Load(source!, gameDirectory, sharedAssets: assets));
        Assert.Equal(SceneKind.WorldMap, session.State.Scene);
        using GameWorld world = new(session, gameDirectory, sharedAssets: assets);
        world.EnsureField();
        WorldMapScene scene = Assert.IsType<WorldMapScene>(world.WorldScene);
        int sourceField = session.State.FieldId;
        long sourceFrame = scene.State.Frame;
        world.ConfigureGameplayPreferences(session.State.GameplayPreferences with { BattleAllyCount = 4, AdditionalEnemies = 3 });
        ushort[] formations = (scene.ReadRandomEncounterDefinition(scene.State)?.Formations ?? []).Distinct().ToArray();
        (ushort formation, byte seed) = SelectEncounter(world, formations);
        RandomEncounterSessionState random = Assert.IsType<RandomEncounterSessionState>(session.State.RandomEncounters);
        session.SetRandomEncounters(random with { Pending = new(SceneKind.WorldMap, sourceField, sourceFrame, formation, scene.AllocationProgramSha256) });
        world.ApplyBattleInput(new(BattleInputKind.StartEncounter, Mode: BattleAtbMode.TurnBased, NativeSeed: seed));
        BattleSession battle = Assert.IsType<BattleSession>(world.Battle);
        ExpandedBattleInstalledEntry.WaitForEntry(world, battle);
        Assert.Equal(new[] { 9, 9, 9, 9, 48 }, battle.Combatants.Where(actor => !actor.IsPlayer && actor.IsActive).Select(actor => actor.ModelId).Order());
        Assert.Equal(4, battle.Combatants.Count(actor => actor.IsPlayer));
        BattleCombatant caster = battle.Combatants.FirstOrDefault(actor => actor.IsPlayer && actor.Abilities.Contains(BattleAbility.Magic) && actor.MagicStocks.Any(stock => stock.Id == 16 && stock.Count > 0))
            ?? throw new InvalidDataException("The selected party has no ordinary Meteor caster with available stock.");
        for (int advance = 0; advance < 16 && !caster.IsReady; advance++)
        {
            world.ApplyBattleInput(new(BattleInputKind.AdvanceTurn));
            for (int callback = 0; callback < 120 && !caster.IsReady && battle.ActiveAction is not null; callback++)
            {
                world.ApplyBattleInput(new(BattleInputKind.Tick));
            }
        }
        Assert.True(caster.IsReady, "The stocked Meteor caster did not become ready through bounded ordinary turn advancement.");
        string target = battle.Combatants.First(actor => !actor.IsPlayer && actor.IsActive).Id;
        int stockBefore = battle.QuoteMagic(caster.Id, 16).Available;
        string directory = TestResources.ArtifactFile("meteor-party-performance");
        Directory.CreateDirectory(directory);
        GlobalSettingsPaths settings = new(isolatedDirectory: directory);
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
        string[] resolution = (Environment.GetEnvironmentVariable("MEMORIA_FF8_METEOR_PERFORMANCE_RESOLUTION") ?? "1024x768").Split('x');
        int width = int.Parse(resolution[0], System.Globalization.CultureInfo.InvariantCulture);
        int height = int.Parse(resolution[1], System.Globalization.CultureInfo.InvariantCulture);
        string? requestedVSync = Environment.GetEnvironmentVariable("MEMORIA_FF8_METEOR_PERFORMANCE_VSYNC");
        Assert.True(requestedVSync is null or "0" or "1", "MEMORIA_FF8_METEOR_PERFORMANCE_VSYNC must be 0 or 1.");
        bool vSync = requestedVSync != "0";
        string? requestedMsaa = Environment.GetEnvironmentVariable("MEMORIA_FF8_METEOR_PERFORMANCE_MSAA");
        int msaaSamples = 8;
        if (requestedMsaa is not null)
        {
            Assert.True(int.TryParse(requestedMsaa, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out msaaSamples) && msaaSamples is 0 or 2 or 4 or 8, "MEMORIA_FF8_METEOR_PERFORMANCE_MSAA must be 0, 2, 4 or 8.");
        }
        GraphicsBackend backend = GraphicsBackendSelection.Parse(GraphicsHostReadiness.Backend);
        UiPreferences preferences = new() { WindowWidth = width, WindowHeight = height, VSync = vSync, BattleFps = 60, Graphics = new() { Backend = backend, MsaaSamples = msaaSamples } };
        preferences.Save(settings.UiPreferencesPath);
        LaunchOptions options = new() { GameDirectory = gameDirectory, InitialState = session.State, StatePath = source, SavePath = Path.Combine(directory, "unused.json"), SettingsDirectory = directory, MuteAudio = true, ExitAfterSeconds = 90, ResolutionSpecified = true, Width = width, Height = height, RequestedGraphicsBackend = backend };
        using RecreateGame game = new(options, session);
        Set(game, "world", world);
        Set(game, "globalSettings", settings);
        Set(game, "uiPreferences", preferences);
        FrameSample[] samples = new FrameSample[2880];
        int count = 0;
        int presented = 0;
        int idleCount = 0;
        int castCount = 0;
        int stage = 0;
        int eventStart = 0;
        int eventCursor = 0;
        int completionFrame = -1;
        bool actionComplete = false;
        bool released = false;
        StagePoint? idleStart = null;
        StagePoint? idleEnd = null;
        StagePoint? castStart = null;
        StagePoint? castEnd = null;
        long idleTimestamp = 0;
        long castTimestamp = 0;
        PresentationPacerObservation idlePacer = default;
        PresentationPacerObservation castPacer = default;
        Stopwatch duration = Stopwatch.StartNew();
        void ExitMeasuredHost()
        {
            Set(game, "windowCloseApproved", true);
            Set(game, "authoringExitAllowed", true);
            game.Exit();
        }
        game.PresentedFrameObserver = (begin, end, pacer, phases) =>
        {
            if (castEnd is not null)
            {
                return;
            }
            presented++;
            if (presented % 30 == 0 && Get<bool>(game, "fieldFaulted"))
            {
                castEnd = Capture();
                ExitMeasuredHost();
                return;
            }
            if (stage == 0)
            {
                if (presented == 120)
                {
                    idleStart = Capture();
                    idleTimestamp = end;
                    idlePacer = pacer;
                    stage = 1;
                }
                return;
            }
            samples[count++] = new(stage, presented, begin, end, pacer, phases, game.IsActive, DiagnosticGcPauseObservation.Capture());
            if (stage == 1)
            {
                idleCount++;
                if (idleCount == 240)
                {
                    idleEnd = Capture();
                    caster = battle.Combatants.FirstOrDefault(actor => actor.IsPlayer && actor.IsReady && actor.Abilities.Contains(BattleAbility.Magic) && battle.QuoteMagic(actor.Id, 16).Available > 0)
                        ?? throw new InvalidDataException("No ready actor has ordinary Meteor stock after idle.");
                    stockBefore = battle.QuoteMagic(caster.Id, 16).Available;
                    eventStart = battle.Events.Count;
                    eventCursor = eventStart;
                    Console.WriteLine("meteor-measurement: cast-start");
                    castStart = Capture();
                    castTimestamp = end;
                    castPacer = pacer;
                    world.ApplyBattleInput(new(BattleInputKind.Action, ActorId: caster.Id, TargetId: target, Ability: BattleAbility.Magic, ActionId: 16));
                    stage = 2;
                }
                return;
            }
            castCount++;
            for (; eventCursor < battle.Events.Count; eventCursor++)
            {
                BattleEvent entry = battle.Events[eventCursor];
                if (entry.Kind == "ActionComplete" && entry.ActorId == caster.Id)
                {
                    actionComplete = true;
                    completionFrame = presented;
                }
            }
            released = actionComplete && battle.ActiveAction is null && !battle.QueuedActions.Any(command => command.ActorId == caster.Id);
            if (released && presented >= completionFrame + 2 || castCount >= 2640 || duration.Elapsed.TotalSeconds >= 90)
            {
                castEnd = Capture();
                ExitMeasuredHost();
            }
        };
        using System.Threading.Timer deadline = new(_ => ExitMeasuredHost(), null, TimeSpan.FromSeconds(90), Timeout.InfiniteTimeSpan);
        game.Run();
        castEnd ??= Capture();
        int stockAfter = battle.QuoteMagic(caster.Id, 16).Available;
        using Process process = Process.GetCurrentProcess();
        object summary = new
        {
            Formation = formation,
            Seed = seed,
            SourceField = sourceField,
            SourceWorldFrame = sourceFrame,
            Roster = battle.Combatants.Select(actor => new { actor.Id, actor.Name, actor.ModelId, actor.IsPlayer, actor.IsActive }).ToArray(),
            Caster = caster.Id,
            StockBefore = stockBefore,
            StockAfter = stockAfter,
            battle.InfiniteMagic,
            battle.UnlimitedMagic,
            ActionComplete = actionComplete,
            Error = Get<string?>(game, "error"),
            Released = released,
            Damages = battle.Events.Skip(eventStart).Where(entry => entry.Kind == "Damage" && entry.ActorId == caster.Id).Select(entry => new { entry.TargetId, entry.Amount }).Take(64).ToArray(),
            Graphics = new { Backend = GraphicsHostReadiness.Backend, RequestedVSync = vSync, RequestedMsaaSamples = msaaSamples, Width = game.GraphicsDevice.PresentationParameters.BackBufferWidth, Height = game.GraphicsDevice.PresentationParameters.BackBufferHeight, Samples = game.GraphicsDevice.PresentationParameters.MultiSampleCount, PresentInterval = game.GraphicsDevice.PresentationParameters.PresentationInterval.ToString() },
            Runtime = new { Version = Environment.Version.ToString(), System.Runtime.GCSettings.IsServerGC, LatencyMode = System.Runtime.GCSettings.LatencyMode.ToString(), Environment.ProcessorCount, QuickJitHostOption = AppContext.GetData("System.Runtime.TieredCompilation.QuickJit"), QuickJitEnvironment = Environment.GetEnvironmentVariable("DOTNET_TC_QuickJit"), GcHeapCountHostOption = AppContext.GetData("System.GC.HeapCount"), GcHeapCountEnvironment = Environment.GetEnvironmentVariable("DOTNET_GCHeapCount") },
            Process = new { PriorityClass = process.PriorityClass.ToString(), Affinity = OperatingSystem.IsWindows() || OperatingSystem.IsLinux() ? process.ProcessorAffinity.ToInt64() : (long?)null, process.PeakWorkingSet64 },
            Presented = presented,
            Seconds = duration.Elapsed.TotalSeconds,
            Idle = Summarize(1, idleTimestamp, idlePacer, idleStart, idleEnd),
            Meteor = Summarize(2, castTimestamp, castPacer, castStart, castEnd)
        };
        string output = Environment.GetEnvironmentVariable("MEMORIA_FF8_METEOR_PERFORMANCE_OUTPUT") ?? Path.Combine(directory, "summary.json");
        if (repetitions > 1)
        {
            output = Path.Combine(Path.GetDirectoryName(output)!, $"{Path.GetFileNameWithoutExtension(output)}-{iteration + 1}{Path.GetExtension(output)}");
        }
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(output))!);
        File.WriteAllText(output, JsonSerializer.Serialize(summary, new JsonSerializerOptions { WriteIndented = true }));
        Assert.Equal(originalHash, SHA256.HashData(File.ReadAllBytes(source!)));
        Assert.False(Get<bool>(game, "fieldFaulted"), Get<string?>(game, "error"));
        Assert.Equal(240, idleCount);
        Assert.True(actionComplete && released, "Ordinary Meteor did not complete and release within the bounded host measurement.");
        Assert.Contains(battle.Events.Skip(eventStart), entry => entry.Kind == "Damage" && entry.ActorId == caster.Id);

        object? Summarize(int selectedStage, long preceding, PresentationPacerObservation baseline, StagePoint? start, StagePoint? finish)
        {
            if (start is null || finish is null)
            {
                return null;
            }
            FrameSample[] selected = samples.Take(count).Where(sample => sample.Stage == selectedStage).ToArray();
            DiagnosticPresentationPacing pacing = new(Stopwatch.Frequency, Math.Max(1, selected.Length), preceding, baseline, start.GcPause);
            foreach (FrameSample sample in selected)
            {
                pacing.Record(sample.Frame, sample.End, sample.Begin, sample.End, sample.Pacer, sample.Phases, sample.GcPause);
            }
            DiagnosticPacingSummary timing = pacing.Complete();
            return new
            {
                Frames = selected.Length,
                ActivePresents = selected.Count(sample => sample.Active),
                EffectiveFps = selected.Length * 1000d / timing.Interval.TotalMilliseconds,
                Intervals = Distribution(selected.Select((sample, index) => sample.End - (index == 0 ? preceding : selected[index - 1].End)).ToArray()),
                Timing = timing,
                NativePhases = typeof(DiagnosticNativeFramePhases).GetProperties().Where(property => property.Name != nameof(DiagnosticNativeFramePhases.Callbacks)).ToDictionary(property => property.Name, property => Distribution(selected.Where(sample => sample.Phases.HasValue).Select(sample => (long)property.GetValue(sample.Phases!.Value.Native)!).ToArray())),
                AllocatedBytes = finish.Allocated - start.Allocated,
                Collections = finish.Collections.Zip(start.Collections, (last, first) => last - first).ToArray(),
                MainThreadCpuMilliseconds = (finish.ThreadCpu - start.ThreadCpu) / 10000d,
                ProcessCpuMilliseconds = finish.ProcessCpu - start.ProcessCpu,
                ReportedGcPauseMilliseconds = (finish.GcPause.TotalPauseTimeSpanTicks - start.GcPause.TotalPauseTimeSpanTicks) / (double)TimeSpan.TicksPerMillisecond
            };
        }
    }

    private static (ushort Formation, byte Seed) SelectEncounter(GameWorld world, ushort[] formations)
    {
        foreach (ushort formation in formations.OrderBy(formation => formation == 717 ? 0 : 1))
        {
            if (world.BattleResources.LoadEncounter(formation).Enemies.Any(enemy => enemy.ModelId != 48))
            {
                continue;
            }
            foreach (byte seed in Enumerable.Range(0, 256).Select(seed => checked((byte)seed)).OrderBy(seed => seed == 4 ? 0 : 1))
            {
                BattleDefinition definition = world.CreateEncounterBattle(formation, seed);
                if (definition.Combatants.Where(actor => !actor.IsPlayer && actor.InitialActive).Select(actor => actor.ModelId).Order().SequenceEqual(new[] { 9, 9, 9, 9, 48 }))
                {
                    return (formation, seed);
                }
            }
        }
        throw new InvalidDataException("The supplied world area's native encounter pool cannot produce one Malboro and four Mesmerize through the ordinary roster policy.");
    }

    private static object Distribution(long[] ticks)
    {
        Array.Sort(ticks);
        double At(double quantile) => ticks.Length == 0 ? 0 : ticks[Math.Clamp((int)Math.Ceiling(ticks.Length * quantile) - 1, 0, ticks.Length - 1)] * 1000d / Stopwatch.Frequency;
        return new { Samples = ticks.Length, TotalMilliseconds = ticks.Sum() * 1000d / Stopwatch.Frequency, MedianMilliseconds = At(0.5), P95Milliseconds = At(0.95), P99Milliseconds = At(0.99), MaximumMilliseconds = At(1) };
    }

    private static StagePoint Capture()
    {
        Assert.True(GetThreadTimes(GetCurrentThread(), out _, out _, out long kernel, out long user));
        using Process process = Process.GetCurrentProcess();
        return new(GC.GetTotalAllocatedBytes(), [GC.CollectionCount(0), GC.CollectionCount(1), GC.CollectionCount(2)], kernel + user, process.TotalProcessorTime.TotalMilliseconds, DiagnosticGcPauseObservation.Capture());
    }

    private sealed record StagePoint(long Allocated, int[] Collections, long ThreadCpu, double ProcessCpu, DiagnosticGcPauseObservation GcPause);
    private readonly record struct FrameSample(int Stage, long Frame, long Begin, long End, PresentationPacerObservation Pacer, DiagnosticHostFramePhases? Phases, bool Active, DiagnosticGcPauseObservation GcPause);
    private static T Get<T>(RecreateGame game, string name) => (T)typeof(RecreateGame).GetField(name, BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(game)!;
    private static void Set(RecreateGame game, string name, object value) => typeof(RecreateGame).GetField(name, BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(game, value);

    [DllImport("kernel32.dll")]
    private static extern nint GetCurrentThread();

    [DllImport("kernel32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetThreadTimes(nint handle, out long created, out long exited, out long kernel, out long user);
}
