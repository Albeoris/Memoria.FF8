using System.Text.Json;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class DrawPointSetupContinuationTests
{
    [Fact]
    public void ActualLibraryDrawPointSetupPreservesItsNativeResetAndRestoresFullVmAndPixels()
    {
        string directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR") ?? LaunchOptions.DefaultGameDirectory;
        Assert.SkipUnless(Directory.Exists(directory), "Installed field corpus is required.");
        var session = new GameSession(GameSession.Load(FindSource()));
        using var world = new GameWorld(session, directory);
        world.EnsureField();
        Assert.Equal(148, world.Events.FieldId);
        Assert.Equal(0, world.Events.Frame);
        Assert.Null(world.Events.Scene.DrawPointIndex);
        Assert.True(world.Apply(new PlayerAction(PlayerActionKind.Wait)).Accepted);
        Assert.Null(world.Events.LastFault);
        Assert.Equal(1, world.Events.Scene.DrawPointEnabled);
        Assert.Equal(1, world.Events.Scene.DrawPointDisplayParameter);
        NativeDrawPointReset reset = Assert.IsType<NativeDrawPointReset>(world.Events.Scene.DrawPointReset);
        Assert.Equal(1, reset.Version);
        Assert.Equal(148, reset.FieldId);
        Assert.Equal(17, reset.ObjectId);
        Assert.Equal(105, reset.ScriptId);
        Assert.Equal(1961, reset.Operation);
        Assert.Equal(new FieldFixedPosition(-6119424, 2744320, 0), reset.RawPosition);
        Assert.Equal(1, reset.SampledDisplayMask);
        Assert.Equal(8, reset.Sparks.Count);
        Assert.All(reset.Sparks, spark => Assert.Equal(new NativeDrawPointSparkAnchor(-1494, 670, 0, 1), spark));
        using var restored = new GameWorld(new GameSession(session.State with { EventState = world.Events.Capture() }), directory);
        restored.EnsureField();
        Assert.True(JsonElement.DeepEquals(world.Events.Capture(), restored.Events.Capture()));
        Assert.Equal(world.Render(TimeSpan.Zero), restored.Render(TimeSpan.Zero));
        for (int tick = 0; tick < 4; tick++)
        {
            var wait = new PlayerAction(PlayerActionKind.Wait);
            Assert.Equal(world.Apply(wait), restored.Apply(wait));
            Assert.True(JsonElement.DeepEquals(world.Events.Capture(), restored.Events.Capture()));
            Assert.Equal(world.Render(TimeSpan.Zero), restored.Render(TimeSpan.Zero));
        }
        Assert.Equal((byte?)3, world.Events.Scene.DrawPointIndex);
        Assert.Equal(148, world.Events.FieldId);
        Assert.Null(world.Events.LastFault);
    }

    private static string FindSource()
    {
        DirectoryInfo? repository = new(AppContext.BaseDirectory);
        while (repository is not null && !File.Exists(Path.Combine(repository.FullName, "docs", "recreate-checkpoint.md")))
        {
            repository = repository.Parent;
        }
        return Path.Combine(repository!.FullName, "../fixtures/external-fixture.json");
    }
}
