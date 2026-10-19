namespace Memoria.FF8.Runtime;

/// <summary>The creature's first camera window and its signed fixed-point eye orbit.</summary>
public static class NativeBattleCarbuncle278Camera
{
    public static NativeBattleCameraState AdvanceFirstWindow(NativeBattleCameraState camera, int floor, ushort counter)
    {
        if (counter is < 1 or > 36)
        {
            throw new ArgumentOutOfRangeException(nameof(counter));
        }
        NativeBattleCameraPose pose;
        if (counter == 1)
        {
            camera = camera.Apply50A730();
            pose = new(new(0, -4000, unchecked((short)(floor + 3000))), new(0, 0, unchecked((short)floor)), 288, 0);
        }
        else
        {
            pose = camera.CurrentPose ?? throw new NotSupportedException("Native Carbuncle camera requires its current eye and target WORDs.");
            pose = pose with { FocalLength = camera.FocalLength, Roll = camera.Roll };
        }
        int fraction = (counter - 1) * 4096 / 36;
        int angle = NativeBattleCameraMath.Cosine(fraction) >> 6;
        pose = Orbit683D10(pose, angle, 0xFAA);
        pose = pose with { Eye = pose.Eye with { Y = unchecked((short)(pose.Eye.Y + 50)) } };
        return camera.WriteEffectGlobals(pose);
    }

    public static NativeBattleCameraState AdvanceSparkleWindow(NativeBattleCameraState camera, ushort counter, NativeBattleVector? followed)
    {
        if (counter is < 86 or >= 110)
        {
            throw new ArgumentOutOfRangeException(nameof(counter));
        }
        NativeBattleCameraPose pose = camera.CurrentPose ?? throw new NotSupportedException("Native Carbuncle camera requires its current eye and target WORDs.");
        if (counter < 92)
        {
            NativeBattleVector target = followed ?? throw new NotSupportedException("Native Carbuncle camera requires its actual posed vertex176.");
            pose = pose with { Target = new(target.X, target.Y, target.Z) };
        }
        pose = Orbit683D10(pose with { FocalLength = camera.FocalLength, Roll = camera.Roll }, -32, 4000);
        pose = pose with { Eye = pose.Eye with { Y = unchecked((short)(pose.Eye.Y - 30)) } };
        return camera.WriteEffectGlobals(pose);
    }

    public static NativeBattleCameraState AdvanceProgrammeWindow(NativeBattleCameraState camera, ushort counter)
    {
        if (counter is < 110 or >= 145)
        {
            throw new ArgumentOutOfRangeException(nameof(counter));
        }
        if (counter < 122)
        {
            return camera;
        }
        NativeBattleCameraPose pose = camera.CurrentPose ?? throw new NotSupportedException("Native Carbuncle camera requires its current eye and target WORDs.");
        short y = pose.Eye.Y;
        int fraction = 4096 + unchecked((121 - counter) * 4096) / 25;
        pose = Orbit683D10(pose with { FocalLength = camera.FocalLength, Roll = camera.Roll }, fraction >> 5, 4096 + fraction / 8);
        return camera.WriteEffectGlobals(pose with { Eye = pose.Eye with { Y = y } });
    }

    public static NativeBattleCameraState AdvanceSecondProgrammeWindow(NativeBattleCameraState camera, int centroid, ushort counter)
    {
        if (counter is < 145 or >= 215)
        {
            throw new ArgumentOutOfRangeException(nameof(counter));
        }
        if (counter != 145)
        {
            return camera;
        }
        NativeBattleCameraPose pose = camera.CurrentPose ?? throw new NotSupportedException("Native Carbuncle camera requires its current scalar owners.");
        pose = pose with { Eye = new(-2729, -2437, unchecked((short)(centroid - 2924))), Target = new(382, -156, unchecked((short)(centroid - 551))), FocalLength = camera.FocalLength, Roll = camera.Roll };
        return camera.WriteEffectGlobals(pose);
    }

    public static NativeBattleCameraState AdvanceReturnWindow(NativeBattleCameraState camera, int floor, ushort counter, NativeBattleVector? anchor)
    {
        if (counter is < 215 or >= 283)
        {
            throw new ArgumentOutOfRangeException(nameof(counter));
        }
        NativeBattleCameraPose pose = camera.CurrentPose ?? throw new NotSupportedException("Native Carbuncle return camera requires its current eye and target WORDs.");
        pose = pose with { FocalLength = camera.FocalLength, Roll = camera.Roll };
        if (counter < 231)
        {
            NativeBattleVector target = anchor ?? throw new NotSupportedException("Native Carbuncle return camera requires its current posed vertex anchor.");
            pose = pose with { Target = new(target.X, target.Y, target.Z) };
            if (counter == 215)
            {
                pose = pose with { Eye = new(0, pose.Target.Y, unchecked((short)(floor + 2000))) };
            }
            pose = Orbit683D10(pose, 0, 4000);
        }
        else if (counter == 231)
        {
            pose = pose with { Target = new(0, -300, unchecked((short)floor)), Eye = new(0, -600, unchecked((short)(floor + 2000))) };
        }
        else if (counter >= 247)
        {
            int angle = NativeBattleCameraMath.Sine((counter - 247) << 7) >> 7;
            pose = Orbit683D10(pose, angle, 4150);
            pose = pose with { Eye = pose.Eye with { Y = unchecked((short)(pose.Eye.Y - 50)) }, Target = pose.Target with { Y = unchecked((short)(pose.Target.Y - 30)) } };
        }
        else
        {
            return camera;
        }
        return camera.WriteEffectGlobals(pose);
    }

    public static NativeBattleCameraPose Orbit683D10(NativeBattleCameraPose pose, int angle, int fraction)
    {
        int y = unchecked((pose.Eye.Y - pose.Target.Y) * fraction) >> 12;
        int x = unchecked((pose.Eye.X - pose.Target.X) * fraction) >> 12;
        int z = unchecked((pose.Eye.Z - pose.Target.Z) * fraction) >> 12;
        int sine = NativeBattleCameraMath.Sine(angle);
        int cosine = NativeBattleCameraMath.Cosine(angle);
        int rotatedX = unchecked(cosine * x - sine * z) >> 12;
        int rotatedZ = unchecked(cosine * z + sine * x) >> 12;
        return pose with { Eye = new(unchecked((short)(pose.Target.X + rotatedX)), unchecked((short)(pose.Target.Y + y)), unchecked((short)(pose.Target.Z + rotatedZ))) };
    }
}
