using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record BattleNativeRenzokukenBlock(byte ShowBar, ImmutableArray<byte> Segments, BattleNativeRenzokukenTimeline Timeline)
{
    public int EndOffset { get; init; }
}

public static partial class NativeBattleRenzokukenRules
{
    /// <summary>504BB0 AB scans the reached pose bytes through A1; A0 separates contact segments.</summary>
    public static BattleNativeRenzokukenBlock ReadBlock(ReadOnlySpan<byte> program, int opcodeOffset, IReadOnlyList<byte> animationFrameCounts)
    {
        if (opcodeOffset < 0 || opcodeOffset + 1 >= program.Length || program[opcodeOffset] != 0xAB)
        {
            throw new InvalidDataException("Renzokuken timing requires an actual reached AB opcode and its display operand.");
        }
        List<byte> segments = [1];
        for (int position = opcodeOffset + 2; position < program.Length; position++)
        {
            byte opcode = program[position];
            if (opcode == 0xA1)
            {
                ImmutableArray<byte> lengths = segments.ToImmutableArray();
                return new(program[opcodeOffset + 1], lengths, Timeline(lengths)) { EndOffset = position };
            }
            if (opcode == 0xA0)
            {
                segments.Add(0);
            }
            else if (opcode < 0x80)
            {
                if (opcode >= animationFrameCounts.Count)
                {
                    throw new InvalidDataException("Native AB pose selector lacks its BYTE animation frame count.");
                }
                segments[^1] = unchecked((byte)(segments[^1] + animationFrameCounts[opcode]));
            }
        }
        throw new InvalidDataException("Native AB timing block has no A1 terminator in its original sequence.");
    }
}
