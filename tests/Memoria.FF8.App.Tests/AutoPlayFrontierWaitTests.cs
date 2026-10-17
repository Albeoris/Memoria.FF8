using System.Text.Json;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class AutoPlayFrontierWaitTests
{
    [Fact]
    public void ExhaustedFrontierAllowsTheOriginalTimerDialogueAndFieldTransition()
    {
        string manifest = TestResources.FixturePath("App.Tests.AutoPlayFrontierWaitTests.1");
        byte[] originalManifest = File.ReadAllBytes(manifest);
        AutoPlayRunState saved = JsonSerializer.Deserialize<AutoPlayRunState>(originalManifest, AutoPlayController.JsonOptions)!;
        string snapshot = Path.Combine(Path.GetDirectoryName(manifest)!, saved.Snapshot!);
        byte[] originalSnapshot = File.ReadAllBytes(snapshot);
        using InstalledRun run = new();
        run.Controller.ResumeFrom(manifest);
        Assert.Equal(141, run.Session.State.FieldId);
        Assert.Null(run.Controller.State.ActiveEvent);
        Assert.True(run.Session.ExamTimer!.RequireRemaining() >= 10);
        for (int tick = 0; tick < 64 && run.Session.State.FieldId == 141 && run.Controller.Error is null; tick++)
        {
            run.Controller.Advance(1);
        }
        Assert.Null(run.Controller.Error);
        Assert.True(run.Controller.Running);
        Assert.Equal(251, run.Session.State.FieldId);
        Assert.Contains(run.TimerReads, remaining => remaining < 10);
        Assert.Contains(run.Commands, command => command.Name == "KILLTIMER");
        Assert.Contains(run.Commands, command => command.Name == "MAPJUMPO");
        Assert.Contains(run.Controller.State.ObservedMessages, message => message.StartsWith("141:", StringComparison.Ordinal));
        Assert.Contains(run.Actions, action => action.Action is { Kind: PlayerActionKind.Wait, WaitWhilePlayable: true, Frames: 300 } && action.Result.FramesAdvanced > 1);
        Assert.False(run.Controller.State.ReachedEnding);
        Assert.Equal(originalManifest, File.ReadAllBytes(manifest));
        Assert.Equal(originalSnapshot, File.ReadAllBytes(snapshot));
    }

    [Fact]
    public void OrdinaryFrontierWaitingStillFailsAtTheTenSecondWallClockDeadline()
    {
        string manifest = TestResources.FixturePath("App.Tests.AutoPlayFrontierWaitTests.1");
        ManualClock clock = new();
        using InstalledRun run = new(clock);
        run.Controller.ResumeFrom(manifest);
        long frame = run.World.Events.Frame;
        long timerCalls = run.Session.ExamTimer!.ClockCalls;
        clock.Advance(TimeSpan.FromMilliseconds(9999));
        run.Controller.CheckIdleDeadline();
        clock.Advance(TimeSpan.FromMilliseconds(1));
        run.Controller.Advance(1);
        Assert.False(run.Controller.Running);
        Assert.Contains("10 seconds", run.Controller.Error);
        Assert.NotNull(run.Controller.ReproductionPath);
        Assert.Equal(frame, run.World.Events.Frame);
        Assert.Equal(timerCalls, run.Session.ExamTimer!.ClockCalls);
        Assert.Empty(run.Actions);
        Assert.False(run.Controller.State.ReachedEnding);
    }

    [Fact]
    public void RetiredFrameCutoffMetadataIsIgnoredAndIsNotWrittenAgain()
    {
        AutoPlayRunState state = JsonSerializer.Deserialize<AutoPlayRunState>("{\"IdleFrames\":1200}", AutoPlayController.JsonOptions)!;
        Assert.DoesNotContain("IdleFrames", JsonSerializer.Serialize(state, AutoPlayController.JsonOptions));
    }

    private sealed class InstalledRun : IDisposable
    {
        private readonly string gameDirectory = TestResources.GameDirectory;
        private GameWorld? world;
        private readonly List<GameWorld> observedWorlds = [];

        public InstalledRun(TimeProvider? clock = null)
        {
            Controller = new(Session, () => World, Restore, Output, deadlineTime: clock);
        }

        public GameSession Session { get; } = GameSession.CreateColdProcess();
        public string Output { get; } = Path.Combine(Path.GetTempPath(), "ff8-autoplay-frontier-wait-" + Guid.NewGuid().ToString("N"));
        public AutoPlayController Controller { get; }
        public List<ScriptCommand> Commands { get; } = [];
        public List<int> TimerReads { get; } = [];
        public IEnumerable<PlayerActionObservation> Actions => observedWorlds.SelectMany(item => item.Events.ActionLog);

        public GameWorld World
        {
            get
            {
                if (world is null)
                {
                    world = new(Session, gameDirectory, validateWorldPresentation: false);
                    observedWorlds.Add(world);
                    world.Events.CommandExecuted += command =>
                    {
                        Commands.Add(command);
                        if (command.Name == "GETTIMER")
                        {
                            TimerReads.Add(Session.ExamTimer!.RequireRemaining());
                        }
                    };
                }
                return world;
            }
        }

        private void Restore(GameState state)
        {
            world?.Dispose();
            world = null;
            Session.Restore(state);
        }

        public void Dispose()
        {
            world?.Dispose();
            if (Directory.Exists(Output))
            {
                Directory.Delete(Output, true);
            }
        }
    }

    private sealed class ManualClock : TimeProvider
    {
        private long timestamp;
        public override long TimestampFrequency => TimeSpan.TicksPerSecond;
        public override long GetTimestamp() => timestamp;
        public override DateTimeOffset GetUtcNow() => DateTimeOffset.UnixEpoch.AddTicks(timestamp);

        public void Advance(TimeSpan elapsed)
        {
            timestamp += elapsed.Ticks;
        }
    }
}
