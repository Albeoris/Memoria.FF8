using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8;

internal sealed partial class WorldMapScene
{
    private int autoPlayGardenWaypoint;
    private static readonly (int X, int Z)[] BalambGardenApproach =
    [
        (14000, -24400), (15000, -24000), (15700, -24400), (16000, -24000)
    ];
    private static readonly (int X, int Z)[] TrabiaGardenApproach =
    [
        (15000, -24000), (14000, -23000), (13600, -21300), (29000, -21800),
        (35000, -26000), (35200, -29870), (34475, -40107), (30000, -45000),
        (32900, -47000), (33500, -47000), (44232, -51855), (46168, -57483)
    ];

    internal PlayerAction? PlanAutoPlayGardenLanding(bool releaseConfirm)
    {
        WorldState current = State;
        if (current.NativeGardenRuntime is not { } garden || current.NativeFootRuntime is not null)
        {
            return null;
        }
        ushort story = WorldEntryVariables.Read(session.State).StoryWord;
        (int X, int Z)[]? approach = story switch
        {
            750 => BalambGardenApproach,
            765 => TrabiaGardenApproach,
            _ => null
        };
        if (approach is null)
        {
            return null;
        }
        if (garden.Transfer is { Counter: < 31 })
        {
            return new(PlayerActionKind.Wait);
        }

        // NG-044, NG-046: Reach the native landing area before attempting the foot transfer.
        bool atLanding = story == 750
            ? current.X is >= 15400 and <= 16600 && current.Z is >= -25000 and <= -23500
            : SquaredDistance(current.X, current.Z, 46168, -57483) <= 250L * 250;
        if (atLanding && ResolveGardenFootPlacement(current) is { Accepted: true })
        {
            return new(garden.Speed == 0 && !releaseConfirm ? PlayerActionKind.Confirm : PlayerActionKind.Wait);
        }

        while (autoPlayGardenWaypoint + 1 < approach.Length)
        {
            (int waypointX, int waypointZ) = approach[autoPlayGardenWaypoint];
            bool passedBalambWaypoint = story == 750 && current.X > waypointX && current.X < 16800 && current.Z is >= -25300 and <= -23500;
            if (SquaredDistance(current.X, current.Z, waypointX, waypointZ) > 175L * 175 && !passedBalambWaypoint)
            {
                break;
            }
            autoPlayGardenWaypoint++;
        }
        if (current.GroundType == 32 && current.NativeHeight > 0)
        {
            return new(PlayerActionKind.Wait);
        }
        (int x, int z) = approach[autoPlayGardenWaypoint];
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
