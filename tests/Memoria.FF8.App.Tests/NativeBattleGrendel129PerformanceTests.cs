using System.Diagnostics;
using System.Reflection;
using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.GameUi;
using Memoria.FF8.Platform;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

[Collection("GPU rendering")]
public sealed class NativeBattleGrendel129PerformanceTests
{
    [Fact]
    public void Original_Grendel_breath_measures_two_independent_casts_in_the_canonical_graphics_host()
    {
        string? output = Environment.GetEnvironmentVariable("MEMORIA_FF8_BREATH_PERFORMANCE_OUTPUT");
        Assert.SkipUnless(output is not null && Environment.GetEnvironmentVariable("MEMORIA_FF8_BATTLE_HOST_PERFORMANCE") == "1" && Environment.GetEnvironmentVariable("MEMORIA_DIAGNOSTIC_PACING") == "1", "Select a private breath output directory and enable battle host performance and diagnostic pacing.");
        string directory = TestResources.GameDirectory;
        using GameAssets assets = new(directory);
        object[] casts = new object[2];
        for (int cast = 0; cast < casts.Length; cast++)
        {
            casts[cast] = MeasureCast(assets, directory, cast);
        }
        using Process process = Process.GetCurrentProcess();
        Directory.CreateDirectory(output!);
        File.WriteAllText(Path.Combine(output!, "grendel129-summary.json"), JsonSerializer.Serialize(new
        {
            Scope = "Two independent original formation 338 component entries; canonical RecreateGame timing and GPU submission; no player battle save or manual callback cadence",
            Formation = 338,
            Model = 53,
            Command = 8,
            Action = 102,
            Sequence = 12,
            Effect = 129,
            Backend = GraphicsHostReadiness.Backend,
            Runtime = new { Version = Environment.Version.ToString(), System.Runtime.GCSettings.IsServerGC, QuickJitHostOption = AppContext.GetData("System.Runtime.TieredCompilation.QuickJit"), QuickJitEnvironment = Environment.GetEnvironmentVariable("DOTNET_TC_QuickJit"), GcHeapCountHostOption = AppContext.GetData("System.GC.HeapCount"), GcHeapCountEnvironment = Environment.GetEnvironmentVariable("DOTNET_GCHeapCount"), Environment.ProcessorCount },
            Process = new { PriorityClass = process.PriorityClass.ToString(), Affinity = OperatingSystem.IsWindows() || OperatingSystem.IsLinux() ? process.ProcessorAffinity.ToInt64() : (long?)null, process.PeakWorkingSet64 },
            Casts = casts
        }));
    }

