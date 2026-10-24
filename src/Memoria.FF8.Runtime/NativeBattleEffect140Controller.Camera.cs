namespace Memoria.FF8.Runtime;

public static partial class NativeBattleEffect140Controller
{
    private static NativeBattleEffect140Frame AdvanceCamera6A72B8(NativeBattleEffect140Frame frame, uint task, int age, int center, NativeBattleEffect140Inputs inputs)
    {
        NativeBattleCameraPose pose = inputs.ReadCameraPose(frame);
        NativeBattleCameraPoint eye = pose.Eye;
        NativeBattleCameraPoint target = pose.Target;
        short? focal = pose.FocalLength;
        short roll = pose.Roll;
        bool changed = true;
        if (age == 0)
        {
            target = V(0, 0, center);
            eye = V(0, -1000, center + 2000);
            focal = 288;
            roll = 0;
        }
        else if (age == 100)
        {
            target = V(-425, -803, center - 119);
            eye = V(2705, -974, center + 43);
        }
        else if (age >= 101 && age < 150)
        {
            NativeBattleVector delta = new(unchecked((short)(eye.X - target.X)), unchecked((short)(eye.Y - target.Y)), unchecked((short)(eye.Z - target.Z)));
            if (age >= 121)
            {
                int magnitude = NativeBattleRayBomb10Geometry.Sqrt56BEC0(unchecked(delta.X * delta.X + delta.Y * delta.Y + delta.Z * delta.Z));
                int amount = 2 * age - 242;
                delta = new(unchecked((short)(delta.X - delta.X * amount / magnitude)), unchecked((short)(delta.Y - delta.Y * amount / magnitude)), unchecked((short)(delta.Z - delta.Z * amount / magnitude)));
                target = target with { Z = unchecked((short)(target.Z + 10)) };
            }
            NativeBattleVector rotated = NativeBattleGfMatrices.RotateShort56C4F0(NativeBattlePandemona291Restoration.Rotation701270(8), delta);
            eye = V(rotated.X + target.X, rotated.Y + target.Y, rotated.Z + target.Z);
        }
        else if (age == 150)
        {
            target = V(46, -1003, center - 183);
            eye = V(274, -843, center - 765);
        }
        else if (age >= 194)
        {
            int relative = age - 194;
            int angle = relative * 2048 / 60;
            if (relative == 0)
            {
                eye = eye with { Y = -348 };
            }
            NativeBattleModulePoolTask current = Read(frame, task).WriteWord(0xA4, 12288).WriteWord(0xAC, 12288).WriteWord(0xB4, 12288);
            frame = Write(frame, task, current);
            target = V(0, -1100 - (NativeBattleCameraMath.Sine(angle + 128) >> 2), center - 5017);
            int cosine = NativeBattleCameraMath.Cosine(angle >> 1);
            int correction = unchecked((int)((long)0x53896E7B * cosine >> 32));
            correction = unchecked(correction - cosine) >> 7;
            correction += (int)((uint)correction >> 31);
            eye = V(0, eye.Y + correction, unchecked((int)frame.State.ReadDword(0x2517B10)) - relative * 4 - 5016);
        }
        else
        {
            changed = false;
        }
        return changed ? inputs.Execute(frame, new NativeBattleEffect140PublishCamera(new(eye, target, focal, roll), inputs.ReadEyePadding(frame), inputs.ReadTargetPadding(frame))) : frame;
    }

    private static NativeBattleCameraPoint V(int x, int y, int z) => new(unchecked((short)x), unchecked((short)y), unchecked((short)z));
}
