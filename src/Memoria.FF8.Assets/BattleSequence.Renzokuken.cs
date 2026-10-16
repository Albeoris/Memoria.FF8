using System.Buffers.Binary;
using System.Collections.Immutable;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets;

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
        return ReadRenzokukenBlock(sequence, opcodeOffset, animations.Select(animation => checked((byte)animation.Frames.Count)).ToArray());
    }

    public BattleNativeRenzokukenBlock ReadRenzokukenBlock(int sequence, int opcodeOffset, IReadOnlyList<byte> animationFrameCounts)
    {
        return NativeBattleRenzokukenRules.ReadBlock(ReadRawProgram(sequence).AsSpan(), opcodeOffset, animationFrameCounts);
    }
}
