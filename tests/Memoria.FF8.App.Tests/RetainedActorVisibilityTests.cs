using System.Text.Json;
using System.Text.Json.Nodes;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class RetainedActorVisibilityTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void Native_allocation_survives_activity_changes(bool active, bool retained)
    {
        SceneEventState scene = Scene(1);
        if (retained)
        {
            scene.NativeActorObjectIds.Add(4);
        }
        ActorState actor = Actor(active);
        Assert.Equal(retained, GameWorld.IsFieldActorAllocated(scene, actor));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Unallocated_script_scenes_keep_their_activity_contract(bool active)
    {
        Assert.Equal(active, GameWorld.IsFieldActorAllocated(Scene(0), Actor(active)));
    }

    [Fact]
    public void Issued_dialogue_restores_retained_inactive_speaker_without_changing_party()
    {
        string source = TestResources.FixturePath("App.Tests.RetainedActorVisibility.1");
        string directory = TestResources.GameDirectory;
        GameSession session = new(PlayerStateLoader.Load(source, directory));
        using GameWorld world = new(session, directory);
        world.EnsureField();
        ActorState speaker = Assert.Single(world.Events.Actors, actor => !actor.Active && actor.Visible && actor.CharacterId == 2 && world.Events.Scene.NativeActorObjectIds.Contains(actor.ObjectId));
        Assert.DoesNotContain(2, world.Events.Scene.Party);
        string before = world.Events.Capture().GetRawText();
        Assert.True(world.CreateVisual(speaker).Visible);
        Assert.Equal(before, world.Events.Capture().GetRawText());
        Assert.DoesNotContain(2, world.Events.Scene.Party);

        string path = Path.Combine(Path.GetTempPath(), "Memoria.FF8-retained-speaker-" + Guid.NewGuid().ToString("N"));
        try
        {
            world.WriteGameplaySave(path);
            using GameWorld resumed = new(new GameSession(PlayerStateLoader.Load(path, directory)), directory);
            resumed.EnsureField();
            Assert.False(resumed.Events.Actors[speaker.ObjectId].Active);
            Assert.True(resumed.CreateVisual(resumed.Events.Actors[speaker.ObjectId]).Visible);
            Assert.Equal(world.Events.Scene.Party, resumed.Events.Scene.Party);
            for (int callback = 0; callback < 1800 && (!resumed.Events.IsUserControlEnabled || !resumed.Events.Scene.Party.Contains(2) || resumed.Events.IsDialogueOpen); callback++)
            {
                Assert.True(resumed.CreateVisual(resumed.Events.Actors[speaker.ObjectId]).Visible);
                PlayerAction action = new(resumed.Events.CanConfirmDialogue ? PlayerActionKind.Confirm : PlayerActionKind.Wait);
                Assert.True(resumed.Apply(action).Accepted);
                Assert.Null(resumed.Events.LastFault);
            }
            Assert.True(resumed.Events.IsUserControlEnabled);
            Assert.False(resumed.Events.IsDialogueOpen);
            Assert.Contains(2, resumed.Events.Scene.Party);
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static ActorState Actor(bool active)
    {
        JsonNode node = JsonSerializer.SerializeToNode(new ActorState(4, 70))!;
        node["Active"] = active;
        return node.Deserialize<ActorState>()!;
    }

    private static SceneEventState Scene(int layout)
    {
        JsonNode node = JsonSerializer.SerializeToNode(new SceneEventState())!;
        node["NativeActorLayoutVersion"] = layout;
        return node.Deserialize<SceneEventState>()!;
    }
}
