using System.Text.Json;
using System.Text.Json.Serialization;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class FreshFieldAnimationTests
{
    [Fact]
    public void NaturalElevatorReturnInitializesFullIdleBeforeForcedNpcMovementAndRestoresEveryPhase()
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
        using JsonDocument epoch = JsonDocument.Parse(File.ReadAllText(Path.Combine(repository.FullName, "../fixtures/external-fixture.json")));
        JsonSerializerOptions options = new() { Converters = { new JsonStringEnumConverter() } };
        foreach (JsonElement observation in epoch.RootElement.GetProperty("Traces").GetProperty("ActionLog").EnumerateArray())
        {
            PlayerAction action = observation.GetProperty("Action").Deserialize<PlayerAction>(options)!;
            for (int tick = 0; tick < 1000 && action.Kind == PlayerActionKind.Move && !world.Events.IsUserControlEnabled; tick++)
            {
                Assert.True(world.Apply(new PlayerAction(PlayerActionKind.Wait)).Accepted);
                world.EnsureField();
            }
            PlayerActionResult result = world.Apply(action);
            Assert.True(result.Accepted, $"Recorded action{observation.GetProperty("Index")} {action} refused at{world.Field!.Id}/{world.Events.Frame}: {world.Events.NextTrigger}, control{world.Events.IsUserControlEnabled}, blocking{world.Events.IsDialogueBlocking}.");
            world.EnsureField();
            if (world.Field!.Id == 139 && world.Events.Scene.NativeFieldLoadVersion == 1)
            {
                break;
            }
        }
        Assert.Equal(139, world.Field!.Id);
        Assert.Equal(1, world.Events.Scene.NativeFieldLoadVersion);
        ActorState[] initialized = world.Events.Actors.Where(actor => world.Events.Scene.NativeActorObjectIds.Contains(actor.ObjectId) && actor.ModelId >= 0).ToArray();
        Assert.All(initialized, actor =>
        {
            Assert.True(actor.AnimationPlaying);
            Assert.True(actor.AnimationLoop);
            if (actor.CharacterId == 0)
            {
                // The recorded input resumes after initialization and already
                // starts the player's run; the NPCs still retain full idle ranges.
                Assert.Equal(actor.RunAnimationId, actor.AnimationId);
                Assert.Equal(world.Field!.GetAnimationFrameCount(actor.ModelId, actor.AnimationId) - 1, actor.AnimationLast);
                return;
            }
            Assert.Equal(actor.BaseAnimationId, actor.AnimationId);
            Assert.Equal(world.Field!.GetAnimationFrameCount(actor.ModelId, actor.AnimationId) - 1, actor.AnimationLast);
            Assert.Equal(0, actor.AnimationFrame);
        });
        Console.WriteLine($"Fresh139/frame{world.Events.Frame} retained idle ranges: {string.Join(';', initialized.Select(actor => $"object{actor.ObjectId}/model{actor.ModelId}/idle{actor.AnimationId}/frames{actor.AnimationLast + 1}"))}.");
        ActorState npc = world.Events.Actors.Single(actor => actor.ObjectId == 11);
        int frames = npc.AnimationLast + 1;
        for (int tick = 0; tick < 10 && npc.NativeScriptMovement is not { Active: true }; tick++)
        {
            Assert.True(world.Apply(new PlayerAction(PlayerActionKind.Wait)).Accepted);
        }
        Assert.True(npc.NativeScriptMovement is { Active: true, Asynchronous: true, AutomaticAnimation: false });
        Assert.Equal(npc.BaseAnimationId, npc.AnimationId);
        Assert.True(npc.AnimationPlaying);
        Console.WriteLine($"Natural139 RFMOVE preserves NPC11 idle{npc.AnimationId}/frames{frames}/phase{npc.AnimationFrame}; source chooses forced animation semantics.");
        using var restored = new GameWorld(new GameSession(session.State with { EventState = world.Events.Capture() }), directory);
        restored.EnsureField();
        for (int tick = 0; tick < 4; tick++)
        {
            float previousFrame = npc.AnimationFrame;
            float previousX = npc.X;
            float previousY = npc.Y;
            var next = new PlayerAction(PlayerActionKind.Wait);
            Assert.Equal(world.Apply(next), restored.Apply(next));
            Assert.True(JsonElement.DeepEquals(world.Events.Capture(), restored.Events.Capture()));
            Assert.Equal(world.Render(TimeSpan.Zero), restored.Render(TimeSpan.Zero));
            Assert.True(npc.AnimationPlaying);
            Assert.True(npc.X != previousX || npc.Y != previousY);
            if (frames > 1)
            {
                Assert.NotEqual(previousFrame, npc.AnimationFrame);
            }
        }
    }
}
