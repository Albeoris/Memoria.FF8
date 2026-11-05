using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class AutoPlaySceneDeadlineTests
{
    [Theory]
    [InlineData("field", "Field:75:0:-1")]
    [InlineData("story", "Field:74:1:-1")]
    [InlineData("movie", "Movie:74:0:0")]
    public void AnUnseenSceneIsObservedBeforeAnExpiredDeadlineAndDoesNotKeepResettingIt(string change, string expected)
    {
        GameSession session = CreateSession();
        ManualClock clock = new();
        AutoPlayController controller = CreateController(session, clock);
        controller.CheckIdleDeadline();
        clock.Advance(TimeSpan.FromSeconds(10));
        switch (change)
        {
            case "field":
                session.Restore(session.State with { FieldId = 75 });
                break;
            case "story":
                session.SetEventMemory(session.State.EventMemory.Write(256, 2, NativeScalarValue.Exact(1)));
                break;
            case "movie":
                session.RequestMovie(0);
                break;
        }

        controller.CheckIdleDeadline();

        Assert.Contains(expected, controller.State.ObservedScenes);
        Assert.Equal(2, controller.State.ObservedScenes.Count);
        Assert.Empty(controller.State.Coverage.Visits);
        clock.Advance(TimeSpan.FromMilliseconds(9999));
        controller.CheckIdleDeadline();
        clock.Advance(TimeSpan.FromMilliseconds(1));
        Assert.Throws<TimeoutException>(controller.CheckIdleDeadline);
    }

    [Fact]
    public void ReturningToAnObservedSceneDoesNotExtendTheDeadline()
    {
        GameSession session = CreateSession();
        ManualClock clock = new();
        AutoPlayController controller = CreateController(session, clock);
        controller.CheckIdleDeadline();
        session.Restore(session.State with { FieldId = 75 });
        controller.CheckIdleDeadline();
        clock.Advance(TimeSpan.FromSeconds(10));
        session.Restore(session.State with { FieldId = 74 });

        Assert.Throws<TimeoutException>(controller.CheckIdleDeadline);
        Assert.Equal(2, controller.State.ObservedScenes.Count);
    }

    [Fact]
    public void MovieIdentityCountsOnlyWithinTheMovieScene()
    {
        GameSession session = CreateSession();
        ManualClock clock = new();
        AutoPlayController controller = CreateController(session, clock);
        controller.CheckIdleDeadline();
        session.Restore(session.State with { MovieIndex = 1 });
        clock.Advance(TimeSpan.FromSeconds(10));
        Assert.Throws<TimeoutException>(controller.CheckIdleDeadline);
        session.RequestMovie(0);
        controller.CheckIdleDeadline();
        clock.Advance(TimeSpan.FromSeconds(10));
        session.RequestMovie(1);
        controller.CheckIdleDeadline();
        Assert.Contains("Movie:74:0:0", controller.State.ObservedScenes);
        Assert.Contains("Movie:74:0:1", controller.State.ObservedScenes);
        Assert.Equal(3, controller.State.ObservedScenes.Count);
        clock.Advance(TimeSpan.FromSeconds(10));
        session.RequestMovie(0);
        Assert.Throws<TimeoutException>(controller.CheckIdleDeadline);
    }

    [Fact]
    public void RetainedFieldTransitionIsRecognizedBeforeItsFirstDestinationAllocation()
    {
        string? manifest = Environment.GetEnvironmentVariable("MEMORIA_FF8_AUTOPLAY_TRANSITION_MANIFEST");
        Assert.SkipUnless(!string.IsNullOrWhiteSpace(manifest) && File.Exists(manifest), "Set MEMORIA_FF8_AUTOPLAY_TRANSITION_MANIFEST to an optional retained field-transition manifest.");
        string gameDirectory = TestResources.GameDirectory;
        AutoPlayRunState saved = JsonSerializer.Deserialize<AutoPlayRunState>(File.ReadAllText(manifest!), AutoPlayController.JsonOptions)!;
        GameState source = GameSession.Load(Path.Combine(Path.GetDirectoryName(Path.GetFullPath(manifest!))!, saved.Snapshot!));
        Assert.Null(source.EventState);
        Assert.NotNull(source.DepartingFieldState);
        Assert.NotEqual(source.FieldId, source.DepartingFieldState.Value.GetProperty("FieldId").GetInt32());
        string scene = $"{source.Scene}:{source.FieldId}:{source.Variables.GetValueOrDefault(256)}:-1";
        Assert.DoesNotContain(scene, saved.ObservedScenes);
        GameSession session = GameSession.CreateColdProcess();
        GameWorld? world = null;
        ManualClock clock = new();
        string output = Path.Combine(Path.GetTempPath(), "autoplay-transition-deadline-" + Guid.NewGuid().ToString("N"));
        try
        {
            GameWorld GetWorld() => world ??= new(session, gameDirectory, validateWorldPresentation: false);
            void Restore(GameState state)
            {
                world?.Dispose();
                world = null;
                session.Restore(state);
            }
            AutoPlayController controller = new(session, GetWorld, Restore, output, deadlineTime: clock);
            controller.ResumeFrom(manifest!);
            Assert.Null(world);
            clock.Advance(TimeSpan.FromSeconds(10));

            controller.Advance(1);

            Assert.Null(controller.Error);
            Assert.True(controller.Running);
            Assert.Contains(scene, controller.State.ObservedScenes);
            Assert.NotNull(world);
            Assert.Equal(source.FieldId, world.Events.FieldId);
            Assert.True(world.HasCurrentFieldAllocation);
        }
        finally
        {
            world?.Dispose();
            if (Directory.Exists(output))
            {
                Directory.Delete(output, recursive: true);
            }
        }
    }

    private static GameSession CreateSession()
    {
        GameSession session = GameSession.CreateColdProcess();
        session.Restore(session.State with { Scene = SceneKind.Field });
        return session;
    }

    private static AutoPlayController CreateController(GameSession session, TimeProvider clock)
    {
        AutoPlayController controller = new(session, () => throw new InvalidOperationException("Deadline observation must not allocate or inspect a world."), session.Restore, Path.GetTempPath(), deadlineTime: clock);
        controller.Resume();
        return controller;
    }

    private sealed class ManualClock : TimeProvider
    {
        private long timestamp;
        public override long TimestampFrequency => TimeSpan.TicksPerSecond;
        public override long GetTimestamp() => timestamp;

        public void Advance(TimeSpan elapsed)
        {
            timestamp += elapsed.Ticks;
        }
    }
}
