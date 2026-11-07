using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8;

internal sealed partial class WorldMapScene
{
    internal bool IsAutoPlayRentalParkingTerrain()
    {
        return State.NativeCarRuntime is { } car && car.GroundCache!.Alternatives.All(item => CanParkRental(assets.ResolveNativeGroundIdentity(item.CurrentGround, replacements), car.EntityType));
    }

    internal (int X, int Z)? FindAutoPlayRentalParkingTerrain()
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
                if (assets.LookupNativeGround(x, z, replacements, []) is { } ground && CanParkRental(ground.Ground.Triangle, entity))
                {
                    return (x, z);
                }
            }
        }
        return null;
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
