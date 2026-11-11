namespace Memoria.FF7.Formats;

public sealed record BattleActionInstruction(int Offset, byte Opcode, ReadOnlyMemory<byte> Operands);

/// <summary>Decodes admitted native battle sequences through their idle transfer.</summary>
public static class BattleActionInstructions
{
    public static IReadOnlyList<BattleActionInstruction> ReadUntilIdle(ReadOnlyMemory<byte> script, int bodyClipCount)
    {
        return ReadUntilTransfer(script, bodyClipCount, 0);
    }

    internal static IReadOnlyList<BattleActionInstruction> ReadUntilTransfer(ReadOnlyMemory<byte> script, int bodyClipCount, int startOffset)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(bodyClipCount);
        List<BattleActionInstruction> instructions = [];
        for (int offset = startOffset; offset < script.Length;)
        {
            byte opcode = script.Span[offset];
            int operandCount = opcode switch
            {
                0xAD or 0xD1 => 5,
                0xFB => 4,
                0xD0 or 0xD8 => 3,
                0x9D or 0xB6 or 0xB9 or 0xBE or 0xC2 or 0xC6 or 0xF4 or 0xF7 => 1,
                0x92 or 0x9E or 0xAA or 0xB0 or 0xB1 or 0xB3 or 0xB8 or 0xC5 or 0xC9 or 0xCA or 0xE0 or 0xE1 or 0xE2 or 0xE3 or 0xE4 or 0xE5 or 0xE6 or 0xE8 or 0xEA or 0xEB or 0xEC or 0xED or 0xEE or 0xF0 or 0xF2 or 0xF3 or 0xF6 or 0xF9 or 0xFA or 0xFC or 0xFF => 0,
                < 0x8E when opcode < bodyClipCount => 0,
                _ => throw new NotSupportedException($"FFVII battle action opcode 0x{opcode:X2} at {offset} requires its native execution contract.")
            };
            if (operandCount >= script.Length - offset)
            {
                throw new InvalidDataException("A FFVII battle action instruction extends beyond its native script.");
            }
            instructions.Add(new(offset, opcode, script.Slice(offset + 1, operandCount)));
            if (opcode is 0x9E or 0xEE or 0xFF)
            {
                return instructions.AsReadOnly();
            }
            offset += 1 + operandCount;
        }
        throw new InvalidDataException("The FFVII battle action does not reach its native idle transfer.");
    }
}
