using System.Diagnostics;
using System.Text.Json;
using Memoria.FF8.Events;
using Memoria.FF8.Media;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class QuistisMoviePresentationTests
{
    [Theory]
    [InlineData("App.Tests.QuistisMoviePresentationTests.1", 320, 224, 1)]
    [InlineData("App.Tests.QuistisMoviePresentationTests.1", 640, 448, 3)]
    [InlineData("App.Tests.QuistisMoviePresentationTests.2", 320, 224, 1)]
    [InlineData("App.Tests.QuistisMoviePresentationTests.2", 640, 448, 3)]
    public void QuistisMovieUsesOnlyTheRendererOwnedByEachCallbackPhase(string checkpoint, int width, int height, int speed)
    {
        string game = TestResources.GameDirectory;
        Assert.SkipUnless(Directory.Exists(game), "Installed original movie and field corpus is required.");
        string source = TestResources.FixturePath(checkpoint);
        byte[] original = File.ReadAllBytes(source);
        GameSession session = new(GameSession.Load(source));
        MovieClock clock = new(session, game);
        using GameWorld world = new(session, game, clock);
        world.EnsureField();
        world.SetRenderSize(width, height);
        GameplayClock simulationClock = new();
        if (speed == 3)
        {
            simulationClock.ToggleSpeed();
        }
        if (!session.IsMoviePlaying)
        {
            Assert.Equal(FieldPresentationRenderer.Field, SelectRenderer(false));
            Assert.NotEmpty(world.Render(TimeSpan.Zero));
            TimeSpan elapsed = simulationClock.Advance(TimeSpan.FromSeconds(1d / EventEngine.SimulationFps), SceneKind.Field);
            Assert.True(world.Apply(new PlayerAction(PlayerActionKind.Wait, Frames: (int)Math.Round(elapsed.TotalSeconds * EventEngine.SimulationFps))).Accepted);
        }
        Assert.True(session.IsMoviePlaying);
        Assert.Equal(1, session.State.MovieIndex);
        Assert.Equal(1, world.Events.Scene.NativeMovieMask!.Playback!.AppliedRecord);
        JsonElement admitted = world.Events.Capture();
        for (int presentation = 0; presentation < 10; presentation++)
        {
            Assert.Equal(FieldPresentationRenderer.None, SelectRenderer(false));
        }
        Assert.True(JsonElement.DeepEquals(admitted, world.Events.Capture()));
        Assert.Equal(TimeSpan.FromSeconds(1), simulationClock.Advance(TimeSpan.FromSeconds(1), SceneKind.Movie));

        MovieInformation information = clock.Prepare();
        Assert.Equal(205, information.FrameCount);
        using FfmpegMovieDecoder decoder = new(clock.ToolsDirectory, clock.SourcePath, information: information);
        Stopwatch timeout = Stopwatch.StartNew();
        int decoded = 0;
        int rendered = 0;
        while (session.IsMoviePlaying && timeout.Elapsed < TimeSpan.FromSeconds(20))
        {
            while (decoder.TryGetAudio(out _))
            {
            }
            if (!decoder.TryGetNextFrame(double.MaxValue, out MovieFrame? frame))
            {
                Assert.Null(decoder.Error);
                Assert.False(decoder.VideoFinished, "The native movie ended before its CAM terminal.");
                Thread.Sleep(1);
                continue;
            }
            Assert.Equal(decoded++, frame!.Index);
            int record = checked((int)frame.Index);
            bool active = world.AdvanceMovieFrame(new(record, information.FrameCount - 1, true, false, false), record / information.FramesPerSecond);
            Assert.Equal(Math.Max(1, record), world.Events.Scene.NativeMovieMask!.Playback!.AppliedRecord);
            if (active && record is 30 or 75 or 150)
            {
                Assert.Equal(FieldPresentationRenderer.Movie, SelectRenderer(true));
                JsonElement beforeRender = world.Events.Capture();
                byte[] pixels = world.RenderMovieFrame(frame.Rgba, information.Width, information.Height).ToArray();
                Assert.Equal(width * height * 4, pixels.Length);
                Assert.True(Enumerable.Range(0, pixels.Length / 4).Count(pixel => pixels[pixel * 4] > 16 || pixels[pixel * 4 + 1] > 16 || pixels[pixel * 4 + 2] > 16) > 100);
                Assert.Equal(pixels, world.RenderMovieFrame(frame.Rgba, information.Width, information.Height));
                Assert.True(JsonElement.DeepEquals(beforeRender, world.Events.Capture()));
                rendered++;
            }
        }
        Assert.False(session.IsMoviePlaying);
        Assert.Equal(3, rendered);
        Assert.Null(decoder.Error);
        Assert.Null(world.Events.LastFault);
        Assert.Equal(FieldPresentationRenderer.None, SelectRenderer(true));
        for (int callback = 0; callback < 100; callback++)
        {
            world.EnsureField();
            if (session.State.FieldId == 180 && !FieldPresentationReadiness.HasPendingInitialization(world.Events.Scene.NativeFieldInitialization) && SelectRenderer(false) == FieldPresentationRenderer.Field)
            {
                break;
            }
            Assert.True(world.Apply(new PlayerAction(PlayerActionKind.Wait)).Accepted);
        }
        Assert.Equal(180, session.State.FieldId);
        Assert.Equal(FieldPresentationRenderer.Field, SelectRenderer(false));
        Assert.NotEmpty(world.Render(TimeSpan.Zero));
        Assert.Null(world.Events.LastFault);
        Assert.Equal(original, File.ReadAllBytes(source));

        FieldPresentationRenderer SelectRenderer(bool hasFrame) => FieldPresentationReadiness.SelectRenderer(session.State.Scene, world.CanPresentCurrentField, hasFrame);
    }
}
