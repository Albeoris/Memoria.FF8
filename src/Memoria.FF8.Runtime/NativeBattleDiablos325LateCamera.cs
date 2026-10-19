namespace Memoria.FF8.Runtime;

public static class NativeBattleDiablos325LateCamera
{
    public static NativeBattleDiablos325TailStep Advance6566FB(NativeBattleDiablos325Frame frame, NativeBattleCameraState camera, ushort counter)
    {
        NativeBattleDiablos325State state = NativeBattleDiablos325Entry.RequireState(frame.Memory);
        int z = state.FarZ51DC;
        if (counter is >= 260 and <= 276)
        {
            NativeBattleCameraPose first = Pose(-838, -8389, z - 1164, 39, -8496, z - 235);
            NativeBattleCameraPose second = Pose(-281, -8437, z - 552, 0, -8871, z - 277);
            camera = camera.WriteEffectGlobals(NativeBattleDiablos325Orbit.Interpolate657DF0(first, second, (counter - 260) << 8));
        }
        if (counter == 297)
        {
            camera = camera.WriteEffectGlobals(Pose(-243, -6692, z - 3807, -7, -8879, z - 265) with { Roll = -256 });
        }
        if (counter is >= 329 and <= 368)
        {
            NativeBattleCameraPose first = Pose(493, -8559, z - 112, 61, -8674, z - 151) with { Roll = 0 };
            NativeBattleCameraPose second = Pose(1617, -6383, z + 1164, 201, -7329, z - 1735) with { Roll = 0 };
            int ratio = (counter - 329) * 4096 / 40 + 102;
            camera = camera.WriteEffectGlobals(NativeBattleDiablos325Orbit.Interpolate657DF0(first, second, ratio));
        }
        if (counter is >= 367 and <= 404)
        {
            int ratio = (counter - 367) * 4096 / 38 + 53;
            int height = NativeBattleDiablos325Recipients.Curve(ratio) * 4 - 16640;
            NativeBattleCameraPose first = Pose(state.CenterX51D8, -526, state.CenterZ51D4 + 11195, state.CenterX51D8, height, state.CenterZ51D4 + 3812);
            NativeBattleCameraPose second = Pose(state.CenterX51D8, -817, z + 2608, state.CenterX51D8, -1053, z - 425);
            camera = camera.WriteEffectGlobals(NativeBattleDiablos325Orbit.Interpolate657DF0(first, second, ratio));
        }
        if (counter == 405)
        {
            camera = camera.WriteEffectGlobals(Pose(state.CenterX51D8 - 6801, -16376, state.CenterZ51D4 + 8839, state.CenterX51D8 - 1004, -3537, state.CenterZ51D4 + 635));
        }
        if (counter == 455)
        {
            camera = camera.WriteEffectGlobals(Pose(3766, -7728, z, 0, -7826, z));
        }
        if (counter is >= 468 and <= 497)
        {
            int angle = (68 + (counter - 468) * 4096 / 30) / 2;
            int ratio = 2048 - NativeBattleCameraMath.Cosine(angle) / 2;
            NativeBattleCameraPose first = Pose(3766, -7728, z, 0, -7826, z);
            NativeBattleCameraPose second = Pose(9412, -6177, z - 24, 0, -7827, z - 1);
            camera = camera.WriteEffectGlobals(NativeBattleDiablos325Orbit.Interpolate657DF0(first, second, ratio));
        }
        if (counter == 498)
        {
            camera = camera.WriteEffectGlobals(Pose(state.CenterX51D8, -1489, state.CenterZ51D4 + 8568, state.CenterX51D8, -780, state.CenterZ51D4 - 1136));
        }
        return new(frame, camera);

        NativeBattleCameraPose Pose(int ex, int ey, int ez, int tx, int ty, int tz)
        {
            return new(new(unchecked((short)ex), unchecked((short)ey), unchecked((short)ez)), new(unchecked((short)tx), unchecked((short)ty), unchecked((short)tz)), camera.FocalLength, camera.Roll);
        }
    }
}
