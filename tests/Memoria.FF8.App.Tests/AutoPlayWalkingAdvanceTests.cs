using System.Numerics;
using Memoria.FF8.Assets;
using Memoria.FF8.Events;

namespace Memoria.FF8.App.Tests;

public sealed class AutoPlayWalkingAdvanceTests
{
    [Fact]
    public void ABlockedDirectStepCanUseAnotherNativeCertifiedDirectionWithoutLosingProgress()
    {
        FieldWalkmesh mesh = new(new[]
        {
            new WalkmeshTriangle(new(0, 0, 40), new(0, 400, 40), new(400, 0, 40), -1, 1, -1),
            new WalkmeshTriangle(new(0, 400, 40), new(400, 400, 40), new(400, 0, 40), -1, -1, 0)
        });
        FieldFixedPosition start = new(100 * 4096, 100 * 4096, 40 * 4096);
        Vector2 target = new(300, 30);
        int probes = 0;
        NativePlayerMovementResult Probe(FieldFixedPosition position, int triangle, Vector2 delta)
        {
            probes++;
            byte nativeDirection = unchecked((byte)(int)MathF.Round(MathF.Atan2(delta.X, -delta.Y) * 128 / MathF.PI));
            FieldNativeMovementRequest request = new(triangle, position.X, position.Y, position.Z, nativeDirection, 7929, 8, true, false, 0, true);
            FieldNativeMovementResult result = FieldNativeMovementProbe.Resolve(mesh, request, hasActorCollision: ground =>
            {
                long dx = ground.FixedX / 4096 - 150;
                long dy = ground.FixedY / 4096 - 60;
                return dx * dx + dy * dy < 45 * 45;
            });
            Assert.NotEqual(FieldNativeMovementStatus.Unsupported, result.Status);
            return new(result.FixedX, result.FixedY, result.FixedZ, result.Triangle, result.Direction, [], Accepted: result.Status == FieldNativeMovementStatus.Accepted);
        }
        Assert.False(Probe(start, 0, target - new Vector2(100, 100)).Accepted);
        probes = 0;

        Vector2? direction = GameWorld.FindAutoPlayWalkingAdvance(start, 0, target, Probe);

        Vector2 selected = Assert.IsType<Vector2>(direction);
        Assert.InRange(probes, 2, 257);
        NativePlayerMovementResult accepted = Probe(start, 0, selected);
        Assert.True(accepted.Accepted);
        Assert.True(Vector2.DistanceSquared(new(accepted.FixedX / 4096f, accepted.FixedY / 4096f), target) < Vector2.DistanceSquared(new(100, 100), target));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void RejectedOrStationaryProposalsCannotBecomeAnAdvance(bool accepted)
    {
        int probes = 0;
        Vector2? direction = GameWorld.FindAutoPlayWalkingAdvance(new(0, 0, 0), 0, new(100, 100), (position, triangle, _) =>
        {
            probes++;
            return new(position.X, position.Y, position.Z, triangle, 0, [], Accepted: accepted);
        });

        Assert.Null(direction);
        Assert.Equal(257, probes);
    }

    [Fact]
    public void AnAlreadyClearDirectStepRetainsTheOriginalSingleProbePath()
    {
        Vector2 target = new(100, 100);
        int probes = 0;
        Vector2? direction = GameWorld.FindAutoPlayWalkingAdvance(new(0, 0, 0), 0, target, (_, triangle, _) =>
        {
            probes++;
            return new(4096, 4096, 0, triangle, 0, [], Accepted: true);
        });

        Assert.Equal(target, direction);
        Assert.Equal(1, probes);
    }
}
