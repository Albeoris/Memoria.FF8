namespace Memoria.FF8.Runtime;

/// <summary>Vehicle variant1 manual yaw and retained signed high camera-speed word.</summary>
public static class NativeVehicleCamera
{
    public static float ReadManualCameraDirection(uint buttons, float analog)
    {
        return (buttons & 12) switch
        {
            4 => -1,
            8 => 1,
            12 => 0,
            _ => analog
        };
    }

    public static (int Yaw, int Velocity) Step(int yaw, int velocity, float horizontal)
    {
        if (yaw is < 0 or >= 4096 || velocity is < -128 or > 128 || !float.IsFinite(horizontal) || Math.Abs(horizontal) > 1)
        {
            throw new InvalidDataException("Invalid native vehicle manual camera state.");
        }
        velocity = horizontal == 0 ? Math.Abs(velocity) < 8 ? 0 : (3 * velocity) >> 2 : Math.Clamp(velocity + Math.Sign(horizontal) * 8, -128, 128);
        return ((yaw + (velocity >> 3) + 4096) % 4096, velocity);
    }
}
