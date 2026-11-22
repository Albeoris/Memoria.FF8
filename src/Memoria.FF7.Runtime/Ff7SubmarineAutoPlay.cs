namespace Memoria.FF7.Runtime;

public static class Ff7SubmarineAutoPlay
{
    public static ushort ChooseInput(Ff7SubmarineRuntime runtime)
    {
        if (runtime.Phase != Ff7SubmarinePhase.Playing)
        {
            return 0;
        }
        var state = runtime.ReadAutoPlayFacts();
        if ((state.Flags & 1) != 0)
        {
            return (ushort)((state.PreviousInput & 0x800) == 0 ? 0x800 : 0);
        }
        Ff7SubmarineFrame frame = runtime.Frame;
        Ff7SubmarineVesselFrame target = frame.Ships[0];
        double dx = (double)target.X4096 - frame.Player.X4096;
        double dy = (double)target.Y4096 - frame.Player.Y4096;
        double dz = (double)target.Z4096 - frame.Player.Z4096;
        double horizontal = Math.Sqrt(dx * dx + dz * dz);
        double distance = Math.Sqrt(horizontal * horizontal + dy * dy) / 4096;
        double yaw = Math.Atan2(dx, dz) * (4096 / (Math.PI * 2));
        double pitch = -Math.Atan2(dy, horizontal) * (4096 / (Math.PI * 2));
        double yawDifference = WrapAngle(yaw - frame.Player.Heading);
        double pitchDifference = pitch - frame.Player.Pitch;
        double closing = dx * state.DirectionX + dy * state.DirectionY + dz * state.DirectionZ;
        bool approachingFromBehind = distance < 128 && Math.Abs(yawDifference) > 1024 && closing < 0;
        ushort held = 0;
        if (!approachingFromBehind)
        {
            if (yawDifference > 4)
            {
                held |= 0x2000;
            }
            else if (yawDifference < -4)
            {
                held |= 0x8000;
            }
            if (pitchDifference > 2)
            {
                held |= 0x4000;
            }
            else if (pitchDifference < -2)
            {
                held |= 0x1000;
            }
        }
        int desiredSpeed = approachingFromBehind || Math.Abs(yawDifference) > 512 ? 9 : distance > 128 ? 18 : distance > 64 ? 17 : 9;
        int speed = state.SpeedIndex;
        if (speed < desiredSpeed)
        {
            held |= 0x10;
        }
        else if (speed > desiredSpeed)
        {
            held |= 0x40;
        }
        if (state.TargetLocked && state.LauncherReady && (state.PreviousInput & 0x80) == 0)
        {
            held |= 0x80;
        }
        return held;
    }

    private static double WrapAngle(double angle)
    {
        while (angle > 2048)
        {
            angle -= 4096;
        }
        while (angle < -2048)
        {
            angle += 4096;
        }
        return angle;
    }
}
