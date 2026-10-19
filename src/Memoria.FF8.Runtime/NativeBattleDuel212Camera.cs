namespace Memoria.FF8.Runtime;

public static partial class NativeBattleDuel212Controller
{
    public static NativeBattleCameraState AdvanceCamera6A1660(NativeBattleCameraState camera, short tick, short originX, short originZ, short centerX, short farZ)
    {
        _ = camera.CurrentPose ?? throw new NotSupportedException("Native Burning Rave requires its actual camera pose.");
        if (tick == 1)
        {
            return camera.WriteEffectGlobals(Pose(originX - 125, -771, originZ - 732, originX - 1089, -229, originZ - 1834) with { FocalLength = 288, Roll = 0 });
        }
        if ((uint)(tick - 18) < 24)
        {
            NativeBattleCameraPose first = Pose(originX - 95, -738, originZ - 630, originX - 200, -359, originZ - 1589);
            NativeBattleCameraPose second = Pose(originX - 101, -853, originZ - 678, originX - 161, -649, originZ - 1173);
            return Interpolate(first, second, ((tick - 18) << 12) / 25);
        }
        if ((uint)(tick - 43) < 38)
        {
            NativeBattleCameraPose first = Pose(originX - 133, -850, originZ - 689, originX + 543, -557, originZ - 1720);
            NativeBattleCameraPose second = Pose(originX - 7, -397, originZ - 1315, originX + 31, -1197, originZ - 2362);
            return Interpolate(first, second, ((tick - 43) << 12) / 38);
        }
        if ((uint)(tick - 81) < 28)
        {
            NativeBattleCameraPose first = Pose(originX - 7, -397, originZ - 1315, originX + 31, -1197, originZ - 2362);
            NativeBattleCameraPose second = Pose(originX - 32, -526, originZ - 1236, originX - 721, -867, originZ - 4832);
            return Interpolate(first, second, ((tick - 81) << 12) / 28);
        }
        if ((uint)(tick - 109) < 25)
        {
            NativeBattleCameraPose first = Pose(centerX, 0, farZ, centerX, -1342, farZ + 10936);
            NativeBattleCameraPose second = Pose(centerX, 0, farZ, centerX, -536, farZ + 7164);
            return Interpolate(first, second, ((tick - 109) << 12) / 25);
        }
        if (tick == 134)
        {
            return camera.WriteEffectGlobals(Pose(originX - 675, -2226, originZ - 4654, originX + 885, -167, originZ + 1059));
        }
        return camera;

        NativeBattleCameraPose Pose(int ex, int ey, int ez, int tx, int ty, int tz) => new(new(unchecked((short)ex), unchecked((short)ey), unchecked((short)ez)), new(unchecked((short)tx), unchecked((short)ty), unchecked((short)tz)), camera.FocalLength, camera.Roll);
        NativeBattleCameraState Interpolate(NativeBattleCameraPose first, NativeBattleCameraPose second, int ratio) => camera.WriteEffectGlobals(NativeBattleDiablos325Orbit.Interpolate657DF0(first, second, ratio));
    }
}
