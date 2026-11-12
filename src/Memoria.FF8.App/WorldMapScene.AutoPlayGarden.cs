using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8;

internal sealed partial class WorldMapScene
{
    private int autoPlayGardenWaypoint;
    private int? autoPlayWhiteSeeDWaypoint;
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
    private static readonly (int X, int Z)[] CentraGardenApproach =
    [
        (49500, -57500), (50200, -57500), (50300, -56300), (49200, -55200), (47500, -55200),
        (46500, -56200), (46400, -56400), (46100, -57500), (44200, -51900), (33500, -47000),
        (32000, -44800), (14500, -45500), (6900, -52900), (2898, -56176), (-7339, -64341),
        (-23851, 80167), (-23610, 68104), (-24200, 68100), (-24200, 65700), (-24100, 65600), (-23410, 63204)
    ];
    private static readonly (int X, int Z)[] OrphanageGardenApproach = [(-24200, 66100), (-24900, 66500)];
    private static readonly (int X, int Z)[] WhiteSeeDShipApproach =
    [
        (-24200, 69000), (-30036, 71538), (-31544, 71393), (-33000, 61000), (-23662, 45908),
        (-22879, 45273), (-18771, 43752), (-17069, 43716), (-16562, 44840), (-16732, 45685), (-18900, 46850)
    ];

    internal PlayerAction? PlanAutoPlayGardenReboarding(bool releaseConfirm)
    {
        WorldState current = State;
        if (WorldEntryVariables.Read(session.State).StoryWord != 1350 || session.State.EventMemory.Read(390, 1).RequireExact() == 0 || current.NativeFootRuntime is null || current.NativeGardenRuntime is not { } garden)
        {
            return null;
        }

        // NG-061: Reboard the retained Garden through its native broad contact.
        if (garden.BroadContact == garden.Allocation.Garden64Index || garden.BroadContact == garden.Allocation.Garden65Index)
        {
            return new(releaseConfirm ? PlayerActionKind.Wait : PlayerActionKind.Confirm);
        }
        NativeWorldPathPosition parked = garden.Objects[garden.Allocation.Garden64Index].Position;
        return WorldNavigationGuide.Next(current, parked.X, -parked.Z);
    }

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
            800 => CentraGardenApproach,
            912 => OrphanageGardenApproach,
            // NG-059: Reuse the Centra landing before Edea's introduction letter.
            1350 when session.State.EventMemory.Read(390, 1).RequireExact() == 0 => OrphanageGardenApproach,
            // NG-061: Sail to the original ship contact after obtaining the letter.
            1350 => WhiteSeeDShipApproach,
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
        if (approach == WhiteSeeDShipApproach)
        {
            autoPlayGardenWaypoint = autoPlayWhiteSeeDWaypoint ?? FindWhiteSeeDWaypoint(current);
        }

        // NG-044, NG-046: Reach the native landing area before attempting the foot transfer.
        bool atLanding = story switch
        {
            750 => current.X is >= 15400 and <= 16600 && current.Z is >= -25000 and <= -23500,
            765 => SquaredDistance(current.X, current.Z, 46168, -57483) <= 250L * 250,
            // NG-054: Use the retained Centra parking position and its original landing probes.
            912 => current.X is >= -25300 and <= -23000 && current.Z is >= 65000 and <= 66900,
            1350 when approach == OrphanageGardenApproach => current.X is >= -25300 and <= -23000 && current.Z is >= 65000 and <= 66900,
            _ => false
        };
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
        if (approach == WhiteSeeDShipApproach)
        {
            autoPlayWhiteSeeDWaypoint = autoPlayGardenWaypoint;
        }
        if (current.GroundType == 32 && current.NativeHeight > 0)
        {
            return new(PlayerActionKind.Wait);
        }
        // NG-047: Settle on the Centra shallows before approaching the native Garden encounter.
        if (story == 800 && current.GroundType == 32 && current.NativeHeight > -128 && SquaredDistance(current.X, current.Z, -24200, 68100) < 300L * 300)
        {
            return new(PlayerActionKind.Wait);
        }
        (int x, int z) = approach[autoPlayGardenWaypoint];
        return SteerAutoPlayGarden(x, z);
    }

    private static int FindWhiteSeeDWaypoint(WorldState current)
    {
        (int X, int Z) start = (-24200, 66100);
        double bestDistance = double.PositiveInfinity;
        int waypoint = 0;
        for (int index = 0; index < WhiteSeeDShipApproach.Length; index++)
        {
            (int X, int Z) end = WhiteSeeDShipApproach[index];
            double dx = end.X - start.X;
            double dz = end.Z - start.Z;
            double along = Math.Clamp(((current.X - start.X) * dx + (current.Z - start.Z) * dz) / (dx * dx + dz * dz), 0, 1);
            double x = current.X - (start.X + along * dx);
            double z = current.Z - (start.Z + along * dz);
            double distance = x * x + z * z;
            if (distance < bestDistance)
            {
                bestDistance = distance;
                waypoint = index;
            }
            start = end;
        }
        return waypoint;
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
