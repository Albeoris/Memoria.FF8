using System.Reflection;
using System.Text.Json;
using Memoria.FF8.GameUi;
using Memoria.FF8.Runtime;
using Microsoft.Xna.Framework;

namespace Memoria.FF8.App.Tests;

[Collection("GPU rendering")]
public sealed class BattleSavedDuelHostPerformanceTests
{
    [Fact]
    public void Standalone_Duel_reports_real_host_cadence_across_repeated_moves()
    {
        string? source = Environment.GetEnvironmentVariable("MEMORIA_FF8_DUEL_HOST_SNAPSHOT");
        string? output = Environment.GetEnvironmentVariable("MEMORIA_FF8_DUEL_OUTPUT");
        Assert.SkipUnless(source is not null && output is not null, "Select a standalone Duel and output directory for optional GPU acceptance.");
        GameSession session = new(GameSession.Load(source!));
        using GameWorld world = BattleHostLoopPerformanceTests.RestorePerformanceWorld(session);
        BattleSession battle = Assert.IsType<BattleSession>(world.Battle);
        BattleCombatant actor = battle.Combatants.Single(candidate => candidate.IsPlayer && candidate.NativeLimit?.Command == 11);
        string settingsDirectory = Path.Combine(Path.GetTempPath(), "ff8-duel-host-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(settingsDirectory);
        try
        {
            GlobalSettingsPaths settings = new(settingsDirectory);
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
            LaunchOptions options = new() { GameDirectory = TestResources.GameDirectory, InitialState = session.State, StatePath = source, SavePath = Path.Combine(settingsDirectory, "unused.json"), MuteAudio = true, ExitAfterSeconds = 40, ResolutionSpecified = true, Width = 1280, Height = 720 };
            using RecreateGame host = new(options, session);
            Set(host, "world", world);
            Set(host, "globalSettings", settings);
            Set(host, "uiPreferences", new UiPreferences { WindowWidth = 1280, WindowHeight = 720, VSync = false, BattleFps = 60 });
            MoveObserver observer = new(host, battle, actor.Id);
            host.Components.Add(observer);
            using System.Threading.Timer deadline = new(_ => host.Exit(), null, TimeSpan.FromSeconds(60), System.Threading.Timeout.InfiniteTimeSpan);
            host.Run();
            Directory.CreateDirectory(output!);
            File.WriteAllText(Path.Combine(output!, "duel-host-windows.json"), JsonSerializer.Serialize(observer.Windows));
            Assert.False(Get<bool>(host, "fieldFaulted"), Get<string?>(host, "error"));
            Assert.Equal(6, observer.Moves);
            Assert.True(observer.Windows.Count >= 3);
            Assert.Equal(2, observer.CompletedWindows);
        }
        finally
        {
            Directory.Delete(settingsDirectory, recursive: true);
        }
    }

    private static T Get<T>(RecreateGame host, string name) => (T)typeof(RecreateGame).GetField(name, BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(host)!;
    private static void Set(RecreateGame host, string name, object value) => typeof(RecreateGame).GetField(name, BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(host, value);

    private sealed class MoveObserver(RecreateGame host, BattleSession battle, string actor) : GameComponent(host)
    {
        private GameLoopSample? previous;
        private bool invoked = battle.GetNativeDuel(actor) is not null;
        private int ordinaryTurns;
        public int Moves { get; private set; }
        public int CompletedWindows { get; private set; }
        public List<object> Windows { get; } = [];

        public override void Update(GameTime gameTime)
        {
            GameLoopSample? sample = Get<GameLoopMetrics>(host, "metrics").Latest;
            if (sample is not null && !ReferenceEquals(previous, sample))
            {
                previous = sample;
                Windows.Add(new { gameTime.TotalGameTime.TotalSeconds, Moves, Phase = battle.GetNativeDuel(actor)?.Phase.ToString(), battle.Frame, Sample = sample });
                if (Moves == 6 && battle.GetNativeDuel(actor) is null && ++CompletedWindows == 2)
                {
                    host.Exit();
                    return;
                }
            }
            if (!invoked && battle.ActiveAction is null && battle.QueuedActions.Count == 0 && battle.GuardianForceCasts.Count == 0)
            {
                if (!battle.CanChoosePlayerCommand(actor))
                {
                    battle.AdvanceTurn();
                }
                if (battle.CanChoosePlayerCommand(actor))
                {
                    BattleCombatant target = battle.Combatants.First(candidate => !candidate.IsPlayer && candidate.IsAlive && candidate.IsActive);
                    if (battle.GetNativeLimitLevel(actor) != 0 || battle.RefreshNativeLimit(actor) != 0)
                    {
                        battle.QueueAction(actor, target.Id, BattleAbility.LimitBreak, 11 << 16);
                        invoked = true;
                    }
                    else
                    {
                        Assert.True(++ordinaryTurns <= 5, "The selected fixture must offer Duel within five ordinary turns.");
                        battle.QueueAttack(actor, target.Id);
                    }
                }
            }
            if (Moves < 6 && battle.GetNativeDuel(actor) is { Phase: BattleNativeDuelPhase.Ready })
            {
                BattleNativeDuelChoice choice = battle.GetNativeDuelChoices(actor).First(candidate => candidate.Learned && !candidate.Finisher);
                TimeSpan pulse = TimeSpan.FromTicks((TimeSpan.TicksPerSecond + 59) / 60);
                foreach (ushort button in choice.Buttons)
                {
                    battle.AdvanceNativeDuelPresentation(pulse, 0);
                    battle.AdvanceNativeDuelPresentation(pulse, button);
                }
                Assert.Equal(BattleNativeDuelPhase.AwaitingEffect, battle.GetNativeDuel(actor)!.Phase);
                Moves++;
            }
            base.Update(gameTime);
        }
    }
}
