using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8;

internal sealed partial class WorldMapScene
{
    internal bool IsAutoPlayRentalParkingTerrain()
    {
        WorldState current = State;
        if (current.NativeCarRuntime is not { } car || !car.GroundCache!.Alternatives.All(item => CanParkRental(assets.ResolveNativeGroundIdentity(item.CurrentGround, replacements), car.EntityType)))
        {
            return false;
        }
        if (car.GardenObjects is not null || car.RagnarokObjects is not null)
        {
            return true;
        }
        NativeTrainWorldAllocation allocation = CarTransferAllocation(car);
        int footEntity = allocation.Objects[allocation.ControlledObjectIndex].EntityType;
        return ResolveCarDisembarkPlacement(current, car, footEntity) is { Accepted: true };
    }

    internal (int X, int Z)? FindAutoPlayRentalParkingTerrain(IReadOnlyCollection<(int X, int Z)> rejected)
    {
        WorldState current = State;
        byte entity = current.NativeCarRuntime!.EntityType;
        const int sampleStride = 64;
        for (int distance = sampleStride; distance <= 2048; distance += sampleStride)
        {
            for (int heading = 0; heading < 4096; heading += 256)
            {
                (int dx, int dz) = Rotate(0, distance, heading);
                int x = current.X + dx;
                int z = current.Z - dz;
                if (rejected.All(place => SquaredDistance(place.X, place.Z, x, z) > 128L * 128) &&
                    assets.LookupNativeGround(x, z, replacements, []) is { } ground && CanParkRental(ground.Ground.Triangle, entity))
                {
                    return (x, z);
                }
            }
        }
        return null;
    }

    internal bool IsNearAutoPlayRentalParkingTarget((int X, int Z) target)
    {
        return SquaredDistance(State.X, State.Z, target.X, target.Z) <= 64L * 64;
    }

    internal PlayerAction SteerAutoPlayRental(int x, int z)
    {
        WorldState current = State;
        double dx = NearWrapped(x, current.X, 262144) - current.X;
        double dz = NearWrapped(z, current.Z, 196608) - current.Z;
        int desired = WrapYaw(NativeRound(Math.Atan2(dx, -dz) / NativeAngleUnit));
        int turn = YawDelta(desired, current.BodyYaw);
        int shift = current.NativeCarRuntime!.AnimationVariant == 0 && Math.Abs(current.CameraYaw - current.BodyYaw) > 511 ? 3 : 2;
        float steering = Math.Clamp((turn << shift) / 127f, -1, 1);
        float throttle = Math.Abs(turn) < 64 && current.NativeCarRuntime.Speed < 24 ? -1 : 0;
        return new(PlayerActionKind.Move, Horizontal: steering, Vertical: throttle);
    }

    private static bool CanParkRental(WorldTriangle triangle, int entity)
    {
        uint flags = triangle.GroundType | (uint)triangle.TextureFlags << 8 | (uint)triangle.VertexFlags << 16;
        return NativeWorldTerrain.CanTraverse(flags, entity) && NativeWorldTerrain.CanDisembark(flags, entity);
    }
}
