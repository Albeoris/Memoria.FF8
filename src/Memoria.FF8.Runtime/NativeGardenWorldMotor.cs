namespace Memoria.FF8.Runtime;

public readonly record struct NativeGardenMotorResult(int Speed, int MotionYaw, int BodyYaw, int CameraYaw, int CameraFollowSpeed);

/// <summary>Reached557A90 entity48/scriptMode0 variant0/1 motor; no positions or terrain results are manufactured.</summary>
public static class NativeGardenMotor
{
    public static NativeGardenMotorResult Step(int bodyYaw, int cameraYaw, int speed, short groundYaw, int cameraFollowSpeed, sbyte steering, sbyte throttle, byte variant = 0)
    {
        if (variant > 1 || bodyYaw is < 0 or >= 4096 || cameraYaw is < 0 or >= 4096 || speed is < -64 or > 64 || cameraFollowSpeed is < 0 or > 128)
        {
            throw new InvalidDataException("Native Garden ordinary motor requires its current normalized yaw, bounded speed and exact process follow word.");
        }
        //557AEA compares the raw signed WORD difference, not shortest-angle distance.
        int shift = variant == 0 && Math.Abs(unchecked((short)cameraYaw) - unchecked((short)bodyYaw)) > 511 ? 3 : 2;
        short turn = unchecked((short)(-steering >> shift));
        bodyYaw = unchecked((short)(bodyYaw + (turn >> 1)));
        speed = throttle == 0 ? Math.Abs(speed) <= 16 ? 0 : speed + (speed > 0 ? -16 : 16) : Math.Clamp(speed + throttle / 16, -64, 64);
        int motionYaw = Wrap(bodyYaw);
        int target = unchecked((short)(bodyYaw + (groundYaw >> 4)));
        int terrainDelta = Delta(Wrap(target), Wrap(bodyYaw));
        bodyYaw = Math.Abs(terrainDelta) <= 256 ? target : unchecked((short)(bodyYaw + (terrainDelta > 0 ? 512 : -512)));
        bodyYaw = Wrap(bodyYaw);
        if (variant == 1)
        {
            return new NativeGardenMotorResult(speed, motionYaw, bodyYaw, cameraYaw, cameraFollowSpeed);
        }
        int cameraDelta = Delta(bodyYaw, cameraYaw);
        short follow = unchecked((short)(cameraFollowSpeed + (Math.Abs(cameraDelta) >= 64 ? 8 : -2)));
        int maximum = Math.Min(128, Math.Abs(cameraDelta >> 1));
        cameraFollowSpeed = Math.Clamp((int)follow, 0, maximum);
        cameraYaw = Wrap(cameraYaw + (cameraDelta > 0 ? cameraFollowSpeed >> 3 : -(cameraFollowSpeed >> 3)));
        return new NativeGardenMotorResult(speed, motionYaw, bodyYaw, cameraYaw, cameraFollowSpeed);
    }

    public static int Delta(int first, int second)
    {
        int delta = first - second;
        if (delta > 2048)
        {
            delta -= 4096;
        }
        if (delta <= -2048)
        {
            delta += 4096;
        }
        return delta;
    }

    private static int Wrap(int yaw) => (yaw % 4096 + 4096) % 4096;
}
