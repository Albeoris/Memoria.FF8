namespace Memoria.FF7.Formats;

public sealed record BattleActionInstruction(int Offset, byte Opcode, ReadOnlyMemory<byte> Operands);

/// <summary>Decodes the native straight-line battle sequence subset used by the opening MP attacks.</summary>
public static class BattleActionInstructions
{
    public static IReadOnlyList<BattleActionInstruction> ReadUntilIdle(ReadOnlyMemory<byte> script, int bodyClipCount)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(bodyClipCount);
        List<BattleActionInstruction> instructions = [];
        for (int offset = 0; offset < script.Length;)
        {
            byte opcode = script.Span[offset];
            int operandCount = opcode switch
            {
                0xAD or 0xD1 => 5,
                0xD8 => 3,
                0xF7 => 1,
                0xE5 or 0xEA or 0xEE or 0xF0 or 0xFA or 0xFC => 0,
                < 0x8E when opcode < bodyClipCount => 0,
                _ => throw new NotSupportedException($"FFVII battle action opcode 0x{opcode:X2} at {offset} requires its native execution contract.")
            };
            if (operandCount >= script.Length - offset)
            {
                throw new InvalidDataException("A FFVII battle action instruction extends beyond its native script.");
            }
            instructions.Add(new(offset, opcode, script.Slice(offset + 1, operandCount)));
            if (opcode == 0xEE)
            {
                return instructions.AsReadOnly();
            }
            offset += 1 + operandCount;
        }
        throw new InvalidDataException("The FFVII battle action does not reach its native idle transfer.");
    }
}
