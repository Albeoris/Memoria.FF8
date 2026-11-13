using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8;

internal sealed partial class WorldMapScene
{
    internal PlayerAction? PlanAutoPlayRagnarokFlight(bool releaseConfirm)
    {
        WorldState current = State;
        if (current.NativeRagnarokRuntime is not { } ship)
        {
            return null;
        }
        int story = WorldEntryVariables.Read(session.State).StoryWord;
        if (current.NativeFootRuntime is not null)
        {
            if (story != 3600)
            {
                return null;
            }
            // NG-079: Approach the original parked body and board at broad contact.
            if (ship.BroadContact == ship.Allocation.RagnarokIndex)
            {
                return new(releaseConfirm || ship.PreviousButtons is null ? PlayerActionKind.Wait : PlayerActionKind.Confirm);
            }
            NativeWorldPathPosition parked = ship.Objects[ship.Allocation.RagnarokIndex].Position;
            if (current.X < -28750 && current.Z > 69380)
            {
                return WorldNavigationGuide.Next(current, current.X, 69345);
            }
            if (current.X < -28750)
            {
                return WorldNavigationGuide.Next(current, -28715, 69345);
            }
            return WorldNavigationGuide.Next(current, NearWrapped(parked.X, current.X, 262144), NearWrapped(-parked.Z, current.Z, 196608));
        }
        if (ship.Transfer is not null)
        {
            return new(PlayerActionKind.Wait, Frames: 12);
        }
        // NG-080: The native contact tests the actual allocated Pandora object.
        NativeRagnarokWorldObject? pandora = story is >= 3790 and < 3900 ? ship.Objects.SingleOrDefault(item => item.ShapeId == 91) : null;
        // NG-076, NG-078, NG-079: Approach the original landing or dock area.
        (int X, int Z)? landing = story switch
        {
            3167 => (76288, 11520),
            3400 or 3410 => (-28160, 69632),
            3600 => (54813, 5197),
            _ when pandora is not null => (pandora.Position.X, -pandora.Position.Z),
            _ => null
        };
        if (landing is not { } target)
        {
            return null;
        }
        double dx = NearWrapped(target.X, current.X, 262144) - current.X;
        double dz = NearWrapped(target.Z, current.Z, 196608) - current.Z;
        double distance = Math.Sqrt(dx * dx + dz * dz);
        if (story == 3600 && distance < 70 && ship.Motor.Speed == 0)
        {
            return new(PlayerActionKind.WorldButtons, releaseConfirm || ship.PreviousButtons is null ? 0 : 0x40);
        }
        if (story != 3600 && pandora is null && distance < 650 && ship.Motor.Speed == 0 && PrepareRagnarokLanding(current) is not null)
        {
            return new(releaseConfirm || ship.PreviousButtons is null ? PlayerActionKind.Wait : PlayerActionKind.Confirm);
        }
        int desired = WrapYaw(NativeRound(Math.Atan2(dx, -dz) / NativeAngleUnit));
        int turn = YawDelta(desired, current.BodyYaw);
        int buttons = Math.Abs(turn) <= 16 ? 0 : turn > 0 ? 0x2000 : 0x8000;
        int braking = Math.Max(0, ship.Motor.Speed) * Math.Max(0, ship.Motor.Speed - 16) / 32;
        if (Math.Abs(turn) < 80 && distance > Math.Max(65, braking + ship.Motor.Speed))
        {
            buttons |= 0x80;
        }
        if (distance > 5000 && current.NativeHeight > -2600)
        {
            buttons |= 0x4000;
        }
        else if (distance < 5500 && pandora is not null && current.NativeHeight < pandora.Position.Y - 450 || distance < 2200 && pandora is null && current.NativeHeight < (story == 3600 ? -1000 : -550))
        {
            buttons |= 0x1000;
        }
        return new(PlayerActionKind.WorldButtons, buttons, Frames: distance > 4000 && Math.Abs(turn) < 64 ? 8 : 1);
    }
}
