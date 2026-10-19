namespace Memoria.FF8.Runtime;

public static class NativeBattleDiablos325MidCamera
{
    public static NativeBattleDiablos325TailStep Advance656429(NativeBattleDiablos325Frame frame, NativeBattleCameraState camera, ushort counter)
    {
        if (counter is < 123 or > 152)
        {
            throw new ArgumentOutOfRangeException(nameof(counter));
        }
        int z = NativeBattleDiablos325Entry.RequireState(frame.Memory).FarZ51DC;
        int ratio = 68 + (counter - 123) * 4096 / 30;
        NativeBattleCameraPose first = new(new(0, -7931, unchecked((short)(z - 1580))), new(0, -8211, unchecked((short)(z - 214))), camera.FocalLength, camera.Roll);
        NativeBattleCameraPose second = new(new(0, -5678, unchecked((short)(z - 3154))), new(0, -7244, unchecked((short)(z + 300))), camera.FocalLength, camera.Roll);
        return new(frame, camera.WriteEffectGlobals(NativeBattleDiablos325Orbit.Interpolate657DF0(first, second, ratio)));
    }

    public static NativeBattleDiablos325TailStep Reset6564BC(NativeBattleDiablos325Frame frame, NativeBattleCameraState camera)
    {
        int z = NativeBattleDiablos325Entry.RequireState(frame.Memory).FarZ51DC;
        NativeBattleCameraPose pose = new(new(0, -6811, unchecked((short)(z - 1194))), new(0, -6862, unchecked((short)(z + 217))), camera.FocalLength, camera.Roll);
        return new(frame, camera.WriteEffectGlobals(pose));
    }

}
