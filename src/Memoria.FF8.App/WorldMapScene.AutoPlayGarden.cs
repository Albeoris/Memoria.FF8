using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8;

internal sealed partial class WorldMapScene
{
    private int autoPlayBalambGardenWaypoint;
    private static readonly (int X, int Z)[] BalambGardenApproach =
    [
        (14000, -24400), (15000, -24000), (15700, -24400), (16000, -24000)
    ];

    internal PlayerAction? PlanAutoPlayGardenLanding(bool releaseConfirm)
    {
        WorldState current = State;
        if (current.NativeGardenRuntime is not { } garden || current.NativeFootRuntime is not null || WorldEntryVariables.Read(session.State).StoryWord != 750)
        {
            return null;
        }
        if (garden.Transfer is { Counter: < 31 })
        {
            return new(PlayerActionKind.Wait);
        }

        // NG-044: Use the shallow Balamb approach, then admit the original foot transfer.
        if (current.X is >= 15400 and <= 16600 && current.Z is >= -25000 and <= -23500 && ResolveGardenFootPlacement(current) is { Accepted: true })
        {
            return new(garden.Speed == 0 && !releaseConfirm ? PlayerActionKind.Confirm : PlayerActionKind.Wait);
        }

        while (autoPlayBalambGardenWaypoint + 1 < BalambGardenApproach.Length)
        {
            (int waypointX, int waypointZ) = BalambGardenApproach[autoPlayBalambGardenWaypoint];
            if (SquaredDistance(current.X, current.Z, waypointX, waypointZ) > 175L * 175 &&
                !(current.X > waypointX && current.X < 16800 && current.Z is >= -25300 and <= -23500))
            {
                break;
            }
            autoPlayBalambGardenWaypoint++;
        }
        if (current.GroundType == 32 && current.NativeHeight > 0)
        {
            return new(PlayerActionKind.Wait);
        }
        (int x, int z) = BalambGardenApproach[autoPlayBalambGardenWaypoint];
        return SteerAutoPlayGarden(x, z);
    }

    private PlayerAction SteerAutoPlayGarden(int x, int z)
    {
        WorldState current = State;
        NativeGardenWorldRuntimeState garden = current.NativeGardenRuntime!;
        double dx = NearWrapped(x, current.X, 262144) - current.X;
        double dz = NearWrapped(z, current.Z, 196608) - current.Z;
        int desired = WrapYaw(NativeRound(Math.Atan2(dx, -dz) / NativeAngleUnit));
        int turn = YawDelta(desired, current.BodyYaw);
        int shift = garden.AnimationVariant == 0 && Math.Abs(current.CameraYaw - current.BodyYaw) > 511 ? 3 : 2;
        float steering = Math.Clamp((turn << (shift + 1)) / 127f, -1, 1);
        float throttle = Math.Abs(turn) < 64 && garden.Speed < 48 ? -1 : 0;
        return new(PlayerActionKind.Move, Horizontal: steering, Vertical: throttle);
    }
}
