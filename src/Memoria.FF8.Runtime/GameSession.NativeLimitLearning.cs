namespace Memoria.FF8.Runtime;

public sealed partial class GameSession
{
    private NativeLimitProgress? ProjectNativeLimitLearning(byte? before, byte? after, byte setMask)
    {
        if (before is null && after is null && setMask == 0)
        {
            return State.LimitProgress;
        }
        if (before is null || after is null || setMask == 0 || (setMask & ~0x3F) != 0 || State.LimitProgress is not { } current || current.SpecialMagicFlags != before || after != (byte)(before | setMask))
        {
            throw new InvalidDataException("Battle Slots learning receipt disagrees with its current saved BYTE1CFE771 owner.");
        }
        current.Validate();
        return current with { SpecialMagicFlags = after };
    }
}
