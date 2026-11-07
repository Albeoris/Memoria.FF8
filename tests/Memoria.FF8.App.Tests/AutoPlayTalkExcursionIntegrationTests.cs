using System.Security.Cryptography;
using System.Text.Json;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class AutoPlayTalkExcursionIntegrationTests(ITestOutputHelper output)
{
    [Fact]
    public void OneOrdinaryFacingMoveCanSelectTheNearbyAuthenticFollower()
    {
        string source = TestResources.FixturePath("App.Tests.AutoPlayTalkExcursionIntegrationTests.1");
        byte[] original = Hash(source);
        GameSession session = new(GameSession.Load(source));
        using GameWorld world = new(session, TestResources.GameDirectory, validateWorldPresentation: false);
        try
        {
            world.EnsureField();
            ActorState actor = world.Events.Actors.Single(actor => actor.ObjectId == 1);
            Assert.Null(world.Events.SelectInteractionTarget());
            Assert.True(world.IsAutoPlayTalkInReach(actor.ObjectId));
            Assert.False(world.FaceAutoPlayActor(actor.ObjectId).Interrupted);
            output.WriteLine($"Selected={world.Events.SelectInteractionTarget()}; actor=({actor.X},{actor.Y},{actor.Z}); player={world.Events.Scene.NativePlayerPosition}");
            Assert.Equal(910, session.State.FieldId);
            Assert.Equal(actor.ObjectId, world.Events.SelectInteractionTarget());
            Assert.True(world.Apply(new PlayerAction(PlayerActionKind.Interact, actor.ObjectId)).Accepted);
            Assert.Null(world.Events.LastFault);
        }
        finally
        {
            Assert.Equal(original, Hash(source));
        }
    }

    [Fact]
    public void ApproachingAnAuthenticFollowingTalkActorCanAdmitAnUnplannedGateway()
    {
        string source = TestResources.FixturePath("App.Tests.AutoPlayTalkExcursionIntegrationTests.1");
        byte[] original = Hash(source);
        GameSession session = new(GameSession.Load(source));
        using GameWorld world = new(session, TestResources.GameDirectory, validateWorldPresentation: false);
        try
        {
            world.EnsureField();
            Assert.Equal(910, session.State.FieldId);
            ActorState follower = world.Events.Actors.Single(actor => actor.ObjectId == 1);
            Assert.Equal(1, follower.ModelId);
            Assert.Equal(1, follower.CharacterId);
            Assert.Contains(follower.ObjectId, world.Events.Scene.FieldPartyNativeActorIds!);
            Assert.True(follower.TalkEnabled);
            Assert.Null(world.Events.SelectInteractionTarget());
            AutoPlayTarget target = world.DiscoverAutoPlayTargets().Single(target => target.Id == "910:talk:1:0");
            int callbacks = 0;
            world.Events.NativePlayerMovementProcessed += _ => callbacks++;
            AutoPlayWalkResult result = world.WalkAutoPlayApproach(target, () => { });
            output.WriteLine($"Approach={result}; callbacks={callbacks}; actualField={session.State.FieldId}; player={world.Events.Scene.NativePlayerPosition}");
            Assert.True(result.Interrupted);
            Assert.Equal(1, result.GatewayIndex);
            Assert.Equal(920, session.State.FieldId);
            Assert.True(callbacks > 0);
            Assert.Null(world.Events.LastFault);
        }
        finally
        {
            Assert.Equal(original, Hash(source));
        }
    }

    [Fact]
    public void ControllerTalksToTheNearbyFollowerAndLeavesTheExcursionCycle()
    {
        string manifest = TestResources.FixturePath("App.Tests.AutoPlayTalkExcursionIntegrationTests.2");
        AutoPlayRunState saved = JsonSerializer.Deserialize<AutoPlayRunState>(File.ReadAllText(manifest), AutoPlayController.JsonOptions)!;
        string snapshot = Path.GetFullPath(saved.Snapshot!, Path.GetDirectoryName(manifest)!);
        byte[] originalManifest = Hash(manifest);
        byte[] originalSnapshot = Hash(snapshot);
        string capture = Path.Combine(Path.GetTempPath(), "autoplay-talk-excursion-" + Guid.NewGuid().ToString("N"));
        GameSession session = GameSession.CreateColdProcess();
        GameWorld? world = null;
        int restores = 0;
        try
        {
            GameWorld GetWorld() => world ??= new GameWorld(session, TestResources.GameDirectory, validateWorldPresentation: false);
            void Restore(GameState state)
            {
                world?.Dispose();
                world = null;
                session.Restore(state);
                restores++;
            }
            AutoPlayController controller = new(session, GetWorld, Restore, capture, deadlineTime: new RoutingClock());
            controller.ResumeFrom(manifest);
            int initialRestores = restores;
            bool talkedFromHere = false;
            for (int step = 0; step < 96 && session.State.FieldId is 910 or 920; step++)
            {
                controller.Advance(1);
                Assert.True(controller.Error is null, $"Step={controller.State.Steps}; error={controller.Error}");
                Assert.True(controller.Running);
                talkedFromHere |= controller.State.RecentActions.Any(action => action.Action == "Talk from current position 910:talk:1:0");
            }
            AutoPlayContactInterruption[] learned = controller.State.ContactInterruptions.Where(attempt => attempt.Source.FieldId == 910 && attempt.Source.Target.Kind == AutoPlayTargetKind.Talk && attempt.AdmittedGatewayIndex == 1).ToArray();
            output.WriteLine($"Steps={controller.State.Steps - saved.Steps}; field={session.State.FieldId}; learnedTalkExcursions={learned.Length}");
            Assert.True(talkedFromHere);
            Assert.True(controller.State.Coverage.Visits.Single(visit => visit.Visit.Key == "910:285").Events.Single(item => item.Event.Id == "910:talk:1").Completed);
            Assert.DoesNotContain(session.State.FieldId, new[] { 910, 920 });
            Assert.Equal(initialRestores, restores);
            Assert.Null(world?.Events.LastFault);
        }
        finally
        {
            world?.Dispose();
            Assert.Equal(originalManifest, Hash(manifest));
            Assert.Equal(originalSnapshot, Hash(snapshot));
            if (Directory.Exists(capture))
            {
                Directory.Delete(capture, recursive: true);
            }
        }
    }

    private static byte[] Hash(string path)
    {
        using FileStream stream = File.OpenRead(path);
        return SHA256.HashData(stream);
    }

    private sealed class RoutingClock : TimeProvider
    {
        public override long GetTimestamp() => 0;
    }
}
