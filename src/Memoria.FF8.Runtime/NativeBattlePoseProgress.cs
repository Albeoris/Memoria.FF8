namespace Memoria.FF8.Runtime;

/// <summary>509440/508F90's pose selector, decoded-frame BYTE, limit BYTE and speed/interpolation flags.</summary>
public sealed record NativeBattlePoseProgress(byte Pose, byte Frame, byte Limit, byte Flags)
{
    public NativeBattlePoseProgress Advance()
    {
        if (Frame >= Limit)
        {
            return this;
        }
        int steps = (Flags & 2) != 0 ? 2 : 1;
        byte interpolation = (byte)(4 * ((((Flags >> 2) & 3) + 1) % 2));
        return this with { Frame = (byte)Math.Min(Limit, Frame + steps), Flags = (byte)((Flags & ~0xC) | interpolation) };
    }
}
