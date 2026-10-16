using Memoria.FF8.Events;
using Memoria.FF8.Rendering;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class ActorColorTests
{
    [Fact]
    public void ReachedSelphieShadowLevelDoesNotDarkenHerAvatar()
    {
        string directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR") ?? LaunchOptions.DefaultGameDirectory;
        Assert.SkipUnless(Directory.Exists(directory), "Installed field corpus is required.");
        DirectoryInfo? repository = new(TestResources.FixtureDirectory);
        string source = Path.Combine(repository!.FullName, "../fixtures/external-fixture.json");
        var session = new GameSession(GameSession.Load(source));
        using var world = new GameWorld(session, directory);
        world.EnsureField();
        int followerId = world.Events.Scene.NativeActorObjectIds[world.Events.Scene.FieldPartyNativeActorIds[1]];
        ActorState selphie = Assert.Single(world.Events.Actors, actor => actor.ObjectId == followerId);
        Assert.Equal(5, selphie.CharacterId);
        Assert.Equal(1, selphie.ShadeLevel);
        Assert.Equal(new System.Numerics.Vector3(128), selphie.PolygonColor);
        string original = world.Events.Capture().GetRawText();
        world.ApplyDebug(new DebugSceneCommand(DebugSceneTarget.Background, Visible: false));
        foreach (ActorState actor in world.Events.Actors.Where(actor => actor.ObjectId != selphie.ObjectId))
        {
            world.ApplyDebug(new DebugSceneCommand(DebugSceneTarget.Actor, ActorId: actor.ObjectId, Visible: false));
        }
        var avatar = new ActorVisual(selphie.ObjectId, selphie.ModelId, selphie.X, selphie.Y, selphie.Z, selphie.Direction, selphie.AnimationId, selphie.AnimationFrame, true, PolygonColor: selphie.PolygonColor);
        var renderer = new FieldRenderer(world.Field!);
        byte[] neutral = renderer.Render(world.Events.BackgroundStates, [avatar], TimeSpan.Zero, backgroundVisible: false).ToArray();
        byte[] rendered = world.Render(TimeSpan.Zero).ToArray();
        Assert.Equal(neutral, rendered);
        Assert.True(Enumerable.Range(0, rendered.Length / 4).Count(pixel => rendered[pixel * 4] > 32 || rendered[pixel * 4 + 1] > 32 || rendered[pixel * 4 + 2] > 32) > 50);
        byte[] oldSilhouette = renderer.Render(world.Events.BackgroundStates, [avatar with { PolygonColor = new System.Numerics.Vector3(selphie.ShadeLevel) }], TimeSpan.Zero, backgroundVisible: false).ToArray();
        Assert.False(oldSilhouette.SequenceEqual(rendered));
        Assert.Equal(original, world.Events.Capture().GetRawText());
    }
}
