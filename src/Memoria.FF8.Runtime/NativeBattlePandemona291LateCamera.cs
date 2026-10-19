namespace Memoria.FF8.Runtime;

public static class NativeBattlePandemona291LateCamera
{
    public static NativeBattleCameraState Advance(NativeBattleCameraState camera, int center, int ground, int age, NativeBattleVector? firstPosition = null, short? firstScale = null, byte? firstState = null)
    {
        NativeBattleCameraPose pose = (camera.CurrentPose ?? throw new NotSupportedException("Native Pandemona late camera requires its retained eye and target.")) with { FocalLength = camera.FocalLength, Roll = camera.Roll };
        if (age is >= 154 and < 164)
        {
            pose = NativeBattleCarbuncle278Camera.Orbit683D10(pose, -21, 4000);
        }
        if (age == 164)
        {
            pose = pose with { Eye = new(-363, -1397, unchecked((short)(center - 7373))), Target = new(-475, -2524, unchecked((short)(center - 369))) };
        }
        if (age is >= 183 and < 224)
        {
            int ratio = unchecked((int)((uint)(unchecked((age - 183) << 12)) / 41));
            int inner = unchecked((4096 - NativeBattleCameraMath.Sine(ratio >> 2)) >> 2);
            int second = unchecked(4096 - NativeBattleCameraMath.Sine(inner));
            pose = pose with { Target = CameraPoint(NativeBattleQuezacotlRibbonMath.Blend(new(-475, -2524, unchecked((short)(center - 369))), new(-412, -3300, unchecked((short)(center + 1252))), 4096 - second, second)), Eye = CameraPoint(NativeBattleQuezacotlRibbonMath.Blend(new(-363, -1397, unchecked((short)(center - 7373))), new(-343, -2199, unchecked((short)(center - 605))), 4096 - second, second)) };
        }
        if (age == 223)
        {
            pose = pose with { Eye = new(5346, -485, unchecked((short)(ground + 2108))), Target = new(847, -1548, unchecked((short)(ground + 592))) };
        }
        if (age is >= 154 and < 184)
        {
            pose = NativeBattleCarbuncle278Camera.Orbit683D10(pose, 0, 4096);
        }
        if (age == 253)
        {
            pose = pose with { Eye = new(-1302, -335, unchecked((short)(center + 5260))), Target = new(0, -2834, unchecked((short)center)) };
        }
        if (age is >= 254 and < 284)
        {
            int ratio = unchecked((int)((uint)((age - 254) << 12) / 30));
            pose = NativeBattleCarbuncle278Camera.Orbit683D10(pose, NativeBattleCameraMath.Cosine(ratio >> 1) >> 8, 4000);
            pose = pose with { Eye = pose.Eye with { Y = unchecked((short)(pose.Eye.Y - 200)) } };
        }
        if (age == 283)
        {
            pose = pose with { Eye = new(-1385, -711, unchecked((short)(ground - 2658))), Target = new(-315, -1788, unchecked((short)(ground + 5013))) };
        }
        if (age == 319)
        {
            pose = pose with { Eye = new(345, -3969, unchecked((short)(center - 491))), Target = new(-115, -2730, unchecked((short)(center + 633))) };
        }
        if (age is >= 334 and < 377)
        {
            int ratio = unchecked((int)((uint)((age - 334) << 12) / 43));
            int second = NativeBattleCameraMath.Cosine(ratio >> 2);
            pose = pose with
            {
                Target = CameraPoint(NativeBattleQuezacotlRibbonMath.Blend(new(-115, -2730, unchecked((short)(center + 633))), new(-747, -1662, unchecked((short)(center - 1941))), 4096 - second, second)),
                Eye = CameraPoint(NativeBattleQuezacotlRibbonMath.Blend(new(345, -3969, unchecked((short)(center - 491))), new(3454, -348, unchecked((short)(center - 7851))), 4096 - second, second))
            };
        }
        if (age == 376)
        {
            NativeBattleVector first = firstPosition ?? throw new NotSupportedException("Native Pandemona final camera requires its current first target position.");
            int y = ((first.Y + 4096) >> 2) - 4096;
            pose = pose with { Eye = new(-888, -3673, unchecked((short)(ground - 7639))), Target = new(0, unchecked((short)y), unchecked((short)ground)) };
        }
        if (age is >= 377 and < 417)
        {
            NativeBattleVector first = firstPosition ?? throw new NotSupportedException("Native Pandemona final orbit requires its current first target position.");
            pose = pose with { Target = pose.Target with { Y = unchecked((short)(((first.Y + 4096) >> 2) - 4096)) } };
            pose = NativeBattleCarbuncle278Camera.Orbit683D10(pose, 32, 4096);
        }
        if (age is >= 440 and < 480 && (firstState ?? throw new NotSupportedException("Native Pandemona impact camera requires its current first target state.")) <= 2)
        {
            NativeBattleVector first = firstPosition ?? throw new NotSupportedException("Native Pandemona impact camera requires its current first target position.");
            short scale = firstScale ?? throw new NotSupportedException("Native Pandemona impact camera requires its current first target scale.");
            pose = pose with { Eye = new(unchecked((short)(first.X + (scale >> 1) + 3000)), unchecked((short)((first.Y >> 2) - 1000)), first.Z), Target = new(first.X, unchecked((short)(first.Y >> 1)), first.Z) };
        }
        return camera.WriteEffectGlobals(pose);
    }

    private static NativeBattleCameraPoint CameraPoint(NativeBattleVector vector)
    {
        return new(vector.X, vector.Y, vector.Z);
    }
}
