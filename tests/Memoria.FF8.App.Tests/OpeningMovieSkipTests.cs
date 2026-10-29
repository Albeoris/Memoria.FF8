using System.Diagnostics;
using System.Text.Json;
using Memoria.FF8.Events;
using Memoria.FF8.Media;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class OpeningMovieSkipTests(ITestOutputHelper output)
{
    [Theory]
    [InlineData(0)]
    [InlineData(150)]
    [InlineData(2800)]
    public void PreparedOpeningSkipsWithoutDecoderAndMatchesDecodedCallbacks(int startFrame)
    {
        string game = TestResources.GameDirectory;
        Assert.SkipUnless(Directory.Exists(game), "Installed original opening movie and field corpus is required.");
        GameSession referenceSession = GameSession.CreateColdProcess();
        referenceSession.StartNewGame();
        MovieClock referenceClock = new(referenceSession, game);
        using GameWorld reference = new(referenceSession, game, referenceClock);
        ReachOpening(referenceSession, reference);
        MovieInformation information = referenceClock.Prepare();
        Assert.Equal(2925, information.FrameCount);
        StorySafeMovieSkipTests.DecodeThrough(referenceSession, reference, referenceClock, game, startFrame);
        referenceSession.SetEventState(reference.Events.Capture());
        GameSession session = new(JsonSerializer.Deserialize<GameState>(JsonSerializer.Serialize(referenceSession.PrepareSnapshot()))!);
        string temporary = Path.Combine(Path.GetTempPath(), "memoria-opening-skip-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(temporary);
        try
        {
            // Only the initial metadata probe can use these tools. Cached skip must never
            // launch a second decoder or depend on pixels/PCM that will not be presented.
            foreach (string executable in new[] { "ffmpeg.exe", "ffprobe.exe" })
            {
                File.Copy(Path.Combine(referenceClock.ToolsDirectory, executable), Path.Combine(temporary, executable));
            }
            MovieClock clock = new(session, game, temporary);
            using GameWorld world = new(session, game, clock);
            world.EnsureField();
            Assert.Equal(information, clock.Prepare());
            File.Delete(Path.Combine(temporary, "ffmpeg.exe"));
            File.Delete(Path.Combine(temporary, "ffprobe.exe"));
            Stopwatch accelerated = Stopwatch.StartNew();
            PlayerActionResult result = world.Apply(new PlayerAction(PlayerActionKind.SkipMovie));
            accelerated.Stop();
            Assert.True(result.Accepted);
            Assert.False(session.IsMoviePlaying);
            Assert.True(session.State.MoviePlayback!.Skipped);
            Assert.Null(world.Events.LastFault);

            // Independent oracle: the old production algorithm actually decodes every
            // frame from the AVI origin, including the already presented prefix.
            reference.Events.MovieSkipRequested -= referenceClock.Skip;
            reference.Events.MovieSkipRequested += () => SkipDecoded(referenceSession, reference, referenceClock, information);
            Stopwatch decoded = Stopwatch.StartNew();
            Assert.Equal(result, reference.Apply(new PlayerAction(PlayerActionKind.SkipMovie)));
            decoded.Stop();
            output.WriteLine($"opening-skip start={startFrame} accelerated-ms={accelerated.Elapsed.TotalMilliseconds:F3} decoded-ms={decoded.Elapsed.TotalMilliseconds:F3} callbacks={result.FramesAdvanced}");
            Assert.Equal(referenceSession.State.MoviePlayback, session.State.MoviePlayback);
            referenceSession.SetEventState(reference.Events.Capture());
            session.SetEventState(world.Events.Capture());
            AssertSameState(referenceSession.PrepareSnapshot(), session.PrepareSnapshot());
            Assert.Equal(reference.Apply(new PlayerAction(PlayerActionKind.Wait)), world.Apply(new PlayerAction(PlayerActionKind.Wait)));
            for (int callback = 0; callback < 60; callback++)
            {
                reference.EnsureField();
                world.EnsureField();
                if (session.State.FieldId == 180 && !FieldPresentationReadiness.HasPendingInitialization(world.Events.Scene.NativeFieldInitialization) && world.CanPresentCurrentField)
                {
                    break;
                }
                Assert.Equal(reference.Apply(new PlayerAction(PlayerActionKind.Wait)), world.Apply(new PlayerAction(PlayerActionKind.Wait)));
            }
            Assert.Equal(180, session.State.FieldId);
            Assert.True(world.CanPresentCurrentField);
            referenceSession.SetEventState(reference.Events.Capture());
            session.SetEventState(world.Events.Capture());
            AssertSameState(referenceSession.PrepareSnapshot(), session.PrepareSnapshot());
            Assert.Equal(reference.Render(TimeSpan.Zero), world.Render(TimeSpan.Zero));

            string source = Path.Combine(temporary, "source.json");
            session.Save(source);
            GameState recreated = GameSession.Load(source);
            AssertSameState(session.PrepareSnapshot(), recreated);
        }
        finally
        {
            Directory.Delete(temporary, recursive: true);
        }
    }

    private static void ReachOpening(GameSession session, GameWorld world)
    {
        for (int callback = 0; !session.IsMoviePlaying && callback < 60; callback++)
        {
            world.EnsureField();
            Assert.True(world.Apply(new PlayerAction(PlayerActionKind.Wait)).Accepted);
        }
        Assert.True(session.IsMoviePlaying);
        Assert.Equal(74, session.State.FieldId);
        Assert.Equal(30, session.State.MovieIndex);
    }

    private static void SkipDecoded(GameSession session, GameWorld world, MovieClock clock, MovieInformation information)
    {
        int first = Math.Max(2, clock.State.FrameIndex + 1);
        int count = 0;
        bool active = true;
        session.BeginMovieAcceleration();
        using FfmpegMovieDecoder decoder = new(clock.ToolsDirectory, clock.SourcePath, information: information);
        Stopwatch timeout = Stopwatch.StartNew();
        while (active && !decoder.VideoFinished)
        {
            while (decoder.TryGetAudio(out _))
            {
            }
            if (!decoder.TryGetNextFrame(double.MaxValue, out MovieFrame? frame))
            {
                Assert.True(timeout.Elapsed < TimeSpan.FromSeconds(30), decoder.Error);
                Thread.Sleep(1);
                continue;
            }
            timeout.Restart();
            if (frame!.Index < first)
            {
                continue;
            }
            Assert.Equal(first + count, frame.Index);
            count++;
            active = world.AdvanceMovieFrame(new(checked((int)frame.Index), information.FrameCount - 1, true, false, false), frame.Index / information.FramesPerSecond);
        }
        Assert.Null(decoder.Error);
        if (active)
        {
            Assert.Equal(information.FrameCount - 1, first + count - 1);
            session.CompleteMovie(true);
        }
        session.EndMovieAcceleration(new(first, count, information.FrameCount - 1, information.FramesPerSecond));
    }

    private static void AssertSameState(GameState expected, GameState actual)
    {
        JsonElement left = JsonSerializer.SerializeToElement(expected);
        JsonElement right = JsonSerializer.SerializeToElement(actual);
        string[] differences = left.EnumerateObject().Where(property => !JsonElement.DeepEquals(property.Value, right.GetProperty(property.Name))).Select(property => property.Name).ToArray();
        Assert.True(differences.Length == 0, "Different state properties: " + string.Join(", ", differences));
    }
}
