using System.Text.Json;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NormalCameraBindingTests
{
    [Fact]
    public void ActualLibraryCameraUsesCompactPartyLeaderRatherThanDenseActorZero()
    {
        string directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR") ?? LaunchOptions.DefaultGameDirectory;
        Assert.SkipUnless(Directory.Exists(directory), "Installed field corpus is required.");
        DirectoryInfo? repository = new(TestResources.FixtureDirectory);
        string source = Path.Combine(repository!.FullName, "../fixtures/external-fixture.json");
        using var world = new GameWorld(new GameSession(GameSession.Load(source)), directory);
        world.EnsureField();
        Assert.Equal(new[] { 1, 255, 255 }, world.Events.Scene.FieldPartyNativeActorIds);
        Assert.Equal(2, world.Events.Scene.NativeActorObjectIds[1]);
        ActorState leader = Assert.Single(world.Events.Actors, actor => actor.ObjectId == 2);
        Assert.Equal(0, leader.CharacterId);
        Assert.Equal(1, leader.ModelId);
        Assert.NotNull(NormalCameraCertificate.Prepare(world.Field!, world.Events.Scene, world.Events.Actors));

        void Refuses(int[] binding)
        {
            SceneEventState scene = JsonSerializer.Deserialize<SceneEventState>(JsonSerializer.Serialize(world.Events.Scene))!;
            scene.NativeActorObjectIds.Clear();
            scene.NativeActorObjectIds.AddRange(world.Events.Scene.NativeActorObjectIds);
            scene.FieldPartyNativeActorIds.Clear();
            scene.FieldPartyNativeActorIds.AddRange(binding);
            Assert.Throws<NotSupportedException>(() => NormalCameraCertificate.Prepare(world.Field!, scene, world.Events.Actors));
        }

        Refuses([0, 255, 255]);
        Refuses([255, 255, 255]);
        Refuses([-1, 255, 255]);
        Refuses([8, 255, 255]);
        Refuses([1, 1, 255]);
        Refuses([1, 2, 2]);
        Refuses([1, 255]);
        SceneEventState sparse = JsonSerializer.Deserialize<SceneEventState>(JsonSerializer.Serialize(world.Events.Scene))!;
        sparse.FieldPartyNativeActorIds[2] = 2;
        Assert.NotNull(NormalCameraCertificate.Prepare(world.Field!, sparse, world.Events.Actors));
        ActorState unmodeled = JsonSerializer.Deserialize<ActorState>(JsonSerializer.SerializeToElement(leader).GetRawText().Replace("\"ModelId\":1", "\"ModelId\":-1"))!;
        Assert.Throws<NotSupportedException>(() => NormalCameraCertificate.Prepare(world.Field!, world.Events.Scene, world.Events.Actors.Where(actor => actor.ObjectId != 2).Append(unmodeled).ToArray()));
        Assert.Throws<NotSupportedException>(() => NormalCameraCertificate.Prepare(world.Field!, world.Events.Scene, world.Events.Actors.Where(actor => actor.ObjectId != 2).ToArray()));
    }
}
