using System.Numerics;
using Memoria.FF8.Assets;
using Memoria.FF8.Events;

namespace Memoria.FF8.App.Tests;

public sealed class AutoPlayWalkingRouteTests
{
    [Fact]
    public void NativeActorEnvelopeBlocksTheEntireNarrowCorridor()
    {
        FieldWalkmesh mesh = Rectangle(1000, 120);
        int collisions = 0;
        NativePlayerMovementResult Probe(FieldFixedPosition position, int triangle, Vector2 direction)
        {
            return Step(mesh, position, triangle, direction, ground =>
            {
                bool collision = SquaredDistance(ground, 500, 60) < 150 * 150;
                collisions += collision ? 1 : 0;
                return collision;
            });
        }
        bool found = GameWorld.TryFindAutoPlayWalkingRoute(mesh, new HashSet<int>(), Fixed(100, 60), 0, new WalkmeshPosition(new Vector3(900, 60, 40), 1), 7929 / 256f, Probe, out AutoPlayWalkingRoute route);
        Assert.False(found);
        Assert.Empty(route.Waypoints);
        Assert.True(collisions > 0);
    }

    [Fact]
    public void PortalVisibilityGraphFindsANativeWalkAroundAnActor()
    {
        FieldWalkmesh mesh = Rectangle(1000, 1000);
        NativePlayerMovementResult Probe(FieldFixedPosition position, int triangle, Vector2 direction)
        {
            return Step(mesh, position, triangle, direction, ground => SquaredDistance(ground, 500, 500) < 100 * 100);
        }
        WalkmeshPosition destination = new(new Vector3(900, 500, 40), 1);
        Assert.True(GameWorld.TryFindAutoPlayWalkingRoute(mesh, new HashSet<int>(), Fixed(100, 500), 0, destination, 7929 / 256f, Probe, out AutoPlayWalkingRoute route));
        Assert.True(route.Waypoints.Count > 1);
        Assert.Equal(destination, route.Waypoints[^1]);
        Assert.Contains(route.Waypoints, point => Math.Abs(point.Position.Y - 500) > 100);
    }

    [Fact]
    public void LockedDestinationIsRejectedBeforeAnyMovementProbe()
    {
        FieldWalkmesh mesh = Rectangle(1000, 1000);
        bool found = GameWorld.TryFindAutoPlayWalkingRoute(mesh, new HashSet<int> { 1 }, Fixed(100, 500), 0, new WalkmeshPosition(new Vector3(900, 500, 40), 1), 7929 / 256f, (_, _, _) => throw new InvalidOperationException("A locked destination must not be probed."), out AutoPlayWalkingRoute route);
        Assert.False(found);
        Assert.Empty(route.Waypoints);
    }

    [Fact]
    public void AnExhaustedSolverBudgetFailsExplicitlyInsteadOfCertifyingNoRoute()
    {
        FieldWalkmesh mesh = Rectangle(1000, 1000);
        NotSupportedException error = Assert.Throws<NotSupportedException>(() => GameWorld.TryFindAutoPlayWalkingRoute(mesh, new HashSet<int>(), Fixed(100, 500), 0, new WalkmeshPosition(new Vector3(900, 500, 40), 1), 7929 / 256f, (position, triangle, direction) => Step(mesh, position, triangle, direction, _ => false), out _, maximumProbes: 2));
        Assert.Contains("probe budget", error.Message);
    }

    [Fact]
    public void SlopingArrivalUsesTheNativePlaneAtTheActualTerminalPosition()
    {
        FieldWalkmesh flat = Rectangle(1000, 1000);
        Vector3 Slope(Vector3 point) => point with { Z = 40 + point.X / 2 };
        FieldWalkmesh mesh = new(flat.Triangles.Select(triangle => triangle with { A = Slope(triangle.A), B = Slope(triangle.B), C = Slope(triangle.C) }).ToArray());
        WalkmeshPosition destination = new(new Vector3(900, 500, 490), 1);
        Assert.True(GameWorld.TryFindAutoPlayWalkingRoute(mesh, new HashSet<int>(), new FieldFixedPosition(100 * 4096, 500 * 4096, 90 * 4096), 0, destination, 7929 / 256f, (position, triangle, direction) => Step(mesh, position, triangle, direction, _ => false), out AutoPlayWalkingRoute route));
        Assert.Equal(destination, route.Waypoints[^1]);
        WalkmeshPosition nearbySlopeWaypoint = new(new Vector3(120, 500, 100), 0);
        Assert.True(GameWorld.AutoPlayWalkingWaypointReached(mesh, new FieldFixedPosition(100 * 4096, 500 * 4096, 90 * 4096), 0, nearbySlopeWaypoint, 7929 / 256f * 0.75f));
        Assert.False(GameWorld.AutoPlayWalkingWaypointReached(mesh, new FieldFixedPosition(100 * 4096, 500 * 4096, 100 * 4096), 0, nearbySlopeWaypoint, 7929 / 256f * 0.75f));
    }

    [Fact]
    public void NearbyOverlappingFloorsCannotSatisfyAnotherTrianglesWaypoint()
    {
        FieldWalkmesh flat = Rectangle(1000, 1000);
        WalkmeshTriangle ground = flat.Triangles[0] with { NeighborB = -1 };
        WalkmeshTriangle upper = ground with { A = ground.A + new Vector3(0, 0, 500), B = ground.B + new Vector3(0, 0, 500), C = ground.C + new Vector3(0, 0, 500) };
        FieldWalkmesh mesh = new(new[] { ground, upper });
        Assert.False(GameWorld.AutoPlayWalkingWaypointReached(mesh, Fixed(100, 100), 0, new WalkmeshPosition(new Vector3(100, 100, 540), 1), 24));
    }

    private static FieldFixedPosition Fixed(int x, int y) => new(x * 4096, y * 4096, 40 * 4096);

    private static long SquaredDistance(FieldNativeGroundProbe ground, int x, int y)
    {
        long dx = (ground.FixedX >> 12) - x;
        long dy = (ground.FixedY >> 12) - y;
        return dx * dx + dy * dy;
    }

    private static NativePlayerMovementResult Step(FieldWalkmesh mesh, FieldFixedPosition position, int triangle, Vector2 delta, Func<FieldNativeGroundProbe, bool> collision)
    {
        byte direction = unchecked((byte)(int)MathF.Round(MathF.Atan2(delta.X, -delta.Y) * 128 / MathF.PI));
        FieldNativeMovementRequest request = new(triangle, position.X, position.Y, position.Z, direction, 7929, 8, true, false, 0, true);
        FieldNativeMovementResult movement = FieldNativeMovementProbe.Resolve(mesh, request, hasActorCollision: collision);
        Assert.NotEqual(FieldNativeMovementStatus.Unsupported, movement.Status);
        return new NativePlayerMovementResult(movement.FixedX, movement.FixedY, movement.FixedZ, movement.Triangle, movement.Direction, movement.Proposals.Select(proposal => new NativeJoinMovementProposal(proposal.FixedX, proposal.FixedY, proposal.Direction)).ToArray(), Accepted: movement.Status == FieldNativeMovementStatus.Accepted);
    }

    private static FieldWalkmesh Rectangle(int width, int height)
    {
        WalkmeshTriangle first = new(new Vector3(0, 0, 40), new Vector3(0, height, 40), new Vector3(width, 0, 40), -1, 1, -1);
        WalkmeshTriangle second = new(new Vector3(0, height, 40), new Vector3(width, height, 40), new Vector3(width, 0, 40), -1, -1, 0);
        return new FieldWalkmesh(new[] { first, second });
    }
}
