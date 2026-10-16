namespace Memoria.FF8.Runtime;

public sealed record NativeBattleContactPresentation(byte Sequence, byte Flags2, byte HitFlags3);

public static class NativeBattleContactPresentations
{
    public static NativeBattleContactPresentation Complete(byte kernelSequence, byte flags2, byte hitFlags3, byte attackClass, byte specialSource)
    {
        //48FE20 common tail, after the formula/status consumers have produced ADC/ADD/ADE.
        byte sequence = kernelSequence;
        if ((hitFlags3 & 4) != 0)
        {
            sequence = attackClass != 0 ? (byte)0 : (byte)9;
        }
        if ((hitFlags3 & 2) != 0 && sequence != 255)
        {
            sequence = 6;
        }
        if ((flags2 & 1) != 0 || (hitFlags3 & 4) != 0)
        {
            if (sequence == 255)
            {
                sequence = 0;
            }
        }
        else if ((hitFlags3 & 1) != 0 || sequence == 255)
        {
            sequence = 0;
        }
        else if (sequence == 0)
        {
            sequence = 5;
        }
        if (specialSource != 255)
        {
            //1D28E19's native linked source branch owns these high bits; other values preserve existing bits.
            hitFlags3 = specialSource switch
            {
                0 => (byte)(hitFlags3 & 0x3F),
                1 => (byte)(hitFlags3 & 0x3F | 0x40),
                2 => (byte)(hitFlags3 & 0x3F | 0x80),
                _ => hitFlags3
            };
            sequence = 22;
            hitFlags3 |= 8;
        }
        return new(sequence, flags2, hitFlags3);
    }
}
