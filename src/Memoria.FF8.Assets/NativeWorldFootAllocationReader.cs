using System.Buffers.Binary;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets;

public readonly record struct NativeWorldFootAllocation(int ClauseIndex, ushort MinimumStory, ushort MaximumStory, byte EntityType, byte ShapeId, NativeSavedWorldPose Pose);

public sealed partial class WorldAssets
{
    /// <summary>Validates the selected original section9 single-foot clause before its controlled record can be excluded from contacts.</summary>
    public NativeWorldFootAllocation ReadFootAllocation(byte entityType, ushort story)
    {
        (int index, ushort minimum, ushort maximum, byte shape) = (entityType, story) switch
        {
            (0, < 34) => (0, (ushort)0, (ushort)34, (byte)0),
            (1, >= 34 and < 205) => (1, (ushort)34, (ushort)205, (byte)4),
            (0, >= 205 and < 290) => (2, (ushort)205, (ushort)290, (byte)0),
            _ => throw new NotSupportedException("Native foot story/entity lies outside the proved original single-record allocation clauses.")
        };
        ReadOnlySpan<byte> program = wmset.AsSpan(sections[9], sections[10] - sections[9]);
        int offset = 84 + index * 24;
        if (program.Length < offset + 24 || BinaryPrimitives.ReadInt32LittleEndian(program[(index * 4)..]) != offset || BinaryPrimitives.ReadInt32LittleEndian(program[((index + 1) * 4)..]) != offset + 24)
        {
            throw new NotSupportedException("Native foot section9 selected clause pointers differ from the original single-record program.");
        }
        ReadOnlySpan<byte> clause = program.Slice(offset, 24);
        ushort[] expected = [0xFF01, 0, 0xFF02, minimum, 0xFF03, maximum, 0xFF04, 0, 0xFF13, shape, 0xFF05, 0];
        for (int word = 0; word < expected.Length; word++)
        {
            if (BinaryPrimitives.ReadUInt16LittleEndian(clause[(word * 2)..]) != expected[word])
            {
                throw new NotSupportedException("Native foot selected allocation differs from the original single controlled record.");
            }
        }
        if (sections[11] - sections[10] < 16)
        {
            throw new InvalidDataException("Original native foot coordinate0 is absent.");
        }
        ReadOnlySpan<byte> coordinate = wmset.AsSpan(sections[10], 16);
        if (coordinate.IndexOfAnyExcept((byte)0) >= 0)
        {
            throw new NotSupportedException("Original native foot coordinate0 differs from the proved zero placement.");
        }
        return new NativeWorldFootAllocation(index, minimum, maximum, entityType, shape, new NativeSavedWorldPose(0, 0, 0, 0));
    }
}
