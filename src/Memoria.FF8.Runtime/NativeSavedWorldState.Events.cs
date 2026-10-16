namespace Memoria.FF8.Runtime;

public sealed partial record NativeSavedWorldState
{
    public byte ReadWorldEventVariable(byte channel)
    {
        byte? value = channel switch
        {
            0 => WorldEventVariable124,
            1 => WorldEventVariable125,
            _ => throw new NotSupportedException($"World event channel{channel} addresses outside the supported saved-world BYTE124/125 channels.")
        };
        return value ?? throw new NotSupportedException($"World event channel{channel} requires authenticated saved-world BYTE{124 + channel}.");
    }

    public NativeSavedWorldState WithWorldEventVariable(byte channel, byte value)
    {
        //54D7E0/FF2E ignores unsigned channel selectors greater than one.
        return channel switch
        {
            0 => this with { WorldEventVariable124 = value },
            1 => this with { WorldEventVariable125 = value },
            _ => this
        };
    }

    public bool MatchesWorldEventVariable(ushort opcode, ushort operand)
    {
        byte value = ReadWorldEventVariable(unchecked((byte)operand));
        int expected = operand >> 8;
        return opcode switch
        {
            0xFF2D => expected == value,
            0xFF30 => expected > value,
            0xFF31 => expected < value,
            _ => throw new NotSupportedException($"World event opcode{opcode:X4} is not a saved-world channel comparison.")
        };
    }

    public NativeSavedWorldState WithOrdinaryWorldFieldEntry()
    {
        //544630 clears BYTE125 after549E80; BYTE124 and DWORD116/120 survive.
        return this with { WorldEventVariable125 = 0 };
    }
}
