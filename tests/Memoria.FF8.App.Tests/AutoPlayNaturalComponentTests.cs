using System.Text.Json;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class AutoPlayNaturalComponentTests
{
    [Fact]
    public void OriginalCurrentComponentDoesNotOfferDisconnectedTalkOrContactActions()
    {
        string manifest = TestResources.FixturePath("App.Tests.AutoPlayNaturalComponentTests.1");
        byte[] originalManifest = File.ReadAllBytes(manifest);
        AutoPlayRunState saved = JsonSerializer.Deserialize<AutoPlayRunState>(originalManifest, AutoPlayController.JsonOptions)!;
        string snapshot = Path.Combine(Path.GetDirectoryName(manifest)!, saved.Snapshot!);
        byte[] originalSnapshot = File.ReadAllBytes(snapshot);
        GameSession session = new(GameSession.Load(snapshot));
        using GameWorld world = new(session, TestResources.GameDirectory, validateWorldPresentation: false);
        world.EnsureField();
        Assert.Equal(764, session.State.FieldId);
        ActorState player = Assert.IsType<ActorState>(world.Events.PlayerActor);
        Assert.Equal(89, player.WalkmeshTriangle);
        Assert.NotEmpty(world.Events.Scene.LockedTriangles);
        Assert.Equal(new[] { 8, 9, 10 }, world.Events.Scene.Party);
        Assert.True(session.State.CharacterVitals!.DreamEnabled);
        Assert.Empty(saved.Coverage.Visits.SelectMany(visit => visit.Events).SelectMany(item => item.ParticipantPolicyDeferrals));
        HashSet<int> reachable = GameWorld.AutoPlayReachableTriangles(world.Field!.Walkmesh, player.WalkmeshTriangle, world.Events.Scene.LockedTriangles);
        Assert.Contains(89, reachable);
        Assert.DoesNotContain(32, reachable);
        Assert.DoesNotContain(147, reachable);
        JsonElement before = world.Events.Capture();
        GameState state = session.State;
        int executedCommands = 0;
        world.Events.CommandExecuted += _ => executedCommands++;

        IReadOnlyList<AutoPlayTarget> targets = world.DiscoverAutoPlayTargets();

        Assert.DoesNotContain(targets, target => target.CoverageId == "764:talk:17");
        Assert.DoesNotContain(targets, target => target.CoverageId == "764:line:9");
        Assert.All(targets, target =>
        {
            Assert.Contains(target.Triangle, reachable);
            Assert.DoesNotContain(target.Triangle, world.Events.Scene.LockedTriangles);
            if (target.ApproachTriangle is int approach)
            {
                Assert.Contains(approach, reachable);
                Assert.DoesNotContain(approach, world.Events.Scene.LockedTriangles);
            }
        });
        Assert.Equal(0, executedCommands);
        Assert.Same(state, session.State);
        Assert.True(JsonElement.DeepEquals(before, world.Events.Capture()));
        Assert.Null(world.Events.LastFault);
        Assert.Equal(originalManifest, File.ReadAllBytes(manifest));
        Assert.Equal(originalSnapshot, File.ReadAllBytes(snapshot));
    }
}
