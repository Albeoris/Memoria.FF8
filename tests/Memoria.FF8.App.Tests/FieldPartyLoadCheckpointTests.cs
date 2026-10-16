using Memoria.FF8.Events;
using Memoria.FF8.Runtime;
using System.Text.Json;

namespace Memoria.FF8.App.Tests;

public sealed class FieldPartyLoadCheckpointTests
{
    [Fact]
    public void NaturalElevatorArrivalRetainsPriorTraceAndRestoresNativePartyInitialization()
    {
        string directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR") ?? LaunchOptions.DefaultGameDirectory;
        Assert.SkipUnless(Directory.Exists(directory), "Installed field corpus is required.");
        DirectoryInfo? repository = new(TestResources.FixtureDirectory);
        string source = Path.Combine(repository!.FullName, "../fixtures/external-fixture.json");
        var session = new GameSession(GameSession.Load(source));
        using var first = new GameWorld(session, directory);
        first.EnsureField();
        PartyTraceSample prior = first.Events.Scene.PartyTrace!.FirstTrail[0]!;
        Assert.True(first.Apply(new PlayerAction(PlayerActionKind.Move, Vertical: 1)).Accepted);
        Assert.Equal(137, session.State.FieldId);
        first.EnsureField();
        Assert.Equal(0, first.Events.Frame);
        Assert.True(first.Apply(new PlayerAction(PlayerActionKind.Wait)).Accepted);
        session.SetEventState(first.Events.Capture());
        string path = Path.Combine(Path.GetTempPath(), "Memoria.FF8-field-party-load-" + Guid.NewGuid().ToString("N") + ".json");
        try
        {
            session.Save(path);
            var resumedSession = new GameSession(GameSession.Load(path));
            using var resumed = new GameWorld(resumedSession, directory);
            resumed.EnsureField();
            Assert.True(JsonElement.DeepEquals(first.Events.Capture(), resumed.Events.Capture()));
            for (int tick = 0; tick < 2; tick++)
            {
                var wait = new PlayerAction(PlayerActionKind.Wait);
                Assert.Equal(first.Apply(wait), resumed.Apply(wait));
                // Temporary-key removal leaves different Dictionary free slots;
                // JSON object property order is not part of the complete VM state.
                Assert.True(JsonElement.DeepEquals(first.Events.Capture(), resumed.Events.Capture()));
            }
            Assert.Null(first.Events.LastFault);
            Assert.Equal(1, first.Events.Scene.NativeFieldLoadVersion);
            Assert.Equal(new[] { 0, 1, 255 }, first.Events.Scene.FieldPartyNativeActorIds);
            Assert.Equal(prior with { X = -14, Y = -5203, FirstFlag = 1, SecondFlag = 1 }, first.Events.Scene.PartyTrace!.FirstTrail[0]);
            int leaderId = first.Events.Scene.NativeActorObjectIds[first.Events.Scene.FieldPartyNativeActorIds[0]];
            ActorState leader = Assert.Single(first.Events.Actors, actor => actor.ObjectId == leaderId);
            int followerId = first.Events.Scene.NativeActorObjectIds[first.Events.Scene.FieldPartyNativeActorIds[1]];
            ActorState follower = Assert.Single(first.Events.Actors, actor => actor.ObjectId == followerId);
            Assert.Equal((-14f, -5203f, 8341f, 72), (leader.X, leader.Y, leader.Z, leader.WalkmeshTriangle));
            Assert.Equal((28f, -5344f, 8341f, 91), (follower.X, follower.Y, follower.Z, follower.WalkmeshTriangle));
        }
        finally
        {
            File.Delete(path);
        }
    }
}
