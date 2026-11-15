namespace Memoria.FF8.Runtime;

public sealed record NativeBattleContactPresentation(byte Sequence, byte Flags2, byte HitFlags3);

public static class NativeBattleContactPresentations
{
    public static NativeBattleContactPresentation Complete(byte kernelSequence, byte flags2, byte hitFlags3, byte attackClass, bool covered)
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
        if (covered)
        {
            sequence = 22;
            hitFlags3 |= 8;
        }
        return new(sequence, flags2, hitFlags3);
    }
}
