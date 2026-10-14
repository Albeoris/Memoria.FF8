using System.Text.Json;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class SoundInitializationContinuationTests
{
    [Fact]
    public void ActualQuistisSoundStartupRestoresFullVmAndPixels()
    {
        string directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR") ?? LaunchOptions.DefaultGameDirectory;
        Assert.SkipUnless(Directory.Exists(directory), "Installed field corpus is required.");
        DirectoryInfo? repository = new(AppContext.BaseDirectory);
        while (repository is not null && !File.Exists(Path.Combine(repository.FullName, "docs", "recreate-checkpoint.md")))
        {
            repository = repository.Parent;
        }
        string source = Path.Combine(repository!.FullName, "../fixtures/external-fixture.json");
        var session = new GameSession(GameSession.Load(source));
        using var world = new GameWorld(session, directory);
        world.EnsureField();
        Assert.Equal(159, world.Events.FieldId);
        Assert.Equal(5, world.Events.Frame);
        int? music = world.Events.Scene.LoadedMusic;
        var wait = new PlayerAction(PlayerActionKind.Wait);
        Assert.True(world.Apply(wait).Accepted);
        Assert.Null(world.Events.LastFault);
        Assert.NotNull(world.Events.Scene.SoundInitialization);
        Assert.Equal((byte?)127, world.Events.Scene.SoundMusicVolume197);
        Assert.Equal((byte?)127, world.Events.Scene.SoundMusicVolume198);
        Assert.Equal(music, world.Events.Scene.LoadedMusic);
        Assert.True(world.Events.Scene.MusicPlaying);
        Assert.NotNull(world.Events.FieldFade);
        Assert.Equal((short)0, world.Events.FieldFade.Physical.Mode);
        using var replay = new GameWorld(new GameSession(session.State with { Variables = new Dictionary<int, int>(session.State.Variables), EventState = world.Events.Capture() }), directory);
        replay.EnsureField();
        Assert.True(JsonElement.DeepEquals(world.Events.Capture(), replay.Events.Capture()));
        Assert.Equal(world.Render(TimeSpan.Zero), replay.Render(TimeSpan.Zero));
    }
}
