using System.Numerics;
using Memoria.FF8.Assets;

namespace Memoria.FF8.App.Tests;

public sealed class SemanticActorDetourTests
{
    [Fact]
    public void CurrentObstacleDetourExecutesThroughNativeFanCollisionWithoutPlacementWrites()
    {
        FieldWalkmesh mesh = Square();
        Vector2 obstacle = new(500, 500);
        Vector2 start = new(200, 500);
        Vector2 destination = new(800, 500);
        IReadOnlyList<Vector2> route = Assert.IsAssignableFrom<IReadOnlyList<Vector2>>(GameWorld.FindSemanticLocalDetour(mesh, start, 0, 40, new HashSet<int>(), [(obstacle, 96)], destination));
        Assert.True(route.Count > 1);
        FieldNativeMovementRequest request = new(0, 200 * 4096, 500 * 4096, 40 * 4096, 64, 2696, 48, true, false, 0, true);
        int callbacks = 0;
        foreach (Vector2 waypoint in route)
        {
            while (Vector2.DistanceSquared(new Vector2(request.FixedX / 4096f, request.FixedY / 4096f), waypoint) > 24 * 24)
            {
                Assert.True(++callbacks < 256);
                byte direction = FieldNativeScriptDirectionProbe.ResolveFacing(request.FixedX, request.FixedY, (int)(waypoint.X * 4096), (int)(waypoint.Y * 4096))!.Value;
                FieldNativeMovementResult moved = FieldNativeMovementProbe.Resolve(mesh, request with { Direction = direction }, hasActorCollision: probe => Vector2.DistanceSquared(new Vector2(probe.FixedX / 4096f, probe.FixedY / 4096f), obstacle) < 48 * 48);
                Assert.Equal(FieldNativeMovementStatus.Accepted, moved.Status);
                request = request with { FixedX = moved.FixedX, FixedY = moved.FixedY, FixedZ = moved.FixedZ, Triangle = moved.Triangle, EarlyRetryState = moved.EarlyRetryState };
            }
        }
        Assert.InRange(Vector2.Distance(new Vector2(request.FixedX / 4096f, request.FixedY / 4096f), destination), 0, 24);
    }

    [Fact]
    public void CurrentLockedDestinationTriangleCannotBeUsedAsDetour()
    {
        IReadOnlyList<Vector2>? route = GameWorld.FindSemanticLocalDetour(Square(), new Vector2(200, 500), 0, 40, new HashSet<int> { 1 }, [(new Vector2(500, 500), 96)], new Vector2(800, 500));
        Assert.Null(route);
    }

    private static FieldWalkmesh Square()
    {
        WalkmeshTriangle first = new(new Vector3(0, 0, 40), new Vector3(0, 1000, 40), new Vector3(1000, 0, 40), -1, 1, -1);
        WalkmeshTriangle second = new(new Vector3(0, 1000, 40), new Vector3(1000, 1000, 40), new Vector3(1000, 0, 40), -1, -1, 0);
        return new FieldWalkmesh([first, second]);
    }
}
