using System.Buffers.Binary;
using System.Diagnostics.CodeAnalysis;

namespace Memoria.FF8.Runtime;

internal readonly struct NativeBattleAiInstruction(byte[] script, int offset, int operandCount)
{
    public byte Opcode => script[offset];
    public int OperandCount => operandCount;
    public int NextOffset => offset + 1 + operandCount;
    public bool IsKnownOpcode => Opcode <= 61;

    public byte this[int operand] => script[offset + 1 + operand];

    public ushort ReadWord(int operand) => BinaryPrimitives.ReadUInt16LittleEndian(script.AsSpan(offset + 1 + operand, 2));

    public NativeBattleAiCondition Condition => new(this[0], this[1], this[2], ReadWord(3));

    // Both branches are relative to the end of the record. IF skips an unsigned WORD;
    // JMP displaces by a signed WORD, including back edges.
    public int BranchOffset => NextOffset + (Opcode == 2 ? (int)ReadWord(5) : unchecked((short)ReadWord(0)));

    public static bool TryDecode(byte[] script, int offset, out NativeBattleAiInstruction instruction, [NotNullWhen(false)] out string? error)
    {
        instruction = default;
        if ((uint)offset >= script.Length)
        {
            error = "branch-outside-script";
            return false;
        }
        byte opcode = script[offset];
        int operandCount = opcode switch
        {
            1 or 3 or 4 or 5 or 7 or 9 or 12 or 13 or 23 or 24 or 25 or 26 or 28 or 29 or 30 or 31 or 32 or 37 or 43 or 49 or 52 or 53 or 55 or 56 or 58 => 1,
            14 or 15 or 17 or 18 or 19 or 21 or 27 or 34 or 35 or 39 or 40 or 59 or 60 => 2,
            11 or 45 => 3,
            38 => 4,
            2 => 7,
            // The native default consumes only the opcode, including values above its dispatch table.
            _ => 0
        };
        if (operandCount > script.Length - offset - 1)
        {
            error = $"truncated-opcode:{opcode}";
            return false;
        }
        instruction = new(script, offset, operandCount);
        error = null;
        return true;
    }
}

internal readonly record struct NativeBattleAiCondition(byte Subject, byte Selector, byte Comparison, ushort Value)
{
    public bool UsesActorSelector => Subject is 0 or 4 or 7 or >= 220 and <= 227;

    public bool? EvaluateKnownSubject(int formation, int levelBand) => Subject switch
    {
        3 => CompareNumbers(formation, Comparison, Value),
        14 => CompareNumbers(levelBand, Comparison, Value),
        _ => null
    };

    public static bool CanCompareActorReference(int comparison, int value) => (comparison is 0 or 3) && value >= NativeBattleSlots.Count;

    public static bool CompareNumbers(long left, int comparison, long right) => comparison switch
    {
        0 => left == right,
        1 => left < right,
        2 => left > right,
        3 => left != right,
        4 => left <= right,
        5 => left >= right,
        _ => false
    };
}
