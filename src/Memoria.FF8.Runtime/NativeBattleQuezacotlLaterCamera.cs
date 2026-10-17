namespace Memoria.FF8.Runtime;

public static class NativeBattleQuezacotlLaterCamera
{
    public static NativeBattleCameraState Orbit6C3AEC(NativeBattleCameraState camera, int localFrame, int targetZ1770)
    {
        if (localFrame is < 0 or >= 20)
        {
            throw new ArgumentOutOfRangeException(nameof(localFrame));
        }
        NativeBattleCameraPose pose = camera.CurrentPose ?? throw new NotSupportedException("Native6C3AEC requires its current global eye coordinates.");
        short angle = unchecked((short)(NativeBattleCameraMath.Sine(localFrame * 4096 / 40) >> 7));
        NativeBattleVector relative = new(pose.Eye.X, pose.Eye.Y, unchecked((short)(pose.Eye.Z - targetZ1770)));
        (int x, int y, int z) = NativeBattleGfMatrices.Rotate56C3D0(NativeBattleGfMatrices.Euler701310(new(0, angle, 0)), relative);
        NativeBattleCameraPoint eye = new(unchecked((short)x), unchecked((short)y), unchecked((short)(z + targetZ1770)));
        return camera.WriteEffectGlobals(new(eye, pose.Target, camera.FocalLength, unchecked((short)(camera.Roll + 8))));
    }

    public static NativeBattleCameraState Apply6C4F80(NativeBattleCameraState camera, ushort phase, int targetZ)
    {
        NativeBattleCameraPoint eye;
        NativeBattleCameraPoint target;
        if (phase == 246)
        {
            eye = new(4313, -885, unchecked((short)(targetZ + 3236)));
            target = new(-1134, -711, unchecked((short)(targetZ - 264)));
        }
        else if (phase == 256)
        {
            eye = new(-4000, -4000, unchecked((short)(targetZ + 4000)));
            target = new(0, 0, unchecked((short)targetZ));
        }
        else if (phase == 276)
        {
            eye = new(-430, -4188, unchecked((short)(targetZ + 1176)));
            target = new(-17, -5387, unchecked((short)(targetZ + 304)));
            short roll = camera.CapturedPose?.Roll ?? throw new NotSupportedException("Native6C506A requires its actual503300 saved rollWORD1D9771C.");
            return camera.WriteEffectGlobals(new(eye, target, camera.FocalLength, roll));
        }
        else if (phase == 302)
        {
            eye = new(0, -2378, unchecked((short)(targetZ + 3555)));
            target = new(0, -1291, unchecked((short)(targetZ - 2362)));
        }
        else if (phase == 318)
        {
            eye = new(4096, -4096, unchecked((short)(targetZ - 4096)));
            target = new(0, 0, unchecked((short)targetZ));
        }
        else
        {
            throw new ArgumentOutOfRangeException(nameof(phase));
        }
        return camera.WriteEffectGlobals(new(eye, target, camera.FocalLength, camera.Roll));
    }

    public static NativeBattleCameraState Shift6C4685(NativeBattleCameraState camera, int localFrame)
    {
        if (localFrame is < 20 or >= 46)
        {
            throw new ArgumentOutOfRangeException(nameof(localFrame));
        }
        NativeBattleCameraPose pose = camera.CurrentPose ?? throw new NotSupportedException("Native6C4685 requires its current EyeY and TargetY globals.");
        int delta = localFrame < 38 ? -64 : 384;
        NativeBattleCameraPoint eye = pose.Eye with { Y = unchecked((short)(pose.Eye.Y + delta)) };
        NativeBattleCameraPoint target = pose.Target with { Y = unchecked((short)(pose.Target.Y + delta)) };
        return camera.WriteEffectGlobals(new(eye, target, camera.FocalLength, camera.Roll));
    }
}
