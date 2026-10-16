using System.Diagnostics;
using System.Text.Json;
using Memoria.FF8.Events;
using Memoria.FF8.Media;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class StorySafeMovieSkipTests
{
    [Theory]
    [InlineData(0, false)]
    [InlineData(34, true)]
    [InlineData(143, false)]
    public void CorridorSkipExecutesIntermediateScriptEventsAndRecreatesWithoutDecoder(int skipFrame, bool userPrefix)
    {
        string game = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR") ?? LaunchOptions.DefaultGameDirectory;
        Assert.SkipUnless(Directory.Exists(game), "Installed original movie and field corpus is required.");
        GameSession session;
        if (userPrefix)
        {
            string prefixPath = Path.Combine(Repository(), "../fixtures/external-fixture.json");
            session = new GameSession(GameSession.Load(prefixPath));
        }
        else
        {
            session = GameSession.CreateColdProcess();
            session.StartNewGame();
        }
        var clock = new MovieClock(session, game);
        using var world = new GameWorld(session, game, clock);
        ReachCorridorMovie(session, world);
        DecodeThrough(session, world, clock, game, skipFrame);
        Assert.Equal(229, session.State.FieldId);
        Assert.True(session.IsMoviePlaying);
        session.SetEventState(world.Events.Capture());
        GameState beforeSkip = session.State;

        // The old implementation's immediate cleanup is native-correct as a low-level close,
        // but is not a story-safe skip: controller0/script8 waits forever for L[80]>=144.
        var closedSession = new GameSession(JsonSerializer.Deserialize<GameState>(JsonSerializer.Serialize(beforeSkip))!);
        using (var control = new GameWorld(closedSession, game))
        {
            control.EnsureField();
            closedSession.CompleteMovie(true);
            for (int callback = 0; callback < 200; callback++)
            {
                control.Events.Step();
            }
            Assert.Equal(229, closedSession.State.FieldId);
            Assert.Null(control.Events.LastFault);
            Assert.Contains(control.Events.Decisions, decision => decision.Predicate == "(native[80/4] < 144)" && decision.Value != 0);
        }

        long frameBefore = world.Events.Frame;
        PlayerActionResult result = world.Apply(new PlayerAction(PlayerActionKind.SkipMovie));
        Assert.True(result.Accepted);
        Assert.True(world.Events.Frame > frameBefore);
        Assert.Equal(world.Events.Frame - frameBefore, result.FramesAdvanced);
        Assert.True(session.State.MoviePlayback!.Skipped);
        Assert.False(session.IsMoviePlaying);
        for (int callback = 0; callback < 500 && session.State.FieldId == 229; callback++)
        {
            world.Events.Step();
        }
        Assert.Equal(232, session.State.FieldId);
        Assert.Equal(new FieldTransition(232, 1389, -3580, 50, 0), session.State.FieldArrival);
        world.EnsureField();
        for (int callback = 0; callback < 500 && world.Events.CurrentDialogue is null; callback++)
        {
            world.Events.Step();
        }
        Assert.Equal(0, world.Events.CurrentDialogue!.MessageId);
        Assert.Null(world.Events.LastFault);
        session.SetEventState(world.Events.Capture());
        GameState source = session.State;
        string temporary = Path.Combine(Path.GetTempPath(), "Memoria.FF8-story-skip-" + Guid.NewGuid().ToString("N") + ".json");
        try
        {
            session.Save(temporary);
            GameState rebuilt = GameSession.Load(temporary);
            Assert.True(JsonElement.DeepEquals(JsonSerializer.SerializeToElement(source), JsonSerializer.SerializeToElement(rebuilt)));
            using var replay = new GameWorld(new GameSession(rebuilt), game);
            replay.EnsureField();
            Assert.Equal(world.Render(TimeSpan.Zero), replay.Render(TimeSpan.Zero));
            Assert.Equal(world.Apply(new PlayerAction(PlayerActionKind.Wait)), replay.Apply(new PlayerAction(PlayerActionKind.Wait)));
            Assert.True(JsonElement.DeepEquals(world.Events.Capture(), replay.Events.Capture()));
            Assert.Equal(world.Render(TimeSpan.Zero), replay.Render(TimeSpan.Zero));
            if (skipFrame == 34)
            {
                string checkpoint = Path.Combine(Repository(), "../fixtures/external-fixture.json");
                session.SetEventState(world.Events.Capture());
                session.Save(checkpoint);
            }
        }
        finally
        {
            File.Delete(temporary);
        }
    }

    internal static void ReachCorridorMovie(GameSession session, GameWorld world, string playerName = "Squall")
    {
        int centering = 0;
        for (int input = 0; input < 6000; input++)
        {
            world.EnsureField();
            if (session.IsMoviePlaying && session.State.MovieIndex == 0 && session.State.FieldId == 229)
            {
                return;
            }
            DialogueState? dialogue = world.Events.CurrentDialogue;
            PlayerAction action = session.IsMoviePlaying ? new(PlayerActionKind.SkipMovie) : dialogue?.IsNamePrompt == true ? new(PlayerActionKind.EnterName, Text: playerName) : dialogue?.FirstChoice is int choice ? new(PlayerActionKind.Choose, choice) : dialogue is { CanConfirm: true } ? new(PlayerActionKind.Confirm) : world.Events.FieldId == 229 && world.Events.CanAcceptMovementInput ? centering++ < 2 ? new(PlayerActionKind.Move, Horizontal: -1) : new(PlayerActionKind.Move, Vertical: 1) : new(PlayerActionKind.Wait);
            Assert.True(world.Apply(action).Accepted);
            Assert.Null(world.Events.LastFault);
        }
        Assert.Fail("The genuine opening route did not reach the corridor movie.");
    }

    internal static void DecodeThrough(GameSession session, GameWorld world, MovieClock clock, string game, int target)
    {
        MovieInformation information = clock.Prepare();
        if (target < 2)
        {
            return;
        }
        using var decoder = new FfmpegMovieDecoder(clock.ToolsDirectory, clock.SourcePath, information: information);
        Stopwatch timeout = Stopwatch.StartNew();
        while (session.State.MoviePlayback!.FrameIndex < target && timeout.Elapsed < TimeSpan.FromSeconds(30))
        {
            while (decoder.TryGetAudio(out _))
            {
            }
            if (decoder.TryGetNextFrame(double.MaxValue, out MovieFrame? frame))
            {
                if (frame!.Index >= 2)
                {
                    Assert.True(world.AdvanceMovieFrame(new MoviePlaybackState(checked((int)frame.Index), information.FrameCount - 1, true, false, false), frame.Index / information.FramesPerSecond));
                }
            }
            else
            {
                Thread.Sleep(1);
            }
        }
        Assert.Equal(target, session.State.MoviePlayback!.FrameIndex);
        Assert.Null(decoder.Error);
    }

    internal static string Repository()
    {
        DirectoryInfo? directory = new(TestResources.FixtureDirectory);
        return directory?.FullName ?? throw new DirectoryNotFoundException("Research checkout not found.");
    }
}
