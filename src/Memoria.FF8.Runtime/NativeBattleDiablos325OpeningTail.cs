namespace Memoria.FF8.Runtime;

public sealed record NativeBattleDiablos325TailStep(NativeBattleDiablos325Frame Frame, NativeBattleCameraState Camera);

public static class NativeBattleDiablos325OpeningTail
{
    public static NativeBattleDiablos325TailStep Advance656203(NativeBattleDiablos325Frame frame, NativeBattleCameraState camera, ushort counter)
    {
        int z = NativeBattleDiablos325Entry.RequireState(frame.Memory).CenterZ51D4;
        if (counter == 0)
        {
            frame = NativeBattleDiablos325Puffs.Reset6545B0(frame);
            camera = camera.WriteEffectGlobals(new(new(0, -1796, unchecked((short)(z + 0x1E57))), new(0, -3166, unchecked((short)(z - 0x4BF))), 288, 0));
        }
        else if (counter is >= 9 and < 30)
        {
            frame = NativeBattleDiablos325Puffs.Rise658D30(NativeBattleDiablos325Puffs.Spawn658B90(frame, 4, -3072, false));
        }
        else if (counter == 30)
        {
            frame = NativeBattleDiablos325Puffs.Reset6545B0(frame);
            camera = camera.WriteEffectGlobals(new(new(0, -18240, unchecked((short)(z + 0x13E8))), new(0, -16384, unchecked((short)z)), camera.FocalLength, camera.Roll));
            for (int index = 0; index < 19; index++)
            {
                frame = NativeBattleDiablos325Puffs.Shrink658C80(NativeBattleDiablos325Puffs.Spawn658B90(frame, 4, -16384, true));
            }
            frame = NativeBattleDiablos325Puffs.Shrink658C80(NativeBattleDiablos325Puffs.Spawn658B90(frame, 4, -16384, true));
        }
        else if (counter is > 30 and < 60)
        {
            frame = NativeBattleDiablos325Puffs.Shrink658C80(NativeBattleDiablos325Puffs.Spawn658B90(frame, 4, -16384, true));
        }
        else if (counter is >= 60 and < 90)
        {
            return NativeBattleDiablos325Orbit.Advance656309(frame, camera, counter);
        }
        else if (counter == 90)
        {
            return NativeBattleDiablos325Orbit.Reset6563D1(frame, camera);
        }
        else if (counter is >= 123 and <= 152)
        {
            return NativeBattleDiablos325MidCamera.Advance656429(frame, camera, counter);
        }
        else if (counter == 153)
        {
            return NativeBattleDiablos325MidCamera.Reset6564BC(frame, camera);
        }
        else if (counter is >= 175 and <= 189)
        {
            return NativeBattleDiablos325MidCamera.Advance656504(frame, camera, counter);
        }
        else if (counter == 202)
        {
            return NativeBattleDiablos325LateReset.Reset65659F(frame, camera);
        }
        else if (counter is >= 217 and <= 236)
        {
            return NativeBattleDiablos325LateReset.Advance6565FA(frame, camera, counter);
        }
        else if (counter == 237)
        {
            return NativeBattleDiablos325LateReset.Reset6566B7(frame, camera);
        }
        if (counter >= 246)
        {
            return NativeBattleDiablos325LateCamera.Advance6566FB(frame, camera, counter);
        }
        return new(frame, camera);
    }
}
