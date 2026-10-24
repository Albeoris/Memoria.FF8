using static Memoria.FF8.Runtime.NativeBattleEffect70Profile;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleEffect70Controller
{
    public static NativeBattleEffect70Frame AdvanceCamera(NativeBattleEffect70Frame frame, int age, NativeBattleEffect70Inputs inputs)
    {
        int x = unchecked((short)frame.State.ReadWord(CenterAddress));
        int z = unchecked((short)frame.State.ReadWord(CenterAddress + 4));
        int side = unchecked((int)frame.State.ReadDword(SideGlobal));
        NativeBattleCameraPose Pose(int tx, int ty, int tz, int ex, int ey, int ez) => new(new(unchecked((short)(x + ex)), unchecked((short)ey), unchecked((short)(z + side * ez))), new(unchecked((short)(x + tx)), unchecked((short)ty), unchecked((short)(z + side * tz))), null, inputs.ReadCameraRoll(frame));
        NativeBattleCameraPose? pose = null;
        if (age == 0)
        {
            frame = inputs.Execute(frame, new NativeBattleEffect70CameraInitialize50A730());
            frame = inputs.Execute(frame, new NativeBattleEffect70CameraFocal(288));
            frame = inputs.Execute(frame, new NativeBattleEffect70CameraRoll(0));
            pose = Pose(8, -807, 340, 1442, -2927, 3431);
        }
        else if (unchecked((uint)(age - 20)) < 39)
        {
            int angle = unchecked((age - 20) << 12) / 38 / 4;
            int ratio = 4096 - NativeBattleCameraMath.Cosine(angle);
            pose = NativeBattleDiablos325Orbit.Interpolate657DF0(Pose(8, -807, 340, 1442, -2927, 3431), Pose(-102, -8385, 2438, 418, 1741, 10573), ratio);
        }
        else if (age == 59)
        {
            pose = Pose(-365, -16445, 538, -361, -13641, 8304);
        }
        else if (unchecked((uint)(age - 79)) < 40)
        {
            frame = inputs.Execute(frame, new NativeBattleEffect70CameraFocal(200));
            pose = NativeBattleDiablos325Orbit.Interpolate657DF0(Pose(-29, 2999, -291, -6357, -5670, 4421), Pose(-204, -13, -2255, 27, -1073, 4566), unchecked((age - 79) << 12) / 39);
        }
        else if (unchecked((uint)(age - 119)) < 60)
        {
            pose = NativeBattleDiablos325Orbit.Interpolate657DF0(Pose(-204, -13, -2255, 27, -1073, 4566), Pose(-205, -14, -2256, 8894, -1218, -2368), unchecked((age - 119) << 12) / 59);
        }
        else if (age == 213)
        {
            frame = inputs.Execute(frame, new NativeBattleEffect70CameraFocal(SavedFocal(frame, inputs)));
            pose = Pose(702, 99, 459, 6818, -7714, 445);
        }
        return pose is null ? frame : inputs.Execute(frame, new NativeBattleEffect70CameraPose(pose));
    }
}
