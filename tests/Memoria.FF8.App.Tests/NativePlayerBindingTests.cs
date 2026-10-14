using System.Text.Json;
using System.Text.Json.Nodes;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativePlayerBindingTests
{
    [Fact]
    public void OriginalLibraryAisleVariantMovesItsActualCompactLeaderAndRestoresVmAndPixels()
    {
        string directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR") ?? LaunchOptions.DefaultGameDirectory;
        Assert.SkipUnless(Directory.Exists(directory), "Installed field corpus is required.");
        var session = new GameSession(GameSession.Load(FindSource()));
        using var world = new GameWorld(session, directory);
        world.EnsureField();
        Assert.Equal(170, world.Events.FieldId);
        Assert.Equal(117, world.Events.Frame);
        ActorState player = world.Events.Actors.Single(actor => actor.ObjectId == 1);
        Assert.Equal(1, player.ModelId);
        Assert.Equal(0, player.CharacterId);
        Assert.Equal(1, world.Events.Scene.NativeActorObjectIds[0]);
        Assert.Equal(new[] { 0, 255, 255 }, world.Events.Scene.FieldPartyNativeActorIds);
        Assert.Equal(0, world.Events.Scene.NativePlayerMovementVersion);
        Assert.True(NativePlayerMovementAdapter.Supports(world.Field!, world.Events.Scene, player));
        FieldFixedPosition before = world.Events.Scene.NativePlayerPosition!;
        Assert.True(world.Apply(new PlayerAction(PlayerActionKind.Move, Horizontal: 0.23607f, Vertical: 0.97174f)).Accepted);
        Assert.Equal(1, world.Events.Scene.NativePlayerMovementVersion);
        Assert.NotEqual(before, world.Events.Scene.NativePlayerPosition);
        Assert.Null(world.Events.LastFault);
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
        Assert.Equal(170, world.Events.FieldId);
        Assert.Null(world.Events.LastFault);
    }

    [Fact]
    public void AdmissionRejectsOriginalObjectZeroAndInvalidModelCharacterOrCanonicalBinding()
    {
        string directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR") ?? LaunchOptions.DefaultGameDirectory;
        Assert.SkipUnless(Directory.Exists(directory), "Installed field corpus is required.");
        using var world = new GameWorld(new GameSession(GameSession.Load(FindSource())), directory);
        world.EnsureField();
        ActorState player = world.Events.Actors.Single(actor => actor.ObjectId == 1);
        Assert.False(NativePlayerMovementAdapter.Supports(world.Field!, world.Events.Scene, ChangeActor(player, "ObjectId", 0)));
        Assert.Null(NativePlayerMovementAdapter.Resolve(world.Field!, world.Events.Scene, new NativePlayerMovementQuery(0, player.WalkmeshTriangle, 0, 0, 0, 0, 2696, 48)));
        foreach (int model in new[] { -1, 65535, world.Field!.Models.Count })
        {
            Assert.False(NativePlayerMovementAdapter.Supports(world.Field, world.Events.Scene, ChangeActor(player, "ModelId", model)));
        }
        Assert.False(NativePlayerMovementAdapter.Supports(world.Field!, world.Events.Scene, ChangeActor(player, "CharacterId", 5)));
        Assert.True(NativePlayerMovementAdapter.Supports(world.Field!, world.Events.Scene, ChangeActor(player, "PushRadius", 49)));
        Assert.False(NativePlayerMovementAdapter.Supports(world.Field!, world.Events.Scene, ChangeActor(player, "PushRadius", -1)));
        Assert.False(NativePlayerMovementAdapter.Supports(world.Field!, world.Events.Scene, ChangeActor(player, "PushRadius", 65536)));
        SceneEventState scene = JsonSerializer.Deserialize<SceneEventState>(JsonSerializer.Serialize(world.Events.Scene))!;
        scene.NativeActorObjectIds[0] = 0;
        Assert.False(NativePlayerMovementAdapter.Supports(world.Field!, scene, player));
        scene.NativeActorObjectIds[0] = 1;
        scene.FieldPartyNativeActorIds[0] = 1;
        Assert.False(NativePlayerMovementAdapter.Supports(world.Field!, scene, player));
    }

    private static ActorState ChangeActor(ActorState actor, string property, int value)
    {
        JsonNode input = JsonNode.Parse(JsonSerializer.Serialize(actor))!;
        input[property] = value;
        return input.Deserialize<ActorState>()!;
    }

    [Fact]
    public void OriginalLibraryPlayerUsesItsNonzeroCompactBindingAndRejectsMalformedCanonicalSlots()
    {
        string directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR") ?? LaunchOptions.DefaultGameDirectory;
        Assert.SkipUnless(Directory.Exists(directory), "Installed field corpus is required.");
        using var world = new GameWorld(new GameSession(GameSession.Load(FindSource("player-garden-library-scroll-boundary-preboundary-checkpoint.json"))), directory);
        world.EnsureField();
        Assert.Equal(145, world.Events.FieldId);
        Assert.Equal(14, world.Events.Frame);
        ActorState player = world.Events.Actors.Single(actor => actor.ObjectId == 2);
        Assert.Equal(0, player.CharacterId);
        Assert.Equal(1, player.ModelId);
        Assert.Equal(new[] { 1, 255, 255 }, world.Events.Scene.FieldPartyNativeActorIds);
        Assert.Equal(2, world.Events.Scene.NativeActorObjectIds[1]);
        Assert.True(NativePlayerMovementAdapter.Supports(world.Field!, world.Events.Scene, player));
        Assert.False(NativePlayerMovementAdapter.Supports(world.Field!, world.Events.Scene, ChangeActor(player, "ObjectId", 0)));
        foreach (int leader in new[] { -1, 255, world.Events.Scene.NativeActorObjectIds.Count })
        {
            SceneEventState scene = CloneScene(world.Events.Scene);
            scene.FieldPartyNativeActorIds[0] = leader;
            Assert.False(NativePlayerMovementAdapter.Supports(world.Field!, scene, player));
        }
        SceneEventState duplicateMapping = CloneScene(world.Events.Scene);
        duplicateMapping.NativeActorObjectIds[0] = duplicateMapping.NativeActorObjectIds[1];
        Assert.False(NativePlayerMovementAdapter.Supports(world.Field!, duplicateMapping, player));
        SceneEventState twoMembers = CloneScene(world.Events.Scene);
        twoMembers.Party[1] = 5;
        twoMembers.FieldPartyNativeActorIds[1] = 2;
        Assert.True(NativePlayerMovementAdapter.Supports(world.Field!, twoMembers, player));
        twoMembers.FieldPartyNativeActorIds[1] = 1;
        Assert.False(NativePlayerMovementAdapter.Supports(world.Field!, twoMembers, player));
        twoMembers.FieldPartyNativeActorIds[1] = 255;
        Assert.True(NativePlayerMovementAdapter.Supports(world.Field!, twoMembers, player));
        SceneEventState internalHole = CloneScene(world.Events.Scene);
        internalHole.FieldPartyNativeActorIds[2] = 2;
        Assert.True(NativePlayerMovementAdapter.Supports(world.Field!, internalHole, player));
    }

    private static SceneEventState CloneScene(SceneEventState scene)
    {
        return JsonSerializer.Deserialize<SceneEventState>(JsonSerializer.Serialize(scene))!;
    }

    private static string FindSource(string fileName = "player-garden-library-route-boundary-preboundary-checkpoint.json")
    {
        DirectoryInfo? repository = new(AppContext.BaseDirectory);
        while (repository is not null && !File.Exists(Path.Combine(repository.FullName, "docs", "recreate-checkpoint.md")))
        {
            repository = repository.Parent;
        }
        return Path.Combine(repository!.FullName, "../fixtures", fileName);
    }
}
