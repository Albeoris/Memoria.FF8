namespace Memoria.FF8.Runtime;

public static partial class NativeBattleDuel215Controller
{
    public static NativeBattleCameraState AdvanceCamera69B070(NativeBattleCameraState camera, short tick, short originX, short originZ, short centerX, short farZ)
    {
        _ = camera.CurrentPose ?? throw new NotSupportedException("Native Duel215 requires its actual camera pose.");
        if (tick == 0)
        {
            camera = camera.Apply50A730();
            camera = camera.WriteEffectGlobals(camera.CurrentPose! with { FocalLength = 288, Roll = 0 });
        }
        if ((uint)tick < 24)
        {
            int ratio = (tick << 12) / 23;
            short roll = unchecked((short)((ratio - 4096) / 14));
            NativeBattleCameraPose first = Pose(originX - 118, -591, originZ - 768, originX + 397, -572, originZ - 5144) with { Roll = roll };
            NativeBattleCameraPose second = Pose(originX - 137, -794, originZ - 136, originX - 121, -767, originZ - 755) with { Roll = roll };
            camera = camera.WriteEffectGlobals(NativeBattleDiablos325Orbit.Interpolate657DF0(first, second, ratio));
        }
        int phase = tick - 61;
        if ((uint)phase < 14)
        {
            if (phase == 0)
            {
                camera = camera.WriteEffectGlobals(Pose(originX - 118, -848, originZ - 792, originX - 148, -1027, originZ + 1287));
            }
            camera = camera.WriteEffectGlobals(camera.CurrentPose! with { Roll = unchecked((short)(1365 * ((phase << 12) / 14) / 4096)) });
        }
        if (tick == 75)
        {
            camera = camera.WriteEffectGlobals(Pose(centerX - 47, -455, farZ - 694, centerX + 2899, -2850, farZ + 939) with { Roll = 0 });
        }
        if (tick == 163)
        {
            camera = camera.WriteEffectGlobals(Pose(centerX - 261, -1707, farZ + 1436, centerX + 30, -543, farZ - 4709) with { Roll = 512 });
        }
        phase = tick - 221;
        if ((uint)phase < 4)
        {
            short roll = unchecked((short)(camera.Roll - 128));
            NativeBattleCameraPose first = Pose(centerX - 261, -1707, farZ + 1436, centerX + 30, -543, farZ - 4709) with { Roll = roll };
            NativeBattleCameraPose second = Pose(centerX + 206, -1002, farZ - 3145, centerX + 859, 60, farZ - 5589) with { Roll = roll };
            camera = camera.WriteEffectGlobals(NativeBattleDiablos325Orbit.Interpolate657DF0(first, second, (phase << 12) / 3));
        }
        return camera;

        NativeBattleCameraPose Pose(int ex, int ey, int ez, int tx, int ty, int tz) => new(new(unchecked((short)ex), unchecked((short)ey), unchecked((short)ez)), new(unchecked((short)tx), unchecked((short)ty), unchecked((short)tz)), camera.FocalLength, camera.Roll);
    }
}
