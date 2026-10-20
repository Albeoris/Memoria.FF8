namespace Memoria.FF8.Runtime;

public static class NativeBattleRinoa89Camera
{
    public static NativeBattleCameraPose Interpolate6FC250(NativeBattleCameraPose original, NativeBattleVector firstTarget, NativeBattleVector firstEye, NativeBattleVector secondTarget, NativeBattleVector secondEye, int firstRoll, int secondRoll, int elapsed, int duration)
    {
        if (duration <= 1)
        {
            throw new InvalidDataException("Native6FC250 requires its original nonzero interpolation divisor.");
        }
        int ratio = unchecked(elapsed << 12) / (duration - 1);
        short roll = unchecked((short)(firstRoll + unchecked(elapsed * (secondRoll - firstRoll)) / (duration - 1)));
        return Interpolate(original, firstTarget, firstEye, secondTarget, secondEye, ratio, roll);
    }

    public static NativeBattleCameraPose Interpolate6E2C10(NativeBattleCameraPose original, NativeBattleVector firstTarget, NativeBattleVector firstEye, NativeBattleVector secondTarget, NativeBattleVector secondEye, int firstRoll, int secondRoll, int ratio)
    {
        short roll = unchecked((short)(firstRoll + unchecked(ratio * (secondRoll - firstRoll)) / 4096));
        return Interpolate(original, firstTarget, firstEye, secondTarget, secondEye, ratio, roll);
    }

    public static NativeBattleCameraPose Orbit6FC300(NativeBattleCameraPose pose, int pitch, short yaw, int scale)
    {
        NativeBattleActionMatrix rotation = NativeBattlePandemona291Restoration.Rotation701270(yaw);
        if (pitch != 0)
        {
            rotation = NativeBattleGfMatrices.Multiply56C090(NativeBattlePandemona291Restoration.Rotation701220(pitch), rotation);
        }
        short scaledEyeY = unchecked((short)(pose.Target.Y + (unchecked(scale * (pose.Eye.Y - pose.Target.Y)) >> 12)));
        NativeBattleVector difference = new(unchecked((short)(unchecked(scale * (pose.Eye.X - pose.Target.X)) >> 12)), unchecked((short)(unchecked(scale * (scaledEyeY - pose.Target.Y)) >> 12)), unchecked((short)(unchecked(scale * (pose.Eye.Z - pose.Target.Z)) >> 12)));
        NativeBattleVector rotated = NativeBattleGfMatrices.RotateShort56C4F0(rotation, difference);
        return pose with { Eye = new(unchecked((short)(pose.Target.X + rotated.X)), unchecked((short)(pose.Target.Y + rotated.Y)), unchecked((short)(pose.Target.Z + rotated.Z))) };
    }

    public static NativeBattleCameraPose Advance(NativeBattleCameraPose pose, short frame, NativeBattleVector center, NativeBattleVector caster)
    {
        if (frame < 4)
        {
            pose = Interpolate6FC250(pose, Point(-361, -705, 4805), Point(1114, -1455, -847), Point(-2618, 1265, 2997), Point(1114, -1455, -847), 0, 0, frame, 22);
        }
        else if (frame < 21)
        {
            pose = pose with { Target = new(caster.X, unchecked((short)(caster.Y - 675)), unchecked((short)(caster.Z - 500))), Eye = new(unchecked((short)(unchecked((frame + 1048572) << 12) / 22 / 4 + 1000 + caster.X)), unchecked((short)(caster.Y - 523)), unchecked((short)(caster.Z - 500))) };
        }
        if (frame is >= 21 and < 35)
        {
            pose = Interpolate6FC250(pose, Point(-189, -1784, 391), Point(-84, -196, 6069), Point(-134, -661, 630), Point(226, -341, 3136), 0, 0, frame - 21, 14);
        }
        if (frame is >= 49 and < 74)
        {
            pose = Interpolate6FC250(pose, Relative(-791, -480, -1731), Relative(-2412, -2599, -5481), Relative(204, -1169, -4036), Relative(287, -1919, -6189), 0, 0, frame - 49, 25);
        }
        if (frame is >= 95 and < 110)
        {
            pose = Interpolate6FC250(pose, Relative(204, -1169, -4036), Relative(287, -1919, -6189), Relative(-1321, -183, -1310), Relative(6836, -430, 1895), 0, 0, frame - 95, 15);
        }
        if (frame is >= 101 and < 161)
        {
            int angle = unchecked(-40 * NativeBattleCameraMath.Sine(((frame - 101) << 12) / 60)) >> 12;
            pose = Orbit6FC300(pose, angle, -48, 4096);
        }
        if (frame == 161)
        {
            NativeBattleVector target = Relative(-344, -93, 3313);
            NativeBattleVector eye = Relative(-406, -87, 1308);
            pose = pose with { Target = new(target.X, target.Y, target.Z), Eye = new(eye.X, eye.Y, eye.Z) };
        }
        if (frame is >= 176 and < 226)
        {
            uint age = unchecked((uint)(frame - 182));
            uint fraction = age <= 1000 ? (age << 10) / 44 : 0;
            int ratio = NativeBattleCameraMath.Sine(NativeBattleCameraMath.Sine(unchecked((int)fraction)) >> 2);
            pose = Interpolate6E2C10(pose, Relative(1009, -697, 2287), Relative(6287, -1176, 6939), Relative(-4264, -3611, -3152), Relative(7291, 4748, 5757), 0, 0, ratio);
        }
        return pose;

        NativeBattleVector Point(int x, int y, int z) => new(unchecked((short)(center.X + x)), unchecked((short)y), unchecked((short)(center.Z + z)));
        NativeBattleVector Relative(int x, int y, int z) => new(unchecked((short)(center.X + x)), unchecked((short)(center.Y + y)), unchecked((short)(center.Z + z)));
    }

    private static NativeBattleCameraPose Interpolate(NativeBattleCameraPose original, NativeBattleVector firstTarget, NativeBattleVector firstEye, NativeBattleVector secondTarget, NativeBattleVector secondEye, int ratio, short roll)
    {
        NativeBattleVector target = NativeBattleQuezacotlRibbonMath.Blend(firstTarget, secondTarget, unchecked(4096 - ratio), ratio);
        NativeBattleVector eye = NativeBattleQuezacotlRibbonMath.Blend(firstEye, secondEye, unchecked(4096 - ratio), ratio);
        return original with { Target = new(target.X, target.Y, target.Z), Eye = new(eye.X, eye.Y, eye.Z), Roll = roll };
    }
}
