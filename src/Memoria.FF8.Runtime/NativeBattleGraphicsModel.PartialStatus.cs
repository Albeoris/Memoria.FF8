namespace Memoria.FF8.Runtime;

public sealed partial record NativeBattleGraphicsModel
{
    public uint Flags8KnownMask { get; init; }
    public uint Flags8KnownBits { get; init; }

    public NativeBattleGraphicsModel WriteRetainedStatusMask(uint mask, uint bits)
    {
        if (Flags8 is uint current)
        {
            return this with { Flags8 = (current & ~mask) | (bits & mask), Flags8KnownMask = 0, Flags8KnownBits = 0 };
        }
        return this with { Flags8KnownMask = Flags8KnownMask | mask, Flags8KnownBits = (Flags8KnownBits & ~mask) | (bits & mask) };
    }

    public void ValidatePartialStatus()
    {
        if ((Flags8KnownBits & ~Flags8KnownMask) != 0 || Flags8 is not null && Flags8KnownMask != 0)
        {
            throw new InvalidDataException("Native model status known bits contradict their observed DWORD owner.");
        }
    }
}
