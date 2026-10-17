namespace Memoria.FF8.Runtime;

public static class NativeBattleQuezacotlLaterCamera
{
    public static NativeBattleCameraState Apply6C4F80(NativeBattleCameraState camera, ushort phase, int targetZ1770)
    {
        NativeBattleCameraPoint eye;
        NativeBattleCameraPoint target;
        if (phase == 246)
        {
            eye = new(4313, -885, unchecked((short)(targetZ1770 + 3236)));
            target = new(-1134, -711, unchecked((short)(targetZ1770 - 264)));
        }
        else if (phase == 256)
        {
            eye = new(-4000, -4000, unchecked((short)(targetZ1770 + 4000)));
            target = new(0, 0, unchecked((short)targetZ1770));
        }
        else
        {
            throw new ArgumentOutOfRangeException(nameof(phase));
        }
        return camera.WriteEffectGlobals(new(eye, target, camera.FocalLength, camera.Roll));
    }
}
