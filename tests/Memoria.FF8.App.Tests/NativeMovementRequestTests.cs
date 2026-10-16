using System.Text.Json;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeMovementRequestTests
{
    [Fact]
    public void AuthenticWithdrawalRequestsMoveAboveHeldMoveActorAndReleasePlayerControl()
    {
        string directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR") ?? LaunchOptions.DefaultGameDirectory;
        Assert.SkipUnless(Directory.Exists(directory), "Installed field corpus is required.");
        DirectoryInfo? repository = new(TestResources.FixtureDirectory);
        GameState recreated = SnapshotFixtures.Require("MEMORIA_FF8_MOVEMENT_REQUEST_SNAPSHOT", 327, 36);
        var session = new GameSession(recreated);
        using var world = new GameWorld(session, directory);
        world.EnsureField();
        Assert.Equal(36, world.Events.Frame);
        Assert.False(world.Events.IsUserControlEnabled);
        NativeMovementRequestState request = world.Events.Actors[2].NativeMovementRequest!;
        Assert.NotNull(request);
        Assert.Equal(1, request.SavedMovement.TargetNativeObjectId);
        JsonElement lower = world.Events.Capture().GetProperty("Fibers").EnumerateArray().Single(fiber => fiber.GetProperty("Sequence").GetInt64() == request.SavedMovement.FiberSequence);
        using var restored = new GameWorld(new GameSession(session.State with { EventMemory = session.State.EventMemory, EventState = world.Events.Capture() }), directory);
        restored.EnsureField();
        List<int> moves = [];
        world.Events.CommandExecuted += command =>
        {
            if (command.ObjectId == 2 && command.ScriptId == 22 && command.Name == "MOVE" && !moves.Contains(command.Operation))
            {
                moves.Add(command.Operation);
                JsonElement suspended = world.Events.Capture().GetProperty("Fibers").EnumerateArray().Single(fiber => fiber.GetProperty("Sequence").GetInt64() == request.SavedMovement.FiberSequence);
                Assert.True(JsonElement.DeepEquals(lower, suspended));
                Assert.Equal(request, world.Events.Actors[2].NativeMovementRequest);
                Assert.Equal(22, world.Events.Actors[2].NativeScriptMovement!.ScriptId);
            }
        };
        for (int tick = 0; tick < 400 && world.Events.NextTrigger.Kind != PlayerTriggerKind.Playable; tick++)
        {
            var wait = new PlayerAction(world.Events.CurrentDialogue is { CanConfirm: true } ? PlayerActionKind.Confirm : PlayerActionKind.Wait);
            Assert.Equal(world.Apply(wait), restored.Apply(wait));
            JsonElement liveState = world.Events.Capture();
            JsonElement restoredState = restored.Events.Capture();
            string changed = string.Join(",", liveState.EnumerateObject().Where(property => !JsonElement.DeepEquals(property.Value, restoredState.GetProperty(property.Name))).Select(property => property.Name));
            if (!JsonElement.DeepEquals(liveState, restoredState))
            {
                throw new InvalidOperationException($"Native withdrawal restore diverged at frame{world.Events.Frame}: {changed}; {FirstDifference(liveState, restoredState)}.");
            }
        }
        Assert.Equal(new[] { 324, 330 }, moves);
        Assert.Equal(327, session.State.FieldId);
        Assert.Equal(125, session.State.Variables[256]);
        Assert.True(world.Events.IsUserControlEnabled);
        Assert.Null(world.Events.LastFault);
        NativeMovementRequestState following = world.Events.Actors[2].NativeMovementRequest!;
        Assert.Equal(23, following.RequestScriptId);
        Assert.Equal(4, following.RequestSlot);
        Assert.Equal(request.SavedMovement, following.SavedMovement);
        Assert.Equal(23, world.Events.Actors[2].NativeScriptMovement!.ScriptId);
        JsonElement held = world.Events.Capture().GetProperty("Fibers").EnumerateArray().Single(fiber => fiber.GetProperty("ObjectId").GetInt32() == 2 && fiber.GetProperty("ScriptId").GetInt32() == 22);
        Assert.Equal("NativeHalt", held.GetProperty("WaitReason").GetString());
        Assert.Equal(334, held.GetProperty("Position").GetInt32());
        Assert.False(held.GetProperty("Complete").GetBoolean());
        Assert.Equal(world.Render(TimeSpan.Zero), restored.Render(TimeSpan.Zero));
        var next = new PlayerAction(PlayerActionKind.Wait);
        Assert.Equal(world.Apply(next), restored.Apply(next));
        Assert.True(JsonElement.DeepEquals(world.Events.Capture(), restored.Events.Capture()));
    }

    private static string FirstDifference(JsonElement left, JsonElement right, string path = "state")
    {
        if (left.ValueKind == JsonValueKind.Object && right.ValueKind == JsonValueKind.Object)
        {
            foreach (JsonProperty property in left.EnumerateObject())
            {
                if (!JsonElement.DeepEquals(property.Value, right.GetProperty(property.Name)))
                {
                    return FirstDifference(property.Value, right.GetProperty(property.Name), $"{path}.{property.Name}");
                }
            }
        }
        if (left.ValueKind == JsonValueKind.Array && right.ValueKind == JsonValueKind.Array && left.GetArrayLength() == right.GetArrayLength())
        {
            for (int index = 0; index < left.GetArrayLength(); index++)
            {
                if (!JsonElement.DeepEquals(left[index], right[index]))
                {
                    return FirstDifference(left[index], right[index], $"{path}[{index}]");
                }
            }
        }
        return $"{path}: {left} != {right}";
    }
}
