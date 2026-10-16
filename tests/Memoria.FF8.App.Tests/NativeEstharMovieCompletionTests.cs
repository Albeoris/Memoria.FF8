using System.Text.Json;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeEstharMovieCompletionTests
{
    [Fact]
    public void ActualSkippedMovieResumesMovieSyncOnTheNextOrdinaryCallback()
    {
        string root = AppContext.BaseDirectory;
        while (!File.Exists(Path.Combine(root, "AGENTS.md")))
        {
            root = Directory.GetParent(root)!.FullName;
        }
        GameSession session = new(GameSession.Load(Path.Combine(root, "../fixtures/external-fixture.json")));
        using GameWorld world = new(session, TestResources.GameDirectory);
        world.EnsureField();
        Assert.False(session.IsMoviePlaying);
        Assert.True(world.Events.CurrentMoviePlayback!.IsPlaying);
        long before = world.Events.Frame;
        PlayerActionResult result = world.Apply(new(PlayerActionKind.Wait));
        Assert.True(result.Accepted);
        Assert.Equal(before + 1, world.Events.Frame);
        Assert.Equal(session.CurrentMoviePlayback, world.Events.CurrentMoviePlayback);
        JsonElement capture = world.Events.Capture();
        Assert.False(capture.GetProperty("MovieProgressExpected").GetBoolean());
        JsonElement controller = capture.GetProperty("Fibers").EnumerateArray().Single(fiber => fiber.GetProperty("ObjectId").GetInt32() == 9 && fiber.GetProperty("ScriptId").GetInt32() == 97);
        Assert.NotEqual("Movie", controller.GetProperty("WaitReason").GetString());
        Assert.NotEqual(1540, controller.GetProperty("Position").GetInt32());
        session.SetEventState(capture);
        GameState saved = JsonSerializer.Deserialize<GameState>(JsonSerializer.Serialize(session.State))!;
        GameSession coldSession = new(saved);
        using GameWorld cold = new(coldSession, TestResources.GameDirectory);
        cold.EnsureField();
        Assert.True(JsonElement.DeepEquals(capture, cold.Events.Capture()));
        Assert.True(world.Apply(new(PlayerActionKind.Wait)).Accepted);
        Assert.True(cold.Apply(new(PlayerActionKind.Wait)).Accepted);
        Assert.True(JsonElement.DeepEquals(world.Events.Capture(), cold.Events.Capture()));
    }
}
