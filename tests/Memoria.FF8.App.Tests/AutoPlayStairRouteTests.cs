using System.Numerics;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class AutoPlayStairRouteTests
{
    [Fact]
    public void NativeStairRouteCanInitiallyMoveAwayFromTheLowerFloorContact()
    {
        GameSession session = new(GameSession.Load(TestResources.FixturePath("App.Tests.AutoPlayStairRouteTests.1")));
        using GameWorld world = new(session, TestResources.GameDirectory, validateWorldPresentation: false) { AutoPlayFastMovement = true };
        world.EnsureField();
        var player = world.Events.PlayerActor!;
        Vector2 start = new(player.X, player.Y);
        float startHeight = player.Z;
        AutoPlayTarget? target = null;
        AutoPlayWalkingRoute? route = null;
        foreach (AutoPlayTarget candidate in world.DiscoverAutoPlayTargets().Where(candidate => candidate.Kind == AutoPlayTargetKind.Contact && candidate.DestinationField is not null && candidate.Z < startHeight - 256))
        {
            if (world.TryFindAutoPlayWalkingRoute(candidate, out AutoPlayWalkingRoute candidateRoute))
            {
                target = candidate;
                route = candidateRoute;
                break;
            }
        }
        Assert.NotNull(target);
        Assert.NotEmpty(route!.Waypoints);
        Vector2 destination = new(target.ApproachX ?? target.X, target.ApproachY ?? target.Y);
        float initialDistance = Vector2.DistanceSquared(start, destination);
        bool movedAway = false;

        AutoPlayWalkResult walked = world.WalkAutoPlayApproach(target, () =>
        {
            movedAway |= Vector2.DistanceSquared(new Vector2(player.X, player.Y), destination) > initialDistance;
        });

        Assert.True(movedAway);
        Assert.True(walked.ContactTriggered);
        Assert.True(player.Z < startHeight - 256);
        Assert.True(world.Events.HasAutoPlayHeldContact(target.ObjectId));
        Assert.Null(world.Events.LastFault);
    }
}
