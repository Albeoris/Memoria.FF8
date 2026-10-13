using System.Text.Json;
using System.Text.Json.Serialization;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class ExamTimerContinuationTests
{
    private static readonly JsonSerializerOptions JsonOptions = new() { Converters = { new JsonStringEnumConverter() } };

    [Fact]
    public void Actual_guard_time_choice_replays_timer_state_and_independently_restores_the_join_boundary()
    {
        string directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR") ?? LaunchOptions.DefaultGameDirectory;
        Assert.SkipUnless(Directory.Exists(directory), "Installed field corpus is required.");
        DirectoryInfo? repository = new(AppContext.BaseDirectory);
        while (repository is not null && !File.Exists(Path.Combine(repository.FullName, "docs", "recreate-checkpoint.md")))
        {
            repository = repository.Parent;
        }
        string root = repository!.FullName;
        string proofPath = Path.Combine(root, "../fixtures/external-fixture.json");
        using JsonDocument proof = JsonDocument.Parse(File.ReadAllText(proofPath));
        JsonElement replayProof = proof.RootElement.GetProperty("examChoiceReplay");
        string sourcePath = Path.Combine(root, replayProof.GetProperty("sourceCheckpoint").GetString()!);
        string destinationPath = Path.Combine(root, replayProof.GetProperty("destinationCheckpoint").GetString()!);
        PlayerAction[] actions = replayProof.GetProperty("acceptedActions").Deserialize<PlayerAction[]>(JsonOptions)!;
        Assert.Equal(259, actions.Length);
        Assert.Equal(new PlayerAction(PlayerActionKind.Choose, Value: 3), actions[0]);
        Assert.Equal(new PlayerAction(PlayerActionKind.Confirm), actions[3]);
        Assert.Equal(257, actions.Count(action => action.Kind == PlayerActionKind.Wait));
        Assert.All(actions.Where(action => action.Kind == PlayerActionKind.Wait), action => Assert.Equal(1, action.Frames));

        var session = new GameSession(GameSession.Load(sourcePath));
        using var world = new GameWorld(session, directory);
        world.EnsureField();
        Assert.Equal((129, 217L), (world.Events.FieldId, world.Events.Frame));
        Assert.Equal(217, replayProof.GetProperty("initialFrame").GetInt64());
        Assert.Null(session.State.ExamTimer);
        Assert.Equal(9, world.Events.CurrentDialogue!.MessageId);
        Assert.Equal((0, 3), (world.Events.CurrentDialogue.FirstChoice, world.Events.CurrentDialogue.LastChoice));
        foreach (PlayerAction action in actions)
        {
            Assert.True(world.Apply(action).Accepted);
            Assert.Null(world.Events.LastFault);
        }

        Assert.Equal((129, 474L), (world.Events.FieldId, world.Events.Frame));
        Assert.Equal(474, replayProof.GetProperty("finalFrame").GetInt64());
        JsonElement completedVm = world.Events.Capture();
        session.SetEventState(completedVm);
        GameState expected = GameSession.Load(destinationPath);
        Assert.True(JsonElement.DeepEquals(expected.EventState!.Value, completedVm));
        Assert.True(JsonElement.DeepEquals(JsonSerializer.SerializeToElement(expected, JsonOptions), JsonSerializer.SerializeToElement(session.State, JsonOptions)));
        NativeExamTimerState timer = Assert.IsType<NativeExamTimerState>(session.State.ExamTimer);
        Assert.Equal(new NativeTimerAlternative(0, uint.MaxValue, 2400), Assert.Single(timer.Alternatives));
        Assert.Equal(0, timer.ClockCalls);
        Assert.Equal((true, (ushort)250, (byte)10), (timer.DisplayEnabled, timer.DisplayX, timer.DisplayY));
        Assert.True(timer.Event104Mask40);
        Assert.True(timer.Event182Mask4);
        Assert.True(timer.SaveGlobalMask4);
        Assert.Null(timer.Event183Mask1);

        GameState copied = JsonSerializer.Deserialize<GameState>(JsonSerializer.Serialize(session.State, JsonOptions), JsonOptions)!;
        Assert.NotSame(session.State.Variables, copied.Variables);
        Assert.NotSame(timer.Alternatives, copied.ExamTimer!.Alternatives);
        var replaySession = new GameSession(copied);
        using var replay = new GameWorld(replaySession, directory);
        replay.EnsureField();
        Assert.True(JsonElement.DeepEquals(completedVm, replay.Events.Capture()));
        Assert.True(JsonElement.DeepEquals(JsonSerializer.SerializeToElement(session.State, JsonOptions), JsonSerializer.SerializeToElement(replaySession.State, JsonOptions)));
        world.SetRenderSize(320, 240);
        replay.SetRenderSize(320, 240);
        Assert.Equal(world.Render(TimeSpan.Zero), replay.Render(TimeSpan.Zero));

        PlayerAction next = new(PlayerActionKind.Wait);
        EventScriptException originalFailure = Assert.Throws<EventScriptException>(() => world.Apply(next));
        EventScriptException restoredFailure = Assert.Throws<EventScriptException>(() => replay.Apply(next));
        Assert.Equal(originalFailure.Message, restoredFailure.Message);
        EventFault fault = Assert.IsType<EventFault>(world.Events.LastFault);
        Assert.Equal((129, 0, 6, 33, "JOIN"), (fault.FieldId, fault.ObjectId, fault.ScriptId, fault.Operation, fault.Opcode));
        Assert.Equal(475, world.Events.Frame);
        Assert.Equal(fault, replay.Events.LastFault);
        NativeExamTimerState advanced = Assert.IsType<NativeExamTimerState>(session.State.ExamTimer);
        Assert.Equal(2, advanced.ClockCalls);
        Assert.Equal(new[] { 2399, 2400 }, advanced.Alternatives.Select(item => item.Remaining).Distinct().Order().ToArray());
        JsonElement faultedVm = world.Events.Capture();
        Assert.True(JsonElement.DeepEquals(faultedVm, replay.Events.Capture()));
        session.SetEventState(faultedVm);
        replaySession.SetEventState(replay.Events.Capture());
        Assert.True(JsonElement.DeepEquals(JsonSerializer.SerializeToElement(session.State, JsonOptions), JsonSerializer.SerializeToElement(replaySession.State, JsonOptions)));
        Assert.Equal(world.Render(TimeSpan.Zero), replay.Render(TimeSpan.Zero));
    }
}
