using System.Security.Cryptography;
using System.Text.Json;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class InstalledRetainedPlayerMovementTests
{
    [Fact]
    public void World_return_keeps_the_retained_player_movable_after_the_default_script_executes_unuse()
    {
        string source = TestResources.FixturePath("App.Tests.InstalledRetainedPlayerMovementTests.1");
        byte[] sourceHash = SHA256.HashData(File.ReadAllBytes(source));
        string directory = TestResources.GameDirectory;
        GameSession session = new(PlayerStateLoader.Load(source, directory));
        using GameWorld world = new(session, directory);
        world.EnsureField();
        Assert.Equal(SceneKind.WorldMap, session.State.Scene);
        world.Apply(new(PlayerActionKind.Wait, Frames: 24));
        PlayerActionResult entry = world.Apply(new(PlayerActionKind.Move, Horizontal: -1, Vertical: 1, Frames: 18));
        Assert.Equal(PlayerTriggerKind.FieldTransition, entry.Trigger.Kind);
        world.EnsureField();
        Assert.Equal(547, world.Events.FieldId);
        for (int index = 0; index < 120; index++)
        {
            world.Apply(new(PlayerActionKind.Wait));
        }
        ActorState player = world.Events.PlayerActor!;
        Assert.False(player.Active);
        Assert.True(player.Visible);
        Assert.True(world.Events.IsUserControlEnabled);
        Assert.True(world.Events.CanAcceptMovementInput);
        Assert.Equal(PlayerTriggerKind.Playable, world.NextTrigger.Kind);
        Assert.Null(world.Events.CurrentDialogue);
        Assert.Null(world.Events.LastFault);

        string saved = Path.Combine(TestResources.ArtifactDirectory, "retained-player.json");
        session.SetEventState(world.Events.Capture());
        session.Save(saved, compact: true);
        using GameWorld restored = new(new GameSession(GameSession.Load(saved)), directory);
        restored.EnsureField();
        Assert.True(JsonElement.DeepEquals(world.Events.Capture(), restored.Events.Capture()));
        Assert.Equal(world.Render(TimeSpan.Zero), restored.Render(TimeSpan.Zero));
        int binding = restored.Events.Scene.FieldPartyNativeActorIds[0];
        restored.Events.Scene.FieldPartyNativeActorIds[0] = 255;
        Assert.False(restored.Events.CanAcceptMovementInput);
        restored.Events.Scene.FieldPartyNativeActorIds[0] = binding;
        float beforeX = player.X;
        float beforeY = player.Y;
        PlayerAction move = new(PlayerActionKind.Move, Horizontal: 1, Frames: 10);
        PlayerActionResult moved = world.Apply(move);
        Assert.True(moved.Accepted);
        Assert.Equal(10, moved.FramesAdvanced);
        Assert.True(player.X != beforeX || player.Y != beforeY);
        Assert.False(player.Active);
        Assert.Equal(moved, restored.Apply(move));
        Assert.True(JsonElement.DeepEquals(world.Events.Capture(), restored.Events.Capture()));
        Assert.Equal(world.Render(TimeSpan.Zero), restored.Render(TimeSpan.Zero));
        Assert.Null(world.Events.LastFault);
        Assert.Equal(sourceHash, SHA256.HashData(File.ReadAllBytes(source)));

        string? output = Environment.GetEnvironmentVariable("MEMORIA_FF8_RETAINED_PLAYER_SAVE_OUTPUT");
        if (output is not null)
        {
            session.SetEventState(world.Events.Capture());
            session.Save(output, compact: true);
        }
    }
}
