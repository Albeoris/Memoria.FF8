using System.Buffers.Binary;
using System.Collections.Immutable;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets;

public sealed record BattleNativeRenzokukenBlock(byte ShowBar, ImmutableArray<byte> Segments, BattleNativeRenzokukenTimeline Timeline);

public sealed partial class BattleSequenceSet
{
    public ImmutableArray<byte> ReadRawProgram(int sequence)
    {
        if (!Contains(sequence))
        {
            throw new NotSupportedException($"Original battle model has no sequence {sequence}.");
        }
        int start = BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(sequence * 2));
        int end = bytes.Length;
        for (int index = 1; index <= BinaryPrimitives.ReadUInt16LittleEndian(bytes); index++)
        {
            int offset = BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(index * 2));
            if (offset > start)
            {
                end = Math.Min(end, offset);
            }
        }
        return bytes.AsSpan(start, end - start).ToArray().ToImmutableArray();
    }

    /// <summary>Decode a reached AB at its exact program offset; selection/branch execution belongs to the native graphics VM.</summary>
    public BattleNativeRenzokukenBlock ReadRenzokukenBlock(int sequence, int opcodeOffset, IReadOnlyList<BattleAnimation> animations)
    {
        ImmutableArray<byte> program = ReadRawProgram(sequence);
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
                return new(program[opcodeOffset + 1], lengths, NativeBattleRenzokukenRules.Timeline(lengths));
            }
            if (opcode == 0xA0)
            {
                segments.Add(0);
            }
            else if (opcode < 0x80)
            {
                if (opcode >= animations.Count || animations[opcode].Frames.Count > 255)
                {
                    throw new InvalidDataException("Native AB pose selector lacks its BYTE animation frame count.");
                }
                segments[^1] = unchecked((byte)(segments[^1] + animations[opcode].Frames.Count));
            }
        }
        throw new InvalidDataException("Native AB timing block has no A1 terminator in its original sequence.");
    }
}
