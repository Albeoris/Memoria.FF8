using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class CheckpointTests
{
    [Fact]
    public void Opening_movie_checkpoint_preserves_native_script_continuation_and_movie_seek_time()
    {
        string directory = TestResources.GameDirectory;
        Assert.SkipUnless(Directory.Exists(directory), "Installed field corpus is required.");
        string checkpoint = Path.Combine(Path.GetTempPath(), "Memoria.FF8-movie-" + Guid.NewGuid().ToString("N") + ".json");
        try
        {
            using var assets = new GameAssets(directory);
            var original = GameSession.CreateColdProcess();
            original.StartNewGame();
            var clock = new MovieClock(original, directory);
            using var world = new GameWorld(original, directory, clock, sharedAssets: assets);
            world.EnsureLoaded();
            EventEngine engine = world.Events;
            for (int frame = 0; frame < 200 && !original.IsMoviePlaying; frame++)
            {
                engine.Update(TimeSpan.FromSeconds(1d / 30));
            }
            Assert.True(original.IsMoviePlaying);
            Assert.Equal(30, original.State.MovieIndex);
            original.SetMoviePosition(8.25);
            var information = clock.Prepare();
            original.SetMoviePlayback(new MoviePlaybackState(MovieClock.AbsoluteFrame(8.25, information), information.FrameCount - 1, true, false, false));
            engine.MoviePlayback = () => original.State.MoviePlayback;
            Assert.Equal(engine.Capture().GetRawText(), original.PrepareSnapshot().EventState!.Value.GetRawText());
            original.Save(checkpoint);
            var resumed = new GameSession(GameSession.Load(checkpoint));
            using var restoredWorld = new GameWorld(resumed, directory, new MovieClock(resumed, directory), sharedAssets: assets);
            restoredWorld.EnsureLoaded();
            EventEngine restoredEngine = restoredWorld.Events;
            Assert.Equal(8.25, resumed.State.MovieSeconds);
            Assert.Equal(engine.Capture().GetRawText(), restoredEngine.Capture().GetRawText());
            long suspendedFrame = restoredEngine.Frame;
            string waitingFibers = restoredEngine.Capture().GetProperty("Fibers").GetRawText();
            restoredEngine.Update(TimeSpan.FromSeconds(1));
            engine.Update(TimeSpan.FromSeconds(1));
            Assert.Equal(suspendedFrame + 30, restoredEngine.Frame);
            Assert.Equal(waitingFibers, restoredEngine.Capture().GetProperty("Fibers").GetRawText());
            Assert.Equal(original.State.MoviePlayback, resumed.State.MoviePlayback);
            original.CompleteMovie();
            resumed.CompleteMovie();
            for (int frame = 0; frame < 200 && resumed.State.FieldId == 74; frame++)
            {
                engine.Update(TimeSpan.FromSeconds(1d / 30));
                restoredEngine.Update(TimeSpan.FromSeconds(1d / 30));
            }
            Assert.Equal(180, original.State.FieldId);
            Assert.Equal(180, resumed.State.FieldId);
            Assert.Equal(JsonSerializer.Serialize(original.State.Variables), JsonSerializer.Serialize(resumed.State.Variables));
        }
        finally
        {
            File.Delete(checkpoint);
        }
    }
}
