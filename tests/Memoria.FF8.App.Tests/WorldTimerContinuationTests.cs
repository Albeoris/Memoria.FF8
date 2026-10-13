using System.Text.Json;
using System.Text.Json.Nodes;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class WorldTimerContinuationTests
{
    [Fact]
    public void Actual_after_Ifrit_world_timer_recreates_loads_and_continues_without_restarting_the_frozen_field_clock()
    {
        string directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR") ?? LaunchOptions.DefaultGameDirectory;
        Assert.SkipUnless(Directory.Exists(directory), "Installed original field/world corpus is required.");
        DirectoryInfo? repository = new(AppContext.BaseDirectory);
        while (repository is not null && !File.Exists(Path.Combine(repository.FullName, "docs", "recreate-checkpoint.md")))
        {
            repository = repository.Parent;
        }
        string sourcePath = Path.Combine(repository!.FullName, "../fixtures/external-fixture.json");
        GameState recreated = GameWorld.RecreateSave(GameSession.LoadReplaySource(sourcePath), directory);
        Assert.Equal(SceneKind.WorldMap, recreated.Scene);
        Assert.Equal(136, recreated.FieldId);
        Assert.Equal(0, recreated.World!.Frame);
        Assert.False(recreated.ExamTimer!.DisplayEnabled);
        var session = new GameSession(recreated);
        using var world = new GameWorld(session, directory, persistReplayJournal: false);
        world.EnsureField();
        JsonElement initialVm = world.Events.Capture();
        NativeExamTimerState initialTimer = session.ExamTimer!;
        Assert.Equal(world.Events.Frame, session.State.EventState!.Value.GetProperty("Frame").GetInt64());
        Assert.Equal(initialTimer.ClockCalls, session.State.EventState.Value.GetProperty("ExamTimer").GetProperty("ClockCalls").GetInt64());
        Assert.Equal(3, world.Apply(new PlayerAction(PlayerActionKind.Wait, Frames: 3)).FramesAdvanced);
        Assert.Equal(initialTimer.ClockCalls + 6, session.ExamTimer!.ClockCalls);
        Assert.Equal(JsonSerializer.Serialize(initialTimer.AdvanceCallbacks(NativeTimerCallbackKind.World, 3)), JsonSerializer.Serialize(session.ExamTimer));
        Assert.True(JsonElement.DeepEquals(FrozenVm(initialVm), FrozenVm(world.Events.Capture())));
        Assert.Equal(initialTimer.ClockCalls, session.State.EventState!.Value.GetProperty("ExamTimer").GetProperty("ClockCalls").GetInt64());
        GameState continued = GameWorld.RecreateSave(session.State, directory);
        Assert.True(JsonElement.DeepEquals(JsonSerializer.SerializeToElement(session.State with { ReplayJournal = null }), JsonSerializer.SerializeToElement(continued with { ReplayJournal = null })));

        string path = Path.Combine(Path.GetTempPath(), "Memoria.FF8-world-timer-" + Guid.NewGuid().ToString("N") + ".json");
        try
        {
            session.Save(path, registerJournalReference: false);
            var restoredSession = new GameSession(GameSession.Load(path));
            using var restored = new GameWorld(restoredSession, directory, persistReplayJournal: false);
            restored.EnsureField();
            Assert.Equal(JsonSerializer.Serialize(session.ExamTimer), JsonSerializer.Serialize(restoredSession.ExamTimer));
            var next = new PlayerAction(PlayerActionKind.Wait);
            Assert.Equal(world.Apply(next), restored.Apply(next));
            Assert.True(JsonElement.DeepEquals(world.Events.Capture(), restored.Events.Capture()));
            Assert.Equal(JsonSerializer.Serialize(session.State.World), JsonSerializer.Serialize(restoredSession.State.World));
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static JsonElement FrozenVm(JsonElement snapshot)
    {
        JsonObject result = JsonNode.Parse(snapshot.GetRawText())!.AsObject();
        result.Remove("ExamTimer");
        return JsonSerializer.SerializeToElement(result);
    }
}