    private static object MeasureCast(GameAssets assets, string directory, int cast)
    {
        GameSession session = GameWorld.CreateColdProcess(assets);
        session.StartNewGame();
        using GameWorld world = new(session, directory, sharedAssets: assets);
        world.ConfigureGameplayPreferences(session.State.GameplayPreferences with { Tuning = session.State.GameplayPreferences.Tuning with { MaximumDamage = 4 } });
        BattleEncounter encounter = world.BattleResources.LoadEncounter(338);
        BattleEnemyPlacement placement = encounter.Slots.First(enemy => enemy.ModelId == 53);
        string casterId = $"enemy-{placement.Slot}";
        DiagnosticCharacterPreparation character = new(0, 0, [], []) { HitPoints = 486, Level = 7 };
        DiagnosticBattleRequest request = new(338, 0, 128, 0, 0)
        {
            BattleMusic = 5,
            PrimaryMusicChannel = -1,
            Fixture = new([], [character], true) { CombatParty = [0, 255, 255] },
            Driver = new(new(BattleInputKind.Action, ActorId: casterId, TargetId: "party-0", Ability: BattleAbility.EnemyAbility, ActionId: 102), 129) { ExpectedNativeCommand = 8, ExpectedNativeAction = 102 }
        };
        BattleSession battle = world.StartDiagnosticBattle(request);
        BattleCombatant caster = Assert.Single(battle.Combatants, actor => actor.Id == casterId);
        Assert.Equal(53, caster.ModelId);
        Assert.Equal(encounter.StageId, battle.EntryStageId);
        NativeBattleAiDefinition ai = Assert.IsType<NativeBattleAiDefinition>(caster.NativeAi);
        BattleDiagnosticEnemyAbility selection = new(53, placement.Slot, ai.LevelBand, 3, 8, 102, 12, 204);
        NativeBattleAiAbility ability = ai.Abilities[16 * selection.LevelBand + selection.AbilitySlot];
        Assert.Equal((byte)8, ability.Command);
        Assert.Equal((ushort)102, ability.Id);
        Assert.Equal((byte)12, ability.Sequence);
        for (int tick = 0; tick < 1200 && !battle.CanSubmitDiagnosticEnemyAbility(casterId, selection); tick++)
        {
            if (battle.NativeEntryReady && battle.ObserveDiagnosticDrainBlockers().Count == 0)
            {
                battle.AdvanceTurn();
            }
            battle.Tick();
        }
        Assert.True(battle.CanSubmitDiagnosticEnemyAbility(casterId, selection));
        string settingsDirectory = TestResources.ArtifactFile($"grendel129-host-{cast}");
        Directory.CreateDirectory(settingsDirectory);
        GlobalSettingsPaths settings = new(isolatedDirectory: settingsDirectory);
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
        GraphicsBackend backend = GraphicsBackendSelection.Parse(GraphicsHostReadiness.Backend);
        UiPreferences preferences = new() { WindowWidth = 960, WindowHeight = 720, VSync = true, BattleFps = 60, Graphics = new() { Backend = backend, MsaaSamples = 8 } };
        preferences.Save(settings.UiPreferencesPath);
        LaunchOptions options = new() { GameDirectory = directory, InitialState = session.State, SavePath = Path.Combine(settingsDirectory, "unused.json"), SettingsDirectory = settingsDirectory, MuteAudio = true, ExitAfterSeconds = 90, ResolutionSpecified = true, Width = 960, Height = 720, RequestedGraphicsBackend = backend };
        using RecreateGame game = new(options, session);
        Set(game, "world", world);
        Set(game, "globalSettings", settings);
        Set(game, "uiPreferences", preferences);
        DiagnosticHostFramePhases[] frames = new DiagnosticHostFramePhases[2640];
        List<BattleDiagnosticEffectDispatch> dispatches = [];
        battle.DiagnosticEffectDispatched += dispatches.Add;
        int presents = 0;
        int count = 0;
        int eventStart = 0;
        int eventCursor = 0;
        int completedAt = -1;
        bool submitted = false;
        bool complete = false;
        bool released = false;
        bool geometry = false;
        long started = 0;
        long finished = 0;
        long callbacksBefore = 0;
        long allocatedBefore = 0;
        GpuTriangleRenderer? gpu = null;
        void ExitHost()
        {
            Set(game, "windowCloseApproved", true);
            Set(game, "authoringExitAllowed", true);
            game.Exit();
        }
        game.PresentedFrameObserver = (_, end, _, phases) =>
        {
            if (finished != 0)
            {
                return;
            }
            presents++;
            if (!submitted)
            {
                if (presents < 120)
                {
                    return;
                }
                Assert.True(battle.CanSubmitDiagnosticEnemyAbility(casterId, selection));
                gpu = Get<GpuTriangleRenderer>(game, "battleGpu");
                eventStart = battle.Events.Count;
                eventCursor = eventStart;
                started = end;
                allocatedBefore = GC.GetTotalAllocatedBytes();
                callbacksBefore = battle.NativeHostCallbacks ?? 0;
                battle.SubmitDiagnosticEnemyAbility(casterId, selection, 129);
                submitted = true;
                return;
            }
            frames[count++] = phases ?? throw new InvalidDataException("Host phase timing must remain enabled throughout the measured cast.");
            if (!geometry && battle.Presentation?.Graphics.OriginalEffect is { RequestedEffect: 129, ReturnedDescriptor: > 0 } && gpu!.FrameDrawCalls > 0 && battle.PreparedPresentation is { } prepared)
            {
                foreach (ref readonly NativeBattleLogicalResolvedCommand command in prepared.Primitives.LastCommands)
                {
                    if (command.Command.Packet.Bank == "20DFAB8" && command.Command.Opcode is >= 0x20 and < 0x80)
                    {
                        geometry = true;
                        break;
                    }
                }
            }
            for (; eventCursor < battle.Events.Count; eventCursor++)
            {
                if (battle.Events[eventCursor] is { Kind: "ActionComplete" } completed && completed.ActorId == casterId)
                {
                    complete = true;
                    completedAt = presents;
                }
            }
            released = complete && battle.ActiveAction is null && battle.QueuedActions.Count == 0 && battle.ObserveDiagnosticDrainBlockers().Count == 0;
            if (released && presents >= completedAt + 2 || count == frames.Length)
            {
                finished = end;
                ExitHost();
            }
        };
        using System.Threading.Timer deadline = new(_ => ExitHost(), null, TimeSpan.FromSeconds(90), Timeout.InfiniteTimeSpan);
        game.Run();
        BattleDiagnosticEffectDispatch dispatch = Assert.Single(dispatches, entry => entry.ActorId == casterId && entry.RequestedEffect == 129);
        Assert.Equal((ushort)129, dispatch.ExecutedEffect);
        Assert.Equal((byte)8, dispatch.NativeCommand);
        Assert.Equal(102, dispatch.ActionId);
        Assert.True(complete && released && geometry, $"Grendel cast {cast}: complete={complete}, released={released}, geometry={geometry}, frames={count}, error={Get<string?>(game, "error")}.");
        BattleEvent[] contacts = battle.Events.Skip(eventStart).Where(entry => entry.Kind == "EffectContact" && entry.ActorId == casterId).ToArray();
        Assert.Single(contacts);
        Assert.Equal("party-0", contacts[0].TargetId);
        Assert.Equal(0, battle.Presentation!.Memory.Active);
        DiagnosticHostFramePhases[] measured = frames[..count];
        return new
        {
            Cast = cast,
            Complete = complete,
            Released = released,
            VisibleGeometry = geometry,
            Contacts = contacts.Length,
            Displays = count,
            Seconds = (finished - started) / (double)Stopwatch.Frequency,
            NativeCallbacks = (battle.NativeHostCallbacks ?? 0) - callbacksBefore,
            MeasuredCallbacks = measured.Sum(frame => frame.Native.Callbacks),
            AllocatedBytes = GC.GetTotalAllocatedBytes() - allocatedBefore,
            Update = Distribution(measured.Select(frame => frame.Update).ToArray()),
            GpuSubmission = Distribution(measured.Select(frame => frame.Draw).ToArray()),
            UpdateAndGpuSubmission = Distribution(measured.Select(frame => frame.Update + frame.Draw).ToArray()),
            Width = game.GraphicsDevice.PresentationParameters.BackBufferWidth,
            Height = game.GraphicsDevice.PresentationParameters.BackBufferHeight,
            Samples = game.GraphicsDevice.PresentationParameters.MultiSampleCount,
            FormationHash = world.BattleResources.FormationSourceHash(338)
        };
    }

    private static object Distribution(long[] values)
    {
        Assert.NotEmpty(values);
        Array.Sort(values);
        double At(double quantile) => values[Math.Clamp((int)Math.Ceiling(values.Length * quantile) - 1, 0, values.Length - 1)] * 1000d / Stopwatch.Frequency;
        return new { MedianMilliseconds = At(0.5), P95Milliseconds = At(0.95), MaximumMilliseconds = At(1) };
    }

    private static T Get<T>(RecreateGame game, string name) => (T)typeof(RecreateGame).GetField(name, BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(game)!;
    private static void Set(RecreateGame game, string name, object value) => typeof(RecreateGame).GetField(name, BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(game, value);
}
